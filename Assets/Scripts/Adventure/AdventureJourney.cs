using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RiscVAdventure.Adventure
{
    public enum AdventureStage { NotStarted, Diagnostic, Lesson, Practice, Result, Unavailable }
    [Serializable] public sealed class AdventureJourney
    {
        [SerializeField] internal AdventureStage stage;
        [SerializeField] internal string topicId = TopicIds.Registers, assignedExerciseId, attemptId;
        [SerializeField] internal List<int> diagnosticAnswers = new List<int>();
        [SerializeField] internal List<string> completedExercises = new List<string>();
        [SerializeField] internal List<string> processedSubmissions = new List<string>();
        [SerializeField] internal int lessonNode, errors, hints;
        [SerializeField] internal double diagnosticScore, failureLoss, lastReward;
        [SerializeField] internal string lastExerciseId;
        [SerializeField] internal int lastErrors, lastHints;
        public AdventureStage Stage => stage;
        public string TopicId => topicId;
        public string AssignedExerciseId => assignedExerciseId;
        public string AttemptId => attemptId;
        public IReadOnlyList<int> DiagnosticAnswers => diagnosticAnswers.AsReadOnly();
        public IReadOnlyList<string> CompletedExercises => completedExercises.AsReadOnly();
        public int LessonNode => lessonNode;
        public int Errors => errors;
        public int Hints => hints;
        public double DiagnosticScore => diagnosticScore;
        public double LastReward => lastReward;
        public string LastExerciseId => lastExerciseId;
        public int LastErrors => lastErrors;
        public int LastHints => lastHints;
        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(AdventureStage), stage) || diagnosticAnswers == null || completedExercises == null || processedSubmissions == null || errors < 0 || hints < 0 || lessonNode < 0)
                throw new InvalidDataException("Invalid adventure position.");
            Guard.Text(topicId, "journey topic"); Guard.Percent(diagnosticScore);
            if (stage == AdventureStage.Practice && (string.IsNullOrEmpty(assignedExerciseId) || string.IsNullOrEmpty(attemptId))) throw new InvalidDataException("Missing adventure assignment.");
        }
    }
}
