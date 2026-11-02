using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RiscVAdventure.Adventure;

// UI and scene orchestration only. Decisions and scoring live in the testable Regisseur.
public sealed class AdventureDirector : MonoBehaviour
{
    public static AdventureDirector Instance { get; private set; }
    public static bool IsInAdventure => Instance != null && Instance.session;
    public AdventureProfileService Profiles { get; private set; }
    public AdventureRegisseur Engine { get; private set; }
    public AdventureProfile Profile => Profiles.ActiveProfile;
    private AdventureSliceConfiguration config;
    private Button prototype;
    private TMP_FontAsset font;
    private GameObject canvasRoot, panel;
    private TMP_InputField nameInput;
    private bool session, loading, profilesAfterLoad;
    private string screen = "profiles", message;
    private bool German => LocalizationSettings.SelectedLocale != null && LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("de");
    private string L(string en, string de) => German ? de : en;
    private string StageName(AdventureStage stage)
    {
        switch(stage) {
            case AdventureStage.NotStarted:return L("Ready to begin","Bereit zum Start");
            case AdventureStage.Diagnostic:return L("Diagnosis","Diagnose");
            case AdventureStage.Lesson:return L("Lesson with Charlie","Lernen mit Charlie");
            case AdventureStage.Practice:return L("Practice","Übung");
            case AdventureStage.Result:return L("Task result","Aufgabenergebnis");
            default:return L("Next chapter coming later","Nächstes Kapitel folgt später");
        }
    }
    private string ExerciseName(string id)
    {
        if(id=="registers.transfer")return L("Registers 1 · Transfer 4","Register 1 · Übertrage 4");
        if(id=="registers.hold")return L("Registers 2 · Preserve 42","Register 2 · Bewahre 42");
        if(id=="registers.negative")return L("Registers 3 · Transfer -12","Register 3 · Übertrage -12");
        return L("Chapter diagnosis","Kapiteldiagnose");
    }
    public static void Open(AdventureSliceConfiguration configuration, Button template)
    {
        if (Instance == null) {
            var go = new GameObject("Adventure Runtime UI"); DontDestroyOnLoad(go);
            Instance = go.AddComponent<AdventureDirector>(); Instance.Initialize(configuration, template);
        }
        Instance.ShowProfiles();
    }
    private void Initialize(AdventureSliceConfiguration configuration, Button template)
    {
        config = configuration;
        Profiles = AdventureRuntime.Profiles;
        if (Profiles == null) throw new InvalidOperationException(AdventureRuntime.StartupError);
        Engine = new AdventureRegisseur(Profiles, config);
        font = template.GetComponentInChildren<TMP_Text>(true).font;
        prototype = Instantiate(template, transform); prototype.name = "Adventure Button Template";
        StripLocalization(prototype);
        prototype.onClick = new Button.ButtonClickedEvent(); prototype.gameObject.SetActive(false);
        canvasRoot = new GameObject("Adventure Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10000;
        var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        // Keep the complete design area visible, including on wide/short Game Views.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        SceneManager.sceneLoaded += Loaded;
        LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
    }
    // Explicit injection for isolated automated/runtime tests. Never changes the production save root.
    public void UseProfilesForTesting(AdventureProfileService isolated)
    {
        if (session) throw new InvalidOperationException("Exit Adventure before replacing the test service.");
        Profiles = isolated ?? throw new ArgumentNullException(nameof(isolated)); Engine = new AdventureRegisseur(Profiles, config); ShowProfiles();
    }
    private void LocaleChanged(UnityEngine.Localization.Locale locale) { if (!loading && canvasRoot != null && canvasRoot.activeSelf) Redraw(); }
    private void Redraw()
    {
        if (screen == "profiles") ShowProfiles(); else if (screen == "diagnostic") ShowDiagnostic();
        else if (screen == "result") ShowResult(); else if (screen == "unavailable") ShowUnavailable(); else ShowGameHud();
    }
    private RectTransform Rect(GameObject go, Transform parent, float x, float y, float w, float h)
    {
        go.transform.SetParent(parent, false); var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f); r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,h); return r;
    }
    private void Clear(bool full, string title)
    {
        if (panel != null) { panel.SetActive(false); Destroy(panel); }
        canvasRoot.SetActive(true);
        panel = new GameObject("Adventure " + screen, typeof(RectTransform)); Rect(panel, canvasRoot.transform, 0,0,1280,720);
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one; panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        if (full) {
            var bg = panel.AddComponent<Image>(); bg.color = new Color(.035f,.055f,.12f,1); }
        if (!string.IsNullOrEmpty(title)) {
            var heading = Text("Title", title, 0,0,1180,55,34);
            TopInset(heading.rectTransform, 0, 55);
        }
    }
    private void TopInset(RectTransform rect, float x, float height)
    {
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        float safeTop = (Screen.height - Screen.safeArea.yMax) / Mathf.Max(scale, .001f);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(x, -safeTop - 32 - height * .5f);
    }
    private TMP_Text Text(string id, string value, float x, float y, float w, float h, float size = 23, Transform parent = null)
    {
        var go = new GameObject(id, typeof(RectTransform), typeof(TextMeshProUGUI)); Rect(go,parent ?? panel.transform,x,y,w,h);
        var t = go.GetComponent<TMP_Text>(); t.font = font; t.fontSize = size; t.color = Color.white; t.text = value; t.alignment = TextAlignmentOptions.Midline; t.raycastTarget = false; return t;
    }
    private Button Button(string id, string label, float x, float y, float w, UnityAction action, Transform parent = null)
    {
        var b = Instantiate(prototype, parent ?? panel.transform); b.name = id;
        StripLocalization(b);
        var r = (RectTransform)b.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f); r.anchoredPosition = new Vector2(x,y); r.sizeDelta = new Vector2(w,48); r.localScale = Vector3.one;
        foreach (var child in b.GetComponentsInChildren<TMP_Text>(true)) {
            var tr = child.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(35,3); tr.offsetMax = new Vector2(-35,-3); tr.localScale = Vector3.one;
            child.text = label; child.fontSize = 21; child.enableAutoSizing = true; child.fontSizeMin = 16; child.fontSizeMax = 23;
        }
        b.onClick = new Button.ButtonClickedEvent(); b.onClick.AddListener(() => Safe(action)); b.interactable = true; b.gameObject.SetActive(true); return b;
    }
    private void StripLocalization(Button button)
    {
        foreach (var localizer in button.GetComponentsInChildren<LocalizeStringEvent>(true)) { localizer.enabled = false; Destroy(localizer); }
        foreach (var localizer in button.GetComponentsInChildren<UnityEngine.Localization.PropertyVariants.GameObjectLocalizer>(true)) { localizer.enabled = false; Destroy(localizer); }
    }
    private void Safe(UnityAction action)
    {
        try { message = null; action(); }
        catch (Exception ex) { message = ex.Message; ShowProfiles(); Debug.LogException(ex); }
    }
    private void Input()
    {
        var go = new GameObject("Profile Name", typeof(RectTransform), typeof(Image), typeof(TMP_InputField)); Rect(go,panel.transform,-270,-145,550,50);
        go.GetComponent<Image>().color = new Color(.12f,.19f,.29f);
        nameInput = go.GetComponent<TMP_InputField>();
        var text = Text("Name Text", "",0,0,510,45,24,go.transform); text.alignment = TextAlignmentOptions.MidlineLeft;
        nameInput.textViewport = (RectTransform)go.transform; nameInput.textComponent = (TMP_Text)text; nameInput.characterLimit = 40;
        var placeholder = Text("Name Placeholder",L("Player name","Spielername"),0,0,510,45,24,go.transform);
        placeholder.color = new Color(.65f,.7f,.8f); nameInput.placeholder = placeholder;
        nameInput.text = Profile?.DisplayName ?? "";
    }
    public void ShowProfiles()
    {
        screen = "profiles"; Clear(true, L("Adventure · Local profiles", "Adventure · Lokale Profile"));
        // Scroll view keeps every local profile reachable, even with many players.
        var viewport = new GameObject("Profiles",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect)); Rect(viewport,panel.transform,-330,85,580,370);
        viewport.GetComponent<Image>().color = new Color(.06f,.09f,.17f); viewport.GetComponent<Mask>().showMaskGraphic = true;
        var content = new GameObject("Content",typeof(RectTransform)); var cr = Rect(content,viewport.transform,0,0,570,Math.Max(360,Profiles.Profiles.Count*58)); cr.anchorMin=cr.anchorMax=cr.pivot=new Vector2(.5f,1); cr.anchoredPosition=Vector2.zero;
        var scroll=viewport.GetComponent<ScrollRect>(); scroll.viewport=(RectTransform)viewport.transform; scroll.content=cr; scroll.horizontal=false;
        int i=0; foreach(var p in Profiles.Profiles) { var selected=p.Id; Button("Select "+p.Id,(Profile?.Id==p.Id?"> ":"")+p.DisplayName,0,-30-i++*58,530,()=>{Profiles.SelectProfile(selected);ShowProfiles();},content.transform); }
        if (Profile != null) {
            string[] names = {L("Registers","Register"),L("Multiplexer","Multiplexer"),"ALU",L("Memory","Speicher"),L("Extender","Erweiterungseinheit"),"Register File","Eintaktprozessor","Mehrtaktprozessor"};
            int row=0; foreach(var t in Profile.Topics) {
                var status=t.IsAssessed || t.IsLessonCompleted ? $"{t.CurrentKnowledge:0.#}% · "+L("Mastery ","Bestätigt ")+$"{t.Mastery:0.#}%" : L("Not assessed","Nicht diagnostiziert");
                Text("Knowledge "+t.TopicId,names[row]+": "+status+(t.IsTopicCompleted?L(" [done]"," [fertig]"):""),320,235-row*43,600,42,20); row++;
            }
            Text("Position",StageName(Profile.Journey.Stage)+" · "+ExerciseName(Profile.Journey.AssignedExerciseId),300,-115,610,52,19);
        }
        Input();
        Button("Create Profile",L("Create profile","Profil erstellen"),-430,-210,280,()=>{var p=Profiles.CreateProfile(nameInput.text);Profiles.SelectProfile(p.Id);ShowProfiles();});
        Button("Rename Profile",L("Rename","Umbenennen"),-135,-210,250,()=>{if(Profile!=null)Profiles.RenameProfile(Profile.Id,nameInput.text);ShowProfiles();});
        Button("Delete Profile",L("Delete…","Löschen…"),-430,-275,280,ConfirmDelete);
        Button("Begin Adventure",L("Start / Continue","Starten / Fortsetzen"),300,-215,480,()=>{if(Profile==null)return;Profiles.SelectProfile(Profile.Id);Engine.Begin(Profile.Id);session=true;Route();});
        Button("Back Main Menu",L("Main menu","Hauptmenü"),300,-280,480,Exit);
        if(message!=null)Text("Save Error",message,0,-335,1200,40,18);
        if(Profiles.LoadIssues.Count>0)Text("Load Issues",L("Some saves could not be loaded. Healthy profiles remain available.","Einige Speicherstände konnten nicht geladen werden. Andere Profile bleiben verfügbar."),0,277,1180,35,18);
    }
    private void ConfirmDelete()
    {
        if(Profile==null)return; var id=Profile.Id;var name=Profile.DisplayName;
        Clear(true,L("Delete profile?","Profil löschen?")); Text("Confirmation",name+"\n"+L("This permanently removes this Adventure profile.","Dieses Adventure-Profil wird endgültig gelöscht."),0,90,1000,150,28);
        Button("Confirm Delete",L("Delete permanently","Endgültig löschen"),-220,-80,390,()=>{Profiles.DeleteProfile(id,true);ShowProfiles();});
        Button("Cancel Delete",L("Cancel","Abbrechen"),220,-80,390,ShowProfiles);
    }
    public void Route()
    {
        var j=Profile.Journey;
        if(j.Stage==AdventureStage.Diagnostic)ShowDiagnostic();
        else if(j.Stage==AdventureStage.Result)ShowResult();
        else if(j.Stage==AdventureStage.Unavailable)ShowUnavailable();
        else if(j.Stage==AdventureStage.Lesson||j.Stage==AdventureStage.Practice) {
            var exercise=Engine.Exercise(j.Stage==AdventureStage.Lesson?config.exercises[0].id:j.AssignedExerciseId);
            if(!loading)StartCoroutine(Load(exercise.scenePath));
        }
    }
    private void ShowDiagnostic()
    {
        screen="diagnostic";Clear(true,L("Charlie · Registers diagnosis","Charlie · Register-Diagnose"));
        var index=Profile.Journey.DiagnosticAnswers.Count;var q=config.questions[index];
        if(config.charlie!=null){var go=new GameObject("Charlie",typeof(RectTransform),typeof(Image));Rect(go,panel.transform,-470,90,240,300);go.GetComponent<Image>().sprite=config.charlie;go.GetComponent<Image>().preserveAspect=true;}
        Text("Question Counter",$"{index+1} / {config.questions.Length}",100,250,900,45);
        Text("Diagnostic Question",German?q.german:q.english,130,130,830,170,28);
        var answers=German?q.answersGerman:q.answersEnglish;
        for(int i=0;i<answers.Length;i++){int answer=i;Button("Answer "+i,answers[i],100,-20-i*70,950,()=>{Engine.Answer(Profile.Id,index,answer);Route();});}
        Button("Exit Diagnosis",L("Save and return","Speichern und zurück"),0,-295,500,Exit);
    }
    private void ShowResult()
    {
        screen="result";Clear(true,L("Task completed","Aufgabe geschafft"));var j=Profile.Journey;var t=Profile.GetTopic(TopicIds.Registers);
        Text("Result",ExerciseName(j.LastExerciseId)+"\n"+L("Knowledge reward: +","Wissenszuwachs: +")+$"{j.LastReward:0.#}\n"+L("Failed checks: ","Fehlversuche: ")+$"{j.LastErrors} · "+L("Hints: ","Hinweise: ")+$"{j.LastHints}\n"+L("Current knowledge: ","Aktuelles Wissen: ")+$"{t.CurrentKnowledge:0.#}% · "+L("Mastery: ","Bestätigt: ")+$"{t.Mastery:0.#}%\n"+L("Next: ","Als Nächstes: ")+(j.AssignedExerciseId!=null?ExerciseName(j.AssignedExerciseId):L("Multiplexer · not implemented yet","Multiplexer · noch nicht implementiert")),0,80,1100,280,27);
        Button("Continue Journey",L("Continue","Weiter"),-230,-160,400,ContinueJourney);
        Button("Result Main Menu",L("Save and return","Speichern und zurück"),230,-160,400,Exit);
    }
    public void ContinueJourney(){Engine.Continue(Profile.Id);Route();}
    private void ShowUnavailable()
    {
        screen="unavailable";Clear(true,L("Registers completed","Register abgeschlossen"));
        Text("Unavailable",L("Your progress is saved. The next chapter, Multiplexer, is not implemented in Adventure yet.","Dein Fortschritt ist gespeichert. Das nächste Kapitel Multiplexer ist in Adventure noch nicht implementiert."),0,70,1050,220,30);
        Button("Unavailable Main Menu",L("Main menu","Hauptmenü"),0,-140,500,Exit);
    }
    public void ShowGameHud()
    {
        screen="game";Clear(false,null);var t=Profile.GetTopic(TopicIds.Registers);
        var status = Text("Adventure Status",$"Adventure · {Profile.DisplayName} · {t.CurrentKnowledge:0.#}%",-240,0,750,40,18);
        TopInset(status.rectTransform, -240, 40);
        var exit = Button("Adventure Save Exit",L("Save & menu","Speichern & Menü"),470,0,300,Exit);
        TopInset((RectTransform)exit.transform, 470, 48);
    }
    private IEnumerator Load(string path)
    {
        loading=true;canvasRoot.SetActive(false);yield return SceneManager.LoadSceneAsync(path);loading=false;ShowGameHud();
    }
    private void Loaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.buildIndex==0){if(profilesAfterLoad){profilesAfterLoad=false;ShowProfiles();}return;}
        if(!session)return;
        var level=FindAnyObjectByType<LevelZeroRegisseur>();var manager=FindAnyObjectByType<LevelManager>();
        if(level!=null && manager!=null)level.gameObject.AddComponent<AdventureLevelAdapter>().Initialize(level,manager.Dialogue);
    }
    public void Exit()
    {
        session=false;screen="profiles";canvasRoot.SetActive(false);
        if(SceneManager.GetActiveScene().buildIndex!=0){profilesAfterLoad=true;StartCoroutine(LoadMenu());}
    }
    private IEnumerator LoadMenu(){loading=true;yield return SceneManager.LoadSceneAsync(0);loading=false;}
    private void OnDestroy()
    {
        SceneManager.sceneLoaded-=Loaded;LocalizationSettings.SelectedLocaleChanged-=LocaleChanged;
        if(Instance==this)Instance=null;
    }
}
