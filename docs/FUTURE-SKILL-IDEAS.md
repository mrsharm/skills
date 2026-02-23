# Future .NET Skill Ideas

> Prioritized backlog of new skills to expand coverage across the .NET developer workflow. Each idea includes the category it belongs to, the developer pain point it solves, example trigger prompts, and a rough scope estimate.

---

## Priority 1 — High Impact, Clear Scope

These fill the most requested gaps and have well-defined boundaries.

### `querying-ef-core`

| | |
|---|---|
| **Category** | Development |
| **Pain point** | Agents generate raw SQL or incorrect LINQ; miss migrations, eager/lazy loading pitfalls, and connection lifetime issues |
| **Trigger prompts** | "Add a database to my API", "Write a query for...", "Create an EF migration" |
| **Scope** | DbContext setup, migrations workflow, LINQ query patterns, relationship mapping, connection pooling, avoiding N+1 queries |
| **Why now** | Data access is present in nearly every .NET project; second most common workload after APIs |

### `triaging-ci-failures`

| | |
|---|---|
| **Category** | DevOps (new) |
| **Pain point** | Agents can't read CI logs, miss flaky test patterns, and don't know how to reproduce pipeline failures locally |
| **Trigger prompts** | "My pipeline is red", "This test passes locally but fails in CI", "Help me fix this GitHub Actions workflow" |
| **Scope** | Reading GitHub Actions / Azure DevOps logs, identifying flaky tests, `dotnet test --blame`, environment-specific failures, secret/config mismatches |
| **Why now** | CI failures are the #2 flow interruption after build errors; directly adjacent to `analyzing-build-errors` |

### `securing-dotnet-apps`

| | |
|---|---|
| **Category** | Security (new) |
| **Pain point** | Agents produce code with SQL injection, missing auth, hardcoded secrets, or insecure defaults |
| **Trigger prompts** | "Review this for security", "Add authentication", "Is this vulnerable?" |
| **Scope** | Authentication/authorization setup, input validation, secret management, HTTPS enforcement, CORS configuration, common OWASP patterns in .NET |
| **Why now** | Security is a cross-cutting concern that affects every other skill's output quality |

### `configuring-dependency-injection`

| | |
|---|---|
| **Category** | Development |
| **Pain point** | Agents register services incorrectly (wrong lifetime), miss interface registrations, or create circular dependencies |
| **Trigger prompts** | "Wire up DI for this service", "Why am I getting a missing service error?", "Scoped vs transient vs singleton?" |
| **Scope** | `IServiceCollection` patterns, lifetime selection (scoped/transient/singleton), keyed services, `IOptions<T>` pattern, diagnosing missing registrations |
| **Why now** | DI is foundational to every modern .NET app; misconfigurations cause subtle runtime bugs |

---

## Priority 2 — High Impact, Larger Scope

Valuable but require more careful scoping to stay under 500 lines.

### `building-blazor-components`

| | |
|---|---|
| **Category** | Development |
| **Pain point** | Agents mix Blazor Server and WebAssembly patterns, misuse `@bind`, forget `StateHasChanged`, or create components that don't render correctly |
| **Trigger prompts** | "Create a Blazor component for...", "Add a form with validation", "Why doesn't my UI update?" |
| **Scope** | Component lifecycle, parameter binding, event handling, forms with `EditForm`/`FluentValidation`, JS interop, render mode selection (Server vs WASM vs Auto) |
| **Complexity note** | May need to split into `blazor-components` and `blazor-forms` if scope grows |

### `optimizing-docker-builds`

| | |
|---|---|
| **Category** | Deployment (new) |
| **Pain point** | Agents produce Dockerfiles with large images, slow builds, no layer caching, or missing runtime dependencies |
| **Trigger prompts** | "Dockerize my .NET app", "My Docker image is 2GB", "Speed up my container build" |
| **Scope** | Multi-stage builds, layer caching strategy, `dotnet publish` flags for containers, `PublishAot`, `.dockerignore`, health checks, non-root user |
| **Why now** | Container deployment is the dominant shipping model; bad Dockerfiles waste CI minutes and registry storage |

### `writing-integration-tests`

| | |
|---|---|
| **Category** | Quality & Testing |
| **Pain point** | Agents don't know about `WebApplicationFactory`, create tests that require a real database, or miss test container setup |
| **Trigger prompts** | "Test my API endpoints", "Integration test with a real database", "How do I test with WebApplicationFactory?" |
| **Scope** | `WebApplicationFactory<T>`, `HttpClient` test patterns, Testcontainers for databases, test fixtures for API tests, authentication in tests |
| **Relationship** | Complements `writing-xunit-tests` (unit) with integration-level patterns |

### `logging-and-observability`

| | |
|---|---|
| **Category** | Diagnostics |
| **Pain point** | Agents add `Console.WriteLine` instead of structured logging, miss correlation IDs, or don't set up health checks |
| **Trigger prompts** | "Add logging to my app", "Set up health checks", "Why can't I trace this request?" |
| **Scope** | `ILogger` structured logging, Serilog/OpenTelemetry setup, distributed tracing, health check endpoints, log level configuration |
| **Relationship** | Upstream complement to `profiling-dotnet-apps` — observability prevents the need for reactive profiling |

---

## Priority 3 — Specialized / Narrower Audience

Valuable for specific teams but not universally needed.

### `managing-nuget-packages`

| | |
|---|---|
| **Category** | Build Triage |
| **Pain point** | Agents don't know about central package management, version conflicts, or how to audit for vulnerabilities |
| **Trigger prompts** | "Set up central package management", "Audit for vulnerable packages", "Why do I have version conflicts?" |
| **Scope** | `Directory.Packages.props`, `dotnet list package --vulnerable`, version pinning strategies, private feed configuration |

### `using-source-generators`

| | |
|---|---|
| **Category** | Development |
| **Pain point** | Agents write reflection-heavy code when source generators would be faster and AOT-compatible |
| **Trigger prompts** | "Generate this boilerplate at compile time", "Make this AOT-compatible", "Create a source generator for..." |
| **Scope** | `IIncrementalGenerator` basics, emitting source, debugging generators, common patterns (JSON, logging, mapping) |

### `benchmarking-dotnet-code`

| | |
|---|---|
| **Category** | Diagnostics |
| **Pain point** | Agents use `Stopwatch` for micro-benchmarks instead of BenchmarkDotNet, miss warmup, or compare wrong things |
| **Trigger prompts** | "Benchmark these two approaches", "Is Span<T> faster here?", "Set up BenchmarkDotNet" |
| **Scope** | BenchmarkDotNet setup, `[Benchmark]` attributes, memory diagnoser, comparing allocations, interpreting results |

### `configuring-grpc-services`

| | |
|---|---|
| **Category** | Development |
| **Pain point** | Agents mix REST and gRPC patterns, miss proto file setup, or don't configure channels correctly |
| **Trigger prompts** | "Create a gRPC service", "Convert this REST API to gRPC", "Set up bidirectional streaming" |
| **Scope** | Proto file authoring, service implementation, client generation, streaming patterns, deadline/cancellation propagation |

### `publishing-aot-apps`

| | |
|---|---|
| **Category** | Deployment |
| **Pain point** | Agents don't account for AOT limitations (no reflection, trim warnings), producing apps that fail at runtime |
| **Trigger prompts** | "Publish as native AOT", "Why does my app crash when published?", "Reduce my app startup time" |
| **Scope** | `PublishAot` setup, trim analysis, source-generated JSON/DI, resolving trim warnings, measuring startup time |

---

## Proposed Roadmap

| Quarter | Skills to Ship | Count |
|---|---|---|
| **Current** | (existing 8 skills) | 8 |
| **Q2 2026** | `querying-ef-core`, `triaging-ci-failures`, `securing-dotnet-apps`, `configuring-dependency-injection` | +4 → 12 |
| **Q3 2026** | `building-blazor-components`, `writing-integration-tests`, `optimizing-docker-builds`, `logging-and-observability` | +4 → 16 |
| **Q4 2026** | P3 skills based on community demand and telemetry | +3-5 → 19-21 |

---

## Evaluation Criteria for New Skills

Before building any new skill, validate it against these questions:

1. **Is the agent bad at this without help?** Run the prompt without a skill. If the agent already handles it well, the skill is ineffective.
2. **Is the scope under 500 lines?** If not, split into two skills.
3. **Are there clear trigger keywords?** The description must include terms users actually type.
4. **Can we write good/bad test transcripts?** If good and bad behavior look similar, the skill isn't differentiated enough.
5. **Does it overlap with an existing skill?** If yes, extend the existing one or create a reference file, don't duplicate.
