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
    public void ComputeProgress_IsByteWeightedNotCountWeighted()
    {
        // Two files totalling 1000 bytes; completing only the 900-byte one is 90% of the work, not
        // 50% of the file count.
        Metrics.BeginRun(2, 1000);
        Metrics.FileCompleted(900, TimeSpan.Zero);

        _ = Metrics.ComputeProgress().Should().BeApproximately(0.9, 1e-9);
    }

    [Fact]
    public void ComputeProgress_ZeroTotal_IsZero()
    {
        Metrics.BeginRun(0, 0);

        _ = Metrics.ComputeProgress().Should().Be(0.0);
    }

    [Fact]
    public void ComputeProgress_AllBytesDone_IsOne()
    {
        Metrics.BeginRun(1, 500);
        Metrics.FileCompleted(500, TimeSpan.Zero);

        _ = Metrics.ComputeProgress().Should().Be(1.0);
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
        Metrics.BeginRun(2, 1000);
        Metrics.FileCompleted(400, TimeSpan.Zero);

        double eta = Metrics.ComputeEtaSeconds();

        _ = double.IsFinite(eta).Should().BeTrue();
        _ = eta.Should().BeGreaterThanOrEqualTo(0.0);
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

        // Two files start; one finishes 400 of 1000 bytes (leaves flight and is credited); record a
        // two-flag outcome for it
        Metrics.BeginRun(2, 1000);
        Metrics.FileStarted(500);
        Metrics.FileStarted(500);
        Metrics.FileInflightDone();
        Metrics.FileCompleted(400, TimeSpan.Zero);
        Metrics.RecordStates(SidecarFile.StatesType.ReMuxed | SidecarFile.StatesType.Verified);
        listener.RecordObservableInstruments();

        // The counter fired one measurement per set flag with the state tag
        List<string?> states =
        [
            .. longs
                .Where(m => m.Name == "plexcleaner.files.processed")
                .Select(m => m.Tags.Single(t => t.Key == "state").Value?.ToString()),
        ];
        _ = states.Should().BeEquivalentTo(["ReMuxed", "Verified"]);

        // One of two started files is still in flight, and progress is byte-weighted
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
    }

    [Fact]
    public void ComputeProgress_FoldsInflightPartialCredit()
    {
        Metrics.BeginRun(2, 1000);
        Metrics.FileStarted(600);
        Metrics.FileSink? sink = Metrics.CurrentFileSink;

        // 600 bytes at half done is 30% of the 1000-byte run
        Metrics.ReportFileFraction(sink, 0.5);
        _ = Metrics.ComputeProgress().Should().BeApproximately(0.3, 1e-9);

        Metrics.ReportFileFraction(sink, 1.0);
        _ = Metrics.ComputeProgress().Should().BeApproximately(0.6, 1e-9);

        // Finishing removes the partial credit and credits the whole size, same result here
        Metrics.FileInflightDone();
        Metrics.FileCompleted(600, TimeSpan.Zero);
        _ = Metrics.ComputeProgress().Should().BeApproximately(0.6, 1e-9);
    }

    [Fact]
    public void ReportFileFraction_ClampsOutOfRange()
    {
        Metrics.BeginRun(1, 1000);
        Metrics.FileStarted(1000);
        Metrics.FileSink? sink = Metrics.CurrentFileSink;

        Metrics.ReportFileFraction(sink, 1.5);
        _ = Metrics.ComputeProgress().Should().Be(1.0);

        Metrics.ReportFileFraction(sink, -0.5);
        _ = Metrics.ComputeProgress().Should().Be(0.0);

        Metrics.FileInflightDone();
    }

    [Fact]
    public void FileInflightDone_RemovesPartialCredit()
    {
        Metrics.BeginRun(1, 1000);
        Metrics.FileStarted(400);
        Metrics.ReportFileFraction(Metrics.CurrentFileSink, 1.0);
        _ = Metrics.ComputeProgress().Should().BeApproximately(0.4, 1e-9);

        Metrics.FileInflightDone();
        _ = Metrics.ComputeProgress().Should().Be(0.0);
    }
}
