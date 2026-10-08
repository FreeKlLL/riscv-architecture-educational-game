using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;

public abstract class BaseProcessorInitialState : ScriptableObject
{
    [Header("Level Target Description")]
    public LocalizedString levelTarget;

    [Header("Program Counter")]
    public int pcRegisterInitialValue;

    [FormerlySerializedAs("_aufgabeTyp")] [Header("Answer Fields")]
    public ExerciseTyp aufgabeTyp = ExerciseTyp.REGISTER_FIELD;

    [FormerlySerializedAs("registerFieldAdressAnswer")] 
    [FormerlySerializedAs("RegisterFieldAdressAnswer")] 
    public int registerFieldAddressAnswer;

    [FormerlySerializedAs("RegisterFieldValueAnswer")] 
    public int registerFieldValueAnswer;

    [FormerlySerializedAs("memoryAdressAnswer")] 
    [FormerlySerializedAs("MemoryAdressAnswer")] 
    public int memoryAddressAnswer;

    [FormerlySerializedAs("MemoryValueAnswer")] 
    public int memoryValueAnswer;

    public int pcValueAnswer;

    [Header("Level Dialogues")]
    public DialogueGraph customDialogueGraph;
    
    [Header("Next Sub-Level (leave empty to return to Main Menu)")]
    public BaseProcessorInitialState nextSceneInitial;
}