# Phase 1: Foundation - HTTP & Schema Files

**Document:** 04-Phase1-Foundation.md  
**Parent:** [README.md](./README.md)  
**Duration:** Weeks 1-2  
**Priority:** 🔴 **P0 - Critical**  
**Status:** Not Started

---

## 🎯 Phase Objectives

### Primary Goals
1. ✅ Eliminate all `.GetAwaiter().GetResult()` anti-patterns in HTTP code
2. ✅ Convert all JSON schema file I/O to async
3. ✅ Establish async coding patterns for the project
4. ✅ Create foundation for subsequent phases

### Success Criteria
- [ ] Zero HTTP deadlock risks
- [ ] All schema file operations async
- [ ] No performance regression
- [ ] All tests passing
- [ ] Code patterns established

---

## 📊 Scope Summary

| Category | Files | Methods | Lines Changed | Complexity |
|----------|-------|---------|---------------|------------|
| HTTP Operations | 3 | 4 | ~100 | Low |
| Schema File I/O | 3 | 6 | ~120 | Low |
| Call Site Updates | 3 | ~8 | ~80 | Medium |
| **Total** | **9** | **~18** | **~300** | **Low-Medium** |

---

## 📝 Task List

### Task 1.1: HTTP Operations Migration

**Priority:** 🔴 Critical  
**Effort:** 16 hours  
**Risk:** High (deadlock prevention)

#### Subtasks

- [ ] **1.1.1** - Tools.cs: Create `GetUrlInfoAsync()`
  - File: `PlexCleaner/Tools.cs`
  - Lines: 345-364
  - Add async version
  - Update XML documentation
  - Add unit test

- [ ] **1.1.2** - Tools.cs: Remove `DownloadFile()` wrapper
  - File: `PlexCleaner/Tools.cs`
  - Lines: 377-389
  - Delete sync wrapper
  - Update call sites to use `DownloadFileAsync()`

- [ ] **1.1.3** - GitHubRelease.cs: Create `GetLatestReleaseAsync()`
  - File: `PlexCleaner/GitHubRelease.cs`
  - Line: 10-25
  - Convert to async
  - Update error handling
  - Add unit test

- [ ] **1.1.4** - MkvMergeTool.cs: Update `GetLatestVersionWindows()`
  - File: `PlexCleaner/MkvMergeTool.cs`
  - Line: ~80
  - Convert to async
  - Update signature

#### Implementation Details

**Tools.cs - GetUrlInfoAsync()**

```csharp
// ADD: New async method
public static async Task<bool> GetUrlInfoAsync(
    MediaToolInfo mediaToolInfo, 
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(mediaToolInfo);
    
    try
    {
        using HttpResponseMessage httpResponse = await Program
            .GetHttpClient()
            .GetAsync(mediaToolInfo.Url, cancellationToken)
            .ConfigureAwait(false);
        
        httpResponse.EnsureSuccessStatusCode();
        
        mediaToolInfo.Size = httpResponse.Content.Headers.ContentLength ?? 0;
        mediaToolInfo.ModifiedTime = 
            httpResponse.Content.Headers.LastModified?.DateTime ?? DateTime.MinValue;
        
        return true;
    }
    catch (HttpRequestException e) when (Log.Logger.LogAndHandle(e))
    {
        return false;
    }
    catch (OperationCanceledException)
    {
        Log.Information("GetUrlInfo cancelled for {Url}", mediaToolInfo.Url);
        return false;
    }
}

// MODIFY: Existing method (temporary backward compatibility)
[Obsolete("Use GetUrlInfoAsync for better async performance", false)]
public static bool GetUrlInfo(MediaToolInfo mediaToolInfo)
{
    return GetUrlInfoAsync(mediaToolInfo, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
}
```

**Tools.cs - Remove DownloadFile() wrapper**

```csharp
// DELETE: This entire method
// public static bool DownloadFile(Uri uri, string fileName) { ... }

// KEEP: Only the async version (already exists)
public static async Task DownloadFileAsync(
    Uri uri, 
    string fileName,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(uri);
    ArgumentException.ThrowIfNullOrEmpty(fileName);
    
    await using Stream httpStream = await Program
        .GetHttpClient()
        .GetStreamAsync(uri, cancellationToken)
        .ConfigureAwait(false);
    
    await using FileStream fileStream = File.OpenWrite(fileName);
    
    await httpStream.CopyToAsync(fileStream, cancellationToken)
        .ConfigureAwait(false);
}
```

**GitHubRelease.cs - GetLatestReleaseAsync()**

```csharp
/// <summary>
/// Gets the latest release version from GitHub asynchronously.
/// </summary>
/// <param name="repo">Repository in format "owner/repo"</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Latest release tag name</returns>
/// <exception cref="HttpRequestException">If GitHub API call fails</exception>
/// <exception cref="JsonException">If response cannot be parsed</exception>
public static async Task<string> GetLatestReleaseAsync(
    string repo, 
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrEmpty(repo);
    
    string uri = $"https://api.github.com/repos/{repo}/releases/latest";
    Log.Information("Getting latest GitHub Release version from : {Uri}", uri);
    
    string json = await Program
        .GetHttpClient()
        .GetStringAsync(uri, cancellationToken)
        .ConfigureAwait(false);
    
    ArgumentException.ThrowIfNullOrEmpty(json);

    JsonNode? releases = JsonNode.Parse(json);
    ArgumentNullException.ThrowIfNull(releases, "Failed to parse GitHub release JSON");
    
    JsonNode? versionTag = releases["tag_name"];
    ArgumentNullException.ThrowIfNull(versionTag, "tag_name not found in GitHub release");
    
    return versionTag.ToString();
}

// KEEP: Sync version for backward compatibility (temporary)
[Obsolete("Use GetLatestReleaseAsync instead", false)]
public static string GetLatestRelease(string repo)
{
    return GetLatestReleaseAsync(repo, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
}
```

#### Testing Checklist

- [ ] Unit test: GetUrlInfoAsync with valid URL
- [ ] Unit test: GetUrlInfoAsync with invalid URL
- [ ] Unit test: GetUrlInfoAsync with cancellation
- [ ] Unit test: GetLatestReleaseAsync with valid repo
- [ ] Unit test: GetLatestReleaseAsync with invalid repo
- [ ] Unit test: DownloadFileAsync success
- [ ] Unit test: DownloadFileAsync cancellation
- [ ] Integration test: CheckForNewTools command
- [ ] Manual test: Slow network conditions
- [ ] Manual test: Network timeout

---

### Task 1.2: Schema File I/O Migration

**Priority:** 🔴 Critical  
**Effort:** 16 hours  
**Risk:** Medium (high frequency operations)

#### Subtasks

- [ ] **1.2.1** - SidecarFileJsonSchema: Add async methods
  - File: `PlexCleaner/SidecarFileJsonSchema.cs`
  - Lines: 219-233
  - Add `FromFileAsync()`
  - Add `ToFileAsync()`
  - Update XML documentation

- [ ] **1.2.2** - ConfigFileJsonSchema: Add async methods
  - File: `PlexCleaner/ConfigFileJsonSchema.cs`
  - Lines: 228-242
  - Add `FromFileAsync()`
  - Add `ToFileAsync()`
  - Add `WriteDefaultsToFileAsync()`
  - Add `WriteSchemaToFileAsync()`

- [ ] **1.2.3** - ToolInfoJsonSchema: Add async methods
  - File: `PlexCleaner/ToolInfoJsonSchema.cs`
  - Lines: 27-30
  - Add `FromFileAsync()`
  - Add `ToFileAsync()`

#### Implementation Details

**SidecarFileJsonSchema.cs**

```csharp
/// <summary>
/// Reads a sidecar file asynchronously.
/// </summary>
/// <param name="path">Path to sidecar file</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Sidecar file schema or null if failed</returns>
public static async Task<SidecarFileJsonSchema?> FromFileAsync(
    string path, 
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrEmpty(path);
    
    try
    {
        string json = await File.ReadAllTextAsync(path, cancellationToken)
            .ConfigureAwait(false);
        return FromJson(json);
    }
    catch (Exception e) when (Log.Logger.LogAndHandle(e))
    {
        return null;
    }
}

/// <summary>
/// Writes a sidecar file asynchronously.
/// </summary>
/// <param name="path">Path to write to</param>
/// <param name="json">Sidecar file schema</param>
/// <param name="cancellationToken">Cancellation token</param>
public static async Task ToFileAsync(
    string path, 
    SidecarFileJsonSchema json, 
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrEmpty(path);
    ArgumentNullException.ThrowIfNull(json);
    
    json.SchemaVersion = Version;
    
    string jsonString = ToJson(json);
    await File.WriteAllTextAsync(path, jsonString, cancellationToken)
        .ConfigureAwait(false);
}

// KEEP: Sync versions for backward compatibility (temporary)
[Obsolete("Use FromFileAsync for better performance", false)]
public static SidecarFileJsonSchema? FromFile(string path)
{
    return FromFileAsync(path, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
}

[Obsolete("Use ToFileAsync for better performance", false)]
public static void ToFile(string path, SidecarFileJsonSchema json)
{
    ToFileAsync(path, json, CancellationToken.None)
        .GetAwaiter()
        .GetResult();
}
```

**ConfigFileJsonSchema.cs**

```csharp
public static async Task<ConfigFileJsonSchema> FromFileAsync(
    string path, 
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrEmpty(path);
    
    string json = await File.ReadAllTextAsync(path, cancellationToken)
        .ConfigureAwait(false);
    
    ConfigFileJsonSchema? result = FromJson(json);
    return result ?? throw new JsonException($"Failed to deserialize config file: {path}");
}

public static async Task ToFileAsync(
    string path, 
    ConfigFileJsonSchema json, 
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrEmpty(path);
    ArgumentNullException.ThrowIfNull(json);
    
    json.SchemaVersion = Version;
    
    string jsonString = ToJson(json);
    await File.WriteAllTextAsync(path, jsonString, cancellationToken)
        .ConfigureAwait(false);
}

public static async Task WriteDefaultsToFileAsync(
    string path, 
    CancellationToken cancellationToken = default)
{
    ConfigFileJsonSchema config = new();
    config.SetDefaults();
    await ToFileAsync(path, config, cancellationToken)
        .ConfigureAwait(false);
}

public static async Task WriteSchemaToFileAsync(
    string path, 
    CancellationToken cancellationToken = default)
{
    JsonNode schemaNode = ConfigFileJsonContext.Default.Options.GetJsonSchemaAsNode(
        typeof(ConfigFileJsonSchema)
    );
    string schemaJson = schemaNode.ToJsonString(ConfigFileJsonContext.Default.Options);
    
    await File.WriteAllTextAsync(path, schemaJson, cancellationToken)
        .ConfigureAwait(false);
}
```

**ToolInfoJsonSchema.cs**

```csharp
public static async Task<ToolInfoJsonSchema?> FromFileAsync(
    string path, 
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrEmpty(path);
    
    string json = await File.ReadAllTextAsync(path, cancellationToken)
        .ConfigureAwait(false);
    return FromJson(json);
}

public static async Task ToFileAsync(
    string path, 
    ToolInfoJsonSchema json, 
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrEmpty(path);
    ArgumentNullException.ThrowIfNull(json);
    
    string jsonString = ToJson(json);
    await File.WriteAllTextAsync(path, jsonString, cancellationToken)
        .ConfigureAwait(false);
}
```

#### Testing Checklist

- [ ] Unit test: FromFileAsync with valid file
- [ ] Unit test: FromFileAsync with invalid file
- [ ] Unit test: FromFileAsync with cancellation
- [ ] Unit test: ToFileAsync success
- [ ] Unit test: ToFileAsync with read-only file
- [ ] Unit test: ToFileAsync with cancellation
- [ ] Integration test: Sidecar file creation
- [ ] Integration test: Config file loading
- [ ] Performance test: Large file handling
- [ ] Performance test: Network drive I/O

---

### Task 1.3: Update Call Sites

**Priority:** 🔴 Critical  
**Effort:** 16 hours  
**Risk:** Medium (ripple effects)

#### Subtasks

- [ ] **1.3.1** - SidecarFile.cs: Update `ReadJson()` and `WriteJson()`
- [ ] **1.3.2** - Tools.cs: Update `VerifyFolderTools()`
- [ ] **1.3.3** - Tools.cs: Update `CheckForNewTools()`
- [ ] **1.3.4** - Program.cs: Update config loading (if needed)
- [ ] **1.3.5** - All tool classes: Update version checking

#### Implementation Details

**SidecarFile.cs - ReadJsonAsync()**

```csharp
private async Task<bool> ReadJsonAsync(CancellationToken cancellationToken = default)
{
    try
    {
        SidecarFileJsonSchema? sidecarJson = await SidecarFileJsonSchema
            .FromFileAsync(_sidecarFileInfo.FullName, cancellationToken)
            .ConfigureAwait(false);
        
        if (sidecarJson == null)
        {
            Log.Error("Failed to read JSON from file : {FileName}", _sidecarFileInfo.Name);
            return false;
        }
        
        _sidecarJson = sidecarJson;
        return true;
    }
    catch (Exception e) when (Log.Logger.LogAndHandle(e))
    {
        return false;
    }
}

private async Task<bool> WriteJsonAsync(CancellationToken cancellationToken = default)
{
    try
    {
        await SidecarFileJsonSchema.ToFileAsync(
            _sidecarFileInfo.FullName, 
            _sidecarJson, 
            cancellationToken
        ).ConfigureAwait(false);
        
        return true;
    }
    catch (Exception e) when (Log.Logger.LogAndHandle(e))
    {
        return false;
    }
}
```

**Tools.cs - CheckForNewToolsAsync()**

```csharp
public static async Task<bool> CheckForNewToolsAsync(
    CancellationToken cancellationToken = default)
{
    // ... existing validation code ...
    
    try
    {
        string toolsFile = GetToolsJsonPath();
        ToolInfoJsonSchema? toolInfoJson = null;
        
        if (File.Exists(toolsFile))
        {
            toolInfoJson = await ToolInfoJsonSchema
                .FromFileAsync(toolsFile, cancellationToken)
                .ConfigureAwait(false);
            
            // ... schema version check ...
        }
        
        toolInfoJson ??= new ToolInfoJsonSchema();
        toolInfoJson.LastCheck = DateTime.UtcNow;
        
        foreach (MediaTool mediaTool in GetToolFamilyList())
        {
            // ... get latest version ...
            
            if (!await GetUrlInfoAsync(latestToolInfo, cancellationToken)
                .ConfigureAwait(false))
            {
                Log.Error("Failed to get URL info");
                return false;
            }
            
            // ... comparison logic ...
            
            if (updateRequired)
            {
                await DownloadFileAsync(
                    new Uri(latestToolInfo.Url ?? string.Empty), 
                    downloadFile,
                    cancellationToken
                ).ConfigureAwait(false);
                
                // ... update logic ...
            }
        }
        
        await ToolInfoJsonSchema.ToFileAsync(toolsFile, toolInfoJson, cancellationToken)
            .ConfigureAwait(false);
        
        return true;
    }
    catch (Exception e) when (Log.Logger.LogAndHandle(e))
    {
        return false;
    }
}
```

#### Testing Checklist

- [ ] Unit test: SidecarFile read/write async
- [ ] Integration test: Full sidecar lifecycle
- [ ] Integration test: CheckForNewTools
- [ ] Integration test: Tool verification
- [ ] System test: End-to-end processing

---

## 📊 Progress Tracking

### Completion Checklist

#### Code Changes
- [ ] HTTP operations converted (4 methods)
- [ ] Schema file I/O converted (6 methods)
- [ ] Call sites updated (8+ locations)
- [ ] Obsolete attributes added
- [ ] XML documentation updated

#### Testing
- [ ] Unit tests written/updated (20+)
- [ ] Integration tests passing
- [ ] Performance tests passing
- [ ] Manual testing complete

#### Documentation
- [ ] Code comments updated
- [ ] HISTORY.md updated
- [ ] Migration notes added
- [ ] Examples updated

#### Code Quality
- [ ] No `.GetAwaiter().GetResult()` in HTTP code
- [ ] All file I/O async
- [ ] ConfigureAwait(false) on all awaits
- [ ] CancellationToken propagated
- [ ] Code review approved

---

## 📈 Success Metrics

### Performance Baseline (Before)
- HTTP call time: ~500ms (blocking)
- Config load time: ~50ms (blocking)
- Sidecar read (100 files): ~1000ms (blocking)
- Thread pool: High contention

### Performance Target (After)
- HTTP call time: ~500ms (non-blocking) ✅
- Config load time: ~50ms (non-blocking) ✅
- Sidecar read (100 files): ~800ms (parallel I/O) ✅
- Thread pool: Low contention ✅

### Quality Metrics
- Test pass rate: 100% ✅
- Code coverage: No decrease ✅
- Deadlock risk: Eliminated ✅
- Performance regression: None ✅

---

## ⚠️ Known Issues & Workarounds

### Issue 1: Temporary Obsolete Warnings
**Problem:** Sync methods marked obsolete will generate warnings  
**Workaround:** Add `#pragma warning disable` in call sites temporarily  
**Resolution:** Remove when all call sites updated

### Issue 2: Mixed Async/Sync Call Chains
**Problem:** Some call chains still have sync wrappers  
**Workaround:** Update incrementally, test at each step  
**Resolution:** Complete in Phase 2

---

## 🔄 Rollback Plan

If critical issues discovered:

1. **Immediate Rollback:**
   - Revert to previous commit
   - Keep async methods but restore sync versions
   - Remove obsolete attributes

2. **Partial Rollback:**
   - Revert specific files
   - Keep working changes
   - Fix issues incrementally

3. **Feature Flag:**
   - Add configuration option
   - Allow runtime switching
   - Gather more data

---

## 📞 Decision Points

### End of Week 1
**Decision:** Continue to Week 2 or iterate?

**Criteria:**
- [ ] HTTP operations stable
- [ ] Schema file I/O working
- [ ] No critical bugs
- [ ] Performance acceptable

### End of Week 2
**Decision:** Proceed to Phase 2?

**Criteria:**
- [ ] All Phase 1 tasks complete
- [ ] All tests passing
- [ ] Performance validated
- [ ] Team confident

---

## ✅ Phase Completion Criteria

Phase 1 is complete when:

1. ✅ All HTTP operations async
2. ✅ All schema file I/O async
3. ✅ Zero `.GetAwaiter().GetResult()` in new code
4. ✅ All tests passing (100%)
5. ✅ Performance baseline maintained
6. ✅ Code review approved
7. ✅ Documentation updated
8. ✅ Ready for Phase 2

---

**Next Phase:** [Phase 2: Core Operations](./05-Phase2-CoreOperations.md)
