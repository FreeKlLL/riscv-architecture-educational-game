using DG.Tweening;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Controls the level lifecycle, including dialogue triggers, 
/// end-of-level results, and scene transitions.
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("Dependencies")]
    public GameObject endLevelMenuUI;
    [FormerlySerializedAs("_animManager")] [SerializeField] private BeginnOfLevelAnimationManager animManager;
    [FormerlySerializedAs("_dialogueManager")] [SerializeField] private DialogueManager dialogueManager;

    [Header("Loading Screen")]
    [SerializeField] private CanvasGroup loadingOverlay;
    [SerializeField] private float transitionDuration = 1f;

    [FormerlySerializedAs("BGGroup")]
    [Header("Results Panel Animation")]
    [SerializeField] private CanvasGroup bgGroup;
    [SerializeField] private CanvasGroup resultsPanelGroup;
    [SerializeField] private RectTransform resultsPanelRectTransform;
    [FormerlySerializedAs("_fadeTime")] [SerializeField] private float fadeTime = 0.3f;

    [Header("Star Rating")]
    public Image[] stars;
    public Sprite gainedStar;
    [FormerlySerializedAs("_starPopDuration")] [SerializeField] private float starPopDuration = 1.8f;

    // for transition between processor levels
    public static Func<object> OnRequestNextLevelData;

    public void Awake()
    {
        // Subscribe to dialogue events to toggle UI visibility
        dialogueManager.OnDialogueBegin += animManager.HideUIAndShowDialogue;
        dialogueManager.OnDialogueEnd += animManager.HideDialogue;
        dialogueManager.OnHintEnabled += animManager.HideUIAndShowDialogue;

        InitializeResultsPanel();

        if (loadingOverlay == null) return;
        loadingOverlay.alpha = 1f;
        loadingOverlay.blocksRaycasts = true;
    }

    private void Start()
    {
        if (loadingOverlay == null) return;
        
        MusicManager.Instance?.PlayForCurrentScene(transitionDuration);
        
        var fadeInSequence = DOTween.Sequence();
        fadeInSequence.AppendInterval(0.2f);
        fadeInSequence.Append(loadingOverlay.DOFade(0f, transitionDuration).SetUpdate(true));
        fadeInSequence.OnComplete(() => {
            loadingOverlay.blocksRaycasts = false;
        });
    }

    public void SetLevelDialogue(DialogueGraph d) {
        dialogueManager.SetActiveGraph(d);
    }

    /// <summary>
    /// Restarts the current active scene.
    /// </summary>
    public DialogueManager Dialogue => dialogueManager;
    public void RestartLevel()
    {
        if (AdventureDirector.IsInAdventure) { AdventureDirector.Instance.Route(); return; }
        StartCoroutine(LoadWithTransition(SceneManager.GetActiveScene().buildIndex));
    }

    /// <summary>
    /// Returns to the main menu.
    /// </summary>
    public void LoadMainMenu()
    {
        if (AdventureDirector.IsInAdventure) { AdventureDirector.Instance.Exit(); return; }
        //SceneManager.LoadScene("MainMenu");
        StartCoroutine(LoadWithTransition(0));
    }

    /// <summary>
    /// Advances to the next level and saves progress.
    /// </summary>
    public void LoadNextLevel()
    {
        if (AdventureDirector.IsInAdventure) { AdventureDirector.Instance.ContinueJourney(); return; }
        var currentIndex = SceneManager.GetActiveScene().buildIndex;
        if (currentIndex is GameConstants.FullProcessorSceneIndex or GameConstants.OneTickProcessorSceneIndex) {
            var nextData = OnRequestNextLevelData?.Invoke();

            if (nextData != null)
            {
                switch (nextData)
                {
                    case OneTickProcessorInitialState oneTickData:
                        OneTickRegisseur.Initial = oneTickData;
                        StartCoroutine(LoadWithTransition(GameConstants.OneTickProcessorSceneIndex));
                        break;
                    case ProcessorInitialState fullData:
                        FullProcessorRegisseur.Initial = fullData;
                        StartCoroutine(LoadWithTransition(GameConstants.FullProcessorSceneIndex));
                        break;
                }
            }
            else
            {
                CustomLog.LogEditor("No more processor sublevels. Loading Main Menu.");
                StartCoroutine(LoadWithTransition(0));
            }
            return;
        }

        var nextLevelIndex = currentIndex + 1;

        // Save progress if this is a new level
        var savedProgress = PlayerPrefs.GetInt(GameConstants.UnlockedLevelKey, 1);
        if (nextLevelIndex > savedProgress)
        {
            PlayerPrefs.SetInt(GameConstants.UnlockedLevelKey, nextLevelIndex);
            PlayerPrefs.Save();
        }

        if (nextLevelIndex < SceneManager.sceneCountInBuildSettings)
        {
            //SceneManager.LoadScene(nextLevelIndex);
            StartCoroutine(LoadWithTransition(nextLevelIndex));
        }
        else
        {
            CustomLog.LogEditor("All levels completed! Loading Main Menu (Index 0).");
            //SceneManager.LoadScene(0);
            StartCoroutine(LoadWithTransition(0));
        }
    }

    private IEnumerator LoadWithTransition(int targetIndex)
    {
        // FadeIn
        loadingOverlay.blocksRaycasts = true;
        loadingOverlay.interactable = false;
        
        MusicManager.Instance?.FadeToSilence(transitionDuration);

        yield return loadingOverlay.DOFade(1f, transitionDuration).SetUpdate(true).WaitForCompletion();

        // Loading next level
        var op = SceneManager.LoadSceneAsync(targetIndex);
        if (op == null) yield break;
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            yield return null;
        }

        // Scene activation
        op.allowSceneActivation = true;
    }

    #region stars counting and next level unlocking
    /// <summary>
    /// Displays the results panel with a smooth animation.
    /// </summary>
    public void OpenEndOfLevelMenu()
    {
        if (endLevelMenuUI == null) return;
        bgGroup.DOKill();
        bgGroup.interactable = true;
        bgGroup.blocksRaycasts = true;
        bgGroup.DOFade(1, fadeTime);


        resultsPanelRectTransform.DOKill();
        resultsPanelGroup.DOKill();

        resultsPanelGroup.interactable = true;
        resultsPanelGroup.blocksRaycasts = true;

        resultsPanelRectTransform.DOAnchorPos(Vector2.zero, fadeTime).SetEase(Ease.OutBack);
        resultsPanelGroup.DOFade(1, fadeTime);
    }
    private void InitializeResultsPanel()
    {
        resultsPanelGroup.alpha = 0f;
        resultsPanelGroup.interactable = false;
        resultsPanelGroup.blocksRaycasts = false;
        resultsPanelRectTransform.anchoredPosition = new Vector2(0, -1000f);
    }

    /// <summary>
    /// Updates stars based on player performance with a pop animation.
    /// </summary>
    /// <param name="gainedStarsCount">Number of stars to fill (0 to 3).</param>
    public void SetGainedStars(int gainedStarsCount) {
        var count = Mathf.Clamp(gainedStarsCount, 0, stars.Length);

        for (var i = 0; i < count; i++)
        {
            stars[i].sprite = gainedStar;

            // Star animation
            stars[i].transform.localScale = Vector3.zero;
            stars[i].transform.DOScale(Vector3.one, starPopDuration)
                .SetEase(Ease.OutBack)
                .SetDelay(i * 0.15f); // Stars pop one by one
        }
    }
    #endregion

    private void OnDestroy()
    {
        // Crucial: unsubscribe to prevent memory leaks and ghost calls
        if (dialogueManager == null) return;
        dialogueManager.OnDialogueBegin -= animManager.HideUIAndShowDialogue;
        dialogueManager.OnDialogueEnd -= animManager.HideDialogue;
        dialogueManager.OnHintEnabled -= animManager.HideUIAndShowDialogue;
    }
}
