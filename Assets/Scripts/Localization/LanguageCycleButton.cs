using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

public class LanguageCycleButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
        LocalizationSettings.SelectedLocaleChanged += UpdateLabel;
        UpdateLabel(LocalizationSettings.SelectedLocale);
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= UpdateLabel;
    }

    private void OnClick()
    {
        LanguageManager.Instance.CycleLocale();
    }

    private void UpdateLabel(UnityEngine.Localization.Locale locale)
    {
        if (label != null)
            label.text = locale.Identifier.Code.ToUpper(); // "EN", "DE"
    }
}