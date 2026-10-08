using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SFXandMusicController : MonoBehaviour
{
    private const float MIN_DB = -80f;

    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    
    private void Start()
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.OnMusicStatusChanged += SetSlidersVisibility;
        
        var playerPrefMusic = PlayerPrefs.GetFloat(GameConstants.MucicParam, 1f);
        var playerPrefSFX = PlayerPrefs.GetFloat(GameConstants.SfxParam, 1f);

        if (musicSlider != null)
        {
            musicSlider.value = playerPrefMusic;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = playerPrefSFX;
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }
        
        SetMusicVolume(playerPrefMusic);
        SetSFXVolume(playerPrefSFX);
    }
    
    private void OnDestroy()
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.OnMusicStatusChanged -= SetSlidersVisibility;
    }

    private void SetMusicVolume(float linearValue)
    {
        audioMixer.SetFloat(GameConstants.MucicParam, LinearToDecibel(linearValue));
        PlayerPrefs.SetFloat(GameConstants.MucicParam, linearValue);
    }

    private void SetSFXVolume(float linearValue)
    {
        audioMixer.SetFloat(GameConstants.SfxParam, LinearToDecibel(linearValue));
        PlayerPrefs.SetFloat(GameConstants.SfxParam, linearValue);
    }
    
    private static float LinearToDecibel(float linear)
    {
        return linear <= 0.0001f ? MIN_DB : Mathf.Log10(linear) * 20f;
    }
    
    private void SetSlidersVisibility(bool isEnabled)
    {
        if(musicSlider != null) musicSlider.interactable = isEnabled;
        if(sfxSlider != null) sfxSlider.interactable = isEnabled;
    }
}