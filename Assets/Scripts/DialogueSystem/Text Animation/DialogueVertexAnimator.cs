using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DialogueVertexAnimator
{
    private bool _textAnimating;
    private bool _stopAnimating;

    public bool TextAnimating => _textAnimating;

    private readonly TMP_Text _textBox;
    private readonly float _textAnimationScale;
    /* private readonly AudioSourceGroup audioSourceGroup;
    public DialogueVertexAnimator(TMP_Text _textBox, AudioSourceGroup _audioSourceGroup)
    {
        textBox = _textBox;
        audioSourceGroup = _audioSourceGroup;
        textAnimationScale = textBox.fontSize;
    }*/

    public DialogueVertexAnimator(TMP_Text textBox)
    {
        this._textBox = textBox;
        _textAnimationScale = this._textBox.fontSize;
    }

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private const float CHAR_ANIM_TIME = 0.07f;
    private static readonly Vector3 VecZero = Vector3.zero;
    public IEnumerator AnimateTextIn(List<DialogueCommand> commands, string processedMessage, AudioClip voiceSound, Action onFinish)
    {
        _textAnimating = true;
        var secondsPerCharacter = 1f / 150f;
        float timeOfLastCharacter = 0;

        var textAnimInfo = SeparateOutTextAnimInfo(commands);
        var textInfo = _textBox.textInfo;
        foreach (var meshInfer in textInfo.meshInfo)
        {
            if (meshInfer.vertices == null) continue;
            for (var j = 0; j < meshInfer.vertices.Length; j++)
            {
                meshInfer.vertices[j] = VecZero;
            }
        }

        _textBox.text = processedMessage;
        _textBox.ForceMeshUpdate();

        var cachedMeshInfo = textInfo.CopyMeshInfoVertexData();
        var originalColors = new Color32[textInfo.meshInfo.Length][];
        for (var i = 0; i < originalColors.Length; i++)
        {
            var theColors = textInfo.meshInfo[i].colors32;
            originalColors[i] = new Color32[theColors.Length];
            Array.Copy(theColors, originalColors[i], theColors.Length);
        }
        var charCount = textInfo.characterCount;
        var charAnimStartTimes = new float[charCount];
        for (var i = 0; i < charCount; i++)
        {
            charAnimStartTimes[i] = -1; //indicate the character as not yet started animating.
        }
        var visibleCharacterIndex = 0;
        while (true)
        {
            if (_stopAnimating)
            {
                for (var i = visibleCharacterIndex; i < charCount; i++)
                {
                    charAnimStartTimes[i] = Time.unscaledTime;
                }
                visibleCharacterIndex = charCount;
                FinishAnimating(onFinish);
            }
            if (ShouldShowNextCharacter(secondsPerCharacter, timeOfLastCharacter))
            {
                if (visibleCharacterIndex <= charCount)
                {
                    ExecuteCommandsForCurrentIndex(commands, visibleCharacterIndex, ref secondsPerCharacter, ref timeOfLastCharacter);
                    if (visibleCharacterIndex < charCount && ShouldShowNextCharacter(secondsPerCharacter, timeOfLastCharacter))
                    {
                        charAnimStartTimes[visibleCharacterIndex] = Time.unscaledTime;
                        PlayDialogueSound(voiceSound);
                        visibleCharacterIndex++;
                        timeOfLastCharacter = Time.unscaledTime;
                        if (visibleCharacterIndex == charCount)
                        {
                            FinishAnimating(onFinish);
                        }
                    }
                }
            }
            for (var j = 0; j < charCount; j++)
            {
                var charInfo = textInfo.characterInfo[j];
                if (!charInfo.isVisible) continue; //Invisible characters have a vertexIndex of 0 because they have no vertices, and so they should be ignored to avoid messing up the first character in the string which also has a vertexIndex of 0
                var vertexIndex = charInfo.vertexIndex;
                var materialIndex = charInfo.materialReferenceIndex;
                var destinationColors = textInfo.meshInfo[materialIndex].colors32;
                var theColor = j < visibleCharacterIndex ? originalColors[materialIndex][vertexIndex] : Clear;
                destinationColors[vertexIndex + 0] = theColor;
                destinationColors[vertexIndex + 1] = theColor;
                destinationColors[vertexIndex + 2] = theColor;
                destinationColors[vertexIndex + 3] = theColor;

                var sourceVertices = cachedMeshInfo[materialIndex].vertices;
                var destinationVertices = textInfo.meshInfo[materialIndex].vertices;
                float charSize = 0;
                var charAnimStartTime = charAnimStartTimes[j];
                if (charAnimStartTime >= 0)
                {
                    var timeSinceAnimStart = Time.unscaledTime - charAnimStartTime;
                    charSize = Mathf.Min(1, timeSinceAnimStart / CHAR_ANIM_TIME);
                }

                var animPosAdjustment = GetAnimPosAdjustment(textAnimInfo, j, _textBox.fontSize, Time.unscaledTime);
                var offset = (sourceVertices[vertexIndex + 0] + sourceVertices[vertexIndex + 2]) / 2;
                destinationVertices[vertexIndex + 0] = ((sourceVertices[vertexIndex + 0] - offset) * charSize) + offset + animPosAdjustment;
                destinationVertices[vertexIndex + 1] = ((sourceVertices[vertexIndex + 1] - offset) * charSize) + offset + animPosAdjustment;
                destinationVertices[vertexIndex + 2] = ((sourceVertices[vertexIndex + 2] - offset) * charSize) + offset + animPosAdjustment;
                destinationVertices[vertexIndex + 3] = ((sourceVertices[vertexIndex + 3] - offset) * charSize) + offset + animPosAdjustment;
            }
            _textBox.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            for (var i = 0; i < textInfo.meshInfo.Length; i++)
            {
                var theInfo = textInfo.meshInfo[i];
                theInfo.mesh.vertices = theInfo.vertices;
                _textBox.UpdateGeometry(theInfo.mesh, i);
            }
            yield return null;
        }
    }

    private static void ExecuteCommandsForCurrentIndex(List<DialogueCommand> commands, int visibleCharacterIndex, ref float secondsPerCharacter, ref float timeOfLastCharacter)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            if (command.Position != visibleCharacterIndex) continue;
            switch (command.Type)
            {
                case DialogueCommandType.Pause:
                    timeOfLastCharacter = Time.unscaledTime + command.FloatValue;
                    break;
                case DialogueCommandType.TextSpeedChange:
                    secondsPerCharacter = 1f / command.FloatValue;
                    break;
            }
            commands.RemoveAt(i);
            i--;
        }
    }

    private void FinishAnimating(Action onFinish)
    {
        _textAnimating = false;
        _stopAnimating = false;
        onFinish?.Invoke();
    }

    private const float NOISE_MAGNITUDE_ADJUSTMENT = 0.06f;
    private const float NOISE_FREQUENCY_ADJUSTMENT = 15f;
    private const float WAVE_MAGNITUDE_ADJUSTMENT = 0.06f;
    private static Vector3 GetAnimPosAdjustment(TextAnimInfo[] textAnimInfo, int charIndex, float fontSize, float time)
    {
        float x = 0;
        float y = 0;
        foreach (var info in textAnimInfo)
        {
            if (charIndex < info.StartIndex || charIndex >= info.EndIndex) continue;
            switch (info.Type)
            {
                case TextAnimationType.SHAKE:
                {
                    var scaleAdjust = fontSize * NOISE_MAGNITUDE_ADJUSTMENT;
                    x += (Mathf.PerlinNoise((charIndex + time) * NOISE_FREQUENCY_ADJUSTMENT, 0) - 0.5f) * scaleAdjust;
                    y += (Mathf.PerlinNoise((charIndex + time) * NOISE_FREQUENCY_ADJUSTMENT, 1000) - 0.5f) * scaleAdjust;
                    break;
                }
                case TextAnimationType.WAVE:
                    y += Mathf.Sin((charIndex * 1.5f) + (time * 6)) * fontSize * WAVE_MAGNITUDE_ADJUSTMENT;
                    break;
            }
        }
        return new Vector3(x, y, 0);
    }

    private static bool ShouldShowNextCharacter(float secondsPerCharacter, float timeOfLastCharacter)
    {
        return (Time.unscaledTime - timeOfLastCharacter) > secondsPerCharacter;
    }
    public void SkipToEndOfCurrentMessage()
    {
        if (_textAnimating)
        {
            _stopAnimating = true;
        }
    }

    private float _timeUntilNextDialogueSound;
    private float _lastDialogueSound;
    private void PlayDialogueSound(AudioClip voiceSound)
    {
        if (!(Time.unscaledTime - _lastDialogueSound > _timeUntilNextDialogueSound)) return;
        _timeUntilNextDialogueSound = UnityEngine.Random.Range(0.02f, 0.08f);
        _lastDialogueSound = Time.unscaledTime;
        // audioSourceGroup.PlayFromNextSource(voice_sound); //Use Multiple Audio Sources to allow playing multiple sounds at once
    }

    private static TextAnimInfo[] SeparateOutTextAnimInfo(List<DialogueCommand> commands)
    {
        var tempResult = new List<TextAnimInfo>();
        var animStartCommands = new List<DialogueCommand>();
        var animEndCommands = new List<DialogueCommand>();
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            switch (command.Type)
            {
                case DialogueCommandType.AnimStart:
                    animStartCommands.Add(command);
                    commands.RemoveAt(i);
                    i--;
                    break;
                case DialogueCommandType.AnimEnd:
                    animEndCommands.Add(command);
                    commands.RemoveAt(i);
                    i--;
                    break;
            }
        }
        if (animStartCommands.Count != animEndCommands.Count)
        {
            CustomLog.LogEditorError("Unequal number of start and end animation commands. Start Commands: " + animStartCommands.Count + " End Commands: " + animEndCommands.Count);
        }
        else
        {
            for (var i = 0; i < animStartCommands.Count; i++)
            {
                var startCommand = animStartCommands[i];
                var endCommand = animEndCommands[i];
                tempResult.Add(new TextAnimInfo
                {
                    StartIndex = startCommand.Position,
                    EndIndex = endCommand.Position,
                    Type = startCommand.TextAnimValue
                });
            }
        }
        return tempResult.ToArray();
    }
}

public struct TextAnimInfo
{
    public int StartIndex;
    public int EndIndex;
    public TextAnimationType Type;
}