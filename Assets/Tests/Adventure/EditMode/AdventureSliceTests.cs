using System;
using System.IO;
using NUnit.Framework;
using RiscVAdventure.Adventure;
using UnityEngine;

namespace RiscVAdventure.Tests.Adventure
{
    public sealed class AdventureSliceTests
    {
        private string root, id;
        private AdventureProfileService service;
        private AdventureSliceConfiguration config;
        private AdventureRegisseur engine;
        private AdventureProfile P => service.GetProfile(id);
        [SetUp] public void Setup()
        {
            root=Path.Combine(Path.GetTempPath(),"RiscVAdventureSliceTests",Guid.NewGuid().ToString("N"));
            service=new AdventureProfileService(new AdventureProfileStore(root));
            config=ScriptableObject.CreateInstance<AdventureSliceConfiguration>();
            engine=new AdventureRegisseur(service,config);id=service.CreateProfile("A").Id;engine.Begin(id);
        }
        [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(config);if(Directory.Exists(root))Directory.Delete(root,true);}
        private void Diagnose(bool high){for(int i=0;i<config.questions.Length;i++)engine.Answer(id,i,high?config.questions[i].correctAnswer:(config.questions[i].correctAnswer+1)%3);}
        private void Practice(bool success,string token=null)
        {var j=P.Journey;Assert.That(engine.Submit(id,j.AssignedExerciseId,j.AttemptId,token??Guid.NewGuid().ToString("N"),success),Is.True);}
        [Test] public void LowDiagnosisRequiresRealLessonCompletion()
        {
            Diagnose(false);Assert.That(P.Journey.Stage,Is.EqualTo(AdventureStage.Lesson));Assert.That(P.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.Zero);
            engine.SaveLessonNode(id,3);Assert.That(P.GetTopic(TopicIds.Registers).IsLessonCompleted,Is.False);
            engine.CompleteLesson(id);Assert.That(P.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(50));
            Assert.That(P.Journey.AssignedExerciseId,Is.EqualTo("registers.transfer"));
        }
        [Test] public void HighDiagnosisSkipsLessonAndPreservesRealScore()
        {Diagnose(true);Assert.That(P.Journey.Stage,Is.EqualTo(AdventureStage.Practice));Assert.That(P.GetTopic(TopicIds.Registers).Mastery,Is.EqualTo(100));Assert.That(P.GetTopic(TopicIds.Registers).IsLessonCompleted,Is.False);}
        [Test] public void StaleAnswerCannotBeCountedTwice()
        {engine.Answer(id,0,0);Assert.Throws<InvalidOperationException>(()=>engine.Answer(id,0,0));Assert.That(P.Journey.DiagnosticAnswers.Count,Is.EqualTo(1));}
        [Test] public void PartialDiagnosisAndLessonCursorSurviveReload()
        {
            engine.Answer(id,0,2);service=new AdventureProfileService(new AdventureProfileStore(root));engine=new AdventureRegisseur(service,config);
            Assert.That(P.Journey.DiagnosticAnswers.Count,Is.EqualTo(1));engine.Answer(id,1,0);engine.Answer(id,2,0);engine.SaveLessonNode(id,5);
            service=new AdventureProfileService(new AdventureProfileStore(root));Assert.That(P.Journey.LessonNode,Is.EqualTo(5));
        }
        [Test] public void FirstTaskGivesFiveAndAssignsMediumForLowKnowledge()
        {Diagnose(false);engine.CompleteLesson(id);Practice(true);Assert.That(P.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(55));Assert.That(P.Journey.AssignedExerciseId,Is.EqualTo("registers.hold"));Assert.That(P.Journey.Stage,Is.EqualTo(AdventureStage.Result));}
        [Test] public void HighKnowledgeGetsDifferentNextTask()
        {Diagnose(true);Practice(true);Assert.That(P.Journey.AssignedExerciseId,Is.EqualTo("registers.negative"));}
        [Test] public void DuplicateReceiptAndOldAttemptGiveNoReward()
        {
            Diagnose(false);engine.CompleteLesson(id);var exercise=P.Journey.AssignedExerciseId;var attempt=P.Journey.AttemptId;Practice(true,"receipt");
            Assert.That(engine.Submit(id,exercise,attempt,"receipt",true),Is.False);engine.Continue(id);
            Assert.That(engine.Submit(id,exercise,attempt,"different",true),Is.False);Assert.That(P.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(55));
        }
        [Test] public void ErrorsAndHintsReduceRewardWithoutReducingMastery()
        {Diagnose(false);engine.CompleteLesson(id);Practice(false);engine.Hint(id);Practice(true);Assert.That(P.Journey.LastReward,Is.EqualTo(2));Assert.That(P.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(50));Assert.That(P.GetTopic(TopicIds.Registers).Mastery,Is.EqualTo(50));}
        [Test] public void FailureLossIsCappedAcrossReloadAndRestart()
        {
            Diagnose(false);engine.CompleteLesson(id);for(int i=0;i<20;i++)Practice(false);
            service=new AdventureProfileService(new AdventureProfileStore(root));engine=new AdventureRegisseur(service,config);Practice(false);
            Assert.That(P.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(40));Assert.That(P.GetTopic(TopicIds.Registers).Mastery,Is.EqualTo(50));
        }
        [Test] public void FullRouteIsFiniteAndUnavailableChapterIsExplicit()
        {
            Diagnose(false);engine.CompleteLesson(id);Practice(true);engine.Continue(id);Practice(true);Assert.That(P.Journey.LastReward,Is.EqualTo(8));engine.Continue(id);Practice(true);
            Assert.That(P.Journey.LastReward,Is.EqualTo(12));Assert.That(P.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(75));
            Assert.That(P.GetTopic(TopicIds.Registers).IsTopicCompleted,Is.True);engine.Continue(id);Assert.That(P.Journey.Stage,Is.EqualTo(AdventureStage.Unavailable));Assert.That(P.Journey.TopicId,Is.EqualTo(TopicIds.Multiplexer));
        }
        [Test] public void UnacknowledgedResultAndNextAssignmentSurviveReload()
        {Diagnose(false);engine.CompleteLesson(id);Practice(true);service=new AdventureProfileService(new AdventureProfileStore(root));Assert.That(P.Journey.Stage,Is.EqualTo(AdventureStage.Result));Assert.That(P.Journey.AssignedExerciseId,Is.EqualTo("registers.hold"));Assert.That(P.Journey.LastReward,Is.EqualTo(5));}
        [Test] public void ProfilesAreIndependent()
        {Diagnose(true);var b=service.CreateProfile("B");Assert.That(b.Journey.Stage,Is.EqualTo(AdventureStage.NotStarted));Assert.That(b.GetTopic(TopicIds.Registers).IsAssessed,Is.False);Assert.That(P.GetTopic(TopicIds.Registers).Mastery,Is.EqualTo(100));}
        [Test] public void LessonCompletionIsIdempotent()
        {Diagnose(false);engine.CompleteLesson(id);Practice(false);engine.CompleteLesson(id);Assert.That(P.GetTopic(TopicIds.Registers).CurrentKnowledge,Is.EqualTo(48));}
        [Test] public void RewardSettingsAreUsed()
        {config.easyReward=9;Diagnose(false);engine.CompleteLesson(id);Practice(true);Assert.That(P.Journey.LastReward,Is.EqualTo(9));}
        [Test] public void LegacyProfileWithoutJourneyLoadsAtDiagnosis()
        {
            var path=Path.Combine(root,id+".json");var text=File.ReadAllText(path);
            // JsonUtility ignores an unknown field, exactly as it handles a missing added field.
            File.WriteAllText(path,text.Replace("\"journey\"","\"oldUnknownField\""));
            service=new AdventureProfileService(new AdventureProfileStore(root));engine=new AdventureRegisseur(service,config);engine.Begin(id);
            Assert.That(P.Journey.Stage,Is.EqualTo(AdventureStage.Diagnostic));
        }
    }
}
