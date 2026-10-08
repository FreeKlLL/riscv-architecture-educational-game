using UnityEngine;

[CreateAssetMenu(fileName = "SFX Library", menuName = "Audio/SFX Library")]
public class SfxLibrary : ScriptableObject
{
    [Header("UI")]
    public AudioClip buttonClick;
    public AudioClip buttonHover;
    public AudioClip toggleOn;
    public AudioClip toggleOff;
    public AudioClip cancel;
 
    [Header("Gameplay")]
    public AudioClip error;
    public AudioClip success;
}
