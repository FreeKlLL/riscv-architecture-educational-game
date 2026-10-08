using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
///     Handles page-based navigation for a ScrollRect.
///     Supports smooth snapping to pages and manual button-based navigation.
/// </summary>
public class LevelPanelScroller : MonoBehaviour
{
    [Header("References")] public ScrollRect scrollRect;

    [FormerlySerializedAs("_contentContainer")] [SerializeField]
    private RectTransform contentContainer;

    [Header("UI Text Settings")] public TMP_Text chapterTitleText;

    public string[] chapterNames;
    public float fadeDuration = 0.3f;

    [Header("Animation Settings")] public int totalPages = 2;

    public float duration = 0.5f;
    public Ease animEase = Ease.OutCubic;

    [Header("Bounce on last page")] [SerializeField]
    private float edgeBounceStrength = 18f;

    [SerializeField] private float edgeBounceDuration = 0.4f;

    [Header("Inertia Tilt")]
    [SerializeField] private InertiaMode inertiaMode = InertiaMode.Wave;
    [SerializeField] private float tiltAngle = 5f;
    [SerializeField] private float waveStagger = 0.028f;

    [Range(0.3f, 0.9f)] [SerializeField] private float settleStartAt = 0.72f;

    [SerializeField] private float settleDuration = 0.42f;

    private int _currentPage;


    private void Awake()
    {
        // Optional: Auto-calculate pages based on children count
        if (contentContainer != null && contentContainer.childCount > 0) totalPages = contentContainer.childCount;

        if (chapterTitleText != null && chapterNames.Length > 0) chapterTitleText.text = chapterNames[0];
    }

    /// <summary>
    ///     Navigates to the next page if available.
    /// </summary>
    public void NextPage()
    {
        if (_currentPage >= totalPages - 1)
        {
            PlayEdgeBounce(true);
            return;
        }

        _currentPage++;
        ScrollToPage(_currentPage);
        UpdateChapterTitle(_currentPage);

        StartInertiaTilt(_currentPage, -1f);
    }

    /// <summary>
    ///     Navigates to the previous page if available.
    /// </summary>
    public void PreviousPage()
    {
        if (_currentPage <= 0)
        {
            PlayEdgeBounce(true);
            return;
        }

        _currentPage--;
        ScrollToPage(_currentPage);
        UpdateChapterTitle(_currentPage);

        StartInertiaTilt(_currentPage, +1f);
    }

    /// <summary>
    ///     Snaps the ScrollRect to a specific page index.
    /// </summary>
    private void ScrollToPage(int pageIndex)
    {
        var targetPos = totalPages > 1 ? (float)pageIndex / (totalPages - 1) : 0;

        scrollRect.DOKill();
        contentContainer.DOKill();
        
        scrollRect.DOHorizontalNormalizedPos(targetPos, duration)
            .SetEase(animEase)
            .SetUpdate(true);
    }

    private void UpdateChapterTitle(int pageIndex)
    {
        if (chapterTitleText == null || chapterNames.Length <= pageIndex) return;

        var titleSequence = DOTween.Sequence();

        titleSequence.Append(chapterTitleText.DOFade(0, fadeDuration))
            .AppendCallback(() => { chapterTitleText.text = chapterNames[pageIndex]; })
            .Append(chapterTitleText.DOFade(1, fadeDuration));
    }

    private enum InertiaMode
    {
        WholeContainer,
        Wave
    }

    # region Optional animation for better effect

    private void StartInertiaTilt(int pageIndex, float tiltDirection)
    {
        if (pageIndex < 0 || pageIndex >= contentContainer.childCount) return;

        var page = contentContainer.GetChild(pageIndex);
        var tiltVec = new Vector3(0f, 0f, tiltAngle * tiltDirection);
        var settleAt = duration * settleStartAt;

        if (inertiaMode == InertiaMode.WholeContainer)
        {
            var rt = page.GetComponent<RectTransform>();
            rt.DOKill();
            rt.localEulerAngles = tiltVec;
            ScheduleSettle(rt, tiltVec, settleAt);
        }
        else // Wave
        {
            var leftToRight = tiltDirection < 0f;
            var count = page.childCount;

            for (var i = 0; i < count; i++)
            {
                var waveIdx = leftToRight ? i : count - 1 - i;
                var child = page.GetChild(waveIdx).GetComponent<RectTransform>();
                if (child == null) continue;

                child.DOKill();
                child.localEulerAngles = tiltVec;
                ScheduleSettle(child, tiltVec, settleAt + i * waveStagger);
            }
        }
    }

    private void ScheduleSettle(RectTransform rt, Vector3 tiltVec, float delay)
    {
        var peakVec = tiltVec * 1.4f;
        var peakDur = settleDuration * 0.28f;
        var returnDur = settleDuration * 0.72f;

        DOTween.Sequence()
            .SetTarget(rt)
            .SetDelay(delay)
            .Append(rt.DOLocalRotate(peakVec, peakDur).SetEase(Ease.OutCubic))
            .Append(rt.DOLocalRotate(Vector3.zero, returnDur).SetEase(Ease.OutCubic));
    }

    private void PlayEdgeBounce(bool isRight)
    {
        if (DOTween.IsTweening(scrollRect) || DOTween.IsTweening(contentContainer))
        {
            return;
        }
        var dir = isRight ? Vector2.left : Vector2.right;
        contentContainer.DOPunchAnchorPos(dir * edgeBounceStrength, edgeBounceDuration, 8, 0f);
    }

    #endregion
}