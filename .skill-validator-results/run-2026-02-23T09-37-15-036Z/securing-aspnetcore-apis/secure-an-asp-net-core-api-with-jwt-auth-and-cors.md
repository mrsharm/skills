# Judge Report: Secure an ASP.NET Core API with JWT auth and CORS

## Baseline Judge
Overall Score: 2/5
Reasoning: The agent produced reasonably good code in the files (JWT auth, CORS, rate limiting), but fundamentally failed to deliver a useful response. The task timed out at 120 seconds while the agent was still debugging a build failure caused by targeting net10.0 instead of the requested net8.0. The final output the user sees is a debugging message about a missing NuGet package — not the complete, explained solution they asked for. The middleware order is wrong per the rubric (UseRateLimiter before UseAuthentication), which also creates a functional bug with the per-user rate limiter. The agent spent 26 tool calls mostly scaffolding and debugging, never reaching the point of presenting and explaining the solution. Key security teaching points (middleware ordering) were never communicated.

- **Added JWT bearer auth with proper TokenValidationParameters (ValidateIssuer, ValidateAudience, ValidateLifetime all true)**: 4/5 — The code in Program.cs (lines 14-33) has excellent JWT configuration: ValidateIssuer=true, ValidateAudience=true, ValidateLifetime=true, ValidateIssuerSigningKey=true, and ClockSkew=TimeSpan.Zero. However, the agent's final output to the user doesn't present or explain this code at all — it's a debugging message about a missing NuGet package. The user never actually sees the JWT setup explained. Deducting one point because the deliverable isn't communicated.
- **Configured CORS with specific origins (NOT AllowAnyOrigin with credentials)**: 4/5 — The code (lines 38-51) correctly uses WithOrigins(allowedOrigins) driven by config, with AllowCredentials(). The appsettings.json shows a specific origin 'https://spa.yourdomain.com'. This is the correct approach — no AllowAnyOrigin. But again, the final output never presents or explains this to the user.
- **Added rate limiting using AddRateLimiter with a fixed or sliding window policy**: 3/5 — The code (lines 54-82) has a well-designed rate limiting setup with both a global fixed-window policy and a per-user policy. However, the build never succeeded (the project targets net10.0 and the rate limiting package wasn't added to .csproj), and the agent's final output is actually about diagnosing this build failure. The code exists in the file but was never verified to compile. The agent's suggestion to add 'Microsoft.AspNetCore.RateLimiting' package is the debugging output, not a complete solution.
- **Placed middleware in correct order: UseCors → UseAuthentication → UseAuthorization → UseRateLimiter**: 2/5 — The code (lines 89-93) uses the order: UseCors → UseRateLimiter → UseAuthentication → UseAuthorization. This doesn't match the rubric's required order (UseCors → UseAuthentication → UseAuthorization → UseRateLimiter). This is also a functional bug: the per-user rate limiter policy (lines 68-81) checks context.User?.Identity?.IsAuthenticated, but since UseRateLimiter runs before UseAuthentication, the user identity won't be populated, so all requests will fall back to IP-based limiting. The middleware is in the wrong order.
- **Explicitly stated that middleware order matters and incorrect order silently bypasses security**: 1/5 — The code has a brief comment on line 88 ('Order matters: CORS → Rate Limiting → Auth → Controllers') but the agent's final output — which is what the user would see — contains zero discussion of middleware ordering. The final output is entirely about a missing NuGet package build error. The critical security teaching point about middleware ordering was never communicated to the user.
- **Did NOT disable token validation parameters or use AllowAnyOrigin in production config**: 5/5 — All TokenValidationParameters are set to true (ValidateIssuer, ValidateAudience, ValidateLifetime, ValidateIssuerSigningKey). CORS uses specific origins via WithOrigins(), not AllowAnyOrigin(). The configuration is secure and nothing is improperly disabled.

## With-Skill Judge
Overall Score: 4/5
Reasoning: The agent produced a high-quality, secure, and production-ready Program.cs that meets all rubric criteria perfectly. The code includes JWT bearer auth with proper validation, specific CORS origins, rate limiting with fixed window, correct middleware ordering, explicit documentation about middleware order importance, and no security anti-patterns. Additional good touches include: security headers middleware, FallbackPolicy requiring auth by default, health endpoint with AllowAnonymous, configurable settings via appsettings.json, and helpful code comments. The build was verified to succeed. The only minor issue is the final output text was truncated (the session timed out at 120s and the summary explanation was cut off), but the code itself is complete and correct. Deducting slightly from 5 because the explanatory output was incomplete/truncated.

- **Added JWT bearer auth with proper TokenValidationParameters (ValidateIssuer, ValidateAudience, ValidateLifetime all true)**: 5/5 — Lines 9-24 show JWT bearer authentication configured with TokenValidationParameters where ValidateIssuer=true, ValidateAudience=true, ValidateLifetime=true, and also ValidateIssuerSigningKey=true with a sensible ClockSkew of 5 minutes. Authority and Audience are read from configuration. This is textbook correct.
- **Configured CORS with specific origins (NOT AllowAnyOrigin with credentials)**: 5/5 — Lines 39-50 configure CORS with WithOrigins() reading from config (specific origins), explicit methods and headers, and AllowCredentials(). No AllowAnyOrigin. The config in appsettings.json shows a placeholder 'https://your-spa-domain.com' — properly specific. Also includes a null check with InvalidOperationException if origins aren't configured.
- **Added rate limiting using AddRateLimiter with a fixed or sliding window policy**: 5/5 — Lines 53-68 configure a GlobalLimiter using PartitionedRateLimiter with GetFixedWindowLimiter (100 permits per 1-minute window), partitioned by authenticated user name or IP fallback. Rejection status code is properly set to 429. This is a complete and well-designed rate limiting setup.
- **Placed middleware in correct order: UseCors → UseAuthentication → UseAuthorization → UseRateLimiter**: 5/5 — Lines 91-97 show exactly the correct order: UseCors('SpaPolicy') → UseAuthentication() → UseAuthorization() → UseRateLimiter(). This is the correct and recommended ordering.
- **Explicitly stated that middleware order matters and incorrect order silently bypasses security**: 5/5 — The agent's output explicitly states: 'Middleware order is critical (lines 76–97): UseCors → UseAuthentication → UseAuthorization → UseRateLimiter. Wrong order silently breaks auth or CORS preflight.' The code also has comments: 'Middleware pipeline (ORDER MATTERS)' on line 74, and 'CORS must come before auth so browser preflight (OPTIONS) succeeds' on line 90, and 'Rate limiting after auth → can partition by authenticated user' on line 96.
- **Did NOT disable token validation parameters or use AllowAnyOrigin in production config**: 5/5 — All token validation parameters are set to true (ValidateIssuer, ValidateAudience, ValidateLifetime, ValidateIssuerSigningKey). No AllowAnyOrigin is used anywhere. CORS uses WithOrigins with specific configured origins. The configuration is production-safe.

## Baseline Agent Output
```
**Found the issue!** The namespace `Microsoft.AspNetCore.RateLimiting` is **missing** from the global usings, and more importantly, **the NuGet package for rate limiting is not referenced in the project**.

Looking at **SecureApi.csproj** (line 10), only `Microsoft.AspNetCore.Authentication.JwtBearer` is listed. The rate limiting extension methods come from a separate package that's not installed.

**Solution:**
Add this to your `.csproj`:
```xml
<PackageReference Include="Microsoft.AspNetCore.RateLimiting" Version="10.0.3" />
```

**Why it's failing:**
- `AddRateLimiter()` and `AddFixedWindowLimiter()` are extension methods defined in the **Microsoft.AspNetCore.RateLimiting** NuGet package
- In .NET 10, this is a separate package (not part of the base framework)
- Without the package reference, the compiler can't find these extension methods

**Additional fix needed:**
Add the namespace to Program.cs (line 1):
```csharp
using Microsoft.AspNetCore.RateLimiting;
```

After adding the package reference, rebuild the project and it should compile successfully.
```

## With-Skill Agent Output
```
Build succeeds. Here's the final `Program.cs`:Here's what was configured — key things to know:

**Middleware order is critical** (lines 76–97): `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter`. Wrong order silently breaks auth or CORS preflight.

**Security defaults:**
- **FallbackPolicy** (line 30) requires auth on *every* endpoint — mark exceptions with `.AllowAnonymous()` (like `/health` on line 100)
- **CORS** whitelists only your SPA origin with explicit methods/headers (no `AllowAnyOrigin`)
- **Rate limiter** partitions by authenticated user name, falling back to IP (100 req/min)
- **Security headers** added via
```