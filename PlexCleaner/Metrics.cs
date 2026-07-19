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

    // Operation-weighted progress: each heavy full-file operation adds the file size to the work total when it starts and the completed total when it ends.
    private static long s_runWorkTotal;
    private static long s_runWorkCompleted;
    private static long s_runInflight;
    private static long s_runStartTimestamp;

    // The current file's size, set on the worker thread so OpStarted and OpCompleted can weight by it.
    private static readonly ThreadLocal<long> s_currentFileSize = new();

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
            "plexcleaner.work.total",
            () => Interlocked.Read(ref s_runWorkTotal),
            unit: "By",
            description: "Operation work discovered, file size added per heavy operation, grows as the path unfolds"
        );
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.work.completed",
            () => Interlocked.Read(ref s_runWorkCompleted),
            unit: "By",
            description: "Operation work finished, file size added per completed heavy operation"
        );
        _ = s_meter.CreateObservableGauge(
            "plexcleaner.progress.ratio",
            ComputeProgress,
            description: "Operation-weighted overall progress [0..1]"
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
        _ = Interlocked.Exchange(ref s_runWorkTotal, 0);
        _ = Interlocked.Exchange(ref s_runWorkCompleted, 0);
        _ = Interlocked.Exchange(ref s_runInflight, 0);
        _ = Interlocked.Exchange(ref s_runStartTimestamp, Stopwatch.GetTimestamp());
    }

    internal static void FileStarted(long sizeBytes)
    {
        s_currentFileSize.Value = sizeBytes;
        _ = Interlocked.Increment(ref s_runInflight);
    }

    internal static void FileInflightDone()
    {
        s_currentFileSize.Value = 0;
        _ = Interlocked.Decrement(ref s_runInflight);
    }

    // A heavy full-file operation started, count the current file's size as work to do.
    internal static void OpStarted() =>
        Interlocked.Add(ref s_runWorkTotal, s_currentFileSize.Value);

    // The heavy operation finished, count the same size as work done.
    internal static void OpCompleted() =>
        Interlocked.Add(ref s_runWorkCompleted, s_currentFileSize.Value);

    internal static void FileCompleted(TimeSpan wall)
    {
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

    internal static void Dispose()
    {
        s_currentFileSize.Dispose();
        s_meter.Dispose();
    }

    // Completed operation work over discovered operation work, guarding a zero total.
    internal static double ComputeProgress()
    {
        long total = Interlocked.Read(ref s_runWorkTotal);
        if (total <= 0)
        {
            return 0.0;
        }
        double completed = Interlocked.Read(ref s_runWorkCompleted);
        return Math.Clamp(completed / total, 0.0, 1.0);
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
