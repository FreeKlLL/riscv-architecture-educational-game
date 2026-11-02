var report=new System.Text.StringBuilder();
var domain=System.AppDomain.CurrentDomain;
domain.SetData("EintaktDialogueAudit","RUNNING");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
object Field(object o,string n)=>o.GetType().GetField(n,flags).GetValue(o);
void Require(bool condition,string message){if(!condition)throw new System.Exception(message);}
void Click(UnityEngine.UI.Button b){Require(b!=null&&b.IsActive()&&b.IsInteractable(),"UI button unavailable: "+(b==null?"NULL":b.name));UnityEngine.EventSystems.ExecuteEvents.Execute(b.gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left},UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);}
UnityEngine.UI.Button MenuButton(string name){foreach(var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsInactive.Exclude))if(b.name==name)return b;throw new System.Exception("Missing menu button "+name);}
void Validate(DialogueManager dm,int expected){
 var index=(int)Field(dm,"_currentNodeIndex");Require(index==expected,"Expected node "+expected+" got "+index);
 var n=(DialogueNode)Field(dm,"_currentNode");var ui=(DialogueUI)Field(dm,"ui");ui.SkipAnimationOfTyping();
 var text=(TMPro.TMP_Text)Field(ui,"textField");DialogueUtility.ProcessInputString(n.GetDialogueText(),out var clean);
 Require(!string.IsNullOrWhiteSpace(clean)&&text.text==clean,"Dialogue text mismatch "+expected);text.ForceMeshUpdate();if(text.isTextOverflowing)report.AppendLine("LAYOUT OVERFLOW "+OneTickRegisseur.Initial.name+" "+UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code+" node="+expected+" graph="+((DialogueGraph)Field(dm,"activeGraph")).name);
 var portrait=(UnityEngine.UI.Image)Field(ui,"characterImage");var emotions=(EmotionController)Field(ui,"currentEmotion");
 Require(portrait.sprite!=null&&portrait.sprite==emotions.emotions[(int)n.emotionIndex],"Charlie portrait mismatch "+expected);
 var answers=new[]{n.firstAnswer,n.secondAnswer,n.thirdAnswer};var names=new[]{"first","second","third"};
 for(int k=0;k<3;k++){var b=(UnityEngine.UI.Button)Field(ui,names[k]+"AnswerButton");if(!answers[k].IsEmpty){Require(b.gameObject.activeInHierarchy,"Missing answer button");Require(((TMPro.TMP_Text)Field(ui,names[k]+"AnswerText")).text==answers[k].GetLocalizedString(),"Answer text mismatch");}}
}
System.Collections.IEnumerator Run(){
 for(int task=0;task<6;task++){
  domain.SetData("EintaktDialogueAudit","RUNNING task 11-"+task+"\n"+report);
  UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale("en");
  Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MainMenu","Expected MainMenu");
  Click(MenuButton("PlayButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);
  for(int page=0;page<6;page++){Click(MenuButton("SrollRight"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);}
  Click(MenuButton("Level "+(20+task)));yield return new UnityEngine.WaitForSecondsRealtime(2f);
  Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path=="Assets/Scenes/Levels/Eintaktprozessor/Level 10.unity","Wrong scene");
  Require(OneTickRegisseur.Initial!=null&&OneTickRegisseur.Initial.name=="Level 11-"+task,"Wrong initial state");
  var ctrl=UnityEngine.Object.FindAnyObjectByType<OneTickRegisseur>();Require(ctrl!=null,"Missing OneTickRegisseur");
  int missing=0,broken=0;
  foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<UnityEngine.Component>(true)){
   if(c==null){missing++;continue;}
   var so=new UnityEditor.SerializedObject(c);var it=so.GetIterator();while(it.Next(true))if(it.propertyType==UnityEditor.SerializedPropertyType.ObjectReference&&it.objectReferenceValue==null&&!typeof(UnityEditor.SerializedProperty).GetProperty("objectReferenceEntityIdValue").GetValue(it).Equals(System.Activator.CreateInstance(typeof(UnityEditor.SerializedProperty).GetProperty("objectReferenceEntityIdValue").PropertyType)))broken++;
  }
  Require(missing==0&&broken==0,"Missing scripts/references "+missing+"/"+broken);
  var dm=UnityEngine.Object.FindAnyObjectByType<DialogueManager>();var ui=(DialogueUI)Field(dm,"ui");var graph=OneTickRegisseur.Initial.customDialogueGraph;
  foreach(var language in new[]{"en","de"}){
   UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale=UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(language);
   if(language=="de"){UnityEngine.Object.FindAnyObjectByType<LevelManager>().RestartLevel();yield return new UnityEngine.WaitForSecondsRealtime(2f);ctrl=UnityEngine.Object.FindAnyObjectByType<OneTickRegisseur>();dm=UnityEngine.Object.FindAnyObjectByType<DialogueManager>();ui=(DialogueUI)Field(dm,"ui");}
   yield return new UnityEngine.WaitForSecondsRealtime(.6f);Validate(dm,0);
   foreach(var step in new[]{"goNextQuoteButton:1","goNextQuoteButton:2","goNextQuoteButton:3","goPrevQuoteButton:2","goNextQuoteButton:3","secondAnswerButton:4","firstAnswerButton:3","thirdAnswerButton:5","secondAnswerButton:6","firstAnswerButton:3","thirdAnswerButton:5","thirdAnswerButton:3","thirdAnswerButton:5","firstAnswerButton:7"}){
    var parts=step.Split(':');Click((UnityEngine.UI.Button)Field(ui,parts[0]));yield return new UnityEngine.WaitForSecondsRealtime(.6f);Validate(dm,int.Parse(parts[1]));
   }
   Click((UnityEngine.UI.Button)Field(ui,"goNextQuoteButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);
   var objective=(TMPro.TMP_Text)typeof(BaseLevelRegisseur<OneTickProcessorLevelState,OneTickBusSegments>).GetField("levelTargetText",flags).GetValue(ctrl);
   var expected=OneTickRegisseur.Initial.levelTarget.GetLocalizedString();
   // The scene was loaded in the tested locale, as goal text is assigned once at level start.
   Require(objective.text==expected,"Initial objective text mismatch "+language);
   Require(!string.IsNullOrWhiteSpace(expected),"Empty localized objective");
   UnityEngine.UI.Button hintButton=null;
   foreach(var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsInactive.Exclude))for(int k=0;k<button.onClick.GetPersistentEventCount();k++)if(button.onClick.GetPersistentMethodName(k)=="SetupHintDialogue")hintButton=button;
   Require(hintButton!=null,"Missing in-game hint button");Click(hintButton);yield return new UnityEngine.WaitForSecondsRealtime(.6f);Validate(dm,0);
   // Visit every hint topic and its extra explanation through actual answer buttons.
   for(int topic=0;topic<6;topic++){
    int group=topic<3?1:topic==3?2:3;
    Click((UnityEngine.UI.Button)Field(ui,group==1?"firstAnswerButton":group==2?"secondAnswerButton":"thirdAnswerButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);Validate(dm,group);
    string pick=topic<3?new[]{"firstAnswerButton","secondAnswerButton","thirdAnswerButton"}[topic]:topic==3?"firstAnswerButton":topic==4?"firstAnswerButton":"secondAnswerButton";
    Click((UnityEngine.UI.Button)Field(ui,pick));yield return new UnityEngine.WaitForSecondsRealtime(.6f);Validate(dm,4+topic);
    Click((UnityEngine.UI.Button)Field(ui,"secondAnswerButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);Validate(dm,11+topic);
    Click((UnityEngine.UI.Button)Field(ui,"firstAnswerButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);Validate(dm,0);
   }
   Click((UnityEngine.UI.Button)Field(ui,"secondAnswerButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);
   Click((UnityEngine.UI.Button)Field(ui,"thirdAnswerButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);Validate(dm,10);
   Click((UnityEngine.UI.Button)Field(ui,"goNextQuoteButton"));yield return new UnityEngine.WaitForSecondsRealtime(.6f);
   report.AppendLine("11-"+task+" "+language+": tutorial nodes 0..7, answers/back/exit, hint nodes 0..16, Charlie, localized text PASS");
   domain.SetData("EintaktDialogueAudit","RUNNING\n"+report);
  }
  Require((int)typeof(BaseLevelRegisseur<OneTickProcessorLevelState,OneTickBusSegments>).GetField("TickCounter",flags).GetValue(ctrl)==0,"Unexpected processor tick");
  UnityEngine.Object.FindAnyObjectByType<LevelManager>().LoadMainMenu();yield return new UnityEngine.WaitForSecondsRealtime(2f);
 }
 domain.SetData("EintaktDialogueAudit","PASS\n"+report);
}
System.Collections.IEnumerator Guard(){var enumerator=Run();while(true){bool more;object next=null;try{more=enumerator.MoveNext();if(more)next=enumerator.Current;}catch(System.Exception ex){domain.SetData("EintaktDialogueAudit","FAIL "+ex+"\n"+report);yield break;}if(!more)yield break;yield return next;}}
LanguageManager.Instance.StartCoroutine(Guard());return "Runtime UI audit started: six tasks, en/de, no processor clocks or victory checks.";
