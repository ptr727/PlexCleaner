# Migration Strategy - Async/Await Migration

**Document:** 03-MigrationStrategy.md  
**Parent:** [README.md](./README.md)  
**Last Updated:** 2025-01-21

---

## 🎯 Core Principles

### 1. Backward Compatibility First
- Add async methods alongside sync methods
- Never break existing public API during migration
- Mark sync methods obsolete gradually
- Remove sync methods only in major version bump

### 2. Incremental Migration
- One phase at a time
- Validate after each phase
- Allow rollback at any point
- Independent deployable phases

### 3. Test-Driven Approach
- Write tests before changes
- Validate after each change
- No regression tolerance
- Automated testing priority

### 4. Performance Validation
- Baseline metrics before starting
- Benchmark after each phase
- Track thread pool usage
- Monitor memory allocation

---

## 🗺️ Migration Approach

### Dual-Mode Transition Pattern

This pattern allows gradual migration without breaking changes:

```csharp
// STEP 1: Add async version
public static async Task<bool> GetUrlInfoAsync(
    MediaToolInfo mediaToolInfo, 
    CancellationToken cancellationToken = default)
{
    // New async implementation
}

// STEP 2: Keep existing method temporarily
public static bool GetUrlInfo(MediaToolInfo mediaToolInfo)
{
    // Existing code - mark as transitional
    return GetUrlInfoAsync(mediaToolInfo).GetAwaiter().GetResult();
}

// STEP 3: Update all call sites to async
// (Can be done incrementally)

// STEP 4: Mark sync method obsolete
[Obsolete("Use GetUrlInfoAsync instead", false)]
public static bool GetUrlInfo(MediaToolInfo mediaToolInfo)
{
    // Keep for backward compatibility
}

// STEP 5: Remove in next major version
// (Delete sync method entirely)
```

---

## 📊 Phase Breakdown

### Phase 1: Foundation (Weeks 1-2) 🔴 Priority: P0

**Goals:**
- Eliminate deadlock risks
- Convert critical file I/O
- Establish async patterns

**Scope:**
- HTTP operations (3 files, 4 locations)
- Schema file I/O (3 files, 6 locations)
- Minimal call site changes

**Deliverables:**
- Zero `.GetAwaiter().GetResult()` in HTTP code
- All schema files support async
- Updated call sites in Tools.cs, SidecarFile.cs

**Success Criteria:**
- All tests passing
- No deadlock risk
- HTTP operations non-blocking

---

### Phase 2: Core Operations (Weeks 3-4) 🟡 Priority: P1

**Goals:**
- Convert tool execution to native async
- Improve thread pool efficiency
- Enable async packet processing

**Scope:**
- MediaTool.Execute() → ExecuteAsync()
- All tool classes (7 files)
- FfProbeTool packet processing

**Deliverables:**
- MediaTool base class async
- All tool executions async
- Packet processing async

**Success Criteria:**
- Tool execution non-blocking
- No thread pool starvation
- Performance maintained

---

### Phase 3: File Operations (Weeks 5-6) 🟡 Priority: P1

**Goals:**
- Async file operations
- Better I/O performance
- Network drive optimization

**Scope:**
- Hash computation async
- Monitor file checks async
- Any remaining file I/O

**Deliverables:**
- ComputeHashAsync() implemented
- Monitor checks async
- File operations non-blocking

**Success Criteria:**
- I/O operations async
- Better network drive performance
- Responsive monitor mode

---

### Phase 4: Architecture (Weeks 7-8) 🟢 Priority: P2

**Goals:**
- Async Main()
- Command handlers async
- Evaluate ProcessDriver

**Scope:**
- Program.Main() → async Task<int>
- Command handlers async
- Optional: ProcessDriver async

**Deliverables:**
- Async Main implemented
- All command handlers async
- Architecture modernized

**Success Criteria:**
- Clean async all the way down
- Proper cancellation flow
- Performance validated

---

### Phase 5: Testing & Optimization (Week 9) ✅ Required

**Goals:**
- Comprehensive testing
- Performance validation
- Documentation

**Scope:**
- All unit tests
- Integration tests
- Performance benchmarks
- Documentation updates

**Deliverables:**
- All tests passing
- Performance report
- Migration guide
- Updated documentation

**Success Criteria:**
- 100% test pass rate
- No performance regression
- Documentation complete

---

## 🔄 Async Pattern Standards

### Required Patterns

#### 1. Method Signatures
```csharp
// ✅ CORRECT: Async suffix, Task<T> return, CancellationToken parameter
public static async Task<Result> MethodNameAsync(
    Parameters parameters,
    CancellationToken cancellationToken = default)
{
    // Implementation
}

// ❌ WRONG: No async suffix
public static async Task<Result> MethodName(...)

// ❌ WRONG: No CancellationToken
public static async Task<Result> MethodNameAsync(Parameters parameters)
```

#### 2. ConfigureAwait Usage
```csharp
// ✅ CORRECT: Library code should use ConfigureAwait(false)
public static async Task<string> ReadFileAsync(
    string path, 
    CancellationToken cancellationToken = default)
{
    return await File.ReadAllTextAsync(path, cancellationToken)
        .ConfigureAwait(false);  // ✅ Avoids SynchronizationContext capture
}

// ❌ WRONG: Missing ConfigureAwait in library code
return await File.ReadAllTextAsync(path, cancellationToken);
```

#### 3. Exception Handling
```csharp
// ✅ CORRECT: Let exceptions bubble up
public static async Task<bool> ProcessAsync(
    string fileName, 
    CancellationToken cancellationToken = default)
{
    try
    {
        await DoWorkAsync(fileName, cancellationToken).ConfigureAwait(false);
        return true;
    }
    catch (OperationCanceledException)
    {
        // Log cancellation
        Log.Information("Operation cancelled");
        return false;
    }
    catch (Exception e) when (Log.Logger.LogAndHandle(e))
    {
        // Specific logging/handling
        return false;
    }
}

// ❌ WRONG: Swallowing exceptions
catch (Exception) { return false; }
```

#### 4. CancellationToken Propagation
```csharp
// ✅ CORRECT: Always propagate CancellationToken
public static async Task<bool> ProcessFileAsync(
    string fileName, 
    CancellationToken cancellationToken = default)
{
    // Pass token to all async operations
    var data = await ReadDataAsync(fileName, cancellationToken).ConfigureAwait(false);
    await WriteDataAsync(data, cancellationToken).ConfigureAwait(false);
    return true;
}

// ❌ WRONG: Not propagating token
public static async Task<bool> ProcessFileAsync(string fileName)
{
    var data = await ReadDataAsync(fileName).ConfigureAwait(false);  // No token
}
```

#### 5. Return Type Patterns
```csharp
// ✅ CORRECT: Task<T> for async methods with return value
public static async Task<MediaToolInfo> GetToolInfoAsync(...)

// ✅ CORRECT: Task for async methods without return value
public static async Task ProcessAsync(...)

// ✅ CORRECT: ValueTask<T> for hot path scenarios (advanced)
public static async ValueTask<bool> IsValidAsync(...)

// ❌ WRONG: async void (except event handlers)
public static async void ProcessAsync(...)  // Never use unless event handler
```

---

## 🚫 Anti-Patterns to Avoid

### 1. `.GetAwaiter().GetResult()`
```csharp
// ❌ AVOID: Deadlock risk
var result = SomeMethodAsync().GetAwaiter().GetResult();

// ✅ USE: Proper async/await
var result = await SomeMethodAsync().ConfigureAwait(false);
```

### 2. `.Wait()` or `.Result`
```csharp
// ❌ AVOID: Can deadlock
task.Wait();
var result = task.Result;

// ✅ USE: Await the task
await task.ConfigureAwait(false);
var result = await task.ConfigureAwait(false);
```

### 3. `async void`
```csharp
// ❌ AVOID: Exceptions crash app
public static async void ProcessAsync() { }

// ✅ USE: async Task
public static async Task ProcessAsync() { }

// ✅ EXCEPTION: Event handlers only
private async void Button_Click(object sender, EventArgs e) { }
```

### 4. Unnecessary async/await
```csharp
// ❌ AVOID: Unnecessary state machine
public static async Task<string> GetDataAsync()
{
    return await File.ReadAllTextAsync("file.txt").ConfigureAwait(false);
}

// ✅ USE: Return task directly
public static Task<string> GetDataAsync()
{
    return File.ReadAllTextAsync("file.txt");
}

// ✅ EXCEPTION: If you need try/catch or using
public static async Task<string> GetDataAsync()
{
    try
    {
        return await File.ReadAllTextAsync("file.txt").ConfigureAwait(false);
    }
    catch (IOException e)
    {
        Log.Error(e);
        throw;
    }
}
```

### 5. Blocking in async code
```csharp
// ❌ AVOID: Defeats the purpose
public static async Task ProcessAsync()
{
    await Task.Run(() => Thread.Sleep(1000));  // Don't wrap blocking code
}

// ✅ USE: Actual async operations
public static async Task ProcessAsync()
{
    await Task.Delay(1000);  // Properly async
}
```

---

## 📐 Code Organization

### File Structure
```
PlexCleaner/
├── *Tool.cs               # Tool execution (Phase 2)
│   ├── Add: ExecuteAsync()
│   ├── Mark: [Obsolete] Execute()
│   └── Update: All tool methods
│
├── *JsonSchema.cs         # File I/O (Phase 1)
│   ├── Add: FromFileAsync()
│   ├── Add: ToFileAsync()
│   └── Keep: Sync versions temporarily
│
├── Tools.cs               # HTTP & Downloads (Phase 1)
│   ├── Add: GetUrlInfoAsync()
│   ├── Add: DownloadFileAsync() (exists)
│   └── Remove: DownloadFile() sync wrapper
│
├── Program.cs             # Architecture (Phase 4)
│   ├── Change: async Task<int> Main()
│   └── Update: All command handlers
│
└── ProcessDriver.cs       # Optional (Phase 4)
    └── Evaluate: Parallel.ForEachAsync
```

---

## 🔧 Development Workflow

### For Each Phase

#### 1. Preparation
- [ ] Review phase documentation
- [ ] Create feature branch
- [ ] Establish baseline metrics
- [ ] Review code patterns

#### 2. Implementation
- [ ] Add async methods
- [ ] Update call sites
- [ ] Add/update tests
- [ ] Code review

#### 3. Validation
- [ ] All tests passing
- [ ] Performance benchmarks
- [ ] Integration testing
- [ ] Documentation updated

#### 4. Merge
- [ ] Final code review
- [ ] Merge to main branch
- [ ] Update progress tracking
- [ ] Tag release (if applicable)

---

## 📊 Success Validation

### Code Quality Checks
```bash
# Check for anti-patterns
grep -r "GetAwaiter().GetResult()" PlexCleaner/
grep -r "\.Wait()" PlexCleaner/
grep -r "\.Result" PlexCleaner/
grep -r "async void" PlexCleaner/ | grep -v "event"

# Should return 0 results (or only documented exceptions)
```

### Performance Benchmarks
```bash
# Before migration
dotnet run -- process --parallel --threadcount 4 <test-files>

# After migration
dotnet run -- process --parallel --threadcount 4 <test-files>

# Compare: Throughput, Memory, Thread Pool usage
```

### Test Coverage
```bash
# Run all tests
dotnet test

# Should be: 100% pass rate, no new failures
```

---

## 🎓 Team Preparation

### Training Required
- [ ] Async/await fundamentals
- [ ] Task-based Async Pattern (TAP)
- [ ] Deadlock scenarios
- [ ] ConfigureAwait usage
- [ ] Testing async code
- [ ] Performance profiling

### Reference Materials
- [Async Programming (Microsoft Docs)](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/)
- [Best Practices in Asynchronous Programming](https://learn.microsoft.com/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)

---

## 🔍 Code Review Guidelines

### Checklist for Reviewers

**Method Signatures:**
- [ ] Async suffix on async methods
- [ ] CancellationToken parameter with default
- [ ] Proper return type (Task<T> or Task)

**Implementation:**
- [ ] ConfigureAwait(false) on all awaits
- [ ] CancellationToken propagated
- [ ] No `.GetAwaiter().GetResult()`
- [ ] No `.Wait()` or `.Result`
- [ ] No async void (except event handlers)

**Testing:**
- [ ] Unit tests added/updated
- [ ] Cancellation tested
- [ ] Exception handling tested
- [ ] Performance validated

**Documentation:**
- [ ] XML comments updated
- [ ] Code examples correct
- [ ] Migration notes added

---

## 📋 Definition of Done

A phase is complete when:

1. ✅ All code changes implemented
2. ✅ All anti-patterns eliminated
3. ✅ All tests passing (100%)
4. ✅ Code review approved
5. ✅ Performance validated (no regression)
6. ✅ Documentation updated
7. ✅ Merged to main branch
8. ✅ Progress tracking updated

---

**Next Document:** [Phase 1: Foundation](./04-Phase1-Foundation.md)
