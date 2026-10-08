using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AudioButtonController : MonoBehaviour
{
    [SerializeField] private Image targetImage; 
    
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void Start()
    {
        if (MusicManager.Instance == null)
        {
            // if we started scene not from main menu
            _button.interactable = false; 
            return;
        }
        
        UpdateSprite(MusicManager.Instance.IsMusicEnabled);
        
        _button.onClick.AddListener(ToggleMusic);
    }

    private void ToggleMusic()
    {
        if (MusicManager.Instance == null || SfxManager.Instance == null) return;
        
        var nextState = !MusicManager.Instance.IsMusicEnabled;
        MusicManager.Instance.SetMusicStatus(nextState);
        SfxManager.Instance.ToggleSfx(nextState);
        UpdateSprite(nextState);
    }

    private void UpdateSprite(bool isEnabled)
    {
        if (targetImage == null) return;
        
        targetImage.sprite = isEnabled ? musicOnSprite : musicOffSprite;
    }
}