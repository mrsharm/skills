# Judge Report: Implement a periodic background service with scoped DbContext

## Baseline Judge
Overall Score: 5/5
Reasoning: The agent produced an excellent, production-quality implementation that addresses every aspect of the user's question. The OrderProcessingService.cs correctly inherits from BackgroundService, uses IServiceScopeFactory to resolve scoped DbContext per iteration, implements two-level error handling (loop-level and per-order), passes CancellationToken throughout, and uses PeriodicTimer for accurate timing. The supporting files (Order.cs, AppDbContext.cs) provide complete context. The explanation is clear and directly addresses the user's scope validation error. The agent was efficient (4 turns, minimal tool usage). All 7 rubric criteria are fully satisfied with best-practice implementations.

- **Explained that BackgroundService is Singleton and DbContext is Scoped — cannot inject directly (captive dependency)**: 5/5 — The agent's output explicitly states: 'BackgroundService is a singleton, but DbContext is scoped. You can't inject scoped into singleton.' This is mentioned both in the inline reasoning during file creation and in the final explanation. The concept is clearly and accurately conveyed.
- **Used IServiceScopeFactory to create a new scope per work iteration**: 5/5 — Line 7 of OrderProcessingService.cs injects IServiceScopeFactory, and line 45 calls `_scopeFactory.CreateScope()` inside `ProcessPendingOrdersAsync`, which is invoked once per loop iteration. This is the correct pattern.
- **Retrieved DbContext from the scope via scope.ServiceProvider.GetRequiredService()**: 5/5 — Line 46: `var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();` — exactly the correct approach to resolve the scoped DbContext from the newly created scope.
- **Disposed the scope properly (using statement or explicit dispose)**: 5/5 — Line 45 uses `using var scope = _scopeFactory.CreateScope();` which is a C# using declaration that disposes the scope (and the DbContext within it) when the method exits. Proper disposal is ensured.
- **Wrapped the work loop body in try-catch and logged errors without rethrowing**: 5/5 — Two levels of error handling: (1) Lines 28-36 wrap the entire ProcessPendingOrdersAsync call with a catch that logs and continues the loop, using a filter `when (ex is not OperationCanceledException)` to avoid swallowing shutdown signals. (2) Lines 59-69 wrap individual order processing, logging per-order failures and marking orders as Failed so one bad order doesn't block others. Both catch blocks log via ILogger and do not rethrow.
- **Passed stoppingToken (CancellationToken) to async operations for graceful shutdown**: 5/5 — The stoppingToken is passed to: timer.WaitForNextTickAsync (line 26), ProcessPendingOrdersAsync (line 30), ToListAsync (line 50), and SaveChangesAsync (line 72). All async operations properly propagate the cancellation token for responsive shutdown.
- **Used PeriodicTimer or Task.Delay for timing, and handled OperationCanceledException on shutdown**: 5/5 — Uses PeriodicTimer (line 23) which is the preferred .NET 6+ approach. OperationCanceledException is handled via the exception filter `when (ex is not OperationCanceledException)` on line 32, allowing it to propagate naturally and exit ExecuteAsync cleanly. The explanation also correctly notes that PeriodicTimer doesn't drift like Task.Delay.

## With-Skill Judge
Overall Score: 4/5
Reasoning: The agent produced excellent, production-quality code that correctly addresses all aspects of the task. The BackgroundService implementation is idiomatic, using PeriodicTimer, IServiceScopeFactory, proper error handling at multiple levels, and graceful shutdown. The code built and ran successfully, processing seed orders as verified by the API endpoint. The only weakness is that the final output explanation was truncated due to session timeout — the agent spent time on verification (building, running, testing via API) which was valuable but left insufficient time for the full written explanation. The code itself, however, is exemplary and addresses the user's scope validation error directly.

- **Explained that BackgroundService is Singleton and DbContext is Scoped — cannot inject directly (captive dependency)**: 4/5 — The agent explained this both in code comments (lines 17-19 of the service: 'DbContext is scoped, so injecting it into a singleton BackgroundService causes a scope validation error') and in the final output ('BackgroundService is a singleton, but AppDbContext is scoped. Injecting it directly violates scope validation'). It also showed the ❌ vs ✅ pattern. The output was truncated due to timeout so the explanation was cut short, but the key concept was communicated.
- **Used IServiceScopeFactory to create a new scope per work iteration**: 5/5 — The constructor injects IServiceScopeFactory (line 14), and a new scope is created per iteration in ProcessPendingOrdersAsync (line 57: '_scopeFactory.CreateScope()'). This is the correct and idiomatic pattern.
- **Retrieved DbContext from the scope via scope.ServiceProvider.GetRequiredService()**: 5/5 — Line 58 clearly shows: 'var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();' — exactly the correct pattern.
- **Disposed the scope properly (using statement or explicit dispose)**: 5/5 — Line 57 uses 'using var scope = _scopeFactory.CreateScope();' which ensures the scope (and the DbContext it contains) is disposed when the method exits. This is proper resource management.
- **Wrapped the work loop body in try-catch and logged errors without rethrowing**: 5/5 — There are two levels of error handling: (1) The main loop body has a try-catch (lines 33-46) that catches general exceptions, logs them, and explicitly comments 'Don't rethrow — keep the service alive for the next cycle'. (2) Individual order processing also has a try-catch (lines 75-86) that logs per-order failures and marks orders as Failed. Excellent error handling strategy.
- **Passed stoppingToken (CancellationToken) to async operations for graceful shutdown**: 5/5 — The stoppingToken is passed to: ProcessPendingOrdersAsync (line 35), ToListAsync(ct) (line 62), SaveChangesAsync(ct) (line 89), and timer.WaitForNextTickAsync(stoppingToken) (line 48). All async operations respect the cancellation token for graceful shutdown.
- **Used PeriodicTimer or Task.Delay for timing, and handled OperationCanceledException on shutdown**: 5/5 — Uses PeriodicTimer (line 28: 'new PeriodicTimer(_interval)') which is the modern .NET 6+ approach. Handles OperationCanceledException specifically when shutdown is requested (lines 37-41) with the 'when (stoppingToken.IsCancellationRequested)' guard, which breaks cleanly out of the loop. The do-while pattern also ensures immediate execution on startup before waiting for the first tick.

## Baseline Agent Output
```
Now register it in your `Program.cs`:

```csharp
// Register the background service — add this line:
builder.Services.AddHostedService<OrderProcessingService>();
```

Here's why each piece matters:

**Scope error fix** — `BackgroundService` is a singleton, but `DbContext` is scoped. You can't inject scoped into singleton. The fix is injecting `IServiceScopeFactory` and calling `CreateScope()` each iteration (line 43–44 in the service), giving EF Core a fresh `DbContext` per cycle.

**Graceful shutdown** — `PeriodicTimer.WaitForNextTickAsync` accepts the `CancellationToken` from the host. When the app stops, the token is cancelled, `OperationCanceledException` propagates naturally, and `ExecuteAsync` exits cleanly.

**Error resilience** — The `catch (Exception ex) when (ex is not OperationCanceledException)` on line 32 logs failures but keeps the loop running. Individual order failures are caught separately (line 62) so one bad order doesn't block the rest.

**30-second interval** — Uses `PeriodicTimer` (.NET 6+), which is more accurate than `Task.Delay` and doesn't drift over time.
```

## With-Skill Agent Output
```
All orders show `Status = 1` (Completed) with timestamps. Stopping the app:Everything works. Here's a summary of the key files:

**The scope validation fix:** `BackgroundService` is a **singleton**, but `AppDbContext` is **scoped**. Injecting it directly violates scope validation. The fix is to inject `IServiceScopeFactory` and create a new scope per iteration:

```csharp
// ❌ This causes the scope validation error
public OrderProcessingService(AppDbContext db) { ... }

// ✅ Create a scope per iteration instead
public OrderProcessingService(IServiceScopeFactory scopeFactory
```