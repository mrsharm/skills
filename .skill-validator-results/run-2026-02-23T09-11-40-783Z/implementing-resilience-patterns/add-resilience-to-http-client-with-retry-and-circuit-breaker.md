# Judge Report: Add resilience to HTTP client with retry and circuit breaker

## Baseline Judge
Overall Score: 4/5
Reasoning: The response is well-structured, technically accurate, and practical. It correctly identifies the modern .NET 8 approach, uses proper Polly v8 APIs, provides complete runnable code with customization examples, and includes a helpful service-level error handling example for BrokenCircuitException. The one notable gap is the omission of the rate limiter stage from the pipeline explanation. The agent also didn't use any tools (no verification of API correctness), but the content is accurate enough that this wasn't a significant issue. Overall a strong, production-ready answer with a minor completeness gap.

- **Used Microsoft.Extensions.Http.Resilience package (NOT raw Polly v8 directly)**: 5/5 — The agent explicitly recommends `Microsoft.Extensions.Http.Resilience`, shows the `dotnet add package` command, includes the `using Microsoft.Extensions.Http.Resilience;` directive, and correctly identifies it as built on Polly v8. No raw Polly wiring is used.
- **Showed AddStandardResilienceHandler() as the recommended starting point**: 5/5 — The agent centers the entire solution around `AddStandardResilienceHandler()`, calls it 'the recommended one-liner for most HTTP scenarios,' and demonstrates it as the primary extension method on `IHttpClientBuilder`.
- **Used Polly v8 API patterns (NOT v7 Policy.Handle/PolicyBuilder syntax)**: 5/5 — All code uses Polly v8 patterns: options-based configuration via `options.Retry`, `options.CircuitBreaker`, `options.AttemptTimeout`, `DelayBackoffType.Exponential`, etc. No v7 `Policy.Handle<>()` or `PolicyBuilder` syntax is present.
- **Explained the pipeline stages: rate limiter, total timeout, retry, circuit breaker, attempt timeout**: 3/5 — The agent lists four stages in a clear table (total request timeout, retry, circuit breaker, attempt timeout) and explains their purposes. However, the **rate limiter** stage is completely omitted. The standard resilience handler pipeline includes a rate limiter/bulkhead layer, which is missing from the explanation. This is a notable gap.
- **Showed how to customize individual strategies if defaults don't fit**: 5/5 — The agent demonstrates customization of retry (MaxRetryAttempts, Delay, UseJitter, BackoffType), circuit breaker (SamplingDuration, FailureRatioThreshold, MinimumThroughput, BreakDuration), and attempt timeout (Timeout) — all through the options object. This clearly shows how to override defaults for each individual strategy.
- **Mentioned exponential backoff with jitter for retry timing**: 5/5 — The agent explicitly sets `BackoffType = DelayBackoffType.Exponential` and `UseJitter = true`, and the closing note explains that 'jittered exponential backoff prevents thundering-herd retries against a recovering service.'

## With-Skill Judge
Overall Score: 4/5
Reasoning: The agent produced a high-quality, fully compilable working example with clear explanations and practical advice. The code correctly uses the modern Microsoft.Extensions.Http.Resilience approach with AddStandardResilienceHandler(), demonstrates customization of multiple strategies, and includes valuable operational wisdom (removing client.Timeout, jitter importance, zero changes needed in service code). The main gap is the omission of the rate limiter stage from the pipeline explanation and the incorrect ordering of stages in the summary table, which is a notable oversight for someone trying to understand the full resilience pipeline. The session was efficient — the agent created the project, installed the package, wrote the code, fixed a build error (missing Polly namespace), and verified compilation. No wasted steps.

- **Used Microsoft.Extensions.Http.Resilience package (NOT raw Polly v8 directly)**: 5/5 — The agent explicitly installed `Microsoft.Extensions.Http.Resilience` via `dotnet add package`, used it in the code (`using Microsoft.Extensions.Http.Resilience;`), and the final output explicitly states 'wraps Polly v8 — don't use raw Polly directly'. The csproj confirms the package reference. Perfect.
- **Showed AddStandardResilienceHandler() as the recommended starting point**: 5/5 — The agent prominently features `.AddStandardResilienceHandler()` chained on `AddHttpClient` in both the code (Program.cs line 16) and the summary output, calling it the 'Key change'. It's clearly positioned as the recommended starting point.
- **Used Polly v8 API patterns (NOT v7 Policy.Handle/PolicyBuilder syntax)**: 5/5 — All code uses Polly v8 patterns: `DelayBackoffType.Exponential`, `options.Retry.MaxRetryAttempts`, `options.CircuitBreaker.FailureRatio`, options-based configuration style. There is zero use of v7 `Policy.Handle`, `PolicyBuilder`, or `WaitAndRetryAsync` patterns. The `using Polly;` import is only for the `DelayBackoffType` enum.
- **Explained the pipeline stages: rate limiter, total timeout, retry, circuit breaker, attempt timeout**: 3/5 — The agent's table lists 4 of the 5 standard pipeline stages: Retry, Circuit Breaker, Attempt Timeout, and Total Timeout. However, the **rate limiter** stage (bulkhead/concurrency limiter) is completely omitted from both the explanation and the code customization. Additionally, the ordering in the table doesn't match the actual pipeline execution order (which is: rate limiter → total timeout → retry → circuit breaker → attempt timeout). The agent lists retry first, which misrepresents how the pipeline is structured.
- **Showed how to customize individual strategies if defaults don't fit**: 5/5 — The code explicitly customizes all mentioned strategies via the `options =>` lambda: retry (MaxRetryAttempts, Delay, BackoffType, UseJitter), circuit breaker (FailureRatio, SamplingDuration, MinimumThroughput, BreakDuration), total timeout, and attempt timeout. Each customization is accompanied by a comment explaining its purpose. Excellent demonstration of overriding defaults.
- **Mentioned exponential backoff with jitter for retry timing**: 5/5 — Both `BackoffType = DelayBackoffType.Exponential` and `UseJitter = true` are in the code with explanatory comments. The output table mentions '500ms exponential + jitter', and the critical details section explains why jitter matters: 'without it, all retries from all clients hit the server at the same instant' (thundering herd problem). Thorough coverage.

## Baseline Agent Output
```


In .NET 8+, use the **`Microsoft.Extensions.Http.Resilience`** package (built on Polly v8). It replaces manual Polly wiring with first-class `IHttpClientBuilder` extensions.

**1. Install the package:**

```
dotnet add package Microsoft.Extensions.Http.Resilience
```

**2. Update your registration:**

```csharp
using Microsoft.Extensions.Http.Resilience;

builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.com");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    // Retry: 3 attempts, exponential backoff, jitter, handles 5xx + timeouts
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.Retry.UseJitter = true;
    options.Retry.BackoffType = DelayBackoffType.Exponential;

    // Circuit breaker: opens after 10+ failures in 30s sampling window
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatioThreshold = 0.9;
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

    // Per-attempt timeout (shorter than the overall 30s)
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
});
```

**What `AddStandardResilienceHandler` gives you (in pipeline order):**

| Layer | Purpose |
|---|---|
| **Total request timeout** | Caps total time across all retries (default 30s) |
| **Retry** | Retries on 5xx, 408, and `TimeoutRejectedException` |
| **Circuit breaker** | Stops calling a failing endpoint temporarily |
| **Attempt timeout** | Per-try timeout (shorter than total) |

All layers handle `HttpRequestException`, timeout exceptions, and 5xx/408 responses out of the box — no manual `ShouldHandle` needed for your scenario.

**3. Handle the circuit breaker being open in your service:**

```csharp
using Polly.CircuitBreaker;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _client;

    public WeatherService(HttpClient client) => _client = client;

    public async Task<WeatherData?> GetWeatherAsync(string city, CancellationToken ct)
    {
        try
        {
            var response = await _client.GetAsync($"/v1/weather?city={city}", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WeatherData>(ct);
        }
        catch (BrokenCircuitException)
        {
            // Circuit is open — return fallback or cached data
            return null;
        }
    }
}
```

> **Key points:** `AddStandardResilienceHandler` is the recommended one-liner for most HTTP scenarios. Set `AttemptTimeout` shorter than the overall `HttpClient.Timeout` so retries have room to execute. The jittered exponential backoff prevents thundering-herd retries against a recovering service.
```

## With-Skill Agent Output
```
Here's what changed and why:

**Package:** `Microsoft.Extensions.Http.Resilience` (wraps Polly v8 — don't use raw Polly directly)

**Key change:** Replace your `AddHttpClient` call with `.AddStandardResilienceHandler()` chained on. This adds a full pipeline:

| Layer | Config | Purpose |
|-------|--------|---------|
| **Retry** | 3 attempts, 500ms exponential + jitter | Handles your 503s and timeouts |
| **Circuit Breaker** | Opens at 10% failures over 30s | Stops hammering a down service |
| **Attempt Timeout** | 10s per try | Kills individual slow attempts |
| **Total Timeout** | 30s across all retries | Hard deadline for the entire operation |

**Critical details:**
- **Remove `client.Timeout`** — the resilience handler manages timeouts now; having both causes conflicts
- **`UseJitter = true`** is essential — without it, all retries from all clients hit the server at the same instant
- The default `ShouldHandle` already covers 408, 429, and 5xx responses, which matches your failure scenarios (503 + timeouts)
- Your `WeatherService` code needs **zero changes** — the pipeline wraps the `HttpClient` automatically via `IHttpClientFactory`
```