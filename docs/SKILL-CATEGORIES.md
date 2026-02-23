# .NET Skill Categories & Rationale

> Explains how the 7 new .NET skills (plus the existing `csharp-scripts`) are organized into categories, why each category exists, and which developer workflow stage it serves.

---

## Category Map

```
                        The .NET Developer Workflow
  ┌──────────────────────────────────────────────────────────────┐
  │                                                              │
  │   Write ──► Build ──► Test ──► Run ──► Profile ──► Ship     │
  │     │         │         │       │         │          │       │
  │     ▼         ▼         ▼       ▼         ▼          ▼       │
  │  Prototyping  Triage   Quality  Dev    Diagnostics  Modern.  │
  │                                                              │
  └──────────────────────────────────────────────────────────────┘
```

| Category | Skills | Workflow Stage | Developer Scenario |
|---|---|---|---|
| **Prototyping** | `csharp-scripts` | Write | "Test this C# concept quickly without creating a project" |
| **Build Triage** | `analyzing-build-errors` | Build | "Build fails after merging a branch" |
| **Quality & Testing** | `writing-xunit-tests` | Test | "Add unit tests for my OrderService" |
| **Development** | `creating-minimal-apis` | Run | "Build a REST API for products" |
| **Diagnostics** | `profiling-dotnet-apps`, `debugging-memory-leaks` | Profile | "My API is slow, help me profile it" / "App keeps getting OOM-killed" |
| **Modernization** | `migrating-dotnet-versions`, `refactoring-to-async` | Ship / Evolve | "Upgrade my .NET 6 project to .NET 8" / "Convert blocking calls to async/await" |

---

## 1. Prototyping

**Skill:** `csharp-scripts`

**What it covers:** Running single-file C# programs without creating a full project — quick experiments, API exploration, one-off scripts.

**Why it's a category:** Every developer workflow starts with "can I quickly try this?" Prototyping is the entry point before any real project exists. This skill eliminates the friction of `dotnet new console` → edit → `dotnet run` for throwaway experiments by using .NET 10's file-based apps (`dotnet hello.cs`).

**Typical triggers:** "Test this concept", "Try this API", "Run a quick script"

---

## 2. Build Triage

**Skill:** `analyzing-build-errors`

**What it covers:** Categorizing and resolving CS (compiler), NU (NuGet), MSB (MSBuild), and NETSDK errors systematically rather than guessing.

**Why it's a category:** Build failures are the #1 interruption in a developer's flow. Without guidance, agents produce generic advice ("check your using statements") instead of systematically categorizing the error code, consulting the right source, and applying a targeted fix. This skill turns the agent from a suggestion engine into a build debugger.

**Typical triggers:** "`dotnet build` fails", "CS0246", "NU1605", "merge broke the build"

---

## 3. Quality & Testing

**Skill:** `writing-xunit-tests`

**What it covers:** Creating xUnit test projects, writing Arrange-Act-Assert tests, adding `[Theory]`/`[InlineData]` parameterization, mocking with Moq, and using class/collection fixtures.

**Why it's a category:** Test coverage is a universal quality gate. Without this skill, agents create placeholder tests (`Assert.True(true)`), ask the user which framework to use, or forget to add project references. This skill ensures the agent produces real, passing tests in one shot.

**Typical triggers:** "Add unit tests", "Cover this class", "Set up a test project"

---

## 4. Development

**Skill:** `creating-minimal-apis`

**What it covers:** Building ASP.NET Core Minimal API endpoints with proper routing, DI, validation, `MapGroup` organization, and OpenAPI documentation.

**Why it's a category:** REST API development is the most common .NET workload. Minimal APIs are the modern default for new services, but agents without guidance often pick the wrong template (`webapi` with controllers), put everything in `Program.cs` without organization, or skip OpenAPI setup. This skill ensures production-ready API scaffolding.

**Typical triggers:** "Create a REST API", "Build an endpoint", "New microservice"

---

## 5. Diagnostics

**Skills:** `profiling-dotnet-apps`, `debugging-memory-leaks`

**What they cover:**
- **Profiling:** Live counter monitoring (`dotnet-counters`), CPU trace collection (`dotnet-trace`), SpeedScope visualization, and interpreting metrics against healthy baselines.
- **Memory leaks:** Heap snapshot comparison, `dumpheap -stat` analysis, `gcroot` tracing, and a catalog of common .NET leak patterns with fixes.

**Why it's a category (and why two skills):**

Performance diagnostics is a distinct discipline from writing code — it requires different tools, a different mental model (observe → hypothesize → measure → verify), and domain-specific knowledge (what's a "healthy" GC heap size? what does thread pool queue length mean?).

Two skills exist because the workflows are meaningfully different:

| | Profiling | Memory Leaks |
|---|---|---|
| Primary symptom | High CPU, slow responses | Growing memory, OOM kills |
| Key tool | `dotnet-trace` (CPU sampling) | `dotnet-dump` (heap analysis) |
| Analysis focus | Hot code paths, thread contention | Object retention chains, GC roots |
| Fix category | Async conversion, caching, algorithm | Dispose patterns, cache eviction, weak refs |

Merging them would exceed the 500-line guideline and force the agent to load irrelevant context (memory leak patterns when the user has a CPU problem, and vice versa).

---

## 6. Modernization

**Skills:** `migrating-dotnet-versions`, `refactoring-to-async`

**What they cover:**
- **Migration:** Upgrading target frameworks, aligning NuGet packages, resolving breaking changes, and handling the .NET Framework → modern .NET path.
- **Async refactoring:** Converting synchronous blocking I/O to `async`/`await`, propagating `CancellationToken`, eliminating `.Result`/`.Wait()` anti-patterns.

**Why it's a category (and why two skills):**

Modernization represents the "evolve" phase — taking existing, working code and bringing it forward. These are high-value, high-risk operations where agents without guidance often:

- Migrate only one project in a solution (missing the test project)
- Forget to update NuGet packages after changing the TFM
- Convert to async at the top of the call chain but leave blocking calls at the bottom (sync-over-async)
- Skip `CancellationToken` propagation

Two skills exist because they address orthogonal problems with different decision trees:

| | Migration | Async Refactoring |
|---|---|---|
| Primary change | Project files, packages, config | Method signatures, call sites |
| Risk | Package incompatibility, API removal | Deadlocks, behavioral changes |
| Validation | `dotnet build` + `dotnet test` | `grep` for `.Result`/`.Wait()` |
| Direction | Top-down (TFM → packages → code) | Bottom-up (I/O → service → controller) |

---

## Why These Categories?

### Alignment with the developer workflow

The categories follow the natural sequence developers go through daily: write → build → test → run → profile → ship. Each skill activates at a specific pain point in this cycle, ensuring coverage without overlap.

### Designed for skill discovery

Each category maps to distinct **trigger keywords** in user prompts. When a developer says "my build is failing", the agent needs `analyzing-build-errors`, not `profiling-dotnet-apps`. Clear category boundaries mean the agent loads the right skill on the first try.

### Follows the conciseness principle

Rather than one monolithic "dotnet-development" skill (which would be thousands of lines), splitting by category keeps each SKILL.md under the 500-line limit and ensures progressive disclosure works — the agent only loads the context relevant to the current task.

### No overlap, no gaps

| Developer pain point | Covered by |
|---|---|
| "How do I try this quickly?" | `csharp-scripts` |
| "My build is broken" | `analyzing-build-errors` |
| "I need tests for this" | `writing-xunit-tests` |
| "Build me an API" | `creating-minimal-apis` |
| "Why is it so slow?" | `profiling-dotnet-apps` |
| "Memory keeps growing" | `debugging-memory-leaks` |
| "Upgrade to .NET 8/10" | `migrating-dotnet-versions` |
| "Fix my blocking calls" | `refactoring-to-async` |

### Future expansion path

The category framework scales naturally. Planned additions would fit cleanly:

| Future Skill | Category | Rationale |
|---|---|---|
| `querying-ef-core` | Development | Data access is as common as APIs |
| `building-blazor-components` | Development | UI counterpart to API development |
| `configuring-ci-pipelines` | DevOps (new) | Post-ship workflow stage |
| `securing-dotnet-apps` | Security (new) | Cross-cutting concern |
| `optimizing-docker-builds` | Deployment (new) | Container-era shipping |
