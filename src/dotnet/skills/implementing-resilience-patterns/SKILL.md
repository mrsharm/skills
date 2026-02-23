---
name: implementing-resilience-patterns
description: Implement resilience patterns in .NET using Microsoft.Extensions.Http.Resilience and Polly v8, including retry, circuit breaker, timeout, and hedging strategies. Use when adding fault tolerance to HTTP clients, external service calls, or distributed systems.
---

# Implementing Resilience Patterns

## When to Use

- Adding retry logic to HTTP client calls
- Implementing circuit breakers for failing external services
- Configuring timeout policies for slow downstream services
- Adding hedging (parallel fallback requests) for critical calls

## When Not to Use

- The user wants simple try/catch error handling
- The service is internal and on the same machine (no network failure mode)
- The user is using an older Polly v7 project and doesn't want to migrate

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| HTTP client or service code | Yes | The code making external calls |
| Failure scenarios | No | Timeouts, 5xx errors, network failures |

## Workflow

### Step 1: Install the correct packages

**Use the Microsoft wrapper, NOT raw Polly v8 directly:**

```bash
dotnet add package Microsoft.Extensions.Http.Resilience
```

This includes Polly v8 and integrates with `IHttpClientFactory`.

**Do NOT install `Polly` directly** — the API in v8 is completely different from v7, and most examples online are outdated v7 patterns.

### Step 2: Add the standard resilience handler (recommended starting point)

```csharp
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.com");
})
.AddStandardResilienceHandler();
```

`AddStandardResilienceHandler()` adds this pipeline automatically:

| Strategy | Default Config | Purpose |
|----------|---------------|---------|
| Rate Limiter | 1000 concurrent | Prevents overwhelming downstream |
| Total Timeout | 30s | Hard deadline for entire request |
| Retry | 3 attempts, exponential backoff + jitter | Handles transient failures |
| Circuit Breaker | 10% failure rate, 5s break | Stops cascading failures |
| Attempt Timeout | 10s per attempt | Prevents individual attempt hanging |

### Step 3: Customize the standard handler

```csharp
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.com");
})
.AddStandardResilienceHandler(options =>
{
    // Customize retry
    options.Retry.MaxRetryAttempts = 5;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;  // ALWAYS use jitter to avoid thundering herd
    options.Retry.ShouldHandle = args => ValueTask.FromResult(
        args.Outcome.Result?.StatusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or >= HttpStatusCode.InternalServerError);

    // Customize circuit breaker
    options.CircuitBreaker.FailureRatio = 0.1;  // 10% failure rate
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.MinimumThroughput = 20;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(5);

    // Customize timeouts
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
});
```

### Step 4: Add hedging for critical calls

Hedging sends parallel requests to reduce latency. Use for idempotent reads only:

```csharp
builder.Services.AddHttpClient<ICriticalService, CriticalService>(client =>
{
    client.BaseAddress = new Uri("https://api.critical.com");
})
.AddStandardHedgingHandler(options =>
{
    options.Hedging.MaxHedgedAttempts = 2;
    options.Hedging.Delay = TimeSpan.FromMilliseconds(500);  // Wait before hedging
});
```

### Step 5: Add resilience to non-HTTP code

For database calls, message queues, or other non-HTTP operations:

```csharp
// Register a named pipeline
builder.Services.AddResiliencePipeline("database", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(200),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<SqlException>(ex =>
                ex.IsTransient)
        })
        .AddTimeout(TimeSpan.FromSeconds(10));
});

// Inject and use
public class OrderRepository
{
    private readonly ResiliencePipeline _pipeline;

    public OrderRepository(
        [FromKeyedServices("database")] ResiliencePipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task<Order> GetOrderAsync(int id, CancellationToken ct)
    {
        return await _pipeline.ExecuteAsync(async token =>
        {
            // Database call here
            return await _db.Orders.FindAsync(new object[] { id }, token);
        }, ct);
    }
}
```

## Key Rules

| Rule | Why |
|------|-----|
| Always use jitter in retry delays | Prevents thundering herd when service recovers |
| Never retry non-idempotent mutations (POST) | Could create duplicate records |
| Set total timeout LOWER than caller's timeout | Prevents upstream timeout before retry completes |
| Circuit breaker needs minimum throughput | Avoids opening circuit on low-traffic endpoints |
| Don't retry on 400-level errors | Client errors won't succeed on retry |

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Using Polly v7 syntax with v8 packages | v8 API is completely different — use `AddStandardResilienceHandler` |
| Retrying on all HTTP status codes | Only retry on 408, 429, and 5xx — not 4xx |
| Missing `UseJitter = true` | Without jitter, all retries hit the server simultaneously |
| Infinite retry loops | Always set `MaxRetryAttempts` and a total timeout |
| Circuit breaker configured per-client-instance | Register as singleton pipeline or use `IHttpClientFactory` |
