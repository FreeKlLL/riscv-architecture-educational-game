# Eintaktprozessor: Charlie dialogue content

The chapter now has six independent tutorials, an instruction-specific hint menu, and English (`en`) / German (`de`) task descriptions. The existing DialogueManager, DialogueUI, Charlie portraits, text animation, colour palette, answer buttons, Back / Next / Skip controls and level-start lifecycle are reused. No processor logic, starting values, victory predicates or Mehrtakt resources were changed.

## Content and integration

- Authoring copy: [eintakt-dialogue-content.json](eintakt-dialogue-content.json). Paired strings are ordered **English, German**. Runtime uses Unity StringTables and ScriptableObjects, not this JSON.
- Localization: `Assets/Locales/ChaptersLocalization/Chapter 8/Chapter 8 text_{en,de}.asset` and the existing Shared Data asset. Existing quest keys / IDs are retained; new keys use `eintakt.*`.
- Tutorials: `Assets/Scriptable objects/Dialogues/Level 8 (Eintakt)/`. The existing `Graph 8 placeholder.asset` and four existing nodes are reused for 11-0 to preserve GUIDs. Despite its legacy filename, that graph no longer contains placeholder dialogue. New task directories hold the other nodes / graphs.
- Initial configurations: `Assets/Scriptable objects/One Tick Processor level entry information/Level 11-0.asset` through `Level 11-5.asset`. Only dialogue graph references changed where necessary; all numerical fields and next-task links are retained.
- Hints: `Assets/Scriptable objects/Dialogues/HintDialogueSample 1/Level 8/HintGraph 8 placeholder.asset`, reusing its existing GUID. Seventeen nodes offer arithmetic, store, and branch/jump menus; each instruction has control settings and an extra explanation.
- Scene: `Assets/Scenes/Levels/Eintaktprozessor/Level 10.unity` (build index 21). Its DialogueManager now references the Eintakt graphs instead of Chapter 7; its previously disabled Hint button is enabled. Scene serialization also normalizes the empty localized target field / managed-reference metadata.

Each tutorial has eight reachable nodes:

| Index | Purpose | Transition |
| --- | --- | --- |
| 0 | Charlie greeting and actual instruction | Next → 1 |
| 1 | Single-cycle theory and data path | Next → 2 |
| 2 | Starting values and concrete objective | Next → 3 |
| 3 | Interactive choice | Ready → 7; theory → 4; controls → 5 |
| 4 | Additional theory | Back to choices → 3 |
| 5 | Exact control settings | Ready → 7; extra hint → 6; choices → 3 |
| 6 | Extra hint / configuration caveat | Choices → 3 |
| 7 | Encouragement and independent work | Next ends dialogue |

There is no automatic clock action in a dialogue or hint. The tutorial distinguishes the parts of the signal animation from separate processor cycles. No new character artwork, dependencies, runtime scripts or changes to the shared dialogue system are required.

## Actual exercises and controls

Source of truth: `OneTickRegisseur.OnLevelStart`, `ComputeSignals`, `CheckWinCondition`, and the six OneTickProcessorInitialState assets. MUX numbers below are zero-based, as in the code and scene labels. ADD/SUB/AND/OR operation values are 0/1/2/3; Extender I/S/B/J values are 0/1/2/3.

The common initial Register File is:

```text
x0..x31 = 0,1,39,43,5,6,8,40,3,39,13,56,63,20,50,51,
          0,12,53,65,29,60,61,0,25,54,0,28,70,30,31,0
```

PC write enable starts on; Register File / Data Memory write enables start off. All three MUXes initially have no selection. Instruction Memory writing is not used by the controller. PC and BTA ALUs are separate from the main ALU.

| Task | Instruction memory [0,4,8,12]; initial PC | Data memory [0,4,8,12] | Existing predicate | Intended setup |
| --- | --- | --- | --- | --- |
| 11-0 | [4391699,0,0,0]; PC=0; `addi x6,x6,4` | [1,2,3,4] | x6=12 | Ext I, SrcB=1, ALU ADD, Result=0, RF WE on, memory WE off |
| 11-1 | [3179059,0,0,0]; PC=0; `add x4,x1,x3` | [12,16,-16,-12] | x4=44 | SrcB=0, ALU ADD, Result=0, RF WE on, memory WE off; Ext irrelevant |
| 11-2 | [0,0,26633747,0]; PC=0; `ori x12,x12,25` at 8 | [0,0,0,0] | x12=63 | Two PC+4 preparation steps with RF / memory WE off; at PC=8: Ext I, SrcB=1, ALU OR, Result=0, RF WE on |
| 11-3 | [4464803,0,0,0]; PC=0; `sw x4,1(x8)` | [0,0,0,0] | memory[4]=5 | Ext S, SrcB=1, ALU ADD, memory WE on, RF WE off; Result irrelevant |
| 11-4 | [0,0,0,1076920931]; PC=12; `beq x1,x3,1028` | [0,0,0,0] | PC=16 | Ext B, SrcB=0, ALU SUB; x1!=x3, choose PC MUX=0; RF / memory WE off |
| 11-5 | [0,0,0,268435695]; PC=12; `jal x1,256` | [0,0,0,0] | x1=16 AND PC=256 | Correct JAL: Ext J, Result=2 (PC+4), PC MUX=1 (BTA), PC / BTA ALUs ADD, RF / PC WE on, memory WE off; computes PC=268 |

For normal sequential flow: PC ALU ADD, PC MUX input 0 (PC+4), PC WE on. BTA uses PC plus extended immediate when its ALU is ADD. The main ALU input A is RD1; SrcB selects RD2 (0) or immediate (1). Result selects ALU (0), memory (1), or PC+4 (2). The next PC selects PC+4 (0) or BTA (1). BEQ's zero flag does **not** drive PC selection automatically in this implementation.

## Existing limitations, deliberately not corrected

- **11-2:** x12 starts at the target value 63; `63 OR 25` also equals 63. The victory predicate alone cannot prove execution. The dialogue explains this and the two preparation steps needed to reach address 8. Zero words at 0 and 4 are not presented as real ORI phases or valid NOP instructions.
- **11-5:** a J-immediate is relative. PC=12 plus 256 gives 268, while the stored goal requires 256. A correct single-cycle JAL therefore cannot satisfy that goal. The tutorial, objective and hints explicitly explain the discrepancy instead of teaching incorrect arithmetic. No goal or instruction was changed.
- The underlying objective label is assigned once at level start. Localization checks reload each task in its test locale; live locale-switch refresh of that shared UI is outside this content change.

## Validation

Static MCP validation confirmed all 65 nodes (48 tutorial + 17 hint) are reachable, all answer targets are in range, and all 310 nonempty localized node/answer references resolve in EN and DE. All six objective entries resolve in both tables. Unity reported no project compilation errors.

The repeatable runtime audit is [verify-eintakt-dialogues.csx](verify-eintakt-dialogues.csx). Run it through Unity MCP `eval_file` while MainMenu is playing. It uses existing menu buttons to load the tasks and reloads each task for German. It traverses tutorial branches / Back / exit and all hint topics through UI events, validates localized text and Charlie's emotion sprite, and checks scene references. Actions are separated by at least 0.6 seconds. Status is available from `AppDomain.CurrentDomain.GetData("EintaktDialogueAudit")`.

The audit never clocks the processor or checks a victory condition. It intentionally leaves Play Mode running so the caller can collect Console output and stop Play Mode explicitly. Temporary MCP helper compilation diagnostics are distinct from Unity project compilation / runtime Console errors.

Preservation check: SHA-256 comparison of 322 files under the processor C# scripts, Mehrtakt scene, initial data, Chapter 7 dialogues / hints / localization found no changes. Existing `.meta` files are unchanged; new resources have Unity-generated metadata. Pre-existing working-tree changes in packages / auditor configuration were left untouched.

Final runtime results: **all six tutorials passed in both EN and DE**. Every tutorial node and every hint-menu node was displayed; answer text, branch destinations, Back, exit and Charlie's emotion sprites were checked. Scene scans found no missing scripts or broken non-null serialized references. No dialogue text overflow remained after shortening the German ORI hint.

After shortening the HUD objectives, a separate [goal audit](verify-eintakt-goals.csx) loaded all six tasks in both languages again. Each final objective matched its localized entry, fitted without overflow, and had processor tick counter 0. The 116 active common / task / quest localized values were also compared exactly against the UTF-8 authoring JSON.

Final Unity checks: `recompile_status` reported no compilation failure; Console had **0 errors and 0 warnings**. Play Mode was stopped; MainMenu remained active and unmodified. No processor execution or victory checks were run for this content verification.

Machine-readable results: [eintakt-validation-results.json](eintakt-validation-results.json). Representative Game View captures: [German Charlie hint menu](screenshots/eintakt-runtime-01.png), [German JAL objective](screenshots/eintakt-runtime-02.png). Screenshots were moved out of Assets into documentation so they do not become game resources.

The complete task-scoped file inventory, including new `.meta` files, is [eintakt-changed-files.txt](eintakt-changed-files.txt). Unrelated package / project-setting changes are excluded. `git diff --check` passed. No Git branch, commit, merge or package-install operation was performed.
