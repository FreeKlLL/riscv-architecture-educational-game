using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSFX : MonoBehaviour, IPointerEnterHandler
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (SfxManager.Instance != null) SfxManager.Instance.PlayButtonClick();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SfxManager.Instance != null) SfxManager.Instance.PlayButtonHover();
    }
}