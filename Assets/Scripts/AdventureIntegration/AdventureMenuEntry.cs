using UnityEngine;
using UnityEngine.UI;
using RiscVAdventure.Adventure;

public sealed class AdventureMenuEntry : MonoBehaviour
{
    public AdventureSliceConfiguration configuration;
    public Button styleTemplate;
    private void Start()
    {
        var button = GetComponent<Button>(); button.interactable = true;
        var banner = transform.Find("ComingSoonBanner"); if (banner != null) banner.gameObject.SetActive(false);
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => AdventureDirector.Open(configuration, styleTemplate));
    }
}
