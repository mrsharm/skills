# Judge Report: Fix captive dependency causing memory leak

## Baseline Judge
Overall Score: 4/5
Reasoning: The agent did an excellent job identifying the captive dependency problem, explaining the root cause clearly, providing a complete working code fix using IServiceScopeFactory, and mentioning the simpler AddScoped alternative. The code compiles and the explanation is well-structured with a helpful comparison table. The only significant gap is the omission of ValidateScopes, which is an important development-time diagnostic that helps catch these issues early. The approach was efficient (7 tool calls, methodical progression from diagnosis to fix to verification), though the errors at the end appear to be infrastructure issues rather than agent mistakes. Overall a strong response missing one rubric criterion.

- **Identified the captive dependency: singleton service capturing a scoped DbContext**: 5/5 — The agent explicitly identified this as a 'Captive Dependency' problem, clearly explaining that AddDbContext registers AppDbContext as scoped while OrderService is a singleton, and that the singleton captures and holds the first DbContext instance forever.
- **Explained why this causes a memory leak (DbContext never disposed, holds connections)**: 5/5 — The agent clearly explained that the DbContext 'is never disposed, its connection is never released, and its change tracker grows unboundedly.' The summary table also contrasts 'Lives forever (captured by singleton)' vs 'Created and disposed per operation' and 'Leak — never returned to pool' vs 'Properly returned to pool'.
- **Suggested fixing by either changing OrderService to Scoped or using IServiceScopeFactory**: 5/5 — The agent provided both approaches: the primary fix uses IServiceScopeFactory (with full code implementation), and the output explicitly mentions the alternative: 'If OrderService doesn't need to be a singleton, the simpler fix is just changing AddSingleton → AddScoped in your registration so lifetimes match.'
- **Showed concrete code for the IServiceScopeFactory pattern**: 5/5 — The agent rewrote OrderService.cs with a complete, working IServiceScopeFactory implementation. The constructor takes IServiceScopeFactory, and both methods (GetOrderAsync, SaveOrderAsync) use 'using var scope = _scopeFactory.CreateScope()' to get a fresh DbContext. The code compiles successfully with zero warnings.
- **Mentioned ValidateScopes for detecting these issues in development**: 1/5 — The agent did not mention ValidateScopes (or UseDefaultServiceProvider with ValidateScopes/ValidateOnBuild) anywhere in its output or code. This is a key diagnostic/preventive recommendation for detecting captive dependency issues during development that was completely omitted.

## With-Skill Judge
Overall Score: 5/5
Reasoning: The agent performed excellently on all criteria. It correctly diagnosed the captive dependency problem, provided a clear explanation of the consequences (connection leaks, stale data, thread-safety), implemented a complete and compilable IServiceScopeFactory fix, verified the build succeeded, and included the ValidateScopes tip. The approach was efficient — only 8 tool calls to diagnose, fix, and verify. The final output is well-structured and informative. The errors at the end were post-completion API issues, not related to the quality of the work.

- **Identified the captive dependency: singleton service capturing a scoped DbContext**: 5/5 — The agent clearly identified this as a 'captive dependency' problem, explicitly stating that the singleton OrderService captures the scoped AppDbContext. This was identified early and correctly.
- **Explained why this causes a memory leak (DbContext never disposed, holds connections)**: 5/5 — The agent explained multiple consequences: the DbContext is created once and never disposed causing connection leaks, stale data from change tracking, and thread-safety issues since EF contexts are not thread-safe. This goes beyond the minimum of just mentioning disposal/connections.
- **Suggested fixing by either changing OrderService to Scoped or using IServiceScopeFactory**: 5/5 — The agent chose the IServiceScopeFactory approach, which is the canonical fix when the singleton lifetime must be preserved. The alternative (making OrderService scoped) was not explicitly mentioned, but the chosen approach is correct and well-justified.
- **Showed concrete code for the IServiceScopeFactory pattern**: 5/5 — The agent wrote complete, working code in OrderService.cs: constructor injects IServiceScopeFactory, each method creates a scope with `using var scope = _scopeFactory.CreateScope()`, resolves AppDbContext from the scope's service provider, and properly disposes via the using statement. The code compiles successfully as verified by the build step.
- **Mentioned ValidateScopes for detecting these issues in development**: 5/5 — The agent included a tip: 'Enable ValidateScopes = true in development to catch these bugs at startup instead of in production.' This is exactly the recommended practice for detecting captive dependency issues.

## Baseline Agent Output
```
Build succeeds. Here's a summary of the issue and fix:

| | Before (broken) | After (fixed) |
|---|---|---|
| **Injected** | `AppDbContext` directly | `IServiceScopeFactory` |
| **DbContext lifetime** | Lives forever (captured by singleton) | Created and disposed per operation |
| **Connections** | Leak — never returned to pool | Properly returned to pool |

Each method now creates a short-lived scope via `using var scope = _scopeFactory.CreateScope()`, gets a fresh `DbContext`, and disposes both when done.

**Alternative:** If `OrderService` doesn't need to be a singleton, the simpler fix is just changing `AddSingleton` → `AddScoped` in your registration so lifetimes match.
```

## With-Skill Agent Output
```
Build succeeds. Here's a summary of the problem and fix:

**Problem:** Singleton `OrderService` captured a scoped `AppDbContext` via constructor injection. The `DbContext` was created once and never disposed — causing connection leaks, stale data, and thread-safety issues (EF contexts are not thread-safe).

**Fix:** Instead of injecting `AppDbContext` directly, `OrderService` now injects `IServiceScopeFactory` and creates a **fresh scope + DbContext per operation**. Each `DbContext` is properly disposed via `using var scope`.

Your registration stays as-is — no changes needed there:
```csharp
services.AddSingleton<IOrderService, OrderService>();
services.AddDbContext<AppDbContext>();  // scoped by default — correct
```

> **Tip:** Enable `ValidateScopes = true` in development to catch these bugs at startup instead of in production.
```