using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the main menu logic, including level unlocking based on player progress.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("List of buttons for level selection. Order in array should match level progression.")]
    public Button[] levelButtons;

    [Header("Loading Screen")]
    [SerializeField] private CanvasGroup loadingOverlay;
    [SerializeField] private float transitionDuration = 1f;
    private bool _isLoadingOverlayNotNull;
    
    [Header("WebGL Quit Button")]
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        SetFrameRate();

        UpdateLevelButtons();
        
        #if UNITY_WEBGL
            if (quitButton != null)
            quitButton.gameObject.SetActive(false);
        #endif

        if (loadingOverlay == null) return;
        loadingOverlay.alpha = 1f;
        loadingOverlay.blocksRaycasts = true;
    }

    private void Start()
    {
        MusicManager.Instance?.PlayForCurrentScene(transitionDuration);
        
        _isLoadingOverlayNotNull = loadingOverlay != null;
        if (loadingOverlay != null)
        {
            loadingOverlay.DOFade(0f, transitionDuration).SetUpdate(true).OnComplete(() => {
                loadingOverlay.blocksRaycasts = false;
            });
        }
    }
    private IEnumerator LoadLevelWithFade(int levelID)
    {
        MusicManager.Instance?.FadeToSilence(transitionDuration);
        
        if (_isLoadingOverlayNotNull)
        {
            loadingOverlay.blocksRaycasts = true;
            yield return loadingOverlay.DOFade(1f, transitionDuration).SetUpdate(true).WaitForCompletion();
        }

        var op = SceneManager.LoadSceneAsync(levelID);
        if (op == null) yield break;
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            yield return null;
        }

        op.allowSceneActivation = true;
    }

    private void UpdateLevelButtons()
    {
        if (levelButtons == null || levelButtons.Length == 0) return;

        // Default to 1 if the key doesn't exist
        var unlockedLevels = PlayerPrefs.GetInt(GameConstants.UnlockedLevelKey, 1);

        // One loop to rule them all: set interactable state based on index
        for (var i = 0; i < levelButtons.Length; i++)
        {
            // A button is interactable if its index is less than the number of unlocked levels
            levelButtons[i].interactable = (i < unlockedLevels);
        }
    }
    public void UnlockAllLevels()
    {
        var totalScenes = levelButtons.Length;

        PlayerPrefs.SetInt(GameConstants.UnlockedLevelKey, totalScenes);
        PlayerPrefs.Save();

        UpdateLevelButtons();

        CustomLog.LogEditor("[MainMenu] All levels unlocked!");
    }

    /// <summary>
    /// Loads a specific scene by its build index.
    /// </summary>
    /// <param name="levelID">The index of the scene in Build Settings.</param>
    public void OpenLevel(int levelID)
    {
        // Optional: Check if the index is valid for BuildSettings
        if (levelID >= 0 && levelID < SceneManager.sceneCountInBuildSettings)
        {
            //SceneManager.LoadScene(levelID);
            StartCoroutine(LoadLevelWithFade(levelID));
        }
        else
        {
            CustomLog.LogEditorError($"[MainMenu] Attempted to load invalid level index: {levelID}");
        }
    }

    /// <summary>
    /// Closes the application.
    /// </summary>
    public void Quit() {
        CustomLog.LogEditor("[MainMenu] Quit requested.");
        Application.Quit();
    }

    private static void SetFrameRate()
    {
    #if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = 60;
    #else
        Application.targetFrameRate = -1; // for webGL, linux and windows
    #endif
    }
}