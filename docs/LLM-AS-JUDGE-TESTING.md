# LLM-as-a-Judge Testing Framework for Agent Skills

> A practical guide and tooling for evaluating .NET agent skills using LLM-based automated judges, designed for CI integration and quality gates.

---

## 1. Overview

Traditional unit tests verify *code correctness*. Agent skills need a different approach — they produce *behavioral outcomes* from natural language prompts. **LLM-as-a-Judge** uses a separate LLM instance to evaluate whether an agent's behavior matches expected quality criteria.

In addition to evaluating the agent's *runtime behavior* (did it follow a structured workflow? did it produce actionable output?), the quality of the skill *itself* is assessed against the 8 Golden Rules from the [Best Practices Primer](BEST-PRACTICES-PRIMER.md): conciseness, appropriate degrees of freedom, well-structured frontmatter, progressive disclosure, feedback loops, defaults over menus, evaluations-first development, and multi-model testing. A skill that produces good agent behavior but violates these authoring principles (e.g., exceeds 500 lines, uses vague descriptions, or presents too many options) will still be flagged during review.

```
┌─────────────┐    ┌───────────────┐    ┌──────────────┐    ┌────────────┐
│  Test Prompt │ →  │ Agent + Skill  │ →  │ Agent Output │ →  │ LLM Judge  │
│  (input)     │    │  (system under │    │  (transcript)│    │ (evaluator)│
│              │    │   test)        │    │              │    │            │
└─────────────┘    └───────────────┘    └──────────────┘    └─────┬──────┘
                                                                  │
                                                          ┌──────▼──────┐
                                                          │ Score Card  │
                                                          │ pass/fail   │
                                                          │ per rubric  │
                                                          └─────────────┘
```

### Why LLM-as-a-Judge?

| Approach | Strengths | Weaknesses |
|---|---|---|
| Manual review | High accuracy, nuanced | Slow, doesn't scale |
| Regex/string matching | Fast, deterministic | Brittle, can't assess quality |
| **LLM-as-a-Judge** | **Scales, assesses quality, handles variation** | **Costs tokens, needs calibration** |

---

## 2. Test Structure

Each skill test consists of three files (matching this repo's existing convention):

```
tests/<skill-name>/
├── README.md       # Input prompt + success criteria definition
├── good.md         # Expected behavior WITH the skill loaded
└── bad.md          # Typical behavior WITHOUT the skill loaded
```

### README.md Format

```markdown
# README

This file contains a prompt, a bad output (without skill), and a good output (with skill).

The skill is considered successful if the output looks like the bad output without
the skill, and like the good output with the skill. If the output looks like the
good output without the skill, the skill is considered ineffective. If the output
looks like the bad output with the skill and not like the good output with the skill,
the skill is considered incorrect.

## Input prompt

<The exact prompt sent to the agent>
```

---

## 3. Evaluation Rubric Design

Each skill evaluation uses a **rubric** — a set of weighted criteria that the judge LLM scores.

### 3.1 Rubric Schema

```json
{
  "skill": "debugging-memory-leaks",
  "prompt": "My ASP.NET Core app is using 2GB of memory and growing...",
  "rubric": [
    {
      "criterion": "skill_activation",
      "weight": 1.0,
      "description": "The agent loaded the correct skill immediately without asking clarifying questions"
    },
    {
      "criterion": "tool_usage",
      "weight": 2.0,
      "description": "The agent used diagnostic tools (dotnet-counters, dotnet-dump) systematically rather than giving generic advice"
    },
    {
      "criterion": "structured_workflow",
      "weight": 2.0,
      "description": "The agent followed a clear diagnostic workflow: confirm leak → capture snapshots → analyze heap → trace roots → recommend fix"
    },
    {
      "criterion": "actionable_output",
      "weight": 2.0,
      "description": "The agent produced a specific root cause and concrete fix, not generic suggestions"
    },
    {
      "criterion": "completeness",
      "weight": 1.0,
      "description": "The agent cleaned up artifacts and verified the fix approach"
    },
    {
      "criterion": "no_unnecessary_questions",
      "weight": 1.0,
      "description": "The agent proceeded autonomously without asking the user to choose between tools or approaches"
    }
  ],
  "scoring": {
    "per_criterion": "0 (not met), 0.5 (partially met), 1.0 (fully met)",
    "pass_threshold": 0.75,
    "formula": "weighted_sum / max_possible_weighted_sum"
  }
}
```

### 3.2 Universal Rubric Criteria (apply to all skills)

| Criterion | Weight | What it measures |
|---|---|---|
| **Skill activation** | 1.0 | Did the agent load the correct skill? |
| **No unnecessary questions** | 1.0 | Did the agent avoid asking the user to pick tools/approaches? |
| **Structured workflow** | 2.0 | Did the agent follow a systematic process with clear steps? |
| **Tool usage** | 2.0 | Did the agent use the right tools/commands for the task? |
| **Actionable output** | 2.0 | Is the final output concrete and directly usable? |
| **Completeness** | 1.0 | Did the agent finish the task without leaving loose ends? |

### 3.3 Skill-Specific Criteria Examples

**profiling-dotnet-apps:**
- Used `dotnet-counters` for initial triage before deeper profiling
- Interpreted counter values against known healthy ranges
- Collected and converted trace for visualization

**writing-xunit-tests:**
- Created test project with correct references and packages
- Tests follow Arrange-Act-Assert pattern
- Used `[Theory]` for parameterized cases where appropriate
- Included edge cases (null, empty, boundary)

**refactoring-to-async:**
- Converted bottom-up (I/O layer first, callers next)
- Added `CancellationToken` propagation throughout
- Updated interfaces and all callers
- Verified no `.Result`/`.Wait()` patterns remain

---

## 4. Judge Prompt Template

The judge is a separate LLM call that evaluates the agent transcript against the rubric.

```
You are an expert evaluator of AI coding agent behavior. You will be given:
1. A PROMPT that was sent to an agent
2. A RUBRIC with weighted criteria
3. A TRANSCRIPT of the agent's actual behavior
4. A REFERENCE GOOD transcript showing ideal behavior
5. A REFERENCE BAD transcript showing behavior without the skill

Your task is to score the TRANSCRIPT against each criterion in the RUBRIC.

For each criterion, assign:
- 1.0 if fully met
- 0.5 if partially met
- 0.0 if not met

Provide a brief justification for each score.

Then compute the weighted score: sum(score_i * weight_i) / sum(weight_i)

Respond ONLY in this JSON format:
{
  "scores": [
    {
      "criterion": "<name>",
      "score": <0.0|0.5|1.0>,
      "justification": "<one sentence>"
    }
  ],
  "weighted_score": <float 0.0-1.0>,
  "pass": <true|false>,
  "classification": "<one of: correct, incorrect, ineffective>",
  "summary": "<2-3 sentence overall assessment>"
}

Classification guide:
- "correct": Agent WITH skill behaves like REFERENCE GOOD (score >= threshold)
- "incorrect": Agent WITH skill behaves like REFERENCE BAD (skill loaded but didn't help)
- "ineffective": Agent WITHOUT skill already behaves like REFERENCE GOOD (skill not needed)
```

---

## 5. Running Evaluations

### 5.1 Manual Evaluation (Quick Start)

1. Run the agent **without** the skill on the test prompt → save transcript as `baseline.md`
2. Run the agent **with** the skill on the test prompt → save transcript as `candidate.md`
3. Send both transcripts + the rubric + good/bad references to the judge LLM
4. Review the scorecard

### 5.2 C# Evaluator (Recommended — powered by GitHub Copilot SDK)

The primary evaluator is a .NET console application in `tests/SkillEvaluator/` that uses the [GitHub Copilot SDK](https://github.com/github/copilot-sdk) for LLM interactions. This means no separate API keys are needed — it authenticates through the Copilot CLI using your existing GitHub Copilot subscription.

**Setup:**

```bash
# Ensure Copilot CLI is installed and authenticated
copilot auth login

# Build the evaluator
cd tests/SkillEvaluator
dotnet build
```

**Commands:**

```bash
# List all available skill test cases
dotnet run -- list

# Calibrate a single skill (verifies rubric separates good from bad)
dotnet run -- calibrate --skill debugging-memory-leaks

# Calibrate all skills at once
dotnet run -- calibrate-all

# Evaluate a transcript against references
dotnet run -- evaluate --skill debugging-memory-leaks --transcript ./candidate.md

# Evaluate all transcripts in a directory
dotnet run -- evaluate-all --transcripts-dir ./transcripts --output results.json

# Use a different judge model
dotnet run -- calibrate --skill debugging-memory-leaks --model claude-sonnet-4.5
```

**Architecture:**

```
tests/SkillEvaluator/
├── Program.cs            # CLI entry point with commands
├── JudgeEvaluator.cs     # Core: Copilot SDK session → LLM judge → parsed scores
├── TestCaseLoader.cs     # Loads README/good/bad files and rubrics
├── Models.cs             # Rubric, EvaluationResult, CriterionScore types
└── SkillEvaluator.csproj # .NET 8, GitHub.Copilot.SDK dependency
```

The `JudgeEvaluator` class:
1. Creates a `CopilotClient` → starts the CLI server
2. Opens a session with `SystemMessageMode.Replace` (pure JSON evaluator, no agent tools)
3. Sends the judge prompt with rubric + references + candidate transcript
4. Parses the JSON scorecard from the response
5. Returns a typed `EvaluationResult` with per-criterion scores and pass/fail

---

## 6. CI Integration

### 6.1 GitHub Actions Workflow

```yaml
name: Skill Evaluation
on:
  pull_request:
    paths: ['skills/**', 'tests/**']

jobs:
  evaluate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Install Copilot CLI
        run: |
          # Install the Copilot CLI (see https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli)
          npm install -g @anthropic-ai/copilot-cli || true

      - name: Build evaluator
        run: dotnet build tests/SkillEvaluator

      - name: Calibrate all skills
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          dotnet run --project tests/SkillEvaluator -- calibrate-all

      - name: Evaluate transcripts (if present)
        if: hashFiles('transcripts/') != ''
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          dotnet run --project tests/SkillEvaluator -- evaluate-all --transcripts-dir transcripts/ --output evaluation-results.json

      - name: Upload results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: skill-evaluation-results
          path: evaluation-results.json
```

### 6.2 Quality Gates

| Gate | Threshold | Action |
|---|---|---|
| Individual skill pass | ≥ 0.75 weighted score | Block PR if any skill drops below |
| Overall suite pass | ≥ 85% of skills pass | Block PR if overall drops |
| Regression detection | Score delta ≤ -0.1 vs. main | Warn on review, block if > -0.2 |

---

## 7. Multi-Model Testing Matrix

Skills should work across models. Test with multiple:

| Model | Role | Expected Behavior |
|---|---|---|
| **Haiku/fast** | Speed-optimized | May need more guidance; score ≥ 0.60 |
| **Sonnet/balanced** | Primary target | Should follow skill well; score ≥ 0.75 |
| **Opus 4.6/powerful** | High-end (default judge) | Should excel; score ≥ 0.85 |

```bash
# C# evaluator — run calibration with different models
dotnet run --project tests/SkillEvaluator -- calibrate-all                            # Default: claude-opus-4.6
dotnet run --project tests/SkillEvaluator -- calibrate-all --model gpt-4.1            # Fast/cheap
dotnet run --project tests/SkillEvaluator -- calibrate-all --model claude-sonnet-4.5  # Balanced
```

---

## 8. Interpreting Results

### Classification Matrix

|  | Agent + Skill → Good | Agent + Skill → Bad |
|---|---|---|
| **Agent alone → Bad** | **CORRECT** (skill helps) | **INCORRECT** (skill broken) |
| **Agent alone → Good** | **INEFFECTIVE** (skill unnecessary) | **REGRESSION** (skill harmful) |

### Common Failure Patterns and Fixes

| Failure Pattern | Rubric Signal | Likely Cause | Fix |
|---|---|---|---|
| Agent asks "which tool?" | Low "no_unnecessary_questions" | Skill offers too many options | Provide a default tool/approach |
| Agent gives generic advice | Low "actionable_output" | Skill lacks concrete commands | Add exact commands and examples |
| Agent skips validation | Low "completeness" | Workflow missing verification step | Add explicit validation step |
| Agent loads wrong skill | Low "skill_activation" | Description too vague | Make description more specific with trigger keywords |
| Agent ignores cleanup | Low "completeness" | No cleanup step in workflow | Add explicit cleanup step |

---

## 9. Best Practices for LLM-as-a-Judge

1. **Use temperature=0** for deterministic judge scores
2. **Use a stronger model as judge** than the model being tested
3. **Calibrate rubrics** — run the judge on `good.md` and `bad.md` first; good should score ≥ 0.85, bad should score ≤ 0.40
4. **Include justifications** — always require the judge to explain each score
5. **Run multiple judge passes** — average 3 runs to reduce variance
6. **Version your rubrics** — track rubric changes alongside skill changes
7. **Test the judge itself** — verify it correctly distinguishes good.md from bad.md before trusting it on new transcripts

---

## 10. Cost Estimation

When using the default Claude Opus 4.6 judge via the Copilot SDK, evaluations count against your Copilot premium request quota (no direct API cost). With BYOK or direct API usage:

| Component | Tokens (approx) | Cost per eval (est.) |
|---|---|---|
| Judge prompt (system + rubric) | ~2,000 | |
| Good reference | ~2,000 | |
| Bad reference | ~1,500 | |
| Candidate transcript | ~2,000 | |
| Judge response | ~500 | |
| **Total per skill** | **~8,000** | **~$0.02** |
| **Full suite (8 skills × 3 models)** | **~192,000** | **~$0.48** |

---

---

## 11. C# Evaluator Quick Reference

| Command | What it does |
|---|---|
| `dotnet run -- list` | Show all discovered skill test cases |
| `dotnet run -- calibrate --skill <name>` | Verify rubric separates good from bad |
| `dotnet run -- calibrate-all` | Calibrate all skills in one pass |
| `dotnet run -- evaluate --skill <name> --transcript <path>` | Score a single transcript |
| `dotnet run -- evaluate-all --transcripts-dir <dir>` | Score all transcripts in a directory |
| `dotnet run -- help` | Show full help with all options |

**Key flags:** `--model <model>` (default: claude-opus-4.6), `--output <path>` (write JSON results)

**Prerequisites:** .NET 8+, Copilot CLI installed and authenticated (`copilot auth login`)

---

*This framework is designed to be iterative. Start with manual evaluation, then automate as your skill library grows.*
