---
name: configuring-native-aot
description: Configure .NET Native AOT compilation, resolve trimming warnings, and fix reflection-based code for AOT compatibility. Use when publishing AOT binaries, encountering trim/AOT warnings, or migrating existing apps to AOT.
---

# Configuring Native AOT

## When to Use

- Publishing a .NET app with `PublishAot=true`
- Encountering IL2xxx trim warnings or AOT analysis warnings
- Converting an existing app to be AOT-compatible
- Reducing startup time or binary size for cloud/serverless deployments

## When Not to Use

- The app has no AOT/trimming requirements
- The app heavily depends on unbounded reflection (e.g., large plugin systems)
- The user wants runtime JIT performance tuning, not AOT

## Inputs

| Input | Required | Description |
|-------|----------|-------------|
| Project path | Yes | The `.csproj` to configure for AOT |
| Target runtime | No | e.g., `linux-x64`, `win-x64` (defaults to current OS) |

## Workflow

### Step 1: Enable AOT publishing

Add to the `.csproj`:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

**Critical:** Also set the `IsAotCompatible` flag to enable compile-time analysis:

```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
</PropertyGroup>
```

### Step 2: Publish and capture all warnings

```bash
dotnet publish -r linux-x64 -c Release 2>&1
```

Warnings fall into these categories:

| Warning | Category | Severity |
|---------|----------|----------|
| `IL2026` | `RequiresUnreferencedCode` — method uses patterns unsafe for trimming | High |
| `IL2057`-`IL2075` | Reflection usage that trimmer can't analyze | High |
| `IL2104` | Assembly with embedded `rd.xml` — usually safe | Low |
| `IL3050` | `RequiresDynamicCode` — needs runtime code gen | High (blocks AOT) |
| `IL3053` | Specific type instantiation can't be AOT-compiled | High |

### Step 3: Fix reflection-based serialization

The #1 AOT issue. JSON serialization MUST use source generators:

**Before (fails with AOT):**
```csharp
var json = JsonSerializer.Serialize(obj);
var result = JsonSerializer.Deserialize<MyType>(json);
```

**After (AOT-compatible):**
```csharp
[JsonSerializable(typeof(MyType))]
[JsonSerializable(typeof(List<MyType>))]
internal partial class AppJsonContext : JsonSerializerContext { }

// Usage:
var json = JsonSerializer.Serialize(obj, AppJsonContext.Default.MyType);
var result = JsonSerializer.Deserialize(json, AppJsonContext.Default.MyType);
```

For ASP.NET Core Minimal APIs, register the context:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});
```

### Step 4: Fix other common AOT issues

| Pattern | Problem | AOT-Safe Alternative |
|---------|---------|---------------------|
| `Activator.CreateInstance<T>()` | Trimmer can't see T | Use factory pattern or DI |
| `Type.GetType(string)` | Dynamic type loading | Use `[DynamicallyAccessedMembers]` annotation |
| `typeof(T).GetProperties()` | Unbounded reflection | Use source generators or `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]` |
| `Assembly.Load(name)` | Dynamic assembly loading | Not supported in AOT — redesign |
| `Expression.Compile()` | Runtime code gen | Use static delegates or precompiled expressions |
| `services.AddControllers()` | MVC uses reflection heavily | Use Minimal APIs with `MapGet`/`MapPost` |

### Step 5: Annotate unavoidable reflection

When reflection can't be eliminated, annotate to preserve types:

```csharp
// Tell the trimmer this parameter needs its public constructors preserved
public T Create<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
    where T : new()
{
    return new T();
}
```

For methods that genuinely require dynamic code:

```csharp
[RequiresUnreferencedCode("Uses reflection for plugin loading")]
[RequiresDynamicCode("Creates types at runtime")]
public void LoadPlugin(string typeName) { /* ... */ }
```

### Step 6: Validate the AOT binary

```bash
# Publish
dotnet publish -r linux-x64 -c Release -o ./publish

# Check binary size
ls -la ./publish/

# Run and test
./publish/MyApp
```

Verify:
- Zero IL20xx/IL30xx warnings during publish
- Binary runs without `MissingMethodException` or `TypeLoadException`
- All API endpoints return correct data (serialization works)

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Missing `JsonSerializerContext` for nested types | Add `[JsonSerializable]` for EVERY type in the object graph |
| Third-party NuGet not AOT-compatible | Check package's `IsAotCompatible`; consider alternatives |
| EF Core not fully AOT-compatible | Use Dapper or raw SQL for AOT scenarios |
| ConfigurationBinder uses reflection | Use source-generated config binding: `builder.Services.Configure<T>()` with `[OptionsValidator]` |
| gRPC client generation | Use `Grpc.Net.ClientFactory` with `GenerateClientFactory=true` |
