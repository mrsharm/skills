# How LLM-as-a-Judge Calibration Works

There are 3 test files per skill and a rubric. The LLM judge scores each transcript against the rubric, and calibration verifies the rubric can tell good from bad.

## The 3 Files

**README.md** -- Contains the user prompt (the question asked to the agent):

> "My dotnet build is failing with a bunch of CS and NU errors after I merged a branch. Can you figure out what's wrong?"

**good.md** -- A captured transcript of an agent with the skill loaded. Key characteristics:

- Immediately activates the skill (e.g. `skill -> analyzing-build-errors`)
- Follows a structured workflow: reproduce -> triage by category -> fix NuGet first -> fix compiler errors -> rebuild -> test
- Uses tools autonomously (dotnet build, grep, edit, dotnet test)
- Produces concrete fixes (exact version numbers, specific using directives, SDK change)
- Verifies: build succeeds, 18 tests pass
- Never asks the user a question

**bad.md** -- A captured transcript of an agent without the skill. Key characteristics:

- No skill loaded -- jumps straight to running commands
- Ad-hoc: runs build, greps, then gives generic advice instead of fixing
- Suggests commands for the user to run instead of executing them
- Ends with "Would you like me to apply these fixes?"
- No verification, no tests, task left incomplete

## The Rubric (universal-rubric.json)

6 weighted criteria the judge scores 0.0 / 0.5 / 1.0:

| Criterion | Weight | What it measures |
|---|---|---|
| skill_activation | 1.0 | Did it load the correct skill? |
| tool_usage | 2.0 | Used tools systematically vs. generic advice? |
| structured_workflow | 2.0 | Clear sequential steps vs. ad-hoc? |
| actionable_output | 2.0 | Concrete results vs. suggestions/TODOs? |
| completeness | 1.0 | Finished + verified vs. left incomplete? |
| no_unnecessary_questions | 1.0 | Autonomous vs. asked user to decide? |

## The Judge Flow

Calibration sends two LLM calls per skill:

- Call 1: Judge, score the GOOD transcript -> expect >= 0.85
- Call 2: Judge, score the BAD transcript -> expect <= 0.40

Each call sends the judge this context:

1. The input prompt (from README.md)
2. The rubric (the 6 criteria above)
3. The reference good transcript (good.md)
4. The reference bad transcript (bad.md)
5. The candidate to score (good.md or bad.md during calibration; a real transcript during evaluation)

The judge returns a JSON scorecard with per-criterion scores, a weighted total, and a classification (correct / incorrect / ineffective).

## What "Calibration Passes" Means

If good.md scores >= 0.85 and bad.md scores <= 0.40, the rubric discriminates -- it can reliably tell skilled behavior from unskilled behavior. That is a passing calibration. The gap between the two scores (>= 0.45 target) is the margin of safety.

When csharp-scripts failed calibration (bad scored 0.67), it meant the "bad" transcript was actually pretty decent -- the agent still completed the task without the skill, just less efficiently. The rubric could not tell them apart well enough.
