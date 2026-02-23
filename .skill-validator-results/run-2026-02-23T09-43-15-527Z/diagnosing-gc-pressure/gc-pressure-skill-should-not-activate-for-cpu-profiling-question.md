# Judge Report: GC pressure skill should not activate for CPU profiling question

## Baseline Judge
Overall Score: 4/5
Reasoning: The agent provided a well-structured, accurate, and practical answer covering multiple CPU profiling approaches for .NET. It correctly recommended dotnet-trace with cpu-sampling as the primary tool, included a sensible workflow, and mentioned visualization options (SpeedScope, Visual Studio). The answer is focused on CPU profiling without drifting into irrelevant diagnostics. It didn't use any tools (no verification of commands), but for a knowledge-based question this is acceptable. The response could have been slightly more thorough (e.g., mentioning PerfView or async method considerations), but it covers the essentials well.

- **Did NOT suggest GC diagnostics for a CPU profiling question**: 5/5 — The agent did not mention GC diagnostics (e.g., dotnet-gcdump, GC counters, or garbage collection tuning) anywhere in its response. It stayed focused entirely on CPU profiling tools and workflows.
- **Suggested CPU profiling tools like dotnet-trace or dotnet-counters for CPU**: 5/5 — The agent recommended dotnet-trace with --profile cpu-sampling as the primary tool, dotnet-counters for quick triage, dotnet-stack for thread snapshots, and the Visual Studio CPU profiler. These are all appropriate and well-known CPU profiling tools for .NET. The commands shown are correct and the workflow is practical.

## With-Skill Judge
Overall Score: 5/5
Reasoning: The agent produced a clear, accurate, and well-structured guide for CPU profiling a .NET application. It correctly identified the right tools (dotnet-trace with cpu-sampling, dotnet-counters), provided the exact commands needed, and included the full workflow from installation through visualization. The response is concise, actionable, and avoids any irrelevant diagnostics like GC analysis. The only minor note is the agent used a 'skill' tool call whose result is opaque, but the final output is excellent regardless.

- **Did NOT suggest GC diagnostics for a CPU profiling question**: 5/5 — The agent stayed entirely focused on CPU profiling. There is no mention of GC diagnostics, memory dumps, garbage collection analysis, or any memory-related tooling. All recommendations (dotnet-trace with cpu-sampling, dotnet-counters monitoring cpu-usage) are squarely targeted at the CPU problem.
- **Suggested CPU profiling tools like dotnet-trace or dotnet-counters for CPU**: 5/5 — The agent recommended both dotnet-trace (with the --profile cpu-sampling flag, which is exactly the right profile for CPU hotspot analysis) and dotnet-counters (for live CPU usage monitoring). It also provided the full workflow: installing tools, finding the PID, collecting a trace, converting to speedscope format, and visualizing with speedscope/Visual Studio/PerfView. The commands are accurate and well-structured.

## Baseline Agent Output
```


Here are the main approaches to profile CPU usage in a .NET app:

---

### 1. `dotnet-trace` (Recommended — lightweight)
```powershell
# Install the tool
dotnet tool install --global dotnet-trace

# Collect a CPU trace (replace <PID> with your process ID)
dotnet-trace collect --process-id <PID> --profile cpu-sampling

# Convert to SpeedScope format for viewing in https://www.speedscope.app
dotnet-trace convert trace.nettrace --format speedscope
```

### 2. `dotnet-counters` (Quick triage)
```powershell
dotnet tool install --global dotnet-counters

# Live monitor CPU and other metrics
dotnet-counters monitor --process-id <PID> --counters System.Runtime
```

### 3. `dotnet-stack` (Instant snapshot)
```powershell
dotnet tool install --global dotnet-stack

# See what every thread is doing right now
dotnet-stack report --process-id <PID>
```

### 4. Visual Studio Profiler (GUI)
**Debug → Performance Profiler → CPU Usage** — gives a flame graph with per-method breakdowns.

---

### Typical workflow:
1. **`dotnet-counters`** to confirm CPU is high and identify the process.
2. **`dotnet-trace`** with `--profile cpu-sampling` to capture a trace.
3. Open the `.nettrace` file in **Visual Studio** or convert to **SpeedScope** — look for hot paths (methods with the highest inclusive/exclusive CPU time).

> **Tip:** Find your process ID with `dotnet-counters ps` or `Get-Process dotnet`.
```

## With-Skill Agent Output
```


Here's how to profile your .NET app for high CPU using the `dotnet-trace` diagnostic tool:

## 1. Install the diagnostic tools (if not already installed)

```powershell
dotnet tool install -g dotnet-counters
dotnet tool install -g dotnet-trace
```

## 2. Find your process ID

```powershell
dotnet-counters ps
```

This lists all running .NET processes with their PIDs.

## 3. (Optional) Quick-check with live counters

```powershell
dotnet-counters monitor --process-id <PID> --counters System.Runtime
```

Watch `cpu-usage` to confirm sustained high CPU. Press `q` to stop.

## 4. Collect a CPU trace

```powershell
dotnet-trace collect --process-id <PID> --profile cpu-sampling --duration 00:00:30 --output cpu-trace.nettrace
```

This samples the call stack for 30 seconds to find hot methods.

## 5. Convert & visualize

```powershell
dotnet-trace convert cpu-trace.nettrace --format speedscope --output cpu-trace.speedscope.json
```

Open `cpu-trace.speedscope.json` at **https://www.speedscope.app/** — the flame graph will show exactly which methods are consuming the most CPU time. You can also open the `.nettrace` file directly in **Visual Studio** or **PerfView**.

Want me to run these commands against a running process now?
```