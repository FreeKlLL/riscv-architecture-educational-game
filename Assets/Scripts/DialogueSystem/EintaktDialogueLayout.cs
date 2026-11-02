using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Scene-local presentation adapter. Only the Eintakt scene owns this component.
[DefaultExecutionOrder(-500)]
public sealed class EintaktDialogueLayout : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private RectTransform answers;
    [SerializeField] private Image background;
    [SerializeField] private Image portrait;
    [SerializeField] private Button previous, next, skip;
    private RectTransform viewport, content, safeRoot;
    private ScrollRect scroll;
    private TMP_Text[] answerTexts;
    private LayoutElement[] answerSizes;
    private Rect lastSafeArea;
    private Vector2 lastScreen;
    private bool resetScroll;
    private Coroutine reflow;

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
    private RectTransform Create(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private void Awake()
    {
        safeRoot = Create("Eintakt Dialogue Safe Area", transform);
        background.transform.SetParent(safeRoot, false);
        Stretch(background.rectTransform, new Vector2(.02f,.03f), new Vector2(.98f,.97f));
        portrait.transform.SetParent(safeRoot, false); portrait.preserveAspect = true;

        var scrollRoot = Create("Dialogue Scroll", safeRoot);
        Stretch(scrollRoot, Vector2.zero, Vector2.one);
        scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60;
        viewport = Create("Viewport", scrollRoot);
        var hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Create("Text and Answers", viewport);
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f,1); content.sizeDelta = Vector2.zero;
        var flow = content.gameObject.AddComponent<VerticalLayoutGroup>();
        flow.spacing = 24; flow.padding = new RectOffset(12,24,12,20);
        flow.childControlWidth = flow.childControlHeight = true;
        flow.childForceExpandWidth = true; flow.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content;

        dialogueText.transform.SetParent(content, false);
        dialogueText.rectTransform.localScale = Vector3.one;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.overflowMode = TextOverflowModes.Overflow;
        dialogueText.enableAutoSizing = false;
        dialogueText.raycastTarget = false;
        // The original click trigger consumes drag events, preventing ScrollRect dragging.
        var trigger = dialogueText.GetComponent<EventTrigger>(); if (trigger != null) trigger.enabled = false;

        answers.SetParent(content, false); answers.localScale = Vector3.one;
        var holderButton = answers.GetComponent<Button>(); if (holderButton != null) holderButton.enabled = false;
        var answerFlow = answers.gameObject.AddComponent<VerticalLayoutGroup>();
        answerFlow.spacing = 14; answerFlow.childControlWidth = answerFlow.childControlHeight = true;
        answerFlow.childForceExpandWidth = true; answerFlow.childForceExpandHeight = false;
        var buttons = answers.GetComponentsInChildren<Button>(true);
        var list = new System.Collections.Generic.List<TMP_Text>();
        var sizes = new System.Collections.Generic.List<LayoutElement>();
        foreach (var button in buttons) {
            if (button.transform == answers) continue;
            button.transform.localScale = Vector3.one;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.rectTransform.offsetMin = new Vector2(24,10); label.rectTransform.offsetMax = new Vector2(-24,-10);
            label.enableAutoSizing = false; label.overflowMode = TextOverflowModes.Overflow;
            sizes.Add(button.gameObject.AddComponent<LayoutElement>()); list.Add(label);
        }
        answerTexts = list.ToArray(); answerSizes = sizes.ToArray();
        SetupNavigation(previous, .05f, .32f);
        SetupNavigation(skip, .365f, .635f);
        SetupNavigation(next, .68f, .95f);
        AddScrollbar(scrollRoot);
        ApplySafeArea();
        dialogueManager.OnNodeShown += NodeShown;
        Canvas.willRenderCanvases += BeforeRender;
    }
    private void SetupNavigation(Button button, float left, float right)
    {
        button.transform.SetParent(safeRoot, false);
        Stretch((RectTransform)button.transform, new Vector2(left,.055f), new Vector2(right,.13f));
        var label = button.GetComponentInChildren<TMP_Text>(true);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);
        label.rectTransform.offsetMin = new Vector2(12,8); label.rectTransform.offsetMax = new Vector2(-12,-8);
        label.enableAutoSizing = true; label.fontSizeMin = 20; label.fontSizeMax = label.fontSize;
        label.overflowMode = TextOverflowModes.Masking;
    }
    private void AddScrollbar(RectTransform parent)
    {
        var bar = Create("Dialogue Scrollbar", parent);
        var image = bar.gameObject.AddComponent<Image>(); image.color = new Color(.25f,.13f,.07f,.8f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
        var handle = Create("Handle",bar); Stretch(handle, Vector2.zero,Vector2.one);
        var graphic = handle.gameObject.AddComponent<Image>(); graphic.color = new Color(1,.65f,.2f);
        scrollbar.handleRect = handle; scrollbar.targetGraphic = graphic;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }
    private void ApplySafeArea()
    {
        lastScreen = new Vector2(Screen.width, Screen.height); lastSafeArea = Screen.safeArea;
        Stretch(safeRoot, lastSafeArea.min / lastScreen, lastSafeArea.max / lastScreen);
        bool portraitScreen = Screen.width < Screen.height;
        Stretch(viewport, new Vector2(.05f,.18f), new Vector2(portraitScreen ? .91f : .76f,portraitScreen ? .79f : .80f));
        Stretch(portrait.rectTransform, new Vector2(portraitScreen ? .72f : .79f,portraitScreen ? .81f : .57f),new Vector2(.95f,.93f));
        var bar = (RectTransform)scroll.verticalScrollbar.transform;
        Stretch(bar, new Vector2(portraitScreen ? .925f : .765f,.18f),new Vector2(portraitScreen ? .95f : .78f,portraitScreen ? .79f : .80f));
        dialogueText.fontSize = portraitScreen ? 36 : 48;
        foreach (var label in answerTexts) label.fontSize = portraitScreen ? 30 : 32;
        resetScroll = true;
        QueueReflow();
    }
    private void NodeShown(int index) { resetScroll = true; QueueReflow(); }
    private void QueueReflow()
    {
        if (reflow != null) StopCoroutine(reflow);
        reflow = StartCoroutine(ReflowText());
    }
    private IEnumerator ReflowText()
    {
        // The existing typewriter caches vertices immediately. Rebuild it only after
        // the flow layout has sized the new text, so its cached vertices use that rect.
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        var graph = dialogueManager.ActiveGraph;
        int index = dialogueManager.CurrentNodeIndex;
        if (graph != null && index >= 0 && index < graph.nodes.Count) dialogueUI.UpdateVisuals(graph.nodes[index]);
        resetScroll = true; reflow = null;
    }
    private void LateUpdate()
    {
        if (lastScreen != new Vector2(Screen.width, Screen.height) || lastSafeArea != Screen.safeArea) ApplySafeArea();
        float width = Mathf.Max(1, viewport.rect.width - 36 - 48);
        for (int i=0;i<answerTexts.Length;i++) {
            float height = Mathf.Max(70,answerTexts[i].GetPreferredValues(answerTexts[i].text,width,0).y+24);
            if (Mathf.Abs(answerSizes[i].preferredHeight-height)>.5f) answerSizes[i].preferredHeight = height;
        }
    }
    private void BeforeRender()
    {
        if (!resetScroll || !isActiveAndEnabled) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; resetScroll = false;
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!eventData.dragging && RectTransformUtility.RectangleContainsScreenPoint(viewport,eventData.position,eventData.pressEventCamera)) dialogueUI.HandleUserQuoteSkip();
    }
    private void OnDestroy()
    {
        if (dialogueManager != null) dialogueManager.OnNodeShown -= NodeShown;
        Canvas.willRenderCanvases -= BeforeRender;
    }
}
