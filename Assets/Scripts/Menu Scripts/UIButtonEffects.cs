using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;


/// <summary>
/// Provides juicy visual feedback when hovering over UI buttons.
/// Uses DOTween for scaling and rotation effects.
/// </summary>
public class UIButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 5f;
    [SerializeField] private int vibrato = 10;

    [Header("Scale Settings")]
    [SerializeField] private float hoverScale = 1.05f;

    [Header("Anim Settings")]
    [SerializeField] private Ease onEnterEase = Ease.OutQuad;
    [SerializeField] private Ease onExitEase = Ease.OutQuad;
    [SerializeField] private float hoverDuration = 0.2f;
    
    [Header("Click - Squish animation (all platforms)")]
    [SerializeField] private float clickSquish   = 0.12f;
    [SerializeField] private float clickPressDur = 0.1f; // mobile
    [SerializeField] private float clickReleaseDur = 0.25f; // mobile
    
    private Vector3 _initialScale;
    private Vector3 _initialRotation;
    private RectTransform _rectTransform;
    
    private static bool HasCursor => !Application.isMobilePlatform;
    
    private bool _isHovered;
    private bool _visualHovered; // in which state is image on button
    private bool _busy; // if hover animation is active right now

    private void Awake()
    {
        // Cache RectTransform for better performance in UI operations
        _rectTransform = GetComponent<RectTransform>();

        _initialScale = _rectTransform.localScale;
        _initialRotation = _rectTransform.localEulerAngles;
    }

    /// <summary>
    /// Triggered when the pointer starts hovering over the button.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!HasCursor) return;
        _isHovered = true;
        RefreshHover();
    }

    /// <summary>
    /// Triggered when the pointer leaves the button.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!HasCursor) return;
        _isHovered = false;
        RefreshHover();
    }
    
    private void RefreshHover()
    {
        if (_busy || _isHovered == _visualHovered) return;

        _visualHovered = _isHovered;
        _busy = true;

        var seq = DOTween.Sequence().SetTarget(_rectTransform);

        if (_visualHovered)
        {
            seq.Join(_rectTransform.DOPunchRotation(new Vector3(0, 0, shakeStrength), shakeDuration, vibrato));
            seq.Join(_rectTransform.DOScale(_initialScale * hoverScale, hoverDuration).SetEase(onEnterEase));
        }
        else
        {
            seq.Join(_rectTransform.DOScale(_initialScale, hoverDuration).SetEase(onExitEase));
            seq.Join(_rectTransform.DOLocalRotate(_initialRotation, hoverDuration).SetEase(onExitEase));
        }

        seq.OnComplete(() =>
        {
            _busy = false;
            RefreshHover();
        });
    }
    
    # region On Click (all platforms)
    public void OnPointerDown(PointerEventData eventData)
    {
        _rectTransform.DOKill();
        // _rectTransform.DOPunchScale(Vector3.one * -clickSquish, clickDuration, 5, 0.5f);
        _rectTransform.DOScale(_initialScale * (1f - clickSquish), clickPressDur).SetEase(Ease.OutCubic);
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        // var target = (HasCursor && _isHovered) ? _initialScale * hoverScale : _initialScale;
        _busy = false;
 
        // _rectTransform.DOScale(target, hoverDuration * 0.5f).SetEase(onEnterEase);
        _rectTransform.DOScale(_initialScale, clickReleaseDur).SetEase(Ease.OutBack);
    }
    # endregion

    private void OnDisable()
    {
        // Kill animations if the object is disabled to prevent ghost tweens
        _rectTransform.DOKill();
        _busy = false;
        _isHovered = false;
        _visualHovered = false;

        _rectTransform.localScale = _initialScale;
        _rectTransform.localEulerAngles = _initialRotation;
    }
}