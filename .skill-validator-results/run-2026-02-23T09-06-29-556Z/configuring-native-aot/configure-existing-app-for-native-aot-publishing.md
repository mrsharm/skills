# Judge Report: Configure existing app for Native AOT publishing

## Baseline Judge
Overall Score: 4/5
Reasoning: The agent produced a well-structured, technically accurate guide covering the core requirements: csproj configuration, JSON source generator, pipeline registration, and the publish command. The explanation of why source generators replace reflection is clear. The main gaps are: (1) no verification steps for the published binary, (2) limited coverage of non-JSON AOT pitfalls like Activator.CreateInstance. The agent's process was efficient — it checked for existing project files, found none, and provided comprehensive guidance from scratch. The content is correct and practical, though it could have gone deeper on broader AOT compatibility concerns and verification.

- **Set PublishAot=true in the .csproj file**: 5/5 — The agent clearly shows `<PublishAot>true</PublishAot>` in the `.csproj` file, along with complementary settings like `<IsAotCompatible>true</IsAotCompatible>` and `<TrimmerSingleWarn>false</TrimmerSingleWarn>`. The explanation table also calls out what `PublishAot` does.
- **Created a JsonSerializerContext with [JsonSerializable] attributes for all serialized types**: 5/5 — The agent provides a complete `AppJsonContext.cs` file with `[JsonSerializable(typeof(List<Todo>))]` and `[JsonSerializable(typeof(Todo))]` attributes on a partial class extending `JsonSerializerContext`. It also states the rule of thumb that every type crossing a JSON boundary must appear in a `[JsonSerializable]` attribute.
- **Registered the source-generated JSON context with the ASP.NET Core pipeline**: 5/5 — The agent shows `builder.Services.ConfigureHttpJsonOptions(options => { options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default); });` in `Program.cs`, which is the correct way to register the source-generated context. The explanation table confirms why `TypeInfoResolverChain.Insert(0, …)` is needed.
- **Explained the need to replace reflection-based serialization with source generators**: 5/5 — The agent explicitly states: 'Native AOT cannot use runtime reflection for System.Text.Json. You must create a source generator context for every type you serialize/deserialize.' The summary table also explains `WebApplication.CreateSlimBuilder` excludes reflection-based services that break AOT, and the `JsonSerializerContext` subclass source-generates (de)serializers with no reflection at runtime.
- **Mentioned common AOT pitfalls like Activator.CreateInstance or dynamic type loading**: 3/5 — The agent provides a table of common AOT warnings (IL2026, IL3050, IL2057, IL2072) and their fixes, mentions `Type.GetType(string)` and `DynamicallyAccessedMembers`. However, it does not explicitly mention `Activator.CreateInstance`, dynamic assembly loading, or other common reflection pitfalls beyond JSON serialization. The coverage is decent but not comprehensive on the broader AOT pitfall landscape.
- **Showed how to publish and verify the AOT binary**: 3/5 — The agent shows the `dotnet publish -c Release -r linux-arm64` command and mentions the output path. However, there's no verification step — no mention of running the binary locally, checking for warnings during publish, testing with Lambda runtime interface emulator, or validating the binary works. The publish command itself is correct but verification is absent.

## With-Skill Judge
Overall Score: 4/5
Reasoning: The agent produced a clean, well-structured, and technically correct AOT configuration for an ASP.NET Core Minimal API. The code compiles with zero warnings, uses all the right patterns (CreateSlimBuilder, JsonSerializerContext, ConfigureHttpJsonOptions, PublishAot), and the summary is clear with a useful table and actionable guidelines. The main gaps are: (1) no AWS Lambda-specific configuration was added despite the task explicitly mentioning Lambda deployment (no Lambda packages, bootstrap handling, or Lambda-specific publish guidance), and (2) the actual `dotnet publish` was never run to prove native AOT compilation works end-to-end. The core AOT configuration knowledge is solid, but the Lambda deployment angle was largely ignored.

- **Set PublishAot=true in the .csproj file**: 5/5 — The .csproj clearly has `<PublishAot>true</PublishAot>` on line 7, plus `<IsAotCompatible>true</IsAotCompatible>` on line 8 for compile-time trimming analysis. Both were explained in the summary table.
- **Created a JsonSerializerContext with [JsonSerializable] attributes for all serialized types**: 5/5 — Lines 39-41 of Program.cs define `AppJsonContext : JsonSerializerContext` with `[JsonSerializable(typeof(Product))]` and `[JsonSerializable(typeof(List<Product>))]`, covering both types used in the API endpoints. The summary also advises adding `[JsonSerializable(typeof(T))]` for every type in request/response graphs going forward.
- **Registered the source-generated JSON context with the ASP.NET Core pipeline**: 5/5 — Lines 6-9 show `builder.Services.ConfigureHttpJsonOptions(options => { options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default); });` which correctly wires the source-generated context into the Minimal API pipeline. This was also explained in the summary table.
- **Explained the need to replace reflection-based serialization with source generators**: 4/5 — The summary table states 'Source-generated JSON serializer — no runtime reflection needed' and explains that CreateSlimBuilder 'Excludes reflection-heavy services'. The summary also mentions the rule to add [JsonSerializable] for every type. However, the explanation could have been more detailed about WHY reflection fails under AOT (no JIT compiler to generate code at runtime, types can be trimmed away, etc.).
- **Mentioned common AOT pitfalls like Activator.CreateInstance or dynamic type loading**: 5/5 — The 'Key rules to avoid AOT warnings going forward' section explicitly lists: 'Avoid `Activator.CreateInstance`, `Type.GetType(string)`, `Assembly.Load` — use DI or factory patterns instead', plus warns against `AddControllers()` and advises checking third-party NuGet packages for `IsAotCompatible`.
- **Showed how to publish and verify the AOT binary**: 3/5 — The publish command `dotnet publish -r linux-x64 -c Release -o ./publish` is shown. The agent also ran `dotnet build -c Release` to verify zero trim/AOT warnings. However, the agent did not actually run `dotnet publish` to produce and verify the native binary. Also, for AWS Lambda specifically, there's no mention of the Lambda-specific runtime identifier, bootstrap file expectations, or AWS Lambda tooling (like Amazon.Lambda.AspNetCoreServer.Hosting or the `dotnet lambda` CLI). The task explicitly asked about AWS Lambda deployment but the solution is a generic Minimal API without Lambda integration.

## Baseline Agent Output
```
The directory is empty. Let me check if there's a project elsewhere or create the configuration from scratch. Since there's no existing project, I'll walk you through the full setup.

---

## Native AOT ASP.NET Core Minimal API for AWS Lambda

### 1. Project File (`.csproj`) Configuration

Add these properties and the Lambda + AOT packages:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>

    <!-- Treat trimmer/AOT warnings as errors so nothing slips through -->
    <IsAotCompatible>true</IsAotCompatible>
    <TrimmerSingleWarn>false</TrimmerSingleWarn>
  </PropertyGroup>

  <PackageReference Include="Amazon.Lambda.AspNetCoreServer.Hosting" Version="1.*" />
  <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.*" />

</Project>
```

### 2. JSON Source Generator (fixes AOT warnings)

Native AOT cannot use runtime reflection for `System.Text.Json`. You must create a **source generator context** for every type you serialize/deserialize:

```csharp
// AppJsonContext.cs
using System.Text.Json.Serialization;

[JsonSerializable(typeof(List<Todo>))]
[JsonSerializable(typeof(Todo))]
// Add every request/response type your API uses
public partial class AppJsonContext : JsonSerializerContext { }
```

### 3. Wire It Up in `Program.cs`

```csharp
using Amazon.Lambda.AspNetCoreServer.Hosting;
using System.Text.Json;

var builder = WebApplication.CreateSlimBuilder(args);

// Use the source-generated serializer (AOT-safe)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

// Add Lambda hosting support
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var app = builder.Build();

app.MapGet("/todos", () => new List<Todo>
{
    new(1, "Learn AOT", true),
    new(2, "Deploy to Lambda", false)
});

app.MapPost("/todos", (Todo todo) => Results.Created($"/todos/{todo.Id}", todo));

app.Run();

public record Todo(int Id, string Title, bool IsComplete);
```

Key points:

| Item | Why |
|---|---|
| `WebApplication.CreateSlimBuilder` | Excludes reflection-based services that break AOT |
| `PublishAot` in csproj | Enables the Native AOT compiler |
| `JsonSerializerContext` subclass | Source-generates (de)serializers — no reflection at runtime |
| `TypeInfoResolverChain.Insert(0, …)` | Registers the source-gen context for Minimal API JSON handling |
| `IsAotCompatible` + `TrimmerSingleWarn=false` | Surfaces per-warning details instead of collapsing them |

### 4. Publish

```powershell
# For Lambda (Amazon Linux), cross-compile to linux-x64 or linux-arm64
dotnet publish -c Release -r linux-arm64
```

The output in `bin/Release/net8.0/linux-arm64/publish/` is a single self-contained native binary — no .NET runtime needed on the host.

### 5. Common AOT Warning Fixes

| Warning | Fix |
|---|---|
| `IL2026` / `IL3050` — reflection-based serialization | Add the type to `[JsonSerializable(typeof(...))]` on your context |
| `IL2057` — `Type.GetType(string)` | Replace with a direct type reference or `typeof()` |
| `IL2072` — value from method with `DynamicallyAccessedMembers` | Add `[DynamicallyAccessedMembers]` attribute to the receiving parameter |
| MVC / Razor warnings | Not supported — stick to Minimal APIs |

> **Rule of thumb:** Every type that crosses a JSON boundary (request body, response body, Lambda event) must appear in a `[JsonSerializable]` attribute on your `JsonSerializerContext`.
```

## With-Skill Agent Output
```
Build succeeded with **zero trim/AOT warnings**. Here's a summary of the key changes:

### What was configured

| Change | Why |
|--------|-----|
| **`PublishAot=true` + `IsAotCompatible=true`** in `.csproj` | Enables AOT compilation and compile-time trimming analysis |
| **`WebApplication.CreateSlimBuilder()`** instead of `CreateBuilder()` | Excludes reflection-heavy services (like MVC) not needed for Minimal APIs |
| **`AppJsonContext : JsonSerializerContext`** with `[JsonSerializable]` | Source-generated JSON serializer — no runtime reflection needed |
| **`ConfigureHttpJsonOptions`** registering the context | Tells Minimal API endpoints to use the AOT-safe serializer |

### To publish for AWS Lambda (linux-x64)

```bash
dotnet publish -r linux-x64 -c Release -o ./publish
```

### Key rules to avoid AOT warnings going forward

1. **Add `[JsonSerializable(typeof(T))]`** for every type in your API's request/response graph
2. **Avoid `Activator.CreateInstance`, `Type.GetType(string)`, `Assembly.Load`** — use DI or factory patterns instead
3. **Don't use `AddControllers()`** — stick to `MapGet`/`MapPost` Minimal API endpoints
4. **Check third-party NuGet packages** for `IsAotCompatible` — some (like EF Core) aren't fully AOT-safe
```