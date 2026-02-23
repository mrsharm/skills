# Judge Report: Diagnose high GC pause times in a .NET service

## Baseline Judge
Overall Score: 5/5
Reasoning: This is an excellent, comprehensive response that covers the full diagnostic and remediation workflow for GC pressure in a containerized .NET 8 application. The answer is well-structured with a clear progression from diagnosis to root cause analysis to fixes. It includes precise commands with correct flags and keywords (e.g., the ETW provider keywords 0x1 and 0x200001), accurate threshold values, a table of common patterns and fixes, container-specific tuning advice with an important insight about Server GC core detection, and a practical diagnostic endpoint. The recommended workflow summary ties everything together. The only minor note is that no tools were used (the agent answered from knowledge), but for a knowledge/expertise question like this, that's entirely appropriate.

- **Recommended starting with dotnet-counters to observe GC metrics in real-time (time-in-gc, gen counts, alloc-rate)**: 5/5 — The agent leads with dotnet-counters as step 1 ('Confirm GC Is the Culprit'), explicitly names time-in-gc, gen-2-gc-count, alloc-rate, and gc-heap-size, and even provides a recommended workflow at the end that starts with dotnet-counters.
- **Showed the specific dotnet-counters command with GC-relevant counter names**: 5/5 — The exact command `dotnet-counters monitor -p <PID> --counters System.Runtime` is provided, along with installation instructions. The relevant counter names are listed in a clear table with red-flag thresholds.
- **Explained how to capture a GC dump using dotnet-gcdump collect**: 5/5 — Step 3 explicitly shows `dotnet-gcdump collect -p <PID>`, notes it's low overhead and safe for production, and explains to open the .gcdump in Visual Studio or PerfView and sort by Inclusive Size.
- **Described what to look for in GC metrics: time-in-gc > 10%, frequent Gen2 collections, high alloc-rate**: 5/5 — The table in step 1 explicitly calls out time-in-gc >10%, frequent Gen2 collections, alloc-rate >5 GB/min, and steadily growing heap size as red flags. These are accurate and well-calibrated thresholds.
- **Suggested specific fixes based on diagnosis: ArrayPool for buffer reuse, reducing allocations in hot paths**: 5/5 — Step 4 provides a comprehensive table of common culprits with specific fixes: ArrayPool<byte>.Shared.Rent() for LOH allocations, StringBuilder/Span<char> for string concatenation, replacing LINQ with for loops, streaming JSON with IAsyncEnumerable, and using MemoryCache with size limits for unbounded caches.
- **Mentioned container-specific GC tuning (GCHeapCount, ServerGC settings)**: 5/5 — Step 5 is entirely dedicated to container-specific GC tuning. It covers DOTNET_gcServer=0, DOTNET_GCHeapCount, DOTNET_GCHeapHardLimit, and DOTNET_GCConserveMemory. It also provides the critical insight about Server GC detecting host cores rather than container limits, which is a well-known production gotcha.

## With-Skill Judge
Overall Score: 4/5
Reasoning: The agent produced a well-structured, actionable guide for diagnosing GC pressure in a containerized ASP.NET Core 8 application. The response is organized logically from diagnosis to analysis to fixes, with specific commands, clear thresholds, and practical advice. The main gaps are the omission of dotnet-gcdump (a key specialized tool for GC analysis) and GCHeapCount for container tuning. The session was efficient (single tool call, no errors), though the 'skill' tool call is opaque and we can't assess what it contributed. Overall, this is a good response that covers most of what's needed with only a couple of notable omissions.

- **Recommended starting with dotnet-counters to observe GC metrics in real-time (time-in-gc, gen counts, alloc-rate)**: 5/5 — The agent explicitly recommends starting with dotnet-counters in step 3, labels it 'your starting point,' and at the end reiterates 'Start with step 3.' It covers time-in-gc, gen counts (gen-0/1/2-gc-count), and alloc-rate clearly.
- **Showed the specific dotnet-counters command with GC-relevant counter names**: 5/5 — The agent provides a complete dotnet-counters monitor command with --counters listing gen-0-gc-count, gen-1-gc-count, gen-2-gc-count, gen-0-size, gen-1-size, gen-2-size, loh-size, gc-heap-size, alloc-rate, time-in-gc, gc-fragmentation, and --refresh-interval 1. This is thorough and correct.
- **Explained how to capture a GC dump using dotnet-gcdump collect**: 2/5 — The agent does NOT mention dotnet-gcdump at all. It covers dotnet-dump (a full process dump) and dotnet-trace, but dotnet-gcdump is a specialized tool specifically designed for GC heap analysis that is lighter weight and purpose-built. The dotnet-dump approach is a partial substitute but not the same tool. This is a notable omission.
- **Described what to look for in GC metrics: time-in-gc > 10%, frequent Gen2 collections, high alloc-rate**: 5/5 — The agent provides an excellent table with healthy vs. problematic thresholds: time-in-gc > 10%, gen-2-gc-count incrementing frequently, alloc-rate > 500 MB/s, LOH growing, and sawtooth heap patterns. This exceeds expectations with clear, actionable thresholds.
- **Suggested specific fixes based on diagnosis: ArrayPool for buffer reuse, reducing allocations in hot paths**: 4/5 — The agent provides a comprehensive table of common causes and fixes, including ArrayPool<byte>.Shared for large file reads, StringBuilder for string concatenation, IHttpClientFactory, streaming serialization, avoiding LINQ .ToList(), and IMemoryCache size limits. It mentions ArrayPool explicitly. It could have gone deeper on hot-path allocation reduction strategies (e.g., Span<T>, stackalloc, object pooling with ObjectPool<T>), but it covers the essentials well.
- **Mentioned container-specific GC tuning (GCHeapCount, ServerGC settings)**: 4/5 — The agent mentions DOTNET_gcServer=1, DOTNET_GCHeapHardLimit, and DOTNET_GCDynamicAdaptationMode=1 (a .NET 8 feature). However, it does NOT mention GCHeapCount specifically, which is important for containers where the runtime may detect more CPUs than allocated. It covers ServerGC and other container-relevant settings well, but misses the specific GCHeapCount tuning parameter mentioned in the rubric.

## Baseline Agent Output
```


Here's a systematic approach to diagnose GC pressure in your containerized .NET 8 app:

---

## 1. Confirm GC Is the Culprit

**Enable GC event counters in real-time:**
```bash
# From inside the container (or via dotnet-counters global tool)
dotnet-counters monitor -p <PID> --counters System.Runtime
```
Key metrics to watch:
| Counter | Red Flag |
|---|---|
| `gc-heap-size` | Steadily growing = possible leak |
| `gen-2-gc-count` | Frequent Gen2 collections = large/long-lived allocations |
| `time-in-gc` | >10% = significant GC pressure |
| `alloc-rate` | >5 GB/min on a small service = excessive allocations |

**If you don't have the tool installed:**
```bash
dotnet tool install -g dotnet-counters
dotnet tool install -g dotnet-gcdump
dotnet tool install -g dotnet-trace
```

---

## 2. Correlate GC Pauses with Latency Spikes

```bash
# Collect a trace with GC events for ~60 seconds
dotnet-trace collect -p <PID> \
  --providers Microsoft-DotNETCore-SampleProfiler,Microsoft-Windows-DotNETRuntime:0x1:5 \
  --duration 00:01:00
```
The `0x1` keyword = `GCKeyword`. This captures every GC pause. Open the `.nettrace` file in **PerfView** or **Speedscope** (`dotnet-trace convert --format speedscope`). Look for Gen2/blocking GC events that align with your latency spikes.

---

## 3. Find What's Allocating

**Capture a GC dump (low overhead, safe for production):**
```bash
dotnet-gcdump collect -p <PID>
```
Open the `.gcdump` in **Visual Studio** or **PerfView**. Sort by **Inclusive Size** — the top types are your pressure sources.

**For allocation-level detail (higher overhead):**
```bash
dotnet-trace collect -p <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0x200001:5 \
  --duration 00:00:30
```
`0x200001` = `GCKeyword | GCAllocationTickKeyword`. This samples every ~100KB of allocations and tells you *which types* are being allocated most.

---

## 4. Common .NET 8 Culprits & Fixes

| Pattern | Symptom | Fix |
|---|---|---|
| LOH allocations (byte[] >85KB) | Frequent Gen2 GCs, fragmentation | Use `ArrayPool<byte>.Shared.Rent()` / `RecyclableMemoryStream` |
| String concatenation in hot paths | High `System.String` alloc rate | Use `StringBuilder`, `string.Create`, or `Span<char>` |
| LINQ in request pipeline | Many small allocations per request | Replace with `for` loops in hot paths |
| Async state machines capturing large closures | `System.Runtime.CompilerServices` types in GC dump | Reduce captured variables, extract locals |
| JSON serialization (large responses) | `byte[]` / `JsonElement` on LOH | Stream with `IAsyncEnumerable`, use `System.Text.Json` source generators |
| Unbounded caches (`Dictionary`, `List`) | `gc-heap-size` grows over hours | Add eviction via `MemoryCache` with size limits |

---

## 5. Container-Specific GC Tuning

Check your current GC mode and container limits:
```bash
# Inside the container
cat /sys/fs/cgroup/memory.max          # cgroup v2
cat /sys/fs/cgroup/memory/memory.limit_in_bytes  # cgroup v1
```

**Critical environment variables to set in your Dockerfile/compose:**
```dockerfile
ENV DOTNET_GCConserveMemory=9        # 0-9, higher = more aggressive compaction
ENV DOTNET_gcServer=0                # Use workstation GC if <4 cores allocated
ENV DOTNET_GCHeapHardLimit=0x1E000000 # 480MB — set to ~75% of container memory limit
```

> **Key insight:** If your container has a 1–2 core CPU limit, **Server GC** (the default) creates one heap *per logical core it detects on the host* (not the container limit, unless `DOTNET_GCHeapCount` is set). This causes massive over-provisioning. Switch to **Workstation GC** (`gcServer=0`) or explicitly set:
```dockerfile
ENV DOTNET_GCHeapCount=2    # Match your container CPU limit
```

---

## 6. Quick Validation via Metrics Endpoint

Add this to `Program.cs` for ongoing visibility without external tools:
```csharp
builder.Services.AddSingleton<GcMetricsService>();
// ...
app.MapGet("/diag/gc", () => new
{
    Gen0 = GC.CollectionCount(0),
    Gen1 = GC.CollectionCount(1),
    Gen2 = GC.CollectionCount(2),
    TotalMemory = GC.GetTotalMemory(false),
    GCInfo = GC.GetGCMemoryInfo()
});
```
Hit this endpoint during spikes — if `Gen2` is incrementing rapidly, you've confirmed the issue.

---

**Recommended workflow:** Start with `dotnet-counters` (step 1) → confirm `time-in-gc` is high → capture `dotnet-gcdump` (step 3) → identify top types → check container GC config (step 5). Most containerized .NET latency spikes I've seen come from Server GC over-provisioning in CPU-limited containers.
```

## With-Skill Agent Output
```


Here's a step-by-step GC diagnosis plan for your containerized ASP.NET Core 8 app.

## 1. Install Diagnostic Tools (in the container)

```bash
dotnet tool install -g dotnet-counters
dotnet tool install -g dotnet-trace
dotnet tool install -g dotnet-dump
export PATH="$PATH:$HOME/.dotnet/tools"
```

## 2. Find Your Process

```bash
dotnet-counters ps
```

Note the PID of your API process.

## 3. Monitor GC Counters Live

This is your starting point — confirms whether GC is the culprit:

```bash
dotnet-counters monitor --process-id <PID> \
  --counters System.Runtime[gen-0-gc-count,gen-1-gc-count,gen-2-gc-count,gen-0-size,gen-1-size,gen-2-size,loh-size,gc-heap-size,alloc-rate,time-in-gc,gc-fragmentation] \
  --refresh-interval 1
```

### What to look for

| Counter | Healthy | **You have GC pressure if…** |
|---|---|---|
| `time-in-gc` | < 5% | **> 10%** — GC is stealing CPU time, causing your latency spikes |
| `gen-2-gc-count` | Rare (< 1/min) | **Incrementing frequently** — full blocking collections cause pauses |
| `alloc-rate` | Stable, < 100 MB/s | **> 500 MB/s** — excessive allocations driving GC |
| `gc-heap-size` | Stable | **Sawtooth or steady growth** — objects surviving too long |
| `loh-size` | Stable, small | **Growing** — large object allocations (>85KB) trigger Gen 2 GCs |

## 4. Collect Data for Deeper Analysis

Once you confirm GC pressure, collect a 30-second trace during a spike:

```bash
# GC-specific trace with allocation tracking
dotnet-trace collect --process-id <PID> \
  --providers Microsoft-DotNETCore-SampleProfiler,Microsoft-Windows-DotNETRuntime:0x1:5 \
  --duration 00:00:30 --output gc-trace.nettrace
```

The provider flags: `0x1` = GC keyword, `:5` = verbose level (includes allocation tick events).

Convert for visualization:

```bash
dotnet-trace convert gc-trace.nettrace --format speedscope --output gc-trace.speedscope.json
```

Open at [speedscope.app](https://www.speedscope.app/) — look for `GC` frames and time gaps in request processing.

## 5. Heap Dump — Find What's Allocating

```bash
dotnet-dump collect --process-id <PID> --output heap.dmp
dotnet-dump analyze heap.dmp
```

Inside the analyzer:

```
> dumpheap -stat
```

This shows all types sorted by total size. Look for:
- **Unexpectedly high counts** of `byte[]`, `string`, `Object[]`
- **Your domain types** appearing in large numbers (possible leaks)
- **Large objects** on the LOH (> 85KB)

Then drill into suspicious types:

```
> dumpheap -type System.Byte[] -min 85000
> gcroot <address-from-above>
```

This tells you exactly what code path is holding references.

## 6. Common ASP.NET Core GC Pressure Causes

| Root Cause | Evidence | Fix |
|---|---|---|
| Large response serialization | High `byte[]`/`string` count, high LOH | Use `System.Text.Json` streaming, `IAsyncEnumerable` |
| No `HttpClient` pooling | Many `Socket`/`HttpConnection` objects | Use `IHttpClientFactory` |
| LINQ `.ToList()` everywhere | High `List<T>` / `Object[]` alloc rate | Use `IEnumerable`, avoid materializing |
| String concatenation in loops | Massive `string` count | Use `StringBuilder` or `string.Create` |
| Unbounded caching | Heap grows monotonically | Add size limits / expiration to `IMemoryCache` |
| Large file reads into `byte[]` | LOH spikes | Use `ArrayPool<byte>.Shared` or `PipeReader` |

## 7. Quick Wins — Container GC Tuning

Add these to your `Dockerfile` or `DOTNET_` environment variables:

```dockerfile
# Use Server GC (default in containers, but verify)
ENV DOTNET_gcServer=1

# Limit heap to container memory (prevents OOM kills)
ENV DOTNET_GCHeapHardLimit=0x1E8480000  # e.g., ~8GB, set to 75% of container RAM

# Enable Dynamic Adaptation To Application Sizes (DATAS) - .NET 8+
ENV DOTNET_GCDynamicAdaptationMode=1
```

**Start with step 3** — the live counters will tell you within 30 seconds whether GC is your problem. If `time-in-gc` is > 10% and Gen 2 collections are frequent, proceed to steps 4–5 to find the allocating code paths.
```