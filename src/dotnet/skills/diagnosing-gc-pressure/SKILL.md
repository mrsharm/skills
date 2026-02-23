```skill
---
name: diagnosing-gc-pressure
description: Diagnose .NET garbage collection pressure and memory issues using dotnet-gcdump, dotnet-counters, and GC event analysis. Use when an app has high GC pause times, excessive Gen2 collections, large object heap fragmentation, or unexplained memory growth.
---

# Diagnosing .NET GC Pressure

## When to Use

- Application has high GC pause times causing latency spikes
- Gen2 or LOH collections are too frequent
- Memory grows over time but no obvious leak (GC pressure, not leak)
- Need to understand allocation patterns and GC behavior

## When Not to Use

- The issue is a managed memory leak (use debugging-memory-leaks skill)
- The app uses unmanaged/native memory (different diagnostic path)
- The user just wants to profile CPU (use profiling-dotnet-apps)

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Process or app to diagnose | Yes | Running .NET process or reproducible scenario |
| Symptoms | No | High latency, memory growth, CPU spikes during GC |

## Workflow

### Step 1: Check GC metrics with dotnet-counters

Start by observing GC behavior in real-time:

```bash
# List running .NET processes to find the PID
dotnet-counters ps

# Monitor GC-specific counters
dotnet-counters monitor --process-id <PID> --counters System.Runtime[gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,gen-0-size,gen-1-size,gen-2-size,loh-size,poh-size,gc-heap-size,time-in-gc,alloc-rate]
```

**What to look for:**

| Metric | Healthy | Problematic |
|--------|---------|-------------|
| `time-in-gc` | < 5% | > 10% = significant GC pressure |
| `gen-2-gc-count` | Rare (minutes apart) | Frequent (seconds apart) = Gen2 pressure |
| `alloc-rate` | < 100 MB/s typical | > 500 MB/s = high allocation rate |
| `loh-size` | Stable | Growing = LOH fragmentation risk |
| `gen-0-size` | ~10-50 MB | Very large = budget adjustment needed |

### Step 2: Capture a GC dump for heap analysis

```bash
# Capture a GC dump (lightweight, safe for production)
dotnet-gcdump collect --process-id <PID> --output gc_snapshot.gcdump

# OR trigger on specific conditions with dotnet-monitor
dotnet-monitor collect --urls http://localhost:52323
# Then: GET /gcdump?pid=<PID>
```

**dotnet-gcdump vs dotnet-dump:**
- `dotnet-gcdump` — GC heap snapshot only. Small file, fast, safe for production. Shows what objects exist and their sizes.
- `dotnet-dump` — Full process dump. Large file, can pause the process. Use only when gcdump isn't enough.

### Step 3: Analyze the GC dump

Open the `.gcdump` file in Visual Studio or PerfView:

```bash
# Using PerfView (most detailed analysis)
PerfView.exe /GCOnly gc_snapshot.gcdump
```

In Visual Studio: File → Open → select `.gcdump` file → shows object allocation tree.

**Key things to check:**

1. **Top types by inclusive size** — what's consuming the most heap?
2. **String objects** — often the #1 consumer; look for duplicate/internable strings
3. **Large arrays** (byte[], object[]) — these go to LOH (≥85,000 bytes)
4. **Event handlers / delegates** — common source of unintentional retention

### Step 4: Capture GC events with dotnet-trace for timing analysis

```bash
# Collect GC events only (minimal overhead)
dotnet-trace collect --process-id <PID> \
    --providers Microsoft-Windows-DotNETRuntime:0x1:5 \
    --duration 00:00:30

# The provider keyword 0x1 = GCKeyword, level 5 = Verbose
# This captures: GCStart, GCEnd, GCAllocationTick, GCHeapStats
```

**Provider keywords for GC events:**

| Keyword | Hex | What it captures |
|---------|-----|-----------------|
| GC | 0x1 | GC start/stop, heap stats |
| GCHandle | 0x2 | GC handle creation/destruction |
| GCSampledObjectAllocation | 0x200000 | Sampled allocation stacks |
| GCAllObjectAllocation | 0x400000 | ALL allocation stacks (HIGH overhead) |

**Analyze the trace in PerfView:**

```
PerfView.exe /GCOnly trace.nettrace
```

Navigate to: GCStats → Per-Generation data → look for:
- **Gen2 GC durations**: should be < 10ms for Workstation GC, varies for Server GC
- **Pause times (Background vs Blocking)**: Background Gen2 is non-blocking; if you see blocking Gen2, investigate why
- **GC reason**: "AllocSmall" = normal allocation trigger; "InducedNotForced" = someone called GC.Collect()

### Step 5: Identify the root cause pattern

| Pattern | Evidence | Fix |
|---------|----------|-----|
| **High allocation rate** | alloc-rate > 500MB/s, frequent Gen0 | Reduce allocations: use `Span<T>`, `stackalloc`, `ArrayPool<T>`, object pooling |
| **LOH fragmentation** | loh-size grows, Gen2 collections frequent | Avoid allocating large arrays; use `ArrayPool<byte>.Shared`; consider LOH compaction (`GCSettings.LargeObjectHeapCompactionMode`) |
| **Pinned objects** | POH size growing, fragmentation | Move pinned buffers to POH (`GC.AllocateArray<byte>(size, pinned: true)` in .NET 5+) |
| **Gen2 pressure from long-lived objects** | Gen2 size grows steadily | Objects promoted too early; reduce object lifetimes or use `MemoryCache` with size limits |
| **Induced GC calls** | GC reason = "Induced" in trace | Find and remove `GC.Collect()` calls (almost always wrong in production) |
| **Finalizer queue backup** | Finalizable objects count growing | Implement `IDisposable` + `GC.SuppressFinalize(this)`; don't rely on finalizers |

### Step 6: Apply targeted fixes

**For high allocation rate (most common):**
```csharp
// BEFORE: allocates new byte[] every call
public byte[] ProcessData(int size)
{
    var buffer = new byte[size]; // Allocates on heap every time
    FillBuffer(buffer);
    return buffer;
}

// AFTER: rent from ArrayPool
public void ProcessData(int size, Action<byte[]> process)
{
    var buffer = ArrayPool<byte>.Shared.Rent(size);
    try
    {
        FillBuffer(buffer);
        process(buffer);
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
    }
}
```

**For LOH fragmentation:**
```csharp
// Force LOH compaction (one-time, expensive — use sparingly)
GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
GC.Collect();  // Only case where GC.Collect is justified

// Better: use pinned object heap for pinned buffers (.NET 5+)
var pinnedBuffer = GC.AllocateArray<byte>(100_000, pinned: true);
```

**For Server GC tuning in containers:**
```xml
<!-- In .csproj — critical for containerized apps -->
<PropertyGroup>
    <ServerGarbageCollection>true</ServerGarbageCollection>
    <!-- Limit GC heap count in containers to match CPU limit -->
    <GarbageCollectionAdaptationMode>1</GarbageCollectionAdaptationMode>
</PropertyGroup>
```

Or via environment variable:
```bash
# In Dockerfile or container config
DOTNET_GCHeapCount=2          # Match to CPU cores allocated
DOTNET_GCConserveMemory=7     # 1-9, higher = more aggressive GC
DOTNET_gcServer=1             # Enable server GC
```

## Validation

- [ ] `time-in-gc` is below 5% after fixes
- [ ] Gen2 collection frequency has decreased
- [ ] Allocation rate (alloc-rate) has decreased
- [ ] LOH size is stable (not growing)
- [ ] No `GC.Collect()` calls in production code (except LOH compaction)
- [ ] Container GC settings match allocated CPU/memory

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Using `GC.Collect()` to "fix" memory | Almost always makes it worse; let the GC self-tune |
| Server GC in memory-constrained container | Server GC uses 1 heap per core; set `GCHeapCount` or use Workstation GC |
| Ignoring POH for pinned buffers | Pinned objects in Gen0/Gen1 cause fragmentation; use `GC.AllocateArray(size, pinned:true)` |
| Allocating in hot loops | Use `stackalloc` for small buffers, `ArrayPool` for larger ones |
| String concatenation in loops | Use `StringBuilder` or `string.Create()` to avoid intermediate allocations |
| Not setting `GC.SuppressFinalize` | Objects with finalizers require 2 GC cycles to collect; always suppress when disposing |
```
