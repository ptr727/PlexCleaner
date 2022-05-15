// See https://aka.ms/new-console-template for more information



// Generate JSON schema
// SchemaGenerator.GenerateSchema();

// Create test list of items
var itemList = Enumerable.Range(0, 1000000).ToList();

// Iterate of items in parallel
/*
var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
Parallel.ForEach(itemList, parallelOptions, item => {
    Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, Item: {item}");
    Thread.Sleep(100);
    }
);
*/

itemList.AsParallel()
    //.WithDegreeOfParallelism(Environment.ProcessorCount)
    .ForAll(item => {
        Console.WriteLine($"Thread: {Environment.CurrentManagedThreadId}, Item: {item}");
        Thread.Sleep(100);
    });
