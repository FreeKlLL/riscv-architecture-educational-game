using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private SfxLibrary library;

    [Header("Pool")]
    [SerializeField] private int poolSize = 8;
    [SerializeField] [Range(0f, 0.2f)] private float pitchVariance = 0.05f;
    
    [Header("UI Volumes")]
    [SerializeField] [Range(0f, 1f)] private float buttonClickVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float buttonHoverVolume = 0.4f;

    private readonly List<AudioSource> _pool = new();
    private int _nextIndex;

    private bool _isSfxEnabled = true;
    public bool IsSfxEnabled => _isSfxEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (var i = 0; i < poolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = sfxMixerGroup;
            src.playOnAwake = false;
            src.loop = false;
            _pool.Add(src);
        }
    }

    /// <summary>
    /// Plays a one-shot clip through the next free-ish pooled source, with a small
    /// random pitch offset so repeated sounds don't feel same.
    /// </summary>
    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !_isSfxEnabled) return;

        var src = GetNextSource();
        src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        src.PlayOneShot(clip, volume);
    }

    private AudioSource GetNextSource()
    {
        var src = _pool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % _pool.Count;
        return src;
    }

    public void ToggleSfx(bool nextState)
    {
        _isSfxEnabled = nextState;
    }

    #region UI shortcuts
    public void PlayButtonClick() => Play(library ? library.buttonClick : null, buttonClickVolume);
    public void PlayButtonHover() => Play(library ? library.buttonHover : null, buttonHoverVolume);
    public void PlayToggleOn() => Play(library ? library.toggleOn : null);
    public void PlayToggleOff() => Play(library ? library.toggleOff : null);
    public void PlayCancel() => Play(library ? library.cancel : null);
    public void PlayError() => Play(library ? library.error : null);
    #endregion
}