using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RiscVAdventure.Adventure
{
    public enum LessonChoice { Undecided, TakeLesson, SkipLesson, Review }
    public enum ExerciseDifficulty { Introductory, Standard, Advanced }

    public sealed class PracticeEvidence
    {
        public string TopicId { get; }
        public string ExerciseId { get; }
        // E.g. "classic" or "adventure"; references content, never Classic save data.
        public string ContentSource { get; }
        public ExerciseDifficulty Difficulty { get; }
        public int ErrorCount { get; }
        public int HintCount { get; }
        public bool IsSuccessful { get; }

        public PracticeEvidence(string topicId, string exerciseId, string contentSource,
            ExerciseDifficulty difficulty, int errorCount, bool isSuccessful, int hintCount = 0)
        {
            TopicId = Guard.Text(topicId, nameof(topicId));
            ExerciseId = Guard.Text(exerciseId, nameof(exerciseId));
            ContentSource = Guard.Text(contentSource, nameof(contentSource));
            if (!Enum.IsDefined(typeof(ExerciseDifficulty), difficulty))
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            if (errorCount < 0) throw new ArgumentOutOfRangeException(nameof(errorCount));
            Difficulty = difficulty;
            if (hintCount < 0) throw new ArgumentOutOfRangeException(nameof(hintCount));
            HintCount = hintCount;
            ErrorCount = errorCount;
            IsSuccessful = isSuccessful;
        }
    }

    public sealed class KnowledgeEvaluation
    {
        public double CurrentKnowledge { get; }
        public double Mastery { get; }
        public bool ConfirmsAssessment { get; }

        public KnowledgeEvaluation(double currentKnowledge, double mastery, bool confirmsAssessment = false)
        {
            CurrentKnowledge = Guard.Percent(currentKnowledge);
            Mastery = Guard.Percent(mastery);
            ConfirmsAssessment = confirmsAssessment;
        }
    }

    // The future Regisseur supplies a scoring policy using difficulty and mistakes.
    // The foundation does not invent diagnostic thresholds or select exercises.
    public interface IKnowledgeEvaluator
    {
        KnowledgeEvaluation Evaluate(TopicKnowledge topic, PracticeEvidence evidence);
    }

    [Serializable]
    public sealed class ChapterProgress
    {
        [SerializeField] private string chapterId;
        [SerializeField] private string topicId;
        [SerializeField] private bool isDiagnosticCompleted;
        [SerializeField] private LessonChoice lessonChoice;
        public string ChapterId => chapterId;
        public string TopicId => topicId;
        public bool IsDiagnosticCompleted => isDiagnosticCompleted;
        public LessonChoice LessonChoice => lessonChoice;

        internal ChapterProgress(string chapter, string topic) { chapterId = chapter; topicId = topic; }
        internal void RecordDiagnostic() { isDiagnosticCompleted = true; }
        internal void RecordChoice(LessonChoice choice)
        {
            if (!Enum.IsDefined(typeof(LessonChoice), choice)) throw new ArgumentOutOfRangeException(nameof(choice));
            if (choice == LessonChoice.SkipLesson && !isDiagnosticCompleted)
                throw new InvalidOperationException("A chapter requires diagnosis before skipping its lesson.");
            lessonChoice = choice;
        }
        internal void Validate()
        {
            chapterId = Guard.Text(chapterId, nameof(chapterId));
            topicId = Guard.Text(topicId, nameof(topicId));
            if (!Enum.IsDefined(typeof(LessonChoice), lessonChoice) ||
                (lessonChoice == LessonChoice.SkipLesson && !isDiagnosticCompleted))
                throw new InvalidDataException("Invalid chapter decision.");
        }
    }

    [Serializable]
    public sealed class PracticeRecord
    {
        [SerializeField] private string topicId;
        [SerializeField] private string exerciseId;
        [SerializeField] private string contentSource;
        [SerializeField] private ExerciseDifficulty difficulty;
        [SerializeField] private int errorCount;
        [SerializeField] private int hintCount;
        [SerializeField] private bool isSuccessful;
        [SerializeField] private string recordedUtc;
        public string TopicId => topicId;
        public string ExerciseId => exerciseId;
        public string ContentSource => contentSource;
        public ExerciseDifficulty Difficulty => difficulty;
        public int ErrorCount => errorCount;
        public int HintCount => hintCount;
        public bool IsSuccessful => isSuccessful;
        public DateTime RecordedUtc => UtcStamp.Parse(recordedUtc).Value;

        internal PracticeRecord(PracticeEvidence evidence, DateTime now)
        {
            topicId = evidence.TopicId; exerciseId = evidence.ExerciseId;
            contentSource = evidence.ContentSource; difficulty = evidence.Difficulty;
            hintCount = evidence.HintCount; errorCount = evidence.ErrorCount; isSuccessful = evidence.IsSuccessful;
            recordedUtc = UtcStamp.Format(now);
        }
        internal void Validate()
        {
            Guard.Text(topicId, nameof(topicId)); Guard.Text(exerciseId, nameof(exerciseId));
            Guard.Text(contentSource, nameof(contentSource));
            if (!UtcStamp.Parse(recordedUtc).HasValue || errorCount < 0 || hintCount < 0 ||
                !Enum.IsDefined(typeof(ExerciseDifficulty), difficulty))
                throw new InvalidDataException("Invalid practice record.");
        }
    }

    [Serializable]
    public sealed class AdventureProfile
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string createdUtc;
        [SerializeField] private List<TopicKnowledge> topics = new List<TopicKnowledge>();
        [SerializeField] private List<ChapterProgress> chapters = new List<ChapterProgress>();
        [SerializeField] private List<PracticeRecord> practiceHistory = new List<PracticeRecord>();

        [SerializeField] private AdventureJourney journey = new AdventureJourney();
        public AdventureJourney Journey => journey ?? (journey = new AdventureJourney());
        public string Id => id;
        public string DisplayName => displayName;
        public DateTime CreatedUtc => UtcStamp.Parse(createdUtc).Value;
        public IReadOnlyList<TopicKnowledge> Topics => topics.AsReadOnly();
        public IReadOnlyList<ChapterProgress> Chapters => chapters.AsReadOnly();
        public IReadOnlyList<PracticeRecord> PracticeHistory => practiceHistory.AsReadOnly();

        internal AdventureProfile(string name, DateTime now, TopicCatalog catalog)
        {
            id = Guid.NewGuid().ToString("N");
            displayName = Guard.Text(name, nameof(name));
            createdUtc = UtcStamp.Format(now);
            AddCatalog(catalog);
        }

        public TopicKnowledge GetTopic(string topicId) => topics.FirstOrDefault(t => t.TopicId == topicId)
            ?? throw new KeyNotFoundException("Unknown topic: " + topicId);

        public ChapterProgress GetChapter(string chapterId) => chapters.FirstOrDefault(c => c.ChapterId == chapterId)
            ?? throw new KeyNotFoundException("Unknown chapter: " + chapterId);

        public bool HasCompletedAdventure(TopicCatalog catalog) => catalog.Topics.Where(t => t.IsRequired)
            .All(t => topics.Any(p => p.TopicId == t.Id && p.IsTopicCompleted));

        internal void Rename(string name) { displayName = Guard.Text(name, nameof(name)); }
        internal void AddPractice(PracticeRecord record) { practiceHistory.Add(record); }

        internal ChapterProgress EnsureChapter(string chapterId, string topicId)
        {
            Guard.Text(chapterId, nameof(chapterId));
            GetTopic(topicId);
            var chapter = chapters.FirstOrDefault(c => c.ChapterId == chapterId);
            if (chapter == null) { chapter = new ChapterProgress(chapterId, topicId); chapters.Add(chapter); }
            if (chapter.TopicId != topicId) throw new InvalidOperationException("Chapter belongs to another topic.");
            return chapter;
        }

        private void AddCatalog(TopicCatalog catalog)
        {
            foreach (var definition in catalog.Topics)
            {
                if (topics.All(t => t.TopicId != definition.Id)) topics.Add(new TopicKnowledge(definition.Id));
                EnsureChapter(definition.Id, definition.Id);
            }
        }

        internal void Validate(string expectedId, TopicCatalog catalog)
        {
            if (!Guid.TryParseExact(id, "N", out _) || id != expectedId ||
                !UtcStamp.Parse(createdUtc).HasValue || topics == null || chapters == null || practiceHistory == null)
                throw new InvalidDataException("Invalid profile document.");
            displayName = Guard.Text(displayName, nameof(displayName));
            if (topics.Any(t => t == null) || chapters.Any(c => c == null) || practiceHistory.Any(p => p == null))
                throw new InvalidDataException("Null progress entry.");
            foreach (var topic in topics) topic.Validate();
            foreach (var chapter in chapters) chapter.Validate();
            foreach (var record in practiceHistory) record.Validate();
            Journey.Validate();
            if (topics.Select(t => t.TopicId).Distinct(StringComparer.Ordinal).Count() != topics.Count ||
                chapters.Select(c => c.ChapterId).Distinct(StringComparer.Ordinal).Count() != chapters.Count ||
                chapters.Any(c => topics.All(t => t.TopicId != c.TopicId)) ||
                practiceHistory.Any(p => topics.All(t => t.TopicId != p.TopicId)))
                throw new InvalidDataException("Duplicate or dangling progress entry.");
            AddCatalog(catalog);
        }
    }
}
