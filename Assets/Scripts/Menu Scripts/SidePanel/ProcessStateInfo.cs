using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "ProcessStateInfo", menuName = "Scriptable Objects/ProcessStateInfo")]
public class ProcessStateInfo : ScriptableObject
{
    public string titel;
    
    public LocalizedString stateInfo;

    [TextArea(3, 20)]
    public string stateSignals;

    public bool doesHaveChoice;
}
