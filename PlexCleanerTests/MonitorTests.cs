using AwesomeAssertions;
using PlexCleaner;
using Serilog.Events;
using Xunit;
using Monitor = PlexCleaner.Monitor;

namespace PlexCleanerTests;

[Collection("Sequential")]
public sealed class MonitorTests : IDisposable
{
    private const string QueuedMessage = "Adding folder to processing queue";
    private const string UpdatedMessage = "Updating timestamp for folder in processing queue";
    private static readonly TimeSpan s_bound = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_twoLoopWakes = TimeSpan.FromSeconds(2.5);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly bool _deleteEmptyFolders = Program.Config.ProcessOptions.DeleteEmptyFolders;
    private readonly HashSet<string> _ignoreList = Program.Config.ProcessOptions.FileIgnoreList;
    private readonly bool _preProcess = Program.Options.PreProcess;
    private readonly string _resultsFile = Program.Options.ResultsFile;
    private readonly int _threadCount = Program.Options.ThreadCount;

    public MonitorTests()
    {
        _ = Directory.CreateDirectory(_directory);
        Program.Config.ProcessOptions.FileIgnoreList = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase
        );
        Program.Config.ProcessOptions.DeleteEmptyFolders = true;
        Program.Options.PreProcess = false;
        Program.Options.ResultsFile = string.Empty;
        Program.Options.ThreadCount = 1;
    }

    public void Dispose()
    {
        Program.Config.ProcessOptions.DeleteEmptyFolders = _deleteEmptyFolders;
        Program.Config.ProcessOptions.FileIgnoreList = _ignoreList;
        Program.Options.PreProcess = _preProcess;
        Program.Options.ResultsFile = _resultsFile;
        Program.Options.ThreadCount = _threadCount;
        Directory.Delete(_directory, true);
    }

    private static FileSystemEventArgs Changed(WatcherChangeTypes changeType, string path) =>
        new(changeType, Path.GetDirectoryName(path)!, Path.GetFileName(path));

    private string NewEmptyFolder(string parent)
    {
        string folder = Path.Combine(_directory, parent, "empty");
        _ = Directory.CreateDirectory(folder);
        return folder;
    }

    private static bool RunMonitorUntil(
        Monitor monitor,
        string folder,
        Func<bool> condition,
        TimeSpan bound
    )
    {
        using CancellationTokenSource cancelSource = new();
        Task<bool> monitorTask = Task.Run(
            () => monitor.MonitorFolders([folder], cancelSource.Token),
            TestContext.Current.CancellationToken
        );
        _ = SpinWait.SpinUntil(condition, bound);
        cancelSource.Cancel();
        return monitorTask.GetAwaiter().GetResult();
    }

    [Fact]
    public void MonitorFolders_Missing_Folder_Fails()
    {
        Monitor monitor = new();

        _ = monitor
            .MonitorFolders(
                [$"\"{Path.Combine(_directory, "missing")}\""],
                TestContext.Current.CancellationToken
            )
            .Should()
            .BeFalse();
    }

    [Fact]
    public void MonitorFolders_PreProcess_Processes_Monitored_Folder()
    {
        Program.Options.PreProcess = true;
        string empty = NewEmptyFolder("media");
        Monitor monitor = new();

        _ = RunMonitorUntil(monitor, _directory, () => !Directory.Exists(empty), s_bound)
            .Should()
            .BeTrue();

        _ = Directory.Exists(empty).Should().BeFalse();
    }

    [Fact]
    public void Created_Folder_Is_Processed_Once_Settled()
    {
        string empty = NewEmptyFolder("media");
        Monitor monitor = new() { SettleTime = TimeSpan.Zero };

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
        {
            monitor.OnChanged(
                Changed(WatcherChangeTypes.Created, Path.Combine(_directory, "media"))
            );
            _ = RunMonitorUntil(monitor, _directory, () => !Directory.Exists(empty), s_bound)
                .Should()
                .BeTrue();
        });

        _ = Directory.Exists(empty).Should().BeFalse();
        _ = LogCapture
            .Messages(events, LogEventLevel.Information)
            .Should()
            .ContainSingle(message => message.Contains(QueuedMessage));
    }

    [Fact]
    public void Changed_File_Queues_Its_Folder_And_Updates_The_Timestamp()
    {
        string media = Path.Combine(_directory, "media");
        _ = Directory.CreateDirectory(media);
        string file = Path.Combine(media, "movie.mkv");
        File.WriteAllBytes(file, []);
        Monitor monitor = new();

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
        {
            monitor.OnChanged(Changed(WatcherChangeTypes.Created, file));
            monitor.OnChanged(Changed(WatcherChangeTypes.Changed, file));
        });

        _ = LogCapture
            .Messages(events, LogEventLevel.Information)
            .Should()
            .ContainSingle(message => message.Contains(QueuedMessage) && message.Contains(media));
        _ = LogCapture
            .Messages(events, LogEventLevel.Verbose)
            .Should()
            .ContainSingle(message => message.Contains(UpdatedMessage));
    }

    [Theory]
    [InlineData("movie.tmp1")]
    [InlineData("movie.PlexCleaner")]
    [InlineData("missing.mkv")]
    public void Temp_Sidecar_And_Missing_Paths_Are_Not_Queued(string fileName)
    {
        string file = Path.Combine(_directory, fileName);
        if (!fileName.StartsWith("missing", StringComparison.Ordinal))
        {
            File.WriteAllBytes(file, []);
        }
        Monitor monitor = new();

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            monitor.OnChanged(Changed(WatcherChangeTypes.Created, file))
        );

        _ = LogCapture
            .Messages(events, LogEventLevel.Information)
            .Should()
            .NotContain(message => message.Contains(QueuedMessage));
    }

    [Fact]
    public void Deleted_Path_Is_Not_Queued()
    {
        Monitor monitor = new();

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            monitor.OnChanged(Changed(WatcherChangeTypes.Deleted, _directory))
        );

        _ = LogCapture
            .Messages(events, LogEventLevel.Information)
            .Should()
            .NotContain(message => message.Contains(QueuedMessage));
    }

    [Fact]
    public void Renamed_Folder_Queues_The_New_Name()
    {
        string media = Path.Combine(_directory, "media");
        _ = Directory.CreateDirectory(media);
        Monitor monitor = new();

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
            monitor.OnRenamed(
                new RenamedEventArgs(WatcherChangeTypes.Renamed, _directory, "media", "old")
            )
        );

        _ = LogCapture
            .Messages(events, LogEventLevel.Information)
            .Should()
            .ContainSingle(message => message.Contains(QueuedMessage) && message.Contains(media));
    }

    [Fact]
    public void Deleted_Folder_Is_Dropped_From_The_Queue()
    {
        string media = Path.Combine(_directory, "media");
        _ = Directory.CreateDirectory(media);
        Monitor monitor = new() { SettleTime = TimeSpan.Zero };

        IReadOnlyList<LogEvent> events = LogCapture.Capture(() =>
        {
            monitor.OnChanged(Changed(WatcherChangeTypes.Created, media));
            Directory.Delete(media);
            _ = RunMonitorUntil(monitor, _directory, () => false, s_twoLoopWakes).Should().BeTrue();
        });

        _ = LogCapture
            .Messages(events, LogEventLevel.Information)
            .Should()
            .ContainSingle(message => message.Contains("Folder deleted"));
    }
}
