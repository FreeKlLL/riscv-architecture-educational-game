using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Manages the visual representation of the dialogue system, 
/// handling text display, branching buttons, and character animations.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [FormerlySerializedAs("_textField")]
    [Header("Main Content")]
    [SerializeField] private TMP_Text textField;
    [FormerlySerializedAs("charackterImage")] [FormerlySerializedAs("_charackterImage")] [SerializeField] private Image characterImage;
    [FormerlySerializedAs("_goNextQuoteButton")] [SerializeField] private Button goNextQuoteButton;
    [SerializeField] private Button goPrevQuoteButton;

    [Header("Answer Options")]
    [SerializeField] private Transform buttonContainer;
    [FormerlySerializedAs("_firstAnswerButton")] [SerializeField] private Button firstAnswerButton;
    [FormerlySerializedAs("_secondAnswerButton")] [SerializeField] private Button secondAnswerButton;
    [FormerlySerializedAs("_thirdAnswerButton")] [SerializeField] private Button thirdAnswerButton;

    [FormerlySerializedAs("_firstAnswerText")] [SerializeField] private TMP_Text firstAnswerText;
    [FormerlySerializedAs("_secondAnswerText")] [SerializeField] private TMP_Text secondAnswerText;
    [FormerlySerializedAs("_thirdAnswerText")] [SerializeField] private TMP_Text thirdAnswerText;

    [Header("Settings & Data")]
    [SerializeField] private EmotionController currentEmotion;
    private Vector2 _basePortraitPos;

    public event Action OnNextRequested;
    public event Action OnBackRequested;
    public event Action<int> OnSpecificPathRequested;

    // text animation variables
    private DialogueVertexAnimator _vertexAnimator;
    private Coroutine _textRoutine;

    private void Awake()
    {
        _basePortraitPos = characterImage.rectTransform.anchoredPosition;
        _vertexAnimator = new DialogueVertexAnimator(textField);
    }

    private void Start()
    {
        // Subscribe to buttons
        goNextQuoteButton.onClick.AddListener(() => OnNextRequested?.Invoke());
        firstAnswerButton.onClick.AddListener(() => OnSpecificPathRequested?.Invoke(1));
        secondAnswerButton.onClick.AddListener(() => OnSpecificPathRequested?.Invoke(2));
        thirdAnswerButton.onClick.AddListener(() => OnSpecificPathRequested?.Invoke(3));
        
        goPrevQuoteButton.onClick.AddListener(() => OnBackRequested?.Invoke());
    }


    /// <summary>
    /// Refreshes the UI with data from the provided DialogueNode.
    /// </summary>
    /// <param name="node">The data container for the current dialogue step.</param>
    public void UpdateVisuals(DialogueNode node) {
        if (_textRoutine != null) StopCoroutine(_textRoutine);
        textField.DOKill();

        var commands = DialogueUtility.ProcessInputString(node.GetDialogueText(), out var cleanText);

        _textRoutine = StartCoroutine(_vertexAnimator.AnimateTextIn(commands, cleanText, null, () => {
            CustomLog.LogEditor("Printing complete!");
        }));


        if (node.firstAnswer is { IsEmpty: false }) {
            DecorateSelection(node);
        }
        else if (buttonContainer.gameObject.activeSelf) {
            buttonContainer.gameObject.SetActive(false);
            goNextQuoteButton.gameObject.SetActive(true);
        }

        var emotionIndex = (int)node.emotionIndex;
        if (emotionIndex < 0 || emotionIndex >= currentEmotion.emotions.Count)
        {
            CustomLog.LogEditorError($"Index of emotion {node.emotionIndex} is out of bounds !");
            return;
        }

        var newSprite = currentEmotion.emotions[emotionIndex];

        // Play animation only if sprite changes
        if (characterImage.sprite != newSprite)
        {
            PlayBounceAndChangeSprite(newSprite);
        }
    }
    public void StopAnimatingText() {
        if (_textRoutine != null) StopCoroutine(_textRoutine);
    }
    public void SkipAnimationOfTyping() {
        _vertexAnimator.SkipToEndOfCurrentMessage();
    }
    
    public void SetBackAvailable(bool available) => goPrevQuoteButton.interactable = available;
    

    public void HandleUserQuoteSkip()
    {
        if (_vertexAnimator.TextAnimating)
        {
            SkipAnimationOfTyping();
        }
        else if(goNextQuoteButton.gameObject.activeSelf)
        {
            OnNextRequested?.Invoke();
        }
    }

    /// <summary>
    /// Configures the visibility and text of the response buttons.
    /// </summary>
    private void DecorateSelection(DialogueNode node) 
    {
        if (node.firstAnswer is { IsEmpty: false }) {
            buttonContainer.gameObject.SetActive(true);
            goNextQuoteButton.gameObject.SetActive(false);
            firstAnswerText.text = node.GetFirstAnswer();
        }
        if (node.secondAnswer is { IsEmpty: false })
        {
            secondAnswerButton.gameObject.SetActive(true);
            secondAnswerText.text = node.GetSecondAnswer();
        }
        else { 
            secondAnswerButton.gameObject.SetActive(false);
        }

        if (node.thirdAnswer is { IsEmpty: false })
        {
            thirdAnswerButton.gameObject.SetActive(true);
            thirdAnswerText.text = node.GetThirdAnswer();
        }
        else
        {
            thirdAnswerButton.gameObject.SetActive(false);
        }

    }

    /// <summary>
    /// Plays a 'jump' animation using DOTween and swaps the sprite at the peak.
    /// </summary>
    private void PlayBounceAndChangeSprite(Sprite nextSprite)
    {
        characterImage.rectTransform.DOKill();
        characterImage.rectTransform.anchoredPosition = _basePortraitPos; // Reset position before animation

        var jumpTarget = _basePortraitPos + new Vector2(0, 45f);

        // Sequence: Jump up -> Swap Sprite -> Bounce down
        characterImage.rectTransform.DOAnchorPos(jumpTarget, 0.15f)
        .SetEase(Ease.OutQuad)
        .OnComplete(() =>
        {
            characterImage.sprite = nextSprite;
            characterImage.rectTransform.DOAnchorPos(_basePortraitPos, 0.35f)
                .SetEase(Ease.OutBounce);
        });
    }

    private void OnDestroy()
    {
        // Clean up listeners to prevent memory leaks
        goNextQuoteButton.onClick.RemoveAllListeners();
        goPrevQuoteButton.onClick.RemoveAllListeners();
        firstAnswerButton.onClick.RemoveAllListeners();
        secondAnswerButton.onClick.RemoveAllListeners();
        thirdAnswerButton.onClick.RemoveAllListeners();
    }
}
