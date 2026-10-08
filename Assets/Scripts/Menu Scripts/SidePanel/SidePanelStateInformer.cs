using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Serialization;

public enum StateName { 
    FETCH = 0,
    DECODE = 1,
    MEM_ADDRESS = 2,
    MEM_READ = 3,
    MEM_WB = 4,
    MEM_WRITE = 5,
    EXECUTE_R = 6,
    ALU_WB = 7,
    EXECUTE_I = 8,
    JAL = 9,
    BEQ = 10,
    UNKNOWN = 11,
}

public class SidePanelStateInformer : MonoBehaviour
{
    [Header("State Info")]
    [SerializeField] private List<ProcessStateInfo> stateList;

    [FormerlySerializedAs("_titelText")]
    [Header("UI elements")]
    [SerializeField] private TMP_Text titelText;
    [FormerlySerializedAs("_stateDescription")] [SerializeField] private TMP_Text stateDescription;
    [FormerlySerializedAs("_stateSignals")] [SerializeField] private TMP_Text stateSignals;
    [FormerlySerializedAs("_signalsGroup")] [SerializeField] private CanvasGroup signalsGroup;
    [FormerlySerializedAs("_buttonContainer")] [SerializeField] private CanvasGroup buttonContainer;

    [FormerlySerializedAs("_fadeDuration")] [SerializeField] private float fadeDuration = 0.2f;

    public void SetStateInfo(StateName state) {
        var s = stateList[(int)state];

        titelText.text = s.titel;
        stateDescription.text = s.stateInfo.GetLocalizedString();

        if (s.doesHaveChoice) {
            SwitchUI(buttonContainer, signalsGroup);
        }
        else {
            stateSignals.text = s.stateSignals;
            SwitchUI(signalsGroup, buttonContainer);
        }
    }

    private void SwitchUI(CanvasGroup toShow, CanvasGroup toHide)
    {
        toHide.DOFade(0, fadeDuration);
        toHide.blocksRaycasts = false;
        toHide.interactable = false;

        toShow.DOFade(1, fadeDuration);
        toShow.blocksRaycasts = true;
        toShow.interactable = true;
    }
}
