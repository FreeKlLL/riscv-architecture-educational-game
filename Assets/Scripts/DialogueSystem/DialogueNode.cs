using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

/// <summary>
/// Represents a single step (node) in a dialogue tree.
/// Stores character data, dialogue text, and branching options.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogueNode", menuName = "Dialogue System/Node")]
public class DialogueNode : ScriptableObject
{
    [Header("Content")]
    [Tooltip("The text spoken by the character to the player.")]
    public LocalizedString dialogueText;

    [Tooltip("The name of the character speaking this line.")]
    private string _characterName;

    [Tooltip("Index used to fetch a specific emotion sprite from the character's sprite array.")]
    public EmotionType emotionIndex;

    [Header("Response Options")]
    public LocalizedString firstAnswer;
    public LocalizedString secondAnswer;
    public LocalizedString thirdAnswer;

    [Header("Branching Indices")]
    [Tooltip("Index of the DialogueNode to load if the first option is selected.")]
    public int firstOption;

    [Tooltip("Index of the DialogueNode to load if the second option is selected.")]
    public int secondOption;

    [Tooltip("Index of the DialogueNode to load if the third option is selected.")]
    public int thirdOption;
    
    public string GetDialogueText()  => dialogueText.GetLocalizedString();
    public string GetFirstAnswer()   => firstAnswer.GetLocalizedString();
    public string GetSecondAnswer()  => secondAnswer.GetLocalizedString();
    public string GetThirdAnswer()   => thirdAnswer.GetLocalizedString();
}
