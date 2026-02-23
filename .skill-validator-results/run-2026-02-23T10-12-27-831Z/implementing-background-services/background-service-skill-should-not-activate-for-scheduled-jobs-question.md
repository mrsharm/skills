# Judge Report: Background service skill should not activate for scheduled jobs question

## Baseline Judge
Overall Score: 5/5
Reasoning: The answer is concise, accurate, and directly addresses the question. It provides a complete working example with NuGet packages, service registration, job scheduling, and a sample service class. The key points section adds valuable context about idempotency, cron expressions, and persistence. No tools were needed for this knowledge-based question, and the agent responded efficiently in a single turn.

- **Did NOT suggest BackgroundService for a Hangfire scheduling question**: 5/5 — The agent correctly stayed focused on Hangfire and did not mention or suggest BackgroundService as an alternative. The entire answer is Hangfire-specific.
- **Provided Hangfire-specific guidance using RecurringJob or similar**: 5/5 — The agent provided accurate, detailed Hangfire-specific guidance using RecurringJob.AddOrUpdate, Cron.Daily(3, 0), proper service registration with AddHangfire/AddHangfireServer, and included key points about idempotency, persistence, and the cron expression equivalent. The code example is correct and follows modern ASP.NET Core (minimal hosting) conventions.

## With-Skill Judge
Overall Score: 5/5
Reasoning: The agent produced an excellent, well-structured answer that directly addresses the question. It provides a complete, step-by-step guide with accurate code examples, a helpful summary table covering important considerations (time zones, storage, dashboard security, retries), and an alternative approach using the Cron helper. The response is technically accurate, comprehensive, and well-organized. No tools were needed for this knowledge question, and the agent correctly refrained from making unnecessary tool calls.

- **Did NOT suggest BackgroundService for a Hangfire scheduling question**: 5/5 — The agent focused entirely on Hangfire and did not mention BackgroundService, IHostedService, or any other non-Hangfire scheduling mechanism. The response stays on-topic throughout.
- **Provided Hangfire-specific guidance using RecurringJob or similar**: 5/5 — The agent provided comprehensive Hangfire-specific guidance including: RecurringJob.AddOrUpdate with correct syntax, proper cron expression ("0 3 * * *"), the Cron.Daily(3, 0) helper alternative, NuGet package installation, Hangfire service configuration (AddHangfire, AddHangfireServer), dashboard setup, time zone handling via RecurringJobOptions, storage requirements, and retry behavior. The code examples are accurate and follow the modern minimal hosting model for ASP.NET Core.

## Baseline Agent Output
```


Use `RecurringJob.AddOrUpdate` with a Cron expression in your `Program.cs` (or `Startup.cs`):

```csharp
// 1. Install NuGet packages
// Hangfire.Core, Hangfire.SqlServer (or your storage), Hangfire.AspNetCore

// 2. In Program.cs, register services
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireDb")));
builder.Services.AddHangfireServer();

var app = builder.Build();

// 3. Schedule the recurring job
app.UseHangfireDashboard(); // optional: adds /hangfire UI
RecurringJob.AddOrUpdate<MyJobService>(
    "daily-3am-job",
    service => service.RunAsync(),
    Cron.Daily(3, 0));  // runs every day at 03:00
```

Where `MyJobService` is any registered service:

```csharp
public class MyJobService
{
    public async Task RunAsync()
    {
        // your work here
    }
}
```

**Key points:**
- `Cron.Daily(3, 0)` is shorthand for the cron expression `"0 3 * * *"` — you can use either.
- The job ID (`"daily-3am-job"`) is idempotent; calling `AddOrUpdate` on app restart updates rather than duplicates the schedule.
- Hangfire persists jobs to the configured storage (SQL Server, Redis, etc.), so jobs survive app restarts.
```

## With-Skill Agent Output
```


## Scheduling a Daily Job with Hangfire in ASP.NET Core

**1. Install the NuGet packages:**

```
dotnet add package Hangfire.Core
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.SqlServer   # or your preferred storage provider
```

**2. Configure Hangfire in `Program.cs`:**

```csharp
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireDb")));
builder.Services.AddHangfireServer();

var app = builder.Build();

app.UseHangfireDashboard(); // optional: exposes /hangfire UI

// Schedule the recurring job to run daily at 3 AM
RecurringJob.AddOrUpdate<MyJobService>(
    "daily-3am-job",
    service => service.ExecuteAsync(),
    "0 3 * * *");           // Cron expression: minute 0, hour 3, every day

app.Run();
```

**3. Define your job service:**

```csharp
public class MyJobService
{
    public async Task ExecuteAsync()
    {
        // Your daily work here
    }
}
```

### Key points

| Topic | Detail |
|---|---|
| **Cron expression** | `"0 3 * * *"` = daily at 3:00 AM. Use `Cron.Daily(3)` helper as an alternative. |
| **Time zone** | Defaults to UTC. Pass a `TimeZoneInfo` to use local time: `RecurringJob.AddOrUpdate(..., "0 3 * * *", new RecurringJobOptions { TimeZone = TimeZoneInfo.Local })` |
| **Storage** | Hangfire requires persistent storage (SQL Server, PostgreSQL, Redis, etc.) to track jobs. |
| **Dashboard** | `UseHangfireDashboard()` gives a web UI at `/hangfire` for monitoring — **secure it in production** with authorization filters. |
| **Retries** | Failed jobs are automatically retried (default: 10 attempts with exponential back-off). |

### Using the `Cron` helper instead of a raw expression

```csharp
RecurringJob.AddOrUpdate<MyJobService>(
    "daily-3am-job",
    service => service.ExecuteAsync(),
    Cron.Daily(3, 0));  // hour: 3, minute: 0
```
```