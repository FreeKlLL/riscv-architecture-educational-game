using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RiscVAdventure.Adventure;

public interface IAdventureLevelPort
{
    bool AdventureBusy { get; }
    void SetAdventureInput(bool enabled);
}

public sealed class AdventureLevelAdapter : MonoBehaviour
{
    public static AdventureLevelAdapter Current { get; private set; }
    private IAdventureLevelPort port;
    private DialogueManager dialogue;
    private DialogueGraph lesson;
    private bool ready;
    private int resumeNode;
    private readonly System.Collections.Generic.List<Button> lessonHints = new System.Collections.Generic.List<Button>();
    public static bool Owns(IAdventureLevelPort candidate) => Current != null && Current.port == candidate && AdventureDirector.IsInAdventure;
    public static bool Blocks(IAdventureLevelPort candidate) => Owns(candidate) && (!Current.ready || AdventureDirector.Instance.Profile.Journey.Stage != AdventureStage.Practice);
    public void Initialize(IAdventureLevelPort level, DialogueManager manager)
    {
        Current = this; port = level; dialogue = manager; lesson = manager.ActiveGraph;
        resumeNode = AdventureDirector.Instance.Profile.Journey.LessonNode;
        dialogue.OnDialogueEnd += Ended;
        dialogue.OnNodeShown += Shown;
        dialogue.OnHintEnabled += Hint;
        StartCoroutine(Prepare());
    }
    private IEnumerator Prepare()
    {
        yield return null;
        var director = AdventureDirector.Instance;
        bool teaching = director.Profile.Journey.Stage == AdventureStage.Lesson;
        // Skip is navigation, not evidence of completing the lesson.
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.name.IndexOf("skip", StringComparison.OrdinalIgnoreCase) >= 0) b.gameObject.SetActive(false);
            else if (teaching && b.name.IndexOf("hint", StringComparison.OrdinalIgnoreCase) >= 0 && b.gameObject.activeSelf) { lessonHints.Add(b); b.gameObject.SetActive(false); }
        port.SetAdventureInput(!teaching);
        if (teaching) dialogue.ResumeAtNode(resumeNode); else dialogue.SkipDialogue();
        ready = true;
    }
    private void Shown(int node)
    {
        if (ready && AdventureDirector.Instance.Profile.Journey.Stage == AdventureStage.Lesson && dialogue.ActiveGraph == lesson)
            AdventureDirector.Instance.Engine.SaveLessonNode(AdventureDirector.Instance.Profile.Id, node);
    }
    private void Ended()
    {
        var d = AdventureDirector.Instance;
        if (d.Profile.Journey.Stage != AdventureStage.Lesson || dialogue.ActiveGraph != lesson || !dialogue.FinishedNaturally) return;
        d.Engine.CompleteLesson(d.Profile.Id);
        foreach (var hint in lessonHints) if (hint != null) hint.gameObject.SetActive(true);
        port.SetAdventureInput(true);
        d.ShowGameHud();
    }
    private void Hint() => AdventureDirector.Instance.Engine.Hint(AdventureDirector.Instance.Profile.Id);
    public void Report(bool success)
    {
        var d = AdventureDirector.Instance; var j = d.Profile.Journey;
        if (d.Engine.Submit(d.Profile.Id, j.AssignedExerciseId, j.AttemptId, Guid.NewGuid().ToString("N"), success) && success) {
            port.SetAdventureInput(false); d.Route();
        } else d.ShowGameHud();
    }
    private void OnDestroy()
    {
        if (dialogue != null) { dialogue.OnDialogueEnd -= Ended; dialogue.OnNodeShown -= Shown; dialogue.OnHintEnabled -= Hint; }
        if (Current == this) Current = null;
    }
}
