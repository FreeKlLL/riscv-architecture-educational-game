# Dialogue tags and vertex animation

The dialogue system separates **what should happen while text is revealed** from
**how TextMeshPro renders the text**:

- `DialogueUtility` parses the author-facing tags and turns them into timed
  `DialogueCommand` objects.
- `DialogueVertexAnimator` reveals the cleaned text character by character and
  changes the TextMeshPro vertex mesh every frame.

This division is important: service tags do not reach TextMeshPro, while normal
TextMeshPro markup (for example, `<b>`, `<color=red>`, and `<sprite>`) remains in
the final string and is rendered by TMP.

## Dialogue text format

`DialogueUtility.ProcessInputString` receives the original line and returns two
results:

1. `processedMessage` — the text after the system's service tags have been
   removed. This is assigned to the `TMP_Text` component.
2. `List<DialogueCommand>` — instructions placed at the visible-character
   position where each tag occurred.

The supported tags are:

| Tag | Effect |
| --- | --- |
| `<p:tiny>`, `<p:short>`, `<p:normal>`, `<p:long>`, `<p:read>` | Pause before the next character for 0.1, 0.25, 0.666, 1, or 2 seconds respectively. |
| `<sp:150>` | Set the reveal speed to 150 characters per second. A value that cannot be parsed as a number falls back to 150. |
| `<anim:SHAKE>text</anim>` | Apply the `SHAKE` vertex animation to `text`. |
| `<anim:WAVE>text</anim>` | Apply the `WAVE` vertex animation to `text`. |

Animation names are case-insensitive because they are parsed as values of
`TextAnimationType`. Every `<anim:...>` must have a corresponding `</anim>`;
the animator logs an editor error when the counts differ. Ranges are paired in
their source order, so they should not be nested. Tags may be mixed with TMP
formatting, for example:

```text
<sp:75><color=yellow>Warning:</color> <anim:SHAKE>overflow!</anim><p:short>
```

### Positions refer to visible characters

Commands are not attached to a raw string offset. The parser calculates the
number of visible characters before each tag with
`VisibleCharactersUpToIndex`. TMP markup occupies characters in the source
string but does not create a normal visible glyph, so it must not shift the
moment at which a command runs. `<sprite>` is handled specially because a TMP
sprite occupies a visible character slot even though it is written as markup.

The handlers run in this order: pauses, speeds, animation starts, animation
ends. Each handler records positions and then removes its own tags. This keeps
the command indices aligned with the progressively cleaned text.

## How the reveal and mesh animation work

`AnimateTextIn` first assigns `processedMessage` to the `TMP_Text` component
and calls `ForceMeshUpdate()`. TMP then fills `textInfo` with metadata about
characters and one or more mesh buffers.

For every visible character, TextMeshPro represents its glyph as a **quad**:

```text
vertexIndex + 1  ─────  vertexIndex + 2
       │                   │
       │       glyph       │
vertexIndex + 0  ─────  vertexIndex + 3
```

Consequently, a visible character owns four adjacent entries in its mesh
buffer. `TMP_CharacterInfo.vertexIndex` gives the first entry, and
`materialReferenceIndex` selects the correct mesh buffer. The latter matters
because TMP can use several materials, such as the main font material and a
sprite/fallback material. Whitespace and other non-visible characters have no
quad and are skipped via `charInfo.isVisible`; otherwise their zero vertex index
could accidentally modify the first visible glyph.

The animator keeps two snapshots of TMP's generated data:

- `CopyMeshInfoVertexData()` provides the original four vertex positions for
  each glyph.
- `originalColors` preserves the four original vertex colours.

Each frame, it loops through `characterInfo` and performs these operations for
every visible glyph:

1. **Visibility:** before a character's reveal index is reached, all four of
   its colours are set to transparent (`Clear`); after that, the original colour
   is restored to all four vertices.
2. **Pop-in:** a newly revealed character grows from the centre of its quad to
   its original size over `CHAR_ANIM_TIME` (0.07 seconds). Each source vertex
   is moved towards the quad centre by the same scale factor, so the shape is
   preserved.
3. **Continuous effect:** `GetAnimPosAdjustment` returns an offset for the
   character's active animation range. That offset is added to all four
   vertices, translating the whole glyph without deforming it. `SHAKE` uses two
   Perlin-noise samples for changing X/Y offsets; `WAVE` uses a sine wave for a
   vertical offset.

Finally, `UpdateVertexData(Colors32)` uploads colour changes and
`UpdateGeometry` uploads each mesh's changed vertex positions. The loop yields
once per frame, so the reveal, pop-in, and active effects remain smooth.

The text reveal itself is time-based and uses `Time.unscaledTime`: it is
unaffected by `Time.timeScale`. At each visible-character index,
`ExecuteCommandsForCurrentIndex` applies pending pause and speed commands,
then the next character is scheduled and (optionally) its voice sound is
played. `SkipToEndOfCurrentMessage` marks every remaining character as started
and completes the reveal, while the coroutine continues to maintain the final
mesh until its owner stops it.

## Adding a new parsed tag

Add a parsed tag when the tag changes dialogue behaviour and should be removed
before the text is sent to TMP. The implementation has four parts:

1. **Define its syntax and regex in `DialogueUtility`.** Reuse
   `REMAINDER_REGEX` when the tag holds a value that ends at `>` (or at the end
   of the string). Named groups make extracting the value explicit.
2. **Add a handler** that enumerates `Regex.Matches`, creates a
   `DialogueCommand` at `VisibleCharactersUpToIndex(processedMessage,
   match.Index)`, and removes exactly those tags with `Regex.Replace`.
3. **Add a command type and data field only if needed.** Extend
   `DialogueCommandType`; use `FloatValue`, `StringValue`, or a new strongly
   typed field to carry the parsed value.
4. **Consume the command in `DialogueVertexAnimator`.** A timing command
   belongs in `ExecuteCommandsForCurrentIndex`. A range animation needs start
   and end commands, extraction in `SeparateOutTextAnimInfo`, and an effect in
   `GetAnimPosAdjustment` (plus a new `TextAnimationType` value).

For example, this regex follows the existing convention for an `<amp:...>`
tag:

```csharp
private const string AMPLITUDE_REGEX_STRING =
    "<amp:(?<amplitude>" + REMAINDER_REGEX + ")>";
private static readonly Regex AmplitudeRegex = new Regex(AMPLITUDE_REGEX_STRING);
```

Its handler should validate the `amplitude` group before adding a command. Do
not merely add the regex: if the tag is removed but no command is created and
consumed, the author-visible syntax silently has no effect. Conversely, if a
tag has a command but is not removed, TMP receives an unknown tag and may render
or report it incorrectly.

### Adding an animation tag or type

The current `<anim:TYPE>...</anim>` syntax already handles new animation
types. To add one, add the enum value and implement its vertex offset:

```csharp
public enum TextAnimationType
{
    NONE,
    SHAKE,
    WAVE,
    BOUNCE
}

// In GetAnimPosAdjustment:
case TextAnimationType.BOUNCE:
    y += Mathf.Abs(Mathf.Sin((charIndex * 1.5f) + (time * 6)))
         * fontSize * 0.06f;
    break;
```

Authors can then write `<anim:BOUNCE>important text</anim>`. Prefer offsets
relative to `fontSize` so the effect scales naturally with the text. If the
effect should change a glyph's shape rather than move it, calculate a different
new position for each of its four vertices; preserve the source snapshot as the
baseline so offsets do not accumulate every frame.

### Decoration tags versus parsed tags

Use ordinary TMP tags for purely visual decoration that TextMeshPro already
understands, such as `<b>`, `<i>`, `<u>`, `<color>`, `<size>`, or `<sprite>`.
They must stay in `processedMessage`, and `VisibleCharactersUpToIndex` must
continue to treat them as non-visible markup (except visible `<sprite>`).

Create a `DialogueUtility` regex only for project-specific decoration that TMP
does not understand, or for decoration that must drive custom vertex behaviour.
For a paired project-specific decoration tag, follow the same start/end pattern
as `anim`: parse both tags, record their visible-character positions, remove
them, and build a range used by the animator. When introducing a new TMP tag
whose visible output is not a standard glyph, update
`VisibleCharactersUpToIndex` as well so later commands do not drift.

## Checklist for changes

- Test a tag at the beginning, middle, and end of a message.
- Test it beside TMP formatting and `<sprite>` tags.
- Check invalid values and unmatched start/end range tags.
- Confirm command positions still match the displayed characters.
- Verify that every mesh/material used by the string is updated, not only the
  primary font mesh.
