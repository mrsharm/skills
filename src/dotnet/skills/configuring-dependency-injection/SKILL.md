---
name: configuring-dependency-injection
description: Configure .NET dependency injection correctly, diagnosing and fixing lifetime scoping bugs including captive dependencies, scoped services in singletons, and disposal issues. Use when setting up DI containers, debugging service resolution failures, or fixing lifetime mismatch bugs.
---

# Configuring Dependency Injection

## When to Use

- Setting up `IServiceCollection` registrations for a .NET app
- Debugging `InvalidOperationException` from service resolution
- Fixing "Cannot consume scoped service from singleton" errors
- Diagnosing memory leaks caused by incorrect service lifetimes
- Registering open generics, decorators, or keyed services (.NET 8+)

## When Not to Use

- The user is using a third-party DI container (Autofac, Ninject) — patterns differ
- The issue is not related to service resolution

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Service registrations | Yes | The `IServiceCollection` setup code |
| Error message | No | Any `InvalidOperationException` or resolution error |

## Workflow

### Step 1: Understand the three lifetimes

| Lifetime | Created | Disposed | Use For |
|----------|---------|----------|---------|
| **Transient** | Every time requested | When scope ends | Lightweight, stateless services |
| **Scoped** | Once per scope (HTTP request in ASP.NET) | When scope ends | DbContext, Unit of Work |
| **Singleton** | Once, first request | App shutdown | Caches, HttpClient, config |

### Step 2: Detect captive dependency violations

**The #1 DI bug.** A service must NEVER depend on a shorter-lived service:

```
Singleton → can depend on → Singleton only
Scoped    → can depend on → Scoped or Singleton
Transient → can depend on → Transient, Scoped, or Singleton
```

**Captive dependency = long-lived service holds a short-lived dependency, preventing it from being disposed/recreated.**

Example bug:

```csharp
// WRONG: Singleton captures a Scoped service — DbContext never gets disposed!
services.AddSingleton<IOrderService, OrderService>();  // lives forever
services.AddScoped<AppDbContext>();                     // should live per-request

public class OrderService  // singleton!
{
    private readonly AppDbContext _db;  // captured! stale data, connection leaks
    public OrderService(AppDbContext db) => _db = db;
}
```

**Fix options:**

```csharp
// Option 1: Match lifetimes
services.AddScoped<IOrderService, OrderService>();

// Option 2: Inject IServiceScopeFactory for on-demand scopes
services.AddSingleton<IOrderService, OrderService>();

public class OrderService
{
    private readonly IServiceScopeFactory _scopeFactory;
    public OrderService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task ProcessOrder(Order order)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // db is properly scoped and disposed
    }
}
```

### Step 3: Enable scope validation in development

**Critical:** ASP.NET Core only validates this in Development by default:

```csharp
var builder = WebApplication.CreateBuilder(args);
// This is already enabled in Development, but verify:
// builder.Host.UseDefaultServiceProvider(options =>
// {
//     options.ValidateScopes = true;
//     options.ValidateOnBuild = true;  // Catches missing registrations at startup
// });
```

For non-web apps, enable explicitly:

```csharp
var host = Host.CreateDefaultBuilder(args)
    .UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    })
    .Build();
```

### Step 4: Register correctly by pattern

**Open generics:**
```csharp
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

**Multiple implementations:**
```csharp
services.AddScoped<INotifier, EmailNotifier>();
services.AddScoped<INotifier, SmsNotifier>();
// Inject IEnumerable<INotifier> to get all, or last-registered wins for single
```

**Keyed services (.NET 8+):**
```csharp
services.AddKeyedScoped<ICache, RedisCache>("redis");
services.AddKeyedScoped<ICache, MemoryCache>("memory");
// Inject with [FromKeyedServices("redis")] ICache cache
```

**Decorators (manual wrapping):**
```csharp
services.AddScoped<OrderRepository>();
services.AddScoped<IOrderRepository>(sp =>
    new CachingOrderRepository(
        sp.GetRequiredService<OrderRepository>(),
        sp.GetRequiredService<IMemoryCache>()));
```

### Step 5: Common resolution errors and fixes

| Error | Cause | Fix |
|-------|-------|-----|
| `No service for type 'X' has been registered` | Missing registration | Add `services.AddScoped<IX, X>()` |
| `Cannot consume scoped service 'X' from singleton 'Y'` | Captive dependency | Change Y to scoped or inject `IServiceScopeFactory` |
| `Unable to resolve service while attempting to activate 'X'` | X has unregistered constructor deps | Register all constructor parameters |
| `A circular dependency was detected` | A→B→A | Break cycle with `Lazy<T>`, factory, or redesign |
| Multiple constructors exception | DI can't choose | Use `[ActivatorUtilitiesConstructor]` on preferred ctor |

## Validation

- [ ] `ValidateOnBuild = true` passes at startup (no missing registrations)
- [ ] `ValidateScopes = true` doesn't throw in Development (no captive dependencies)
- [ ] No Scoped services injected into Singletons
- [ ] `IDisposable` services properly disposed (check with logging)

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Registering DbContext as Singleton | Always `AddDbContext` (scoped) — EF contexts are NOT thread-safe |
| `IHttpClientFactory` vs `HttpClient` | Register with `AddHttpClient<T>()`, never `AddSingleton<HttpClient>()` |
| Forgetting `IDisposable` tracked by DI | DI tracks disposables; singleton disposables leak until shutdown |
| Using `BuildServiceProvider()` in ConfigureServices | Creates a second container — use `Configure<T>` instead |
| Background service with scoped deps | Inject `IServiceScopeFactory`, create scope in `ExecuteAsync` |
