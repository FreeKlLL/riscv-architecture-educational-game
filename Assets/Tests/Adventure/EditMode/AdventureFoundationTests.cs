using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RiscVAdventure.Adventure;

namespace RiscVAdventure.Tests.Adventure
{
    public sealed class AdventureFoundationTests
    {
        private string root;
        private FakeClock clock;
        private AdventureProfileStore store;
        private AdventureProfileService service;
        private static readonly DateTime Start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private sealed class FakeClock : IUtcClock { public DateTime UtcNow { get; set; } = Start; }
        private sealed class Evaluation : IKnowledgeEvaluator
        {
            private readonly double current, mastery;
            public PracticeEvidence Received { get; private set; }
            public Evaluation(double current, double mastery) { this.current = current; this.mastery = mastery; }
            public KnowledgeEvaluation Evaluate(TopicKnowledge topic, PracticeEvidence evidence)
            {
                Received = evidence;
                return new KnowledgeEvaluation(current, mastery);
            }
        }

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "RiscVAdventureFoundationTests", Guid.NewGuid().ToString("N"));
            clock = new FakeClock();
            Reload();
        }

        [TearDown]
        public void TearDown()
        {
            // Only this test's unique temporary directory, never persistentDataPath.
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private void Reload(KnowledgeDecaySettings settings = null, TopicCatalog catalog = null)
        {
            store = new AdventureProfileStore(root);
            service = new AdventureProfileService(store, clock, settings, catalog);
        }
        private AdventureProfile Create(string name = "Charlie") => service.CreateProfile(name);
        private void Assess(string id, double score = 80) =>
            service.RecordAssessment(id, TopicIds.Registers, TopicIds.Registers, score);
        private TopicKnowledge Topic(string id) => service.GetProfile(id).GetTopic(TopicIds.Registers);
        private static PracticeEvidence Evidence(bool success = true) => new PracticeEvidence(TopicIds.Registers,
            "register-exercise-1", "classic", ExerciseDifficulty.Advanced, 2, success);

        [Test]
        public void MultipleProfilesHaveStableIndependentIdsAndState()
        {
            var a = Create("Alice"); var b = Create("Bob");
            Assert.AreNotEqual(a.Id, b.Id);
            Assert.IsTrue(Guid.TryParseExact(a.Id, "N", out _));
            Assess(a.Id, 80);
            Assert.AreEqual(0, Topic(b.Id).CurrentKnowledge);
            Reload();
            Assert.AreEqual(80, Topic(a.Id).CurrentKnowledge);
            Assert.AreEqual("Bob", service.GetProfile(b.Id).DisplayName);
            Assert.AreEqual(2, service.Profiles.Count);
        }

        [Test]
        public void RoundTripPersistsKnowledgeFlagsHistoryUtcAndChapterDecisions()
        {
            var p = Create(); Assess(p.Id);
            service.RecordLessonChoice(p.Id, TopicIds.Registers, LessonChoice.SkipLesson);
            service.CompleteLesson(p.Id, TopicIds.Registers);
            service.CompleteTopic(p.Id, TopicIds.Registers);
            service.RecordPractice(p.Id, Evidence(), new Evaluation(85, 85));
            Reload();
            var topic = Topic(p.Id); var loaded = service.GetProfile(p.Id);
            Assert.AreEqual(85, topic.CurrentKnowledge);
            Assert.AreEqual(85, topic.Mastery);
            Assert.IsTrue(topic.IsAssessed && topic.IsLessonCompleted && topic.IsTopicCompleted);
            Assert.AreEqual(Start, topic.LastPracticeUtc);
            Assert.AreEqual(DateTimeKind.Utc, topic.LastDecayCalculationUtc.Value.Kind);
            Assert.AreEqual(LessonChoice.SkipLesson, loaded.GetChapter(TopicIds.Registers).LessonChoice);
            Assert.AreEqual("classic", loaded.PracticeHistory.Single().ContentSource);
            Assert.AreEqual(ExerciseDifficulty.Advanced, loaded.PracticeHistory.Single().Difficulty);
            Assert.AreEqual(2, loaded.PracticeHistory.Single().ErrorCount);
        }

        [Test]
        public void ActiveSelectionAndRenameSurviveRestart()
        {
            var a = Create("Alice"); var b = Create("Bob");
            Assert.AreEqual(a.Id, service.ActiveProfileId);
            service.SelectProfile(b.Id);
            service.RenameProfile(b.Id, "  Robert  ");
            Reload();
            Assert.AreEqual(b.Id, service.ActiveProfile.Id);
            Assert.AreEqual("Robert", service.ActiveProfile.DisplayName);
        }

        [Test]
        public void CorruptProfileIsSkippedWithoutDestroyingItOrOtherProfiles()
        {
            var a = Create("Damaged"); var b = Create("Healthy");
            var path = store.GetProfilePath(a.Id);
            File.WriteAllText(path, "{broken");
            Reload();
            Assert.AreEqual(1, service.Profiles.Count);
            Assert.AreEqual(b.Id, service.ActiveProfileId);
            Assert.AreEqual("{broken", File.ReadAllText(path));
            Assert.AreEqual(1, service.LoadIssues.Count);
        }

        [Test]
        public void CorruptPrimaryRecoversBackupWithoutOverwritingDamagedBytesOnLoad()
        {
            var p = Create("Original"); service.RenameProfile(p.Id, "Renamed");
            var path = store.GetProfilePath(p.Id);
            File.WriteAllText(path, "broken");
            Reload();
            Assert.AreEqual("Original", service.GetProfile(p.Id).DisplayName);
            Assert.AreEqual("broken", File.ReadAllText(path));
            Assert.AreEqual(1, service.LoadIssues.Count);
        }

        [Test]
        public void DamagedIndexDoesNotHideProfilesAndUsesItsBackup()
        {
            var a = Create(); var b = Create(); service.SelectProfile(b.Id);
            File.WriteAllText(Path.Combine(root, "index.json"), "[]bad");
            Reload();
            Assert.AreEqual(2, service.Profiles.Count);
            Assert.AreEqual(a.Id, service.ActiveProfileId);
            Assert.IsNotEmpty(service.LoadIssues);
        }

        [Test]
        public void NewProfileHasEightUnknownTopicsAndFirstChapterNeedsDiagnosis()
        {
            var p = Create();
            Assert.AreEqual(8, p.Topics.Count);
            foreach (var t in p.Topics)
            {
                Assert.AreEqual(0, t.CurrentKnowledge); Assert.AreEqual(0, t.Mastery);
                Assert.IsFalse(t.IsAssessed || t.IsLessonCompleted || t.IsTopicCompleted);
                Assert.IsNull(t.LastPracticeUtc); Assert.IsNull(t.LastDecayCalculationUtc);
                Assert.IsFalse(p.GetChapter(t.TopicId).IsDiagnosticCompleted);
            }
            Assert.IsFalse(service.HasCompletedAdventure(p.Id));
        }

        [Test]
        public void LessonAwardsAtLeastFiftyWithoutFalselyMarkingDiagnosis()
        {
            var p = Create(); service.CompleteLesson(p.Id, TopicIds.Registers);
            Assert.AreEqual(50, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(50, Topic(p.Id).Mastery);
            Assert.IsTrue(Topic(p.Id).IsLessonCompleted);
            Assert.IsFalse(Topic(p.Id).IsAssessed);
        }

        [Test]
        public void LessonDoesNotLowerEightyPercent()
        {
            var p = Create(); Assess(p.Id); service.CompleteLesson(p.Id, TopicIds.Registers);
            Assert.AreEqual(80, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(80, Topic(p.Id).Mastery);
        }

        [TestCase(6, 80)]
        [TestCase(7, 79)]
        [TestCase(70, 70)]
        [TestCase(105, 65)]
        [TestCase(7000, 65)]
        public void DecayUsesFullWeeksAndCapsAtFifteen(int days, double expected)
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(days); service.RefreshKnowledge(p.Id);
            Assert.AreEqual(expected, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(80, Topic(p.Id).Mastery);
        }

        [Test]
        public void DecayDoesNotRoundPartialDaysUp()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(7).AddTicks(-1); Reload();
            Assert.AreEqual(80, Topic(p.Id).CurrentKnowledge);
        }

        [Test]
        public void RepeatedLoadsNeverApplyTheSameLossTwice()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(70);
            Reload(); Assert.AreEqual(70, Topic(p.Id).CurrentKnowledge);
            Reload(); Assert.AreEqual(70, Topic(p.Id).CurrentKnowledge);
            service.SaveProfile(p.Id); Reload();
            Assert.AreEqual(70, Topic(p.Id).CurrentKnowledge);
            clock.UtcNow = Start.AddDays(77); Reload();
            Assert.AreEqual(69, Topic(p.Id).CurrentKnowledge);
        }

        [Test]
        public void UnknownTopicsNeverDecayAndAssessedZeroIsDifferentFromUnknownZero()
        {
            var p = Create(); Assess(p.Id, 0);
            clock.UtcNow = Start.AddYears(10); Reload();
            Assert.IsTrue(Topic(p.Id).IsAssessed);
            var unknown = service.GetProfile(p.Id).GetTopic(TopicIds.Memory);
            Assert.AreEqual(0, unknown.CurrentKnowledge); Assert.IsFalse(unknown.IsAssessed);
            Assert.IsNull(unknown.LastDecayCalculationUtc);
        }

        [Test]
        public void LessonOnlyTopicsCanDecayWithoutDiagnosis()
        {
            var p = Create(); service.CompleteLesson(p.Id, TopicIds.Registers);
            clock.UtcNow = Start.AddDays(70); Reload();
            Assert.AreEqual(40, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(50, Topic(p.Id).Mastery);
        }

        [Test]
        public void CompletionAndAdventureCompletionSurviveDecay()
        {
            var p = Create();
            foreach (var id in TopicCatalog.Default.Topics.Select(t => t.Id))
            {
                service.RecordAssessment(p.Id, id, id, 80); service.CompleteTopic(p.Id, id);
            }
            clock.UtcNow = Start.AddYears(5); Reload();
            Assert.IsTrue(service.HasCompletedAdventure(p.Id));
            Assert.IsTrue(service.GetProfile(p.Id).Topics.All(t => t.IsTopicCompleted && t.Mastery == 80));
        }

        [Test]
        public void ClockRollbackDoesNotRestoreKnowledgeOrMoveHighWaterMarkBack()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(70); Reload();
            clock.UtcNow = Start.AddDays(-10); Reload();
            Assert.AreEqual(70, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(Start.AddDays(70), Topic(p.Id).LastDecayCalculationUtc);
            clock.UtcNow = Start.AddDays(71); Reload();
            Assert.AreEqual(70, Topic(p.Id).CurrentKnowledge);
            clock.UtcNow = Start.AddDays(77); Reload();
            Assert.AreEqual(69, Topic(p.Id).CurrentKnowledge);
        }

        [Test]
        public void SuccessfulPracticeAfterRollbackRestoresKnowledgeAndResetsAnchorMonotonically()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(70); Reload();
            clock.UtcNow = Start.AddDays(5);
            service.RecordPractice(p.Id, Evidence(), new Evaluation(80, 80));
            Assert.AreEqual(80, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(Start.AddDays(70), Topic(p.Id).LastPracticeUtc);
            clock.UtcNow = Start.AddDays(76); Reload(); Assert.AreEqual(80, Topic(p.Id).CurrentKnowledge);
            clock.UtcNow = Start.AddDays(77); Reload(); Assert.AreEqual(79, Topic(p.Id).CurrentKnowledge);
        }

        [Test]
        public void SuccessfulPracticeRestoresKnowledgeAndOnlyThenRestartsDecay()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(70); Reload();
            var evaluator = new Evaluation(85, 85); var evidence = Evidence();
            service.RecordPractice(p.Id, evidence, evaluator);
            Assert.AreSame(evidence, evaluator.Received);
            Assert.AreEqual(Start.AddDays(70), Topic(p.Id).LastPracticeUtc);
            clock.UtcNow = Start.AddDays(77); Reload();
            Assert.AreEqual(84, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(85, Topic(p.Id).Mastery);
        }

        [Test]
        public void FailedPracticeRecordsErrorsWithoutMovingLastSuccessfulPractice()
        {
            var p = Create(); Assess(p.Id); service.RecordPractice(p.Id, Evidence(), new Evaluation(80, 80));
            clock.UtcNow = Start.AddDays(7);
            service.RecordPractice(p.Id, Evidence(false), new Evaluation(75, 80));
            Reload();
            Assert.AreEqual(Start, Topic(p.Id).LastPracticeUtc);
            Assert.AreEqual(75, Topic(p.Id).CurrentKnowledge);
            Assert.IsFalse(service.GetProfile(p.Id).PracticeHistory.Last().IsSuccessful);
        }

        [TestCase(-100, 0)]
        [TestCase(0, 0)]
        [TestCase(80, 80)]
        [TestCase(100, 100)]
        [TestCase(900, 100)]
        public void AssessmentClampsBothIndicators(double score, double expected)
        {
            var p = Create(); Assess(p.Id, score); Reload();
            Assert.AreEqual(expected, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(expected, Topic(p.Id).Mastery);
        }

        [Test]
        public void PracticeClampsBothIndicatorsAndDecayNeverGoesBelowZero()
        {
            var p = Create(); Assess(p.Id, 5);
            service.RecordPractice(p.Id, Evidence(), new Evaluation(-100, 900));
            clock.UtcNow = Start.AddYears(2); Reload();
            Assert.AreEqual(0, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(100, Topic(p.Id).Mastery);
        }

        [Test]
        public void NonFiniteScoresAreRejectedWithoutPartiallyCreatingAChapter()
        {
            var p = Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => service.RecordAssessment(p.Id, "new", TopicIds.Registers, double.NaN));
            Assert.Throws<KeyNotFoundException>(() => service.GetProfile(p.Id).GetChapter("new"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnowledgeEvaluation(double.PositiveInfinity, 80));
            Assert.IsFalse(Topic(p.Id).IsAssessed);
        }

        [Test]
        public void EveryChapterIncludingFirstRequiresItsOwnDiagnosticBeforeSkipping()
        {
            var p = Create();
            Assert.Throws<InvalidOperationException>(() => service.RecordLessonChoice(p.Id, TopicIds.Registers, LessonChoice.SkipLesson));
            Assess(p.Id, 95);
            service.RecordLessonChoice(p.Id, TopicIds.Registers, LessonChoice.SkipLesson);
            Assert.IsFalse(service.GetProfile(p.Id).GetChapter(TopicIds.Multiplexer).IsDiagnosticCompleted);
            Assert.Throws<InvalidOperationException>(() => service.RecordLessonChoice(p.Id, TopicIds.Multiplexer, LessonChoice.SkipLesson));
        }

        [Test]
        public void AdditionalChaptersAndTopicsCanBeAddedWithoutLosingOldProgress()
        {
            var p = Create(); Assess(p.Id);
            var extended = new TopicCatalog(TopicCatalog.Default.Topics.Concat(new[] { new TopicDefinition("pipelineprozessor") }));
            Reload(catalog: extended);
            Assert.AreEqual(9, service.GetProfile(p.Id).Topics.Count);
            service.RecordAssessment(p.Id, "pipeline-1", "pipelineprozessor", 90);
            Reload();
            Assert.AreEqual(90, service.GetProfile(p.Id).GetTopic("pipelineprozessor").Mastery);
            Assert.AreEqual(80, Topic(p.Id).Mastery);
        }

        [Test]
        public void DeletionRequiresConfirmationAndRemovesBackupsWithoutResurrection()
        {
            var a = Create("A"); var b = Create("B"); service.RenameProfile(a.Id, "A2");
            Assert.IsFalse(service.DeleteProfile(a.Id, false)); Assert.AreEqual(2, service.Profiles.Count);
            Assert.IsTrue(service.DeleteProfile(a.Id, true)); Reload();
            Assert.AreEqual(b.Id, service.ActiveProfileId); Assert.AreEqual(1, service.Profiles.Count);
            Assert.IsFalse(File.Exists(store.GetProfilePath(a.Id) + ".bak"));
            service.DeleteProfile(b.Id, true); Reload();
            Assert.IsNull(service.ActiveProfile); Assert.IsEmpty(service.Profiles); Assert.IsEmpty(service.LoadIssues);
        }

        [Test]
        public void FutureProfileSchemaIsPreservedAndNotReplacedByOlderBackup()
        {
            var a = Create(); service.RenameProfile(a.Id, "Backup"); var b = Create();
            var path = store.GetProfilePath(a.Id); var text = File.ReadAllText(path).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
            File.WriteAllText(path, text); Reload();
            Assert.AreEqual(1, service.Profiles.Count); Assert.AreEqual(b.Id, service.Profiles[0].Id);
            Assert.AreEqual(text, File.ReadAllText(path)); Assert.IsNotEmpty(service.LoadIssues);
        }

        [Test]
        public void FutureIndexPreventsOverwritingItsMetadata()
        {
            var p = Create(); var path = Path.Combine(root, "index.json");
            var text = "{\"schemaVersion\":99,\"activeProfileId\":\"" + p.Id + "\"}";
            File.WriteAllText(path, text); Reload();
            Assert.Throws<UnsupportedSaveVersionException>(() => service.SelectProfile(p.Id));
            Assert.Throws<UnsupportedSaveVersionException>(() => service.CreateProfile("Blocked"));
            Assert.AreEqual(text, File.ReadAllText(path));
        }

        [Test]
        public void WritesKeepAPreviousBackupAndIgnoreInterruptedTemporaryFiles()
        {
            var p = Create("A"); var original = File.ReadAllText(store.GetProfilePath(p.Id));
            service.RenameProfile(p.Id, "B");
            Assert.AreEqual(original, File.ReadAllText(store.GetProfilePath(p.Id) + ".bak"));
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp"));
            File.WriteAllText(store.GetProfilePath(p.Id) + ".interrupted.tmp", "garbage");
            Reload(); Assert.AreEqual("B", service.GetProfile(p.Id).DisplayName); Assert.IsEmpty(service.LoadIssues);
        }

        [Test]
        public void FailedWriteCleansItsTemporaryFileAndRollsBackInMemoryRename()
        {
            var p = Create("Before"); var path = store.GetProfilePath(p.Id);
            File.Delete(path); Directory.CreateDirectory(path); // Simulated unavailable destination, entirely in the test sandbox.
            Assert.Throws<IOException>(() => service.RenameProfile(p.Id, "After"));
            Assert.AreEqual("Before", service.GetProfile(p.Id).DisplayName);
            Assert.IsEmpty(Directory.GetFiles(root, "*.tmp"));
        }

        [Test]
        public void DecaySettingsAreInjectable()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(9); Reload(new KnowledgeDecaySettings(3, 2, 4));
            Assert.AreEqual(76, Topic(p.Id).CurrentKnowledge);
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnowledgeDecaySettings(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new KnowledgeDecaySettings(maximumLoss: -1));
        }

        [Test]
        public void RepeatedLessonCompletionDoesNotExtendOrStackDecay()
        {
            var p = Create(); Assess(p.Id); service.CompleteLesson(p.Id, TopicIds.Registers);
            clock.UtcNow = Start.AddDays(70); Reload();
            service.CompleteLesson(p.Id, TopicIds.Registers);
            clock.UtcNow = Start.AddDays(140); Reload();
            Assert.AreEqual(65, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(80, Topic(p.Id).Mastery);
        }

        [Test]
        public void NonUtcClockAndUnsafeProfileIdsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => store.GetProfilePath("../other"));
            Assert.Throws<ArgumentException>(() => service.CreateProfile("  "));
            clock.UtcNow = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified);
            Assert.Throws<ArgumentException>(() => Create());
        }

        [Test]
        public void CompletingLessonAfterDecayDoesNotStackLossBelowConfirmedMasteryFloor()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(70); Reload();
            service.CompleteLesson(p.Id, TopicIds.Registers);
            Assert.AreEqual(70, Topic(p.Id).CurrentKnowledge);
            clock.UtcNow = Start.AddDays(700); Reload();
            Assert.AreEqual(65, Topic(p.Id).CurrentKnowledge);
            Assert.AreEqual(80, Topic(p.Id).Mastery);
        }

        [Test]
        public void DailyReloadsDoNotDiscardPartialWeeksOrPostponeDecay()
        {
            var p = Create(); Assess(p.Id);
            for (int day = 1; day <= 14; day++)
            {
                clock.UtcNow = Start.AddDays(day); Reload();
                Assert.AreEqual(80 - day / 7, Topic(p.Id).CurrentKnowledge);
            }
        }

        [Test]
        public void ExtremeForwardClockIsCappedAndSubsequentRollbackIsStable()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc); Reload();
            Assert.AreEqual(65, Topic(p.Id).CurrentKnowledge);
            clock.UtcNow = Start; Reload();
            Assert.AreEqual(65, Topic(p.Id).CurrentKnowledge);
        }

        [Test]
        public void NewTopicAfterRollbackUsesTheProfilesExistingUtcHighWaterMark()
        {
            var p = Create(); Assess(p.Id);
            clock.UtcNow = Start.AddDays(70); Reload();
            clock.UtcNow = Start.AddDays(5); service.CompleteLesson(p.Id, TopicIds.Memory);
            Assert.AreEqual(Start.AddDays(70), service.GetProfile(p.Id).GetTopic(TopicIds.Memory).LastDecayCalculationUtc);
            clock.UtcNow = Start.AddDays(76); Reload();
            Assert.AreEqual(50, service.GetProfile(p.Id).GetTopic(TopicIds.Memory).CurrentKnowledge);
            clock.UtcNow = Start.AddDays(77); Reload();
            Assert.AreEqual(49, service.GetProfile(p.Id).GetTopic(TopicIds.Memory).CurrentKnowledge);
        }
    }
}
