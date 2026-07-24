using System.Diagnostics.Metrics;
using AwesomeAssertions;
using PlexCleaner;
using Xunit;

namespace PlexCleanerTests;

// Metrics is process-static, so run these in the non-parallel collection and reset run state with
// BeginRun at the start of every test.
[Collection("Sequential")]
public class MetricsTests
{
    [Fact]
    public void ComputeProgress_ZeroTotal_IsZero()
    {
        Metrics.BeginRun(0, 0);

        _ = Metrics.ComputeProgress().Should().Be(0.0);
    }

    [Fact]
    public void ComputeEtaSeconds_NoProgress_IsZero()
    {
        Metrics.BeginRun(2, 1000);

        _ = Metrics.ComputeEtaSeconds().Should().Be(0.0);
    }

    [Fact]
    public void ComputeEtaSeconds_PartialProgress_IsFiniteAndNonNegative()
    {
        // Discover two operations and finish one, so progress is partial and ETA is finite
        Metrics.BeginRun(2, 1000);
        Metrics.FileStarted(500);
        Metrics.OpStarted();
        Metrics.OpCompleted();
        Metrics.OpStarted();

        double eta = Metrics.ComputeEtaSeconds();

        _ = double.IsFinite(eta).Should().BeTrue();
        _ = eta.Should().BeGreaterThanOrEqualTo(0.0);

        Metrics.OpCompleted();
        Metrics.FileInflightDone();
    }

    [Fact]
    public void EnumerateSetStates_ReturnsEachSetFlag()
    {
        SidecarFile.StatesType state =
            SidecarFile.StatesType.ReMuxed
            | SidecarFile.StatesType.Verified
            | SidecarFile.StatesType.ClearedTags;

        List<SidecarFile.StatesType> flags = [.. Metrics.EnumerateSetStates(state)];

        _ = flags
            .Should()
            .BeEquivalentTo([
                SidecarFile.StatesType.ReMuxed,
                SidecarFile.StatesType.Verified,
                SidecarFile.StatesType.ClearedTags,
            ]);
    }

    [Fact]
    public void EnumerateSetStates_None_IsEmpty() =>
        _ = Metrics.EnumerateSetStates(SidecarFile.StatesType.None).Should().BeEmpty();

    [Fact]
    public void Instruments_AreObservableViaMeterListener()
    {
        List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)> longs = [];
        List<(string Name, double Value)> doubles = [];

        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "PlexCleaner.Process")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, _) =>
                longs.Add((instrument.Name, measurement, tags.ToArray()))
        );
        listener.SetMeasurementEventCallback<double>(
            (instrument, measurement, _, _) => doubles.Add((instrument.Name, measurement))
        );
        listener.Start();

        // File 1 runs and completes one op, file 2 starts an op and stays in flight, so operation-weighted progress is 400 of 1000 bytes
        Metrics.BeginRun(2, 1000);
        Metrics.FileStarted(400);
        Metrics.OpStarted();
        Metrics.OpCompleted();
        Metrics.FileInflightDone();
        Metrics.FileCompleted(TimeSpan.Zero);
        Metrics.RecordStates(SidecarFile.StatesType.ReMuxed | SidecarFile.StatesType.Verified);
        Metrics.FileStarted(600);
        Metrics.OpStarted();
        listener.RecordObservableInstruments();

        // The counter fired one measurement per set flag with the state tag
        List<string?> states =
        [
            .. longs
                .Where(m => m.Name == "plexcleaner.files.processed")
                .Select(m => m.Tags.Single(t => t.Key == "state").Value?.ToString()),
        ];
        _ = states.Should().BeEquivalentTo(["ReMuxed", "Verified"]);

        // One of two started files is still in flight, and progress is operation-weighted
        _ = longs
            .Should()
            .ContainSingle(m => m.Name == "plexcleaner.files.inflight")
            .Which.Value.Should()
            .Be(1);
        _ = doubles
            .Should()
            .ContainSingle(m => m.Name == "plexcleaner.progress.ratio")
            .Which.Value.Should()
            .BeApproximately(0.4, 1e-9);

        // Clear the in-flight file
        Metrics.OpCompleted();
        Metrics.FileInflightDone();
    }

    [Fact]
    public void ComputeProgress_IsCompletedOverDiscoveredWork()
    {
        // Progress is completed operation work over discovered operation work, each op weighted by file size
        Metrics.BeginRun(1, 100_000);
        Metrics.FileStarted(100_000);

        // No operations yet, guard the zero total
        _ = Metrics.ComputeProgress().Should().Be(0.0);

        // First op started but not done, counted in the total only
        Metrics.OpStarted();
        _ = Metrics.ComputeProgress().Should().Be(0.0);

        // First op done, all discovered work is complete
        Metrics.OpCompleted();
        _ = Metrics.ComputeProgress().Should().Be(1.0);

        // A second op is discovered, the total grows and the ratio dips
        Metrics.OpStarted();
        _ = Metrics.ComputeProgress().Should().BeApproximately(0.5, 1e-9);

        Metrics.OpCompleted();
        _ = Metrics.ComputeProgress().Should().Be(1.0);

        Metrics.FileInflightDone();
    }

    [Fact]
    public void OpAborted_RollsBackTheStartedWork()
    {
        // An operation that never ran rolls its size back out of the total so progress still converges
        Metrics.BeginRun(1, 1000);
        Metrics.FileStarted(400);

        Metrics.OpStarted();
        Metrics.OpCompleted();
        _ = Metrics.ComputeProgress().Should().Be(1.0);

        // A second operation starts then aborts, the total returns to the completed work
        Metrics.OpStarted();
        _ = Metrics.ComputeProgress().Should().BeApproximately(0.5, 1e-9);
        Metrics.OpAborted();
        _ = Metrics.ComputeProgress().Should().Be(1.0);

        Metrics.FileInflightDone();
    }

    [Fact]
    public void OpAfterInflightDone_CountsNothing()
    {
        // FileInflightDone clears the current file size, so a stray late op adds no work
        Metrics.BeginRun(1, 1000);
        Metrics.FileStarted(400);
        Metrics.FileInflightDone();

        Metrics.OpStarted();
        Metrics.OpCompleted();
        _ = Metrics.ComputeProgress().Should().Be(0.0);
    }
}
