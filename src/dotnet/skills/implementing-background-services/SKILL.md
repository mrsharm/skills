```skill
---
name: implementing-background-services
description: Implement background services in ASP.NET Core using BackgroundService and IHostedService, including periodic tasks, scoped dependency injection, graceful shutdown, and queue processing. Use when adding background work like timers, queue consumers, or long-running tasks to a web application.
---

# Implementing Background Services in ASP.NET Core

## When to Use

- Adding periodic/timer-based background tasks to an ASP.NET Core app
- Processing items from a queue or channel in the background
- Running a long-lived service alongside the web server
- Migrating from Windows Services or Hangfire to built-in hosting

## When Not to Use

- The user needs a distributed job scheduler (use Hangfire, Quartz.NET)
- The task runs once at startup only (use `IHostApplicationLifetime`)
- The user needs a separate worker process (use .NET Worker Service template)

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Background task description | Yes | What the service should do (timer, queue, event) |
| DI requirements | No | Whether the service needs scoped services like DbContext |

## Workflow

### Step 1: Create a BackgroundService

```csharp
public class OrderProcessingService : BackgroundService
{
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(ILogger<OrderProcessingService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderProcessingService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown — don't log as error
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderProcessingService");
                // Don't rethrow — rethrow kills the service permanently
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("OrderProcessingService stopped");
    }
}
```

**Register in Program.cs:**
```csharp
builder.Services.AddHostedService<OrderProcessingService>();
```

### Step 2: Handle scoped dependencies correctly (CRITICAL GOTCHA)

**`BackgroundService` is registered as a Singleton. You CANNOT inject Scoped services (like `DbContext`) directly into its constructor.** This throws at startup with `ValidateScopes` enabled.

**WRONG — will throw or cause bugs:**
```csharp
// ❌ WRONG: DbContext is Scoped, BackgroundService is Singleton
public class BadService : BackgroundService
{
    private readonly AppDbContext _db; // Captive dependency!

    public BadService(AppDbContext db) => _db = db;
}
```

**CORRECT — create a scope per work iteration:**
```csharp
public class CorrectService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CorrectService> _logger;

    public CorrectService(IServiceScopeFactory scopeFactory,
        ILogger<CorrectService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Create a new scope for each iteration
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var pendingOrders = await db.Orders
                    .Where(o => o.Status == OrderStatus.Pending)
                    .ToListAsync(stoppingToken);

                foreach (var order in pendingOrders)
                {
                    await ProcessOrderAsync(order, emailService, stoppingToken);
                    order.Status = OrderStatus.Processed;
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            // Scope disposed here — DbContext is properly cleaned up

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

### Step 3: Implement a periodic timer (PeriodicTimer — .NET 6+)

**`Task.Delay` has drift over time. Use `PeriodicTimer` for accurate intervals:**

```csharp
public class MetricCollectorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer compensates for work duration (no drift)
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CollectMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metric collection failed");
                // Timer continues on next tick despite error
            }
        }
    }
}
```

### Step 4: Implement a queue processor using Channel<T>

```csharp
// 1. Define the channel as a service
public class BackgroundTaskQueue
{
    private readonly Channel<Func<IServiceScopeFactory, CancellationToken, Task>> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
        _queue = Channel.CreateBounded<Func<IServiceScopeFactory, CancellationToken, Task>>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait // Back-pressure when full
            });
    }

    public async ValueTask QueueAsync(
        Func<IServiceScopeFactory, CancellationToken, Task> workItem,
        CancellationToken cancellationToken = default)
    {
        await _queue.Writer.WriteAsync(workItem, cancellationToken);
    }

    public async ValueTask<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(
        CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

// 2. Background service that processes the queue
public class QueueProcessorService : BackgroundService
{
    private readonly BackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueueProcessorService> _logger;

    public QueueProcessorService(BackgroundTaskQueue queue,
        IServiceScopeFactory scopeFactory, ILogger<QueueProcessorService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _queue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(_scopeFactory, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued work item");
            }
        }
    }
}

// 3. Register
builder.Services.AddSingleton<BackgroundTaskQueue>();
builder.Services.AddHostedService<QueueProcessorService>();

// 4. Enqueue from a controller
app.MapPost("/orders", async (Order order, BackgroundTaskQueue queue) =>
{
    await queue.QueueAsync(async (scopeFactory, ct) =>
    {
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOrderProcessor>();
        await processor.ProcessAsync(order, ct);
    });

    return Results.Accepted();
});
```

### Step 5: Handle graceful shutdown properly

```csharp
public class GracefulService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Register shutdown callback for cleanup
        stoppingToken.Register(() =>
        {
            _logger.LogInformation("Shutdown signal received, finishing current work...");
        });

        while (!stoppingToken.IsCancellationRequested)
        {
            // Check cancellation BEFORE starting expensive work
            if (stoppingToken.IsCancellationRequested)
                break;

            await ProcessBatchAsync(stoppingToken);
        }
    }

    // Override StopAsync for cleanup that needs to happen after ExecuteAsync returns
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Flushing remaining items...");
        await FlushAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
```

### Step 6: Handle the ExecuteAsync exception behavior

**Before .NET 8:** An unhandled exception in `ExecuteAsync` was **silently swallowed** — the service stopped but the app kept running with no error.

**In .NET 8+:** An unhandled exception in `ExecuteAsync` **crashes the host** by default.

```csharp
// To get .NET 8+ behavior in older versions, configure:
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.StopHost; // Crash on unhandled exception
});

// To get the old behavior in .NET 8+ (NOT recommended):
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.Ignore;
});
```

**Always catch exceptions inside your loop** to prevent either behavior from being triggered unexpectedly.

## Validation

- [ ] Scoped services accessed via `IServiceScopeFactory`, NOT injected directly
- [ ] `CancellationToken` (stoppingToken) is passed to all async operations
- [ ] Exceptions inside the work loop are caught and logged (not rethrown)
- [ ] `OperationCanceledException` is handled separately from other exceptions
- [ ] `PeriodicTimer` used instead of `Task.Delay` for drift-sensitive intervals
- [ ] Service registered via `AddHostedService<T>()`

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Injecting `DbContext` directly into BackgroundService | Use `IServiceScopeFactory` to create scope per iteration |
| Rethrowing exceptions in the work loop | Kills the service; catch, log, and continue |
| Using `Task.Delay` for periodic work | Drift accumulates; use `PeriodicTimer` (.NET 6+) |
| Not passing `stoppingToken` to async methods | Service hangs during shutdown |
| `ExecuteAsync` blocking synchronously | Blocks app startup; always use `await` or `Task.Run` |
| Not configuring `BackgroundServiceExceptionBehavior` | Silent failures pre-.NET 8; crashes post-.NET 8 |
```
