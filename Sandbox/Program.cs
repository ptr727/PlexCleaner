// See https://aka.ms/new-console-template for more information

using System.Globalization;

var cultureInfo1 = new CultureInfo("ger");
var cultureInfo2 = CultureInfo.CreateSpecificCulture("ger");
var cultureInfo3 = CultureInfo.GetCultureInfo("deu", true);

var tag1 = PlexCleaner.Language.GetIetfTag("xxx", false);

// Generate JSON schema
// SchemaGenerator.GenerateSchema();

var fileList = new List<string>
{
    "/path/file1.ext",
    "/path/file2.ext",
    "/path/file3.ext",
    "/path/file1.avi",
    "/path/file2.avi",
    "/path/file3.avi",
    "/path/file1.mkv",
    "/path/file2.mkv",
    "/path/file3.mkv"
};

var numList = Enumerable.Range(0, 1000).ToList();

// Group files by path ignoring extensions
var pathDictionary = new Dictionary<string, List<string>>();
fileList.ForEach(path => {
    string normalPath = Path.ChangeExtension(path, null).ToLowerInvariant();
    if (pathDictionary.TryGetValue(normalPath, out var pathList))
    {
        pathList.Add(path);
    }
    else
    {
        pathDictionary.Add(normalPath, new List<string> { path });
    }
});

// Process groups in parallel
//var partitioner = Partitioner.Create(pathDictionary, EnumerablePartitionerOptions.NoBuffering);
//var partitioner = Partitioner.Create(fileList, false);
//var partitioner = Partitioner.Create(fileList, EnumerablePartitionerOptions.NoBuffering);
fileList
    .GroupBy(path => Path.ChangeExtension(path, null), StringComparer.OrdinalIgnoreCase)
    .AsParallel()
    .WithDegreeOfParallelism(2)
    .ForAll(group =>
    {
        Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}, Key: {group.Key}");
        foreach (string fileName in group) {
            Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}, Path: {fileName}");
            Thread.Sleep(100);
        }
    });

