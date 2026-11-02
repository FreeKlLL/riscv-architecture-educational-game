# Adventure Mode: playable Registers slice

Branch: `workflow`. No branch or history operations are required.

## Running the slice

Open `Assets/Scenes/MainMenu.unity`, enter Play Mode, and click the existing **Adventure Mode** button. Enter a player name, create/select a profile, and choose **Start / Continue**. Profiles can be renamed and deleted (a separate permanent-deletion confirmation is required). The profile screen shows all eight topics, current knowledge, confirmed mastery, and whether a topic is still unassessed.

The interface uses the existing Play button's sprites, TMP font, `UIButtonEffects`, and `UIButtonSFX`. Screens are built at runtime by `AdventureDirector`; they are not duplicate scenes. Existing English and German locale selection controls the new interface and diagnostic text. The portrait is the existing Charlie Cool sprite. Original Registers dialogue graphs and task objectives are reused unchanged.

## Real route

1. The profile starts at Registers diagnosis, including the first chapter.
2. Three configured questions test WE=0, simultaneous register updates, and the number of edges needed to transfer through a three-register chain. Correct answer indices are 0, 1, 2. Score is `100 * correct / questionCount`.
3. Below the configured 80% threshold, the original first Registers Charlie conversation opens in the original level. Processor controls and the dialogue Skip button are unavailable during the lesson. Only reaching the natural end of that dialogue records lesson completion and awards at least 50%; opening it, exiting, or skipping a practice introduction does not.
4. At or above 80%, the lesson is skipped. With three equally weighted questions this requires three correct answers (100%).
5. Both paths assign the actual first Registers task, not a copy:

| Stable Adventure ID | Existing scene | Difficulty | Original target |
|---|---|---|---|
| `registers.transfer` | `Assets/Scenes/Levels/RegisterLevels/Level 0.unity` | Introductory | Write 4 to register 3; initial A=4, B=0 |
| `registers.hold` | `Assets/Scenes/Levels/RegisterLevels/Level 0-1.unity` | Standard | Write 42 to register 3; initial A=256, B=42 |
| `registers.negative` | `Assets/Scenes/Levels/RegisterLevels/Level 0-2.unity` | Advanced | Write -12 to register 3; initial A=-12, B=-4 |

6. The normal clock buttons, WE controls, bus animations and original `CheckWinCondition()` determine the outcome. No values or victory flags are injected by the adapter.
7. A success shows the real reward, failed checks, hint count, updated knowledge, and the automatically assigned next task. Below 80% mastery the lowest remaining difficulty is assigned; at/above 80%, the highest remaining difficulty is assigned. Thus low diagnosis normally produces transfer → hold → negative; high diagnosis produces transfer → negative → hold.
8. Passing all three configured distinct tasks completes Registers. This is an evidence-based completion rule, independent of the diagnostic skip threshold; it does not require repeatedly farming knowledge to reach 100%. A clean low-diagnosis route ends at 75% (50+5+8+12), with Registers permanently completed.
9. Multiplexer displays an explicit not-yet-implemented screen with a working return to the menu. No loading loop or inaccessible assignment is created.

## Configuration and responsibilities

`Assets/Resources/Adventure/RegistersSlice.asset` (`AdventureSliceConfiguration`) contains diagnostic content in EN/DE, correct answers, threshold, reward/penalty settings, the real exercise catalog, the eight chapter definitions and Charlie portrait reference.

- `AdventureRegisseur`: deterministic, testable route and scoring; commits each decision atomically through the existing profile service.
- `AdventureJourney`: persisted current stage/topic, diagnostic answers/score, lesson cursor, assigned exercise/attempt, completed exercises, result receipts, counters and pending result.
- `AdventureMenuEntry`: activates the existing Adventure button and opens profiles.
- `AdventureDirector`: localized runtime UI, profile API calls, existing-scene loading, continuation and result screens.
- `AdventureLevelAdapter` / `IAdventureLevelPort`: connects the original dialogue, hint action and original win check to Adventure. Added only at runtime to assigned Registers levels.

Minimal shared extensions:

- `RISCadventure.asmdef` references `Adventure.Foundation`.
- `DialogueManager` exposes its graph/cursor, a node-shown event, cursor restoration, explicit skip and natural-completion status. Existing Classic dialogue events remain in place.
- `BaseLevelRegisseur` delegates Adventure results before Classic unlock/star/achievement writes and blocks clocks during Adventure teaching. Its register computations and win conditions are unchanged.
- `LevelManager` routes Adventure next/restart/menu actions through the Adventure director. Classic branches are unchanged.
- `MainMenu.unity` adds one component and enables its existing Adventure button; GUIDs and existing references are retained. Incidental layout serialization from Unity's save was removed after verifying that MainMenu was clean at the pre-task SHA-256 baseline.

## Scoring and persistence

Success reward is `max(0, difficultyReward - failedChecks*errorPenalty - openedHints*hintPenalty)`, initially 5/8/12, 1 and 2. A failed solution check lowers current knowledge by 2, capped at 10 per assigned exercise; it does not lower confirmed mastery. Hint usage is counted only when the existing hint dialogue actually opens. Tick count and rollback usage are not treated as invented errors.

On success, current knowledge increases by the reward (clamped 0–100), mastery is `max(oldMastery, newCurrentKnowledge)`, and successful-practice UTC is updated. Each distinct exercise can reward only once. Persisted attempt IDs and submission receipts reject duplicate/stale submissions. Knowledge, practice record (including hint count), next assignment and receipt are written together in one atomic profile mutation.

This extends the existing schema-1 profile JSON additively with `profile.journey`; old saves with no journey load at `NotStarted`. Practice records add `hintCount` (old records default to zero). Storage remains `Application.persistentDataPath/AdventureMode/profiles`; no parallel save store and no Classic `PlayerPrefs` are used. Saving uses the existing temporary-file/replace/back-up mechanism. Every diagnostic answer and lesson node is saved. An unacknowledged result survives a restart.

Leaving an unfinished practical task preserves its assignment, attempt ID, errors and hints. Returning reloads the original level from its original initial state; individual register/tick snapshots are intentionally not serialized. An unfinished lesson resumes at its saved dialogue node. The established UTC knowledge decay policy is unchanged, and completed topics remain completed.

## Tests and verification

EditMode coverage is in `Assets/Tests/Adventure/EditMode/AdventureSliceTests.cs`. The runner `AdventureSliceTestExecution.RunAllEditMode()` also runs the foundation and existing component suites. PlayMode coverage is in `Assets/Tests/Adventure/PlayMode/AdventureSlicePlayModeTests.cs`; run via Test Runner or `AdventureSliceTestExecution.RunPlayMode()` through MCP from Edit Mode.

Both suites use unique temporary roots and remove their own test data. PlayMode tests click actual UI buttons, answer diagnosis, complete the existing branching lesson, run the normal clock animations, check the original solution, reload the profile service from disk, resume the next real scene, and verify profile independence/high-diagnosis skipping and an unchanged Classic unlock value.

Actual verification on 2026-10-23, through Unity MCP:

| Check | Actual result |
|---|---|
| C# compilation (`recompile_status`) | Completed; no compilation failure |
| EditMode Test Runner | **150 passed, 0 failed, 0 skipped**: 44 foundation, 15 slice, 91 existing component tests |
| PlayMode Test Runner | **1 passed, 0 failed, 0 skipped**; the single coroutine covers low/high diagnosis, lesson gating/completion, restored hint button, real first task, disk reload/continuation, A/B isolation and Classic entry plus a normal clock cycle |
| Additional manual MCP UI run | A: diagnosis 0%, original branching lesson → 50%, two normal ticks and original solution check → 55%, next `registers.hold`; B: diagnosis 100%, no lesson, original first task passed, next `registers.negative` |
| Profile UI | Create, rename, select, cancel deletion and confirmed deletion exercised |
| Additional practice evidence | Original hint opened in level 0-1: hints=1; original incorrect check: errors=1, current 55→53, mastery stays 55 |
| Resume after stopping/re-entering Play Mode | Isolated A loaded from disk at Practice, assigned task retained, knowledge 53, hints=1, errors=1 |
| Classic save isolation | `UnlockedLevelIndex` stayed 31 throughout the observed Adventure/Classic smoke runs; no Classic victory was submitted |
| Languages / Game View | EN profiles and DE profiles/diagnosis/result inspected; locale restored to EN |
| Runtime Adventure UI reference audit | Live director and all child objects: 0 Missing Scripts, 0 unresolved serialized object references |
| Scene reference audit | Three original Registers scenes: 0 Missing Scripts, 0 unresolved references |
| Final Unity Console | **0 errors, 0 warnings** after the final clean verification pass |
| Final Editor state | Outside Play Mode, MainMenu active |

Reports: [EditMode JSON](adventure-slice-test-results.json), [EditMode XML](adventure-slice-test-results.xml), [PlayMode XML](adventure-slice-playmode-results.xml), [PlayMode summary](adventure-slice-playmode-results.txt). Full created/modified file manifest, including `.meta`, is [adventure-slice-changes.json](adventure-slice-changes.json).

Game View screenshots are stored in this directory: [EN profiles](adventure-slice-profiles-en.png), [DE profiles](adventure-slice-profiles-de.png), [DE diagnosis](adventure-slice-diagnosis-de.png), [original Charlie lesson](adventure-slice-lesson.png), [DE result](adventure-slice-result-de.png), [Classic entry](adventure-slice-classic.png). The MCP capture command writes under Assets even with a requested absolute path; generated screenshots were moved here and their temporary Unity import metadata removed.

Known pre-existing issue: the MainMenu preview audit finds **40 unresolved AR/RU font/material variant references in `GameObjectLocalizer` components**. Those localization blocks are unchanged from the pre-task scene; the corresponding font GUIDs do not resolve. Active locales are EN and DE. Existing MainMenu localizers were not repaired or globally replaced. New Adventure buttons strip inherited string/font localizers from their runtime clones and use their own EN/DE text, so these legacy references are not copied into the new UI.

Earlier verification found and fixed a duplicate test-assembly reference, unreadable cloned button text bounds, missing checkmark glyph warnings, and the hint button remaining hidden after teaching. The counts above describe the final passing version. Temporary test profiles were removed from their isolated roots; none were created in the normal player store. Test-generated dynamic fallback-font atlas serialization was restored to the verified pre-task bytes. All unrelated pre-existing dirty files match their pre-task hashes.

Not claimed as tested: a standalone player build, actually closing/reopening the Unity process, mobile layouts, all possible dialogue branches, runtime solving of tasks 2/3, or the original Classic victory/achievement flow. Tasks 2/3 and terminal routing are covered by configuration inspection, original scene reference checks and EditMode route tests; the real second scene was loaded during continuation.

## Scope and next chapters

Registers is the only implemented Adventure chapter. The other seven chapter definitions are configured as unavailable. Their original Classic levels remain available through Classic. Adding a chapter requires its diagnostic/lesson configuration, real exercise entries, its topic-specific route and a compatible result adapter; marking a configuration flag alone does not implement it.

This version uses runtime-built desktop UI with scrolling profiles and EN/DE strings. It has no free level selection, cloud accounts, adaptive question generation, full CPU snapshots, or mobile-specific layout validation. Profile names are limited to 40 characters in the UI; existing font glyph availability still governs rendering of arbitrary user-entered names.
