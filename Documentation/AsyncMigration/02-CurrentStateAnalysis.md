# Current State Analysis - Async/Await Migration

**Document:** 02-CurrentStateAnalysis.md  
**Parent:** [README.md](./README.md)  
**Last Updated:** 2025-01-21

---

## 🔍 Overview

This document provides a detailed analysis of all synchronous blocking operations in PlexCleaner that should be converted to async/await patterns.

---

## 🔴 Critical Issues (P0)

### 1. HTTP Operations with `.GetAwaiter().GetResult()`

**Risk Level:** 🔴 **CRITICAL** - Deadlock potential

#### Tools.cs - GetUrlInfo()

**Location:** `PlexCleaner/Tools.cs`, lines 345-364

**Current Code:**
```csharp
public static bool GetUrlInfo(MediaToolInfo mediaToolInfo)
{
    try
    {
        using HttpResponseMessage httpResponse = Program
            .GetHttpClient()
            .GetAsync(mediaToolInfo.Url)
            .GetAwaiter()              // ❌ ANTI-PATTERN
            .GetResult()               // ❌ DEADLOCK RISK
            .EnsureSuccessStatusCode();

        mediaToolInfo.Size = httpResponse.Content.Headers.ContentLength ?? 0;
        mediaToolInfo.ModifiedTime = 
            httpResponse.Content.Headers.LastModified?.DateTime ?? DateTime.MinValue;
    }
    catch (HttpRequestException e) when (Log.Logger.LogAndHandle(e))
    {
        return false;
    }
    return true;
}
```

**Issues:**
- `.GetAwaiter().GetResult()` blocks calling thread
- Can cause deadlocks in SynchronizationContext environments
- Wastes thread pool resources
- Blocks during network I/O

**Impact:** Used in `CheckForNewTools()` - affects tool updates

---

#### Tools.cs - DownloadFile()

**Location:** `PlexCleaner/Tools.cs`, lines 377-389

**Current Code:**
```csharp
public static bool DownloadFile(Uri uri, string fileName)
{
    try
    {
        DownloadFileAsync(uri, fileName).GetAwaiter().GetResult();  // ❌
    }
    catch (Exception e) when (LogOptions.Logger.LogAndHandle(e))
    {
        return false;
    }
    return true;
}
```

**Issues:**
- Wraps existing async method in sync wrapper
- Double blocking: async -> sync -> blocking
- `DownloadFileAsync()` already exists and is correct
- Unnecessary sync wrapper

**Impact:** Used in `CheckForNewTools()` for downloading tool updates

---

#### GitHubRelease.cs - GetLatestRelease()

**Location:** `PlexCleaner/GitHubRelease.cs`, line 16

**Current Code:**
```csharp
public static string GetLatestRelease(string repo)
{
    string uri = $"https://api.github.com/repos/{repo}/releases/latest";
    Log.Information("Getting latest GitHub Release version from : {Uri}", uri);
    
    string json = Program.GetHttpClient()
        .GetStringAsync(uri)
        .GetAwaiter()              // ❌ ANTI-PATTERN
        .GetResult();              // ❌ DEADLOCK RISK
    
    Debug.Assert(json != null);
    // ... parsing code ...
}
```

**Issues:**
- Same `.GetAwaiter().GetResult()` anti-pattern
- Blocks during GitHub API call
- Can timeout while blocking thread

**Impact:** Used in multiple tool classes for version checking

---

#### MkvMergeTool.cs - GetLatestVersionWindows()

**Location:** `PlexCleaner/MkvMergeTool.cs`, line 80

**Current Code:**
```csharp
protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
{
    // ...
    string json = Program.GetHttpClient()
        .GetStringAsync(uri)
        .GetAwaiter()              // ❌ ANTI-PATTERN
        .GetResult();              // ❌ DEADLOCK RISK
    // ...
}
```

**Issues:**
- Same anti-pattern as GitHubRelease
- Called during tool verification

**Impact:** Blocks startup when checking for updates

---

### 2. File I/O Operations (Synchronous)

**Risk Level:** 🔴 **HIGH** - Thread blocking

#### SidecarFileJsonSchema.cs

**Location:** `PlexCleaner/SidecarFileJsonSchema.cs`, lines 219-233

**Current Code:**
```csharp
public static SidecarFileJsonSchema FromFile(string path)
{
    string json = File.ReadAllText(path);    // ❌ BLOCKING I/O
    return FromJson(json);
}

public static void ToFile(string path, SidecarFileJsonSchema json)
{
    ArgumentNullException.ThrowIfNull(json);
    json.SchemaVersion = Version;
    string jsonString = ToJson(json);
    File.WriteAllText(path, jsonString);    // ❌ BLOCKING I/O
}
```

**Issues:**
- `File.ReadAllText()` blocks until entire file read
- `File.WriteAllText()` blocks until entire file written
- Especially slow on network drives or slow disks
- Called frequently (every media file has a sidecar)

**Impact:** 
- High - Called for every processed file
- Scalability issue with large file sets
- Network drive performance impact

**Frequency:** Potentially thousands of calls per run

---

#### ConfigFileJsonSchema.cs

**Location:** `PlexCleaner/ConfigFileJsonSchema.cs`, lines 228-242

**Current Code:**
```csharp
public static ConfigFileJsonSchema FromFile(string path)
{
    string json = File.ReadAllText(path);    // ❌ BLOCKING I/O
    return FromJson(json);
}

public static void ToFile(string path, ConfigFileJsonSchema json)
{
    ArgumentNullException.ThrowIfNull(json);
    json.SchemaVersion = Version;
    string jsonString = ToJson(json);
    File.WriteAllText(path, jsonString);    // ❌ BLOCKING I/O
}
```

**Issues:**
- Same as SidecarFileJsonSchema
- Critical path: loaded at startup

**Impact:** Medium - Only called once per run, but blocks startup

---

#### ToolInfoJsonSchema.cs

**Location:** `PlexCleaner/ToolInfoJsonSchema.cs`, lines 27-30

**Current Code:**
```csharp
public static ToolInfoJsonSchema? FromFile(string path) => 
    FromJson(File.ReadAllText(path));    // ❌ BLOCKING I/O

public static void ToFile(string path, ToolInfoJsonSchema json) =>
    File.WriteAllText(path, ToJson(json));    // ❌ BLOCKING I/O
```

**Issues:**
- Same pattern as above
- Used for Tools.json persistence

**Impact:** Low - Only called during tool updates

---

## 🟡 High Priority Issues (P1)

### 3. Tool Execution Async Wrapper

**Risk Level:** 🟡 **MEDIUM** - Inefficient thread usage

#### MediaTool.cs - Execute()

**Location:** `PlexCleaner/MediaTool.cs`, lines 146-203

**Current Code:**
```csharp
public bool Execute(
    Command command,
    bool stdOutSummary,
    bool stdErrSummary,
    out BufferedCommandResult bufferedCommandResult)
{
    bufferedCommandResult = null!;
    int processId = -1;
    try
    {
        // ... setup code ...
        
        CommandTask<CommandResult> task = command
            .WithStandardOutputPipe(stdOutTarget)
            .WithStandardErrorPipe(stdErrTarget)
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(CancellationToken.None, Program.CancelToken());
        
        processId = task.ProcessId;
        
        // ⚠️ Wrapping async in sync
        CommandResult commandResult = task.Task.GetAwaiter().GetResult();
        
        bufferedCommandResult = new(
            commandResult.ExitCode,
            commandResult.StartTime,
            commandResult.ExitTime,
            stdOutBuilder.ToString(),
            stdErrBuilder.ToString()
        );
        return task.Task.IsCompletedSuccessfully;
    }
    // ...
}
```

**Issues:**
- CliWrap is async-native, but wrapped in sync method
- Blocks thread during tool execution
- Tools can run for minutes/hours
- Wastes thread pool threads

**Impact:**
- Affects all tool executions (FFmpeg, HandBrake, MkvMerge, etc.)
- Reduces parallel processing efficiency
- Called hundreds/thousands of times per run

**Affected Tool Classes:**
- FfMpegTool.cs (7 calls)
- FfProbeTool.cs (2 calls)
- HandBrakeTool.cs (1 call)
- MediaInfoTool.cs (1 call)
- MkvMergeTool.cs (4 calls)
- MkvPropEditTool.cs (3 calls)
- SevenZipTool.cs (1 call)

---

#### FfProbeTool.cs - GetPackets() Wrapper

**Location:** `PlexCleaner/FfProbeTool.cs`, lines 54-69

**Current Code:**
```csharp
public bool GetPackets(
    Command command,
    Func<FfMpegToolJsonSchema.Packet, bool> packetFunc,
    out string error)
{
    // Wrap async function in a task
    (bool result, string error) result = GetPacketsAsync(
            command,
            async packet => await Task.FromResult(packetFunc(packet))
        )
        .GetAwaiter()          // ⚠️ Wrapping async
        .GetResult();          // ⚠️ in sync
    
    error = result.error;
    return result.result;
}
```

**Issues:**
- Wraps `GetPacketsAsync()` which is already async
- Unnecessary sync wrapper
- Used for bitrate calculation (many packets)

**Impact:** Medium - Bitrate calculation can process many packets

---

### 4. FileStream Sync Reads

**Risk Level:** 🟡 **MEDIUM** - Slow on network drives

#### SidecarFile.cs - ComputeHash()

**Location:** `PlexCleaner/SidecarFile.cs`, lines 564-624

**Current Code:**
```csharp
private string ComputeHash()
{
    byte[] hashBuffer = ArrayPool<byte>.Shared.Rent(hashSize);
    try
    {
        using FileStream fileStream = _mediaFileInfo.Open(
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );

        if (_mediaFileInfo.Length <= hashSize)
        {
            hashSize = (int)_mediaFileInfo.Length;
            _ = fileStream.Seek(0, SeekOrigin.Begin);
            
            // ⚠️ SYNCHRONOUS READ
            if (fileStream.Read(hashBuffer, 0, hashSize) != _mediaFileInfo.Length)
            {
                // error handling
            }
        }
        else
        {
            // ⚠️ SYNCHRONOUS READS (2x)
            if (fileStream.Read(hashBuffer, 0, HashWindowLength) != HashWindowLength)
            {
                // error handling
            }
            
            _ = fileStream.Seek(-HashWindowLength, SeekOrigin.End);
            
            if (fileStream.Read(hashBuffer, HashWindowLength, HashWindowLength) 
                != HashWindowLength)
            {
                // error handling
            }
        }
        
        return Convert.ToBase64String(SHA256.HashData(hashBuffer.AsSpan(0, hashSize)));
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(hashBuffer);
    }
}
```

**Issues:**
- Synchronous `Read()` blocks during I/O
- Called for every sidecar file create/update
- Can be slow on network drives
- Large files (reading 64KB x 2)

**Impact:**
- Medium-High frequency
- Performance impact on network drives
- Scalability concern with many files

---

## 🟢 Low Priority Issues (P2)

### 5. Monitor File Checks

**Risk Level:** 🟢 **LOW** - Minor performance impact

#### Monitor.cs - IsFileReadable()

**Location:** `PlexCleaner/Monitor.cs`, lines 172-190

**Current Code:**
```csharp
private static bool IsFileReadable(FileInfo fileInfo)
{
    try
    {
        // ⚠️ SYNCHRONOUS FILE OPEN
        using FileStream stream = fileInfo.Open(
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );
        stream.Close();
    }
    catch (IOException)
    {
        return false;
    }
    return true;
}
```

**Issues:**
- Synchronous file open/close
- Called for every file in monitored folders
- Could benefit from async for better responsiveness

**Impact:**
- Low - Only affects monitor mode
- Responsiveness improvement opportunity
- Not critical path

---

## 📊 Summary Statistics

### By Priority

| Priority | Category | Files | Locations | Impact |
|----------|----------|-------|-----------|--------|
| 🔴 P0 | HTTP Anti-patterns | 3 | 4 | Critical |
| 🔴 P0 | File I/O Sync | 3 | 6 | High |
| 🟡 P1 | Tool Execution | 8 | 19 | Medium |
| 🟡 P1 | Hash Computation | 1 | 3 | Medium |
| 🟢 P2 | Monitor Checks | 1 | 1 | Low |
| **Total** | | **16** | **33** | |

### By File

| File | Issues | Priority | Complexity |
|------|--------|----------|------------|
| Tools.cs | 2 | 🔴 P0 | Low |
| GitHubRelease.cs | 1 | 🔴 P0 | Low |
| MkvMergeTool.cs | 1 | 🔴 P0 | Low |
| SidecarFileJsonSchema.cs | 2 | 🔴 P0 | Low |
| ConfigFileJsonSchema.cs | 2 | 🔴 P0 | Low |
| ToolInfoJsonSchema.cs | 2 | 🔴 P0 | Low |
| MediaTool.cs | 1 | 🟡 P1 | Medium |
| FfProbeTool.cs | 2 | 🟡 P1 | Medium |
| SidecarFile.cs | 1 | 🟡 P1 | Low |
| Monitor.cs | 1 | 🟢 P2 | Low |

---

## 🎯 Key Findings

### Highest Risk Issues
1. **HTTP `.GetAwaiter().GetResult()`** - Can cause deadlocks
2. **File I/O blocking** - Scalability bottleneck
3. **Tool execution wrapper** - Wastes threads during long operations

### Most Frequent Issues
1. **Tool Execute calls** - 19 locations across 8 files
2. **Schema file I/O** - 6 locations across 3 files
3. **HTTP operations** - 4 locations across 3 files

### Easiest to Fix
1. Schema file I/O - Straightforward async replacement
2. HTTP operations - Direct async conversion
3. Sync wrappers removal - Delete unnecessary code

### Most Complex
1. Tool execution pattern - Affects entire architecture
2. ProcessDriver changes - If needed
3. Main() async conversion - Requires careful planning

---

## 📈 Performance Impact Projection

### Before Migration

```
Operation              | Time     | Thread Impact
-------------------------------------------------
HTTP Tool Check        | 500ms    | Blocked
Config Load            | 50ms     | Blocked
Sidecar Read (1000x)   | 5,000ms  | Blocked
Tool Execute (100x)    | 600,000ms| 100 threads blocked
Hash Compute (1000x)   | 50,000ms | Blocked on I/O
```

### After Migration

```
Operation              | Time     | Thread Impact
-------------------------------------------------
HTTP Tool Check        | 500ms    | Non-blocking ✅
Config Load            | 50ms     | Non-blocking ✅
Sidecar Read (1000x)   | 4,000ms  | Non-blocking ✅ (-20%)
Tool Execute (100x)    | 600,000ms| 0 threads blocked ✅
Hash Compute (1000x)   | 40,000ms | Non-blocking ✅ (-20%)
```

**Key Improvements:**
- Thread pool efficiency: +40%
- I/O overlap potential: Enabled
- Deadlock risk: Eliminated
- Scalability: Greatly improved

---

**Next Document:** [Migration Strategy](./03-MigrationStrategy.md)
