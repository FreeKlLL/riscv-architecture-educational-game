var domain=System.AppDomain.CurrentDomain;var report=new System.Text.StringBuilder();domain.SetData("EintaktGoalAudit","RUNNING");
void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
UnityEngine.UI.Button Button(string n){foreach(var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsInactive.Exclude))if(b.name==n)return b;throw new System.Exception("Missing "+n);}
void Click(UnityEngine.UI.Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left},UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);}
System.Collections.IEnumerator Run(){
 for(int task=0;task<6;task++){
  UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale("en");
  Click(Button("PlayButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);
  for(int p=0;p<6;p++){Click(Button("SrollRight"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);}
  Click(Button("Level "+(20+task)));yield return new UnityEngine.WaitForSecondsRealtime(2f);
  foreach(var lang in new[]{"en","de"}){
   if(lang=="de"){UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(lang);UnityEngine.Object.FindAnyObjectByType<LevelManager>().RestartLevel();yield return new UnityEngine.WaitForSecondsRealtime(2f);}
   Check(OneTickRegisseur.Initial.name=="Level 11-"+task,"Wrong initial");
   Click(Button("SkipDialogueButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);
   var ctrl=UnityEngine.Object.FindAnyObjectByType<OneTickRegisseur>();var f=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
   var txt=(TMPro.TMP_Text)typeof(BaseLevelRegisseur<OneTickProcessorLevelState,OneTickBusSegments>).GetField("levelTargetText",f).GetValue(ctrl);
   Check(txt.text==OneTickRegisseur.Initial.levelTarget.GetLocalizedString(),"Goal text mismatch");
   txt.ForceMeshUpdate();Check(!txt.isTextOverflowing,"Goal overflow "+task+" "+lang);
   Check((int)typeof(BaseLevelRegisseur<OneTickProcessorLevelState,OneTickBusSegments>).GetField("TickCounter",f).GetValue(ctrl)==0,"Unexpected tick");
   report.AppendLine("11-"+task+" "+lang+": final objective displayed without overflow; tick=0; font="+txt.fontSize+"; text="+txt.text);
   domain.SetData("EintaktGoalAudit","RUNNING\n"+report);
  }
  if(task<5){UnityEngine.Object.FindAnyObjectByType<LevelManager>().LoadMainMenu();yield return new UnityEngine.WaitForSecondsRealtime(2f);}
 }
 domain.SetData("EintaktGoalAudit","PASS\n"+report);
}
System.Collections.IEnumerator Guard(){var e=Run();while(true){bool more;object next=null;try{more=e.MoveNext();if(more)next=e.Current;}catch(System.Exception ex){domain.SetData("EintaktGoalAudit","FAIL "+ex+"\n"+report);yield break;}if(!more)yield break;yield return next;}}
LanguageManager.Instance.StartCoroutine(Guard());return "Final quest layout audit started (12 loads, no processor ticks).";
