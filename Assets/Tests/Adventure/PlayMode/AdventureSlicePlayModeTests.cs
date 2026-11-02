using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RiscVAdventure.Adventure;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class AdventureSlicePlayModeTests
{
    private string root;
    private static Button Find(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).First(b=>b.name==name);
    private static void Click(string name)
    {
        var b=Find(name);Assert.That(b.IsInteractable(),Is.True,name);
        ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
    }
    private static IEnumerator Pause(){yield return new WaitForSecondsRealtime(.55f);}
    private static IEnumerator WaitScene(int index)
    {float end=Time.realtimeSinceStartup+20;while(SceneManager.GetActiveScene().buildIndex!=index&&Time.realtimeSinceStartup<end)yield return null;Assert.That(SceneManager.GetActiveScene().buildIndex,Is.EqualTo(index));yield return new WaitForSecondsRealtime(1.5f);}
    [UnityTest] public IEnumerator LowAndHighDiagnosisUseRealLessonAndRegisterLevel()
    {
        yield return SceneManager.LoadSceneAsync(0);yield return new WaitForSecondsRealtime(1.5f);
        int classic=PlayerPrefs.GetInt(GameConstants.UnlockedLevelKey,1);
        Click("AdventureButton");yield return Pause();
        root=Path.Combine(Path.GetTempPath(),"RiscVAdventureSlicePlayModeTests",Guid.NewGuid().ToString("N"));
        AdventureDirector.Instance.UseProfilesForTesting(new AdventureProfileService(new AdventureProfileStore(root)));
        UnityEngine.Object.FindAnyObjectByType<TMP_InputField>().text="Automated A";Click("Create Profile");yield return Pause();
        var a=AdventureDirector.Instance.Profile.Id;
        Click("Begin Adventure");yield return Pause();Click("Answer 1");yield return Pause();Click("Answer 0");yield return Pause();Click("Answer 0");
        yield return WaitScene(1);
        Assert.That(AdventureDirector.Instance.Profile.Journey.Stage,Is.EqualTo(AdventureStage.Lesson));
        Assert.That(Find("ClickUp").interactable,Is.False);
        Assert.That(AdventureDirector.Instance.Profile.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.Zero);
        int guard=0;
        while(AdventureDirector.Instance.Profile.Journey.Stage==AdventureStage.Lesson&&guard++<12) {
            UnityEngine.Object.FindAnyObjectByType<DialogueUI>().SkipAnimationOfTyping();yield return Pause();
            var answer=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).FirstOrDefault(b=>b.name=="FirstAnswerButton");
            Click(answer!=null?answer.name:"GoNextQuoteButton");yield return Pause();
        }
        Assert.That(AdventureDirector.Instance.Profile.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(50));
        Assert.That(AdventureDirector.Instance.Profile.GetTopic(TopicIds.Registers).IsLessonCompleted,Is.True);
        Assert.That(Find("HintButton").gameObject.activeInHierarchy,Is.True,"Practice hints must return after the lesson.");
        for(int i=0;i<2;i++){Click("ClickUp");yield return Pause();float end=Time.realtimeSinceStartup+20;while(UnityEngine.Object.FindAnyObjectByType<LevelZeroRegisseur>().isProcessing&&Time.realtimeSinceStartup<end)yield return null;Assert.That(UnityEngine.Object.FindAnyObjectByType<LevelZeroRegisseur>().isProcessing,Is.False);}
        yield return Pause();Click("CheckSolutionButton");yield return Pause();
        Assert.That(AdventureDirector.Instance.Profile.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(55));
        Assert.That(AdventureDirector.Instance.Profile.Journey.AssignedExerciseId,Is.EqualTo("registers.hold"));
        Click("Result Main Menu");yield return WaitScene(0);
        // Recreate the service from disk to test continuation beyond an in-memory route.
        AdventureDirector.Instance.UseProfilesForTesting(new AdventureProfileService(new AdventureProfileStore(root)));
        Click("Begin Adventure");yield return Pause();Click("Continue Journey");yield return WaitScene(2);
        Assert.That(AdventureDirector.Instance.Profile.Journey.AssignedExerciseId,Is.EqualTo("registers.hold"));
        Click("Adventure Save Exit");yield return WaitScene(0);
        UnityEngine.Object.FindAnyObjectByType<TMP_InputField>().text="Automated B";Click("Create Profile");yield return Pause();
        Assert.That(AdventureDirector.Instance.Profile.GetTopic(TopicIds.Registers).IsAssessed,Is.False);
        Click("Begin Adventure");yield return Pause();Click("Answer 0");yield return Pause();Click("Answer 1");yield return Pause();Click("Answer 2");yield return WaitScene(1);
        Assert.That(AdventureDirector.Instance.Profile.Journey.Stage,Is.EqualTo(AdventureStage.Practice));
        Assert.That(AdventureDirector.Instance.Profile.GetTopic(TopicIds.Registers).Mastery,Is.EqualTo(100));
        Assert.That(AdventureDirector.Instance.Profile.GetTopic(TopicIds.Registers).IsLessonCompleted,Is.False);
        Assert.That(AdventureDirector.Instance.Profiles.GetProfile(a).GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(55));
        Assert.That(PlayerPrefs.GetInt(GameConstants.UnlockedLevelKey,1),Is.EqualTo(classic));
        Click("Adventure Save Exit");yield return WaitScene(0);Click("Back Main Menu");yield return Pause();Click("PlayButton");yield return Pause();
        var classicLevel=UnityEngine.Object.FindAnyObjectByType<MainMenu>().levelButtons[0];
        ExecuteEvents.Execute(classicLevel.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
        yield return WaitScene(1);
        Assert.That(AdventureDirector.IsInAdventure,Is.False);Assert.That(AdventureLevelAdapter.Current,Is.Null);
        var skip=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).First(b=>b.name.IndexOf("skip",StringComparison.OrdinalIgnoreCase)>=0);
        ExecuteEvents.Execute(skip.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);yield return new WaitForSecondsRealtime(1.5f);
        Click("ClickUp");yield return Pause();float classicEnd=Time.realtimeSinceStartup+20;
        while(UnityEngine.Object.FindAnyObjectByType<LevelZeroRegisseur>().isProcessing&&Time.realtimeSinceStartup<classicEnd)yield return null;
        Assert.That(UnityEngine.Object.FindAnyObjectByType<LevelZeroRegisseur>().isProcessing,Is.False);
        Assert.That(PlayerPrefs.GetInt(GameConstants.UnlockedLevelKey,1),Is.EqualTo(classic));
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(AdventureDirector.Instance!=null)UnityEngine.Object.Destroy(AdventureDirector.Instance.gameObject);
        yield return SceneManager.LoadSceneAsync(0);yield return null;
        if(root!=null&&Directory.Exists(root))Directory.Delete(root,true);
    }
}
