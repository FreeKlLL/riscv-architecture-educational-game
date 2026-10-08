using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }
    public event Action<bool> OnMusicStatusChanged;

    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioSource sourceA;
    [SerializeField] private AudioSource sourceB;

    [Header("Tracks")]
    [SerializeField] private MusicPlaylist playlist;

    private AudioSource _active;
    private AudioSource _standby;
    private Coroutine _fadeRoutine;
    
    // for every clip saves his time on stop
    private readonly Dictionary<AudioClip, float> _savedPositions = new Dictionary<AudioClip, float>();
    
    private bool _isMusicEnabled = true;
    public bool IsMusicEnabled => _isMusicEnabled;
    private Coroutine _toggleRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var src in new[] { sourceA, sourceB })
        {
            src.outputAudioMixerGroup = musicMixerGroup;
            src.playOnAwake = false;
            src.volume = 0f;
        }

        _active = sourceA;
        _standby = sourceB;
    }

    /// <summary>
    /// Picks the right track for the scene that is CURRENTLY active (menu / regular level /
    /// processor level) based on GameConstants scene indices.
    /// </summary>
    public void PlayForCurrentScene(float fadeDuration = 1.5f)
    {
        if (playlist == null) return;

        var index = SceneManager.GetActiveScene().buildIndex;

        AudioClip clip;
        if (index is GameConstants.FullProcessorSceneIndex or GameConstants.OneTickProcessorSceneIndex)
            clip = playlist.processorMusic;
        else if (index == GameConstants.MainMenuSceneIndex)
            clip = playlist.menuMusic;
        else
            clip = playlist.levelsMusic;

        if (clip != null) PlayLoop(clip, fadeDuration);
    }

    /// <summary>
    /// Crossfades to a single clip that already loops cleanly on its own (no separate intro).
    /// </summary>
    public void PlayLoop(AudioClip clip, float fadeDuration = 1.5f)
    {
        if (clip == null) return;
        
        if (!_isMusicEnabled)
        {
            if (_active.clip != clip)
            {
                if (_active.clip != null) _savedPositions[_active.clip] = _active.time;

                _active.clip = clip;
                _active.volume = 0f;
            
                if (_savedPositions.TryGetValue(clip, out float savedT))
                {
                    _active.time = savedT % clip.length;
                }
                _active.Stop();
            }
            return; 
        }
        
        // Case 1: clip is still playing
        if (_active.clip == clip)
        {
            // turn back volume
            if (_active.isPlaying)
            {
                if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
                _fadeRoutine = StartCoroutine(FadeVolume(_active, 1f, fadeDuration));
                return;
            }
        }

        // Case 2: changing to another track (need to save time for current track)
        if (_active.clip != null && _active.isPlaying)
        {
            _savedPositions[_active.clip] = _active.time;
        }

        _standby.clip = clip;
        _standby.loop = true;
        _standby.volume = 0f;
        _standby.Play();
        
        if (_savedPositions.TryGetValue(clip, out float savedTime))
        {
            _standby.time = savedTime % clip.length; 
        }

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(CrossfadeRoutine(fadeDuration));
    }

    /// <summary>
    /// Fades whatever is currently audible down to silence, without touching clips.
    /// Call this at the START of a scene transition, alongside your loadingOverlay fade-out.
    /// </summary>
    public void FadeToSilence(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(FadeVolume(sourceA, 0f, duration));
        StartCoroutine(FadeVolume(sourceB, 0f, duration));
    }

    private IEnumerator CrossfadeRoutine(float duration)
    {
        var from = _active;
        var to = _standby;
        var fromStart = from.volume;
        var t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            var p = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            from.volume = Mathf.Lerp(fromStart, 0f, p);
            to.volume = Mathf.Lerp(0f, 1f, p);
            yield return null;
        }

        from.volume = 0f;
        from.Stop();
        to.volume = 1f;

        _active = to;
        _standby = from;
    }

    private IEnumerator FadeVolume(AudioSource src, float target, float duration)
    {
        var start = src.volume;
        var t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, target, duration <= 0f ? 1f : t / duration);
            yield return null;
        }

        src.volume = target;
    }

    #region  UI toggle
    
    public void SetMusicStatus(bool isEnabled, float fadeDuration = 0.5f)
    {
        if (_isMusicEnabled == isEnabled) return;

        _isMusicEnabled = isEnabled;
        OnMusicStatusChanged?.Invoke(isEnabled);

        if (_toggleRoutine != null) StopCoroutine(_toggleRoutine);
        _toggleRoutine = StartCoroutine(ToggleMusicRoutine(isEnabled, fadeDuration));
    }

    private IEnumerator ToggleMusicRoutine(bool isEnabled, float duration)
    {
        if (isEnabled)
        {
            if (!_active.isPlaying)
            {
                _active.UnPause();
                if (!_active.isPlaying) _active.Play();
            }
            
            yield return StartCoroutine(FadeVolume(_active, 1f, duration));
        }
        else
        {
            yield return StartCoroutine(FadeVolume(_active, 0f, duration));
            
            _active.Pause();
        }
    }
    #endregion
}