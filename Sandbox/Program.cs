// See https://aka.ms/new-console-template for more information

using System.Collections.Concurrent;
using System.Diagnostics;

// Generate JSON schema
// SchemaGenerator.GenerateSchema();

// Input path list out of order
// All files with the same path minus extension must be processed together
var pathList = new List<string>()
{
    "path1/file1.ext",
    "path1/file2.ext",
    "path2/file1.avi",
    "path1/file1.mkv",
    "path1/file3.avi",
    "path1/file1.avi",
    "path2/file3.mkv",
    "path1/file2.mkv",
    "path2/file2.ext",
    "path1/file3.ext",
    "path1/file3.mkv",
    "path1/file2.avi",
    "path2/file1.ext",
    "path2/file1.mkv",
    "path2/file3.avi",
    "path2/file2.mkv",
    "path2/file3.ext",
    "path2/file2.avi",
    "path/file2",
    "",
    ".",
    ".ext"
};

// Create a grouping key using the path minus the extension
string GroupPath(string path)
{
    // Valid path
    if (String.IsNullOrEmpty(path) || path.Length < 2)
    {
        return path;
    }

    // Get the path excluding the extension
    var groupPath = path;
    var extensionOffset = path.LastIndexOf('.');
    if (extensionOffset != -1)
    {
        groupPath = path.Substring(0, extensionOffset);

        // Valid path
        if (String.IsNullOrEmpty(groupPath))
        {
            return path;
        }
    }

    // Group path
    return groupPath;
}


// *** Brute force implementation ***

// Sort by full path
var sortedPathList = new List<string>(pathList);
sortedPathList.Sort();

// Files grouped by path excluding the extension
var groupList = new List<List<string>>();

// Iterate over all paths
var lastGroupPath = "";
List<string>? lastGroup = null;
sortedPathList.ForEach(path => {
    // Get group path
    var groupPath = GroupPath(path);
    if (String.IsNullOrEmpty(path))
    {
        return;
    }

    // Is this group path the same as the last group path
    if (!lastGroupPath.Equals(groupPath))
    {
        // Create a new group and add the path
        lastGroup = new List<string>() { path };

        // Add this group to the group list
        groupList.Add(lastGroup);
        lastGroupPath = groupPath;
    }
    else
    {
        // Use the last group and add the path
        Debug.Assert(lastGroup != null);
        lastGroup.Add(path);
    }
});


// Iterate over groups
groupList.ForEach(group => {
    group.ForEach(path => {  
        Console.WriteLine($"Path: {path}");
    });
});

// *** Parallel implementation *** HOWTO?
// Iterate in parallel
//var partitioner = Partitioner.Create(pathList, true);
//var partitioner = Partitioner.Create(pathList, EnumerablePartitionerOptions.NoBuffering);
//partitioner.AsParallel()
pathList.AsParallel()
    // .GroupBy(path => GroupPath(path))
    .ForAll(path => {
        Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}, Path: {path}");
        Thread.Sleep(100);
    });

