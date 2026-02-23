# Judge Report: Add health checks with Kubernetes probes

## Baseline Judge
Overall Score: 4/5
Reasoning: The agent produced a high-quality, buildable ASP.NET Core health check implementation. The code follows best practices: tag-based separation, a thread-safe custom startup health check with `volatile`, proper NuGet packages, clean JSON response writing, and correct endpoint mapping. The approach was efficient (19 tool calls to scaffold, install packages, create files, and verify the build). The only notable gap is the lack of explanation about WHY liveness probes should not check external dependencies (restart cascading), which is an important Kubernetes concept the user should understand. Everything else — code quality, Kubernetes YAML, architecture — is excellent.

- **Separated liveness and readiness health checks using tags**: 5/5 — Tags are properly implemented. The StartupHealthCheck uses the `"live"` tag, while SQL Server and Redis checks use the `"ready"` tag. Endpoint mappings use `Predicate = check => check.Tags.Contains("live")` and `check.Tags.Contains("ready")` to filter appropriately. Clean, idiomatic implementation.
- **Liveness probe does NOT check external dependencies (database, Redis) — only process health**: 5/5 — The `/healthz/live` endpoint only runs checks tagged `"live"`, which is just the `StartupHealthCheck`. This check only verifies the app has finished starting (via a `volatile bool _isReady` flag). It does NOT touch SQL Server or Redis. This is the correct pattern — liveness only checks process health.
- **Readiness probe checks database and Redis connectivity**: 5/5 — The `/healthz/ready` endpoint runs checks tagged `"ready"`, which includes `.AddSqlServer(...)` and `.AddRedis(...)` from the `AspNetCore.HealthChecks.SqlServer` and `AspNetCore.HealthChecks.Redis` NuGet packages (both version 9.0.0). Connection strings are provided in `appsettings.json`. This correctly checks external dependency connectivity.
- **Mapped separate endpoints for liveness and readiness (e.g., /healthz/live and /healthz/ready)**: 5/5 — Two distinct endpoints are mapped: `/healthz/live` and `/healthz/ready`, each with their own `HealthCheckOptions` and tag-based predicates. Both include a custom JSON response writer (`WriteMinimalResponse`) that returns status, check names, and durations.
- **Explained WHY liveness should not check dependencies (restart cascading)**: 2/5 — The agent's output says 'Kubernetes restarts the pod if this fails' for liveness and 'Kubernetes stops routing traffic if this fails' for readiness. The code comments mirror this. However, there is NO explicit explanation of WHY liveness should avoid checking external dependencies — specifically the restart cascading problem (if a database goes down temporarily, all pods would be killed and restarted simultaneously, causing an outage cascade). The agent describes the behavior but not the critical reasoning behind the design decision.
- **Provided Kubernetes probe YAML configuration or explained probe settings**: 5/5 — A complete Kubernetes YAML snippet is provided with both `livenessProbe` and `readinessProbe` including `httpGet` paths, port, `initialDelaySeconds`, and `periodSeconds`. The values are reasonable (liveness: 5s initial, 10s period; readiness: 10s initial, 15s period). This is directly usable in a pod spec.

## With-Skill Judge
Overall Score: 5/5
Reasoning: The agent delivered an exemplary implementation that hits every rubric criterion perfectly. The approach was methodical: scaffold project → install NuGet packages → implement health checks with tag-based separation → add K8s YAML → verify build succeeds. The code is clean, idiomatic, and production-aware (timeouts, AllowAnonymous, detailed response writers). Going beyond requirements, the agent added a startup probe, detailed JSON response for readiness, and connection string placeholders. The explanation of design decisions (especially the restart cascading rationale) demonstrates genuine understanding. The only very minor nitpicks are wildcard package versions and leftover weather forecast boilerplate, neither of which affect the health check implementation quality.

- **Separated liveness and readiness health checks using tags**: 5/5 — The agent cleanly separated checks using tags: `tags: ["live"]` for the self-check and `tags: ["ready"]` for database and Redis. The tag-based filtering is idiomatic ASP.NET Core health check design.
- **Liveness probe does NOT check external dependencies (database, Redis) — only process health**: 5/5 — The liveness endpoint (`/healthz/live`) uses `Predicate = check => check.Tags.Contains("live")` which only matches the trivial self-check (`AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])`). Database and Redis are tagged only with "ready" and are completely excluded from the liveness path.
- **Readiness probe checks database and Redis connectivity**: 5/5 — Both `.AddSqlServer()` and `.AddRedis()` are registered with `tags: ["ready"]` and 5-second timeouts. The readiness endpoint filters on the "ready" tag. The detailed JSON response writer even reports per-check status, description, and duration — very useful for debugging.
- **Mapped separate endpoints for liveness and readiness (e.g., /healthz/live and /healthz/ready)**: 5/5 — Three distinct endpoints are mapped: `/healthz/live` (liveness), `/healthz/ready` (readiness), and a bonus `/healthz/startup` (startup probe). Each uses tag-based predicates to serve only the appropriate checks. The endpoints also have `AllowAnonymous()` which is important for K8s probes.
- **Explained WHY liveness should not check dependencies (restart cascading)**: 5/5 — The agent's output explicitly states: 'DB/Redis are readiness-only — if they go down, Kubernetes stops traffic but won't restart pods (which would make an outage worse)'. This directly addresses the restart cascading problem. The code comments also reinforce this design rationale.
- **Provided Kubernetes probe YAML configuration or explained probe settings**: 5/5 — A complete `k8s-probes.yaml` file was created with properly configured livenessProbe, readinessProbe, and startupProbe. It includes appropriate settings like `initialDelaySeconds`, `periodSeconds`, `failureThreshold` with helpful comments (e.g., 'Allow up to 150s for startup'). The agent also noted in the output to copy it into the Deployment manifest.

## Baseline Agent Output
```
Build succeeded with 0 warnings and 0 errors. Here's what was set up:

**Endpoints:**
- **`/healthz/live`** — Liveness probe. Returns healthy once the app has started; Kubernetes restarts the pod if this fails.
- **`/healthz/ready`** — Readiness probe. Checks SQL Server and Redis connectivity; Kubernetes stops routing traffic if this fails.

**Files created/modified:**
- `Program.cs` — Registers health checks with tags (`live`/`ready`) and maps the two endpoints with a JSON response writer.
- `HealthChecks/StartupHealthCheck.cs` — Custom check that reports unhealthy during startup, healthy after.
- `appsettings.json` — Added `ConnectionStrings` for `SqlServer` and `Redis`.

**Kubernetes usage example:**
```yaml
livenessProbe:
  httpGet:
    path: /healthz/live
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
readinessProbe:
  httpGet:
    path: /healthz/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 15
```
```

## With-Skill Agent Output
```
Here's what was added:

| Endpoint | Purpose | Checks |
|---|---|---|
| `/healthz/live` | Liveness probe — restarts pod on failure | Process self-check only |
| `/healthz/ready` | Readiness probe — stops traffic on failure | SQL Server + Redis (detailed JSON) |
| `/healthz/startup` | Startup probe — grace period for cold start | All checks |

**Key design decisions:**
- DB/Redis are **readiness-only** — if they go down, Kubernetes stops traffic but won't restart pods (which would make an outage worse)
- All health endpoints have `AllowAnonymous()` and 5-second timeouts
- `k8s-probes.yaml` has the matching Kubernetes probe configuration — copy it into your Deployment manifest

Update the connection strings in `appsettings.json` (or via environment variables) to match your actual infrastructure.
```