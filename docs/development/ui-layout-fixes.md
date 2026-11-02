# Two local UI fixes

Branch: `workflow`. Verification date: 2026-10-23.

## Adventure headings

Changed only presentation in `Assets/Scripts/AdventureIntegration/AdventureDirector.cs`.

Previously the title and HUD used centre anchors and Y coordinates 315/335 in a 1280×720 design. CanvasScaler's width/height blend could produce a logical canvas shorter than 720 on wide Game Views, so those positions exceeded its top edge. The new scaler uses Expand to keep the full design area visible. The panel stretches to its canvas; title, status and exit button use top anchors, an explicit top padding and the device safe-area inset. Header placement no longer depends on the canvas centre or a scale factor that has not updated yet.

No profile, assessment, scoring, route, persistence or Regisseur code changed. No test profile was created and no task result was submitted during this UI check.

## Eintakt Charlie dialogue

Added `Assets/Scripts/DialogueSystem/EintaktDialogueLayout.cs` and its `.meta`. Added one serialized component override to `Canvas - PlayerOverlay/Canvas - DialogueSystem` in `Assets/Scenes/Levels/Eintaktprozessor/Level 10.unity`. Unity generated the scene-local references needed by that component; existing GUIDs and prior scene edits are retained. No shared UI prefab was modified.

The original text had a fixed 1262×488 rectangle with Overflow, centred at Y=-112; the answer holder occupied the same lower area, and scaled answer/navigation buttons could cross these bounds. The layout had neither a text/answer flow nor a clipping/scroll region.

The local component reuses the existing text, portrait, background and buttons. It creates a standard ScrollRect/RectMask2D and vertical layout containing the entire text followed by the actual answer buttons. Preferred text heights determine button heights. Back/Skip/Next stay in a separate fixed bottom region; the menu and portrait have reserved space. The scrollbar matches the viewport and long content can be scrolled completely into view. Portrait layout uses the available width and a smaller, separate portrait area.

The original text EventTrigger consumed drag events, so it is disabled locally and viewport clicks retain the existing text-skip/advance interaction. A second cause was the existing typewriter's vertex cache: it was created before the new text rectangle had its final height. The local component rebuilds that node's visual animation after layout completes, including after a resize. It does not modify the graph, node, cursor, choices or educational state. The shared DialogueUI/DialogueManager/animation code is unchanged.

## Actual Unity MCP verification

- Compilation completed without errors.
- Adventure profiles: Game View 800×450, 1600×720 and 450×800. Title remains inside the screen. HUD status/exit were also inspected at 1600×720 and 450×800, using the UI display method without starting or changing a player's Adventure route.
- Classic Eintakt was opened through its original MainMenu button and ProcessorLevelLoader with `Level 11-0`. No processor clocks or victory checks were executed.
- Existing Next buttons led to the branching dialogue; the third answer opened long node 5 with three answers (English source length 355, including markup). Checked at actual Game View resolutions 800×450 and 450×800.
- Scrolling to the bottom exposes all three complete answer buttons and the final sentence. The text-to-answer gap measured 10 screen pixels at 800×450; the viewport-to-bottom-navigation gap measured 22.5 pixels.
- EventSystem raycasts confirmed that visible answer centres resolve to their answer buttons. Clicking the second answer opened node 6, Back restored node 5, selecting the first answer opened node 7, and Next completed the dialogue naturally.
- Final Console: **0 errors, 0 warnings**. Play Mode stopped. MainMenu active. Original Free Aspect selection restored and all five temporary Game View presets removed.
- SHA-256 comparison against the pre-task snapshot confirms that the only changed pre-existing files are `AdventureDirector.cs` and the Eintakt scene. All dialogue assets, shared prefabs, processor code, Adventure domain code and other Classic scenes match their pre-task bytes.

Screenshots:

- [Adventure 800×450](ui-fix-adventure-800x450.png)
- [Adventure 1600×720](ui-fix-adventure-1600x720.png)
- [Adventure portrait](ui-fix-adventure-450x800.png)
- [Adventure HUD wide](ui-fix-adventure-hud-1600x720.png)
- [Adventure HUD portrait](ui-fix-adventure-hud-450x800.png)
- [Eintakt 800×450, scrolled answers](ui-fix-eintakt-800x450-bottom.png)
- [Eintakt portrait, beginning](ui-fix-eintakt-450x800-top.png)
- [Eintakt portrait, end and answers](ui-fix-eintakt-450x800-bottom.png)

No clipping/overlap remained in the inspected dialogue and heading states. Portrait Adventure still scales the existing desktop profile layout down; a full redesign of that screen was outside this task. Physical phones/notches and every dialogue/language combination were not tested. An attempted DE selection was superseded by the existing language controller, so the captured dialogue verification is EN; no DE-specific success is claimed. Automated gameplay suites were not rerun for these presentation-only changes.
