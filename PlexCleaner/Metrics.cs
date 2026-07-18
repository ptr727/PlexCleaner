using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PlexCleaner;

internal static class Metrics
{
    private static readonly Meter s_meter = new("PlexCleaner.Process");

    // Cumulative for the process lifetime, not reset between runs.
    private static readonly Counter<long> s_filesCompleted = s_meter.CreateCounter<long>(
        "plexcleaner.files.completed",
        description: "Files finished, any outcome"
    );
    private static readonly Counter<long> s_filesModified = s_meter.CreateCounter<long>(
        "plexcleaner.files.modified",
        description: "Files whose media was changed"
    );
    private static readonly Counter<long> s_filesErrors = s_meter.CreateCounter<long>(
        "plexcleaner.files.errors",
        description: "Files that errored"
    );
    private static readonly Counter<long> s_filesVerifyFailed = s_meter.CreateCounter<long>(
        "plexcleaner.files.verifyfailed",
        description: "Files that failed verification"
    );
    private static readonly Counter<long> s_filesProcessed = s_meter.CreateCounter<long>(
        "plexcleaner.files.processed",
        description: "Per-outcome tally, tagged by each State flag set"
    );

    private static readonly Histogram<double> s_fileDuration = s_meter.CreateHistogram<double>(
        "plexcleaner.file.duration",
        unit: "ms",
        description: "Per-file wall-clock time"
    );
    private static readonly Histogram<double> s_toolDuration = s_meter.CreateHistogram<double>(
        "plexcleaner.tool.duration",
        unit: "ms",
        description: "Per media-tool invocation time, tagged by tool"
    );

    // Run-scoped state, reset by BeginRun, read by the observable gauges.
    // All access is via Interlocked so the parallel loop needs no lock.
    private static long s_runFilesTotal;
    private static long s_runBytesTotal;
    private static long s_runBytesCompleted;
    private static long s_runInflight;
    private static long s_runStartTimestamp;

    // Each State flag (minus None) with its tag, pre-built once so RecordStates allocates nothing per file.
    private static readonly (
        SidecarFile.StatesType Flag,
        KeyValuePair<string, object?> Tag
    )[] s_stateTags =
    [
        .. Enum.GetValues<SidecarFile.StatesType>()
            .Where(flag => flag != SidecarFile.StatesType.None)
            .Select(flag => (flag, new KeyValuePair<string, object?>("state", flag.ToString()))),
    ];

    // Each tool with its pre-built tag, so RecordToolDuration allocates nothing per invocation.
    private static readonly Dictionary<
        MediaTool.ToolType,
        KeyValuePair<string, object?>
    > s_toolTags = Enum.GetValues<MediaTool.ToolType>()
        .ToDictionary(
            tool => tool,
            tool => new KeyValuePair<string, object?>("tool", tool.ToString())
        );

    static Metrics()
    {
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.files.total",
            () => Interlocked.Read(ref s_runFilesTotal),
            description: "Files in the current run"
        );
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.files.inflight",
            () => Interlocked.Read(ref s_runInflight),
            description: "Files currently processing"
        );
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.threads.active",
            () => (long)(Program.Options?.ThreadCount ?? 0),
            description: "Configured worker threads"
        );
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.bytes.total",
            () => Interlocked.Read(ref s_runBytesTotal),
            unit: "By",
            description: "Sum of input sizes in the current run"
        );
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.bytes.completed",
            () => Interlocked.Read(ref s_runBytesCompleted),
            unit: "By",
            description: "Bytes of finished files (no partial credit for in-flight files)"
        );
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.progress.ratio",
            ComputeProgress,
            description: "Byte-weighted overall progress [0..1]"
        );
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.eta.seconds",
            ComputeEtaSeconds,
            unit: "s",
            description: "Estimated time remaining"
        );
    }

    // Reset the run-scoped gauges and ETA clock, called once per ProcessFiles run.
    internal static void BeginRun(long totalFiles, long totalBytes)
    {
        _ = Interlocked.Exchange(ref s_runFilesTotal, totalFiles);
        _ = Interlocked.Exchange(ref s_runBytesTotal, totalBytes);
        _ = Interlocked.Exchange(ref s_runBytesCompleted, 0);
        _ = Interlocked.Exchange(ref s_runInflight, 0);
        _ = Interlocked.Exchange(ref s_runStartTimestamp, Stopwatch.GetTimestamp());
    }

    internal static void FileStarted() => Interlocked.Increment(ref s_runInflight);

    internal static void FileInflightDone() => Interlocked.Decrement(ref s_runInflight);

    // A finished file credits its whole size (no partial credit in v1) and its wall-clock time.
    internal static void FileCompleted(long sizeBytes, TimeSpan wall)
    {
        _ = Interlocked.Add(ref s_runBytesCompleted, sizeBytes);
        s_filesCompleted.Add(1);
        s_fileDuration.Record(wall.TotalMilliseconds);
    }

    internal static void FileErrored() => s_filesErrors.Add(1);

    internal static void RecordModified() => s_filesModified.Add(1);

    internal static void RecordVerifyFailed() => s_filesVerifyFailed.Add(1);

    internal static void RecordStates(SidecarFile.StatesType state)
    {
        foreach ((SidecarFile.StatesType flag, KeyValuePair<string, object?> tag) in s_stateTags)
        {
            if ((state & flag) == flag)
            {
                s_filesProcessed.Add(1, tag);
            }
        }
    }

    internal static void RecordToolDuration(MediaTool.ToolType tool, double milliseconds) =>
        s_toolDuration.Record(milliseconds, s_toolTags[tool]);

    internal static void Dispose() => s_meter.Dispose();

    // Byte-weighted progress, guards a zero (or not-yet-started) total.
    internal static double ComputeProgress()
    {
        long total = Interlocked.Read(ref s_runBytesTotal);
        return total <= 0 ? 0.0 : (double)Interlocked.Read(ref s_runBytesCompleted) / total;
    }

    // Linear extrapolation from weighted progress and elapsed time.
    // Returns 0 before any progress and never a non-finite value.
    internal static double ComputeEtaSeconds()
    {
        double ratio = ComputeProgress();
        if (ratio <= 0.0)
        {
            return 0.0;
        }
        double elapsed = Stopwatch
            .GetElapsedTime(Interlocked.Read(ref s_runStartTimestamp))
            .TotalSeconds;
        double eta = elapsed * (1.0 - ratio) / ratio;
        return double.IsFinite(eta) ? eta : 0.0;
    }

    internal static IEnumerable<SidecarFile.StatesType> EnumerateSetStates(
        SidecarFile.StatesType state
    )
    {
        foreach ((SidecarFile.StatesType flag, _) in s_stateTags)
        {
            if ((state & flag) == flag)
            {
                yield return flag;
            }
        }
    }
}
