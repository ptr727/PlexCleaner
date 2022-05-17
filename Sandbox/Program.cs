// See https://aka.ms/new-console-template for more information

using System.Collections.Concurrent;

// Generate JSON schema
// SchemaGenerator.GenerateSchema();

// Create test list of items
var itemList = Enumerable.Range(0, 1000).ToList();

// Create a dynamic partitioner
//var partitioner = Partitioner.Create(itemList, true);
var partitioner = Partitioner.Create(itemList, EnumerablePartitionerOptions.NoBuffering);

// Iterate in parallel
partitioner.AsParallel()
    .ForAll(item => {
        Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}, Item: {item}");
        Thread.Sleep(100);
    });
