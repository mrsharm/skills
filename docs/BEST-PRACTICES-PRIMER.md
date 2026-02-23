# Agent Skill Authoring Best Practices Primer

> Distilled from the [official Claude Skill authoring best practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices), adapted for .NET skill development in this repository.

## At a Glance

- **Be concise** — only add context the model doesn't already have; every token competes with the user's request.
- **Match specificity to risk** — use exact scripts for fragile operations (migrations, deployments) and loose guidance for open-ended tasks (code review, refactoring).
- **Structure your `SKILL.md` well** — include clear YAML frontmatter with a specific description that states *what* the skill does AND *when* to use it.
- **Use progressive disclosure** — keep `SKILL.md` under 500 lines; split detailed references and examples into sibling files.
- **Build feedback loops** — always validate changes with `dotnet build` / `dotnet test` before moving on.
- **Provide defaults, not menus** — recommend one tool (e.g., xUnit) rather than listing every option.
- **Write evaluations first** — define 3+ test scenarios before writing the skill, measure a baseline, then iterate.
- **Test across models** — verify the skill works with Haiku, Sonnet, and Opus since each has different guidance needs.

---

## 1. Core Principles

### 1.1 Concise Is Key

The context window is a shared resource. Your skill competes with the system prompt, conversation history, other skills' metadata, and the user's actual request.

**Rule of thumb:** Only add context the model doesn't already have.

| Ask yourself | If "yes", keep it | If "no", cut it |
|---|---|---|
| Does the model really need this explanation? | ✅ | ❌ |
| Can I assume the model knows this? | ❌ | ✅ |
| Does this paragraph justify its token cost? | ✅ | ❌ |

**Good** (~50 tokens):
```
## Read JSON configuration
Use System.Text.Json for deserialization:
```csharp
using var stream = File.OpenRead("appsettings.json");
var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream);
```

**Bad** (~150 tokens): Explaining what JSON is, how serialization works, how to install NuGet packages, etc.

### 1.2 Set Appropriate Degrees of Freedom

Match specificity to the task's fragility:

| Freedom Level | When to Use | Example |
|---|---|---|
| **High** (text instructions) | Multiple valid approaches, context-dependent | Code review, refactoring suggestions |
| **Medium** (pseudocode/templates) | Preferred pattern exists, some variation OK | Report generation, test scaffolding |
| **Low** (exact scripts) | Fragile operations, consistency critical | Database migrations, deployment scripts |

**Analogy:** Think of the agent as a robot on a path:
- **Narrow bridge with cliffs** → Provide exact instructions (low freedom)
- **Open field** → Give general direction (high freedom)

### 1.3 Test with All Target Models

Skills behave differently across models:

| Model | Consideration |
|---|---|
| **Haiku** (fast, economical) | Does the skill provide enough guidance? |
| **Sonnet** (balanced) | Is the skill clear and efficient? |
| **Opus** (powerful reasoning) | Does the skill avoid over-explaining? |

---

## 2. Skill Structure

### 2.1 YAML Frontmatter

Every `SKILL.md` must include:

```yaml
---
name: my-skill-name
description: What the skill does and when to use it. Third person. Specific.
---
```

**`name` rules:**
- Max 64 characters
- Lowercase letters, numbers, hyphens only
- No XML tags, no reserved words (`anthropic`, `claude`)

**`description` rules:**
- Non-empty, max 1024 characters
- No XML tags
- Must include *what* it does AND *when* to use it
- Always third person ("Processes files..." not "I help you..." or "You can use...")

### 2.2 Naming Conventions

Prefer gerund form (verb + -ing):

| ✅ Good | ❌ Avoid |
|---|---|
| `profiling-dotnet-apps` | `helper` |
| `writing-xunit-tests` | `utils` |
| `migrating-dotnet-versions` | `tools` |
| `debugging-memory-leaks` | `documents` |

### 2.3 Writing Effective Descriptions

The description is the **discovery mechanism** — the model uses it to choose from 100+ available skills.

**Effective:**
```yaml
description: Profile .NET application performance using dotnet-counters, dotnet-trace, and dotnet-dump. Use when diagnosing CPU spikes, memory issues, or slow request handling in .NET applications.
```

**Ineffective:**
```yaml
description: Helps with .NET performance
```

### 2.4 Progressive Disclosure

Keep `SKILL.md` body under **500 lines**. Split into separate files:

```
my-skill/
├── SKILL.md              # Main instructions (loaded when triggered)
├── REFERENCE.md          # Detailed reference (loaded as needed)
├── EXAMPLES.md           # Usage examples (loaded as needed)
└── scripts/
    └── validate.csx      # Utility script (executed, not loaded)
```

**Critical:** Keep references **one level deep** from SKILL.md. No deeply nested chains.

---

## 3. Workflows and Feedback Loops

### 3.1 Use Workflows for Complex Tasks

Break operations into clear, sequential steps with checklists:

```markdown
Copy this checklist and track your progress:

- [ ] Step 1: Analyze the problem
- [ ] Step 2: Apply the fix
- [ ] Step 3: Validate the result
- [ ] Step 4: Clean up
```

### 3.2 Implement Feedback Loops

The pattern: **Run validator → fix errors → repeat**

```markdown
1. Make changes
2. Validate immediately: `dotnet build --no-restore`
3. If build fails: fix compiler errors, then rebuild
4. Run tests: `dotnet test --no-build`
5. If tests fail: fix, rebuild, retest
6. Only proceed when both build and tests pass
```

---

## 4. Content Guidelines

### 4.1 No Time-Sensitive Information

**Bad:** "If you're doing this before August 2025, use the old API."

**Good:** Document the current method, put deprecated approaches in an "Old patterns" collapsible section.

### 4.2 Consistent Terminology

Pick one term and stick with it:
- Always "assembly" (not "DLL", "binary", "module" interchangeably)
- Always "project file" (not "csproj", ".csproj file", "project" interchangeably)

### 4.3 Always Use Forward Slashes

Even on Windows: `scripts/helper.py`, not `scripts\helper.py`

### 4.4 Provide Defaults, Not Menus

**Bad:** "You can use xUnit, or NUnit, or MSTest, or..."

**Good:** "Use xUnit for new test projects. For existing NUnit projects, follow the NUnit-specific section."

---

## 5. Common Patterns

### 5.1 Template Pattern

Provide output format templates with appropriate strictness level.

### 5.2 Examples Pattern

Input/output pairs teach style better than descriptions:

```
Input: Added null check to user service
Output: fix(UserService): add null-check guard for UserDto parameter

Input: Upgraded target framework
Output: chore(MyApp.csproj): migrate TargetFramework from net8.0 to net9.0
```

### 5.3 Conditional Workflow Pattern

Guide through decision points:

```
1. Determine the scenario:
   - Creating a new .NET project? → Follow "Creation workflow" (dotnet new, add packages, scaffold)
   - Migrating an existing .NET project? → Follow "Migration workflow" (update TFM, resolve breaking changes)
   - Adding a new skill to the repo? → Follow "Skill scaffolding workflow"
```

---

## 6. Evaluation and Iteration

### 6.1 Build Evaluations First

**Evaluation-driven development:**

1. **Identify gaps:** Run the model on representative tasks *without* a skill. Document failures.
2. **Create evaluations:** Build 3+ scenarios that test these gaps.
3. **Establish baseline:** Measure performance without the skill.
4. **Write minimal instructions:** Just enough to address gaps and pass evaluations.
5. **Iterate:** Execute evaluations, compare against baseline, refine.

### 6.2 Evaluation Structure

```json
{
  "skills": ["my-skill"],
  "query": "The user prompt that triggers the skill",
  "files": ["test-files/input.cs"],
  "expected_behavior": [
    "Behavior assertion 1",
    "Behavior assertion 2",
    "Behavior assertion 3"
  ]
}
```

### 6.3 Iterative Development with Two Instances

- **Claude A** (the author): Helps design and refine the skill
- **Claude B** (the tester): Uses the skill on real tasks
- Observe Claude B's behavior, bring insights back to Claude A, repeat

### 6.4 Observe Navigation Patterns

Watch for:
- Unexpected file reading order
- Missed references to important files
- Overreliance on certain sections
- Ignored bundled content

---

## 7. Checklist for Effective Skills

### Core Quality
- [ ] Description is specific and includes key terms
- [ ] Description includes what AND when
- [ ] SKILL.md body under 500 lines
- [ ] Additional details in separate files (if needed)
- [ ] No time-sensitive information
- [ ] Consistent terminology throughout
- [ ] Concrete examples, not abstract
- [ ] File references one level deep
- [ ] Progressive disclosure used appropriately
- [ ] Workflows have clear steps

### Code and Scripts
- [ ] Scripts solve problems rather than punt to the model
- [ ] Error handling is explicit and helpful
- [ ] No "voodoo constants"
- [ ] Required packages listed and verified
- [ ] No Windows-style paths
- [ ] Validation/verification steps for critical operations
- [ ] Feedback loops for quality-critical tasks

### Testing
- [ ] At least three evaluations created
- [ ] Tested with multiple models (Haiku, Sonnet, Opus)
- [ ] Tested with real usage scenarios
- [ ] Team feedback incorporated

---

*This primer is a living document. Update it as the best practices evolve.*
