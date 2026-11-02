using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace RiscVAdventure.Adventure
{
    public interface IUtcClock { DateTime UtcNow { get; } }
    public sealed class SystemUtcClock : IUtcClock { public DateTime UtcNow => DateTime.UtcNow; }

    public sealed class KnowledgeDecaySettings
    {
        public int FullDaysPerStep { get; }
        public double PercentagePointsPerStep { get; }
        public double MaximumLoss { get; }

        public KnowledgeDecaySettings(int fullDaysPerStep = 7, double percentagePointsPerStep = 1,
            double maximumLoss = 15)
        {
            if (fullDaysPerStep <= 0) throw new ArgumentOutOfRangeException(nameof(fullDaysPerStep));
            if (double.IsNaN(percentagePointsPerStep) || double.IsInfinity(percentagePointsPerStep) ||
                percentagePointsPerStep <= 0 || percentagePointsPerStep > 100)
                throw new ArgumentOutOfRangeException(nameof(percentagePointsPerStep));
            if (double.IsNaN(maximumLoss) || double.IsInfinity(maximumLoss) || maximumLoss < 0 || maximumLoss > 100)
                throw new ArgumentOutOfRangeException(nameof(maximumLoss));
            FullDaysPerStep = fullDaysPerStep;
            PercentagePointsPerStep = percentagePointsPerStep;
            MaximumLoss = maximumLoss;
        }
    }

    internal static class UtcStamp
    {
        internal static string Format(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("The clock must return UTC.");
            return value.ToString("O", CultureInfo.InvariantCulture);
        }

        internal static DateTime? Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (!DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                    out var date) || date.Kind != DateTimeKind.Utc)
                throw new InvalidDataException("Invalid UTC timestamp.");
            return date;
        }
    }

    [Serializable]
    public sealed class TopicKnowledge
    {
        [SerializeField] private string topicId;
        [SerializeField] private double currentKnowledge;
        [SerializeField] private double mastery;
        [SerializeField] private bool isAssessed;
        [SerializeField] private bool isLessonCompleted;
        [SerializeField] private bool isTopicCompleted;
        [SerializeField] private string lastPracticeUtc;
        [SerializeField] private string lastDecayCalculationUtc;
        [SerializeField] private string decayBaselineUtc;
        [SerializeField] private double decayBaselineKnowledge;

        public string TopicId => topicId;
        public double CurrentKnowledge => currentKnowledge;
        public double Mastery => mastery;
        public bool IsAssessed => isAssessed;
        public bool IsLessonCompleted => isLessonCompleted;
        public bool IsTopicCompleted => isTopicCompleted;
        public DateTime? LastPracticeUtc => UtcStamp.Parse(lastPracticeUtc);
        public DateTime? LastDecayCalculationUtc => UtcStamp.Parse(lastDecayCalculationUtc);

        internal TopicKnowledge(string id) { topicId = Guard.Text(id, nameof(id)); }

        internal void Validate()
        {
            topicId = Guard.Text(topicId, nameof(topicId));
            currentKnowledge = Guard.Percent(currentKnowledge);
            mastery = Guard.Percent(mastery);
            decayBaselineKnowledge = Guard.Percent(decayBaselineKnowledge);
            var practice = LastPracticeUtc;
            var calculation = LastDecayCalculationUtc;
            var baseline = UtcStamp.Parse(decayBaselineUtc);
            if ((isAssessed || isLessonCompleted) && (!baseline.HasValue || !calculation.HasValue))
                throw new InvalidDataException("An assessed/learned topic needs a decay anchor.");
            if (baseline.HasValue && calculation.HasValue && calculation < baseline)
                throw new InvalidDataException("Decay calculation precedes its baseline.");
            if (practice.HasValue && calculation.HasValue && practice > calculation)
                throw new InvalidDataException("Practice exceeds the saved time high-water mark.");
        }

        private DateTime EffectiveTime(DateTime now)
        {
            UtcStamp.Format(now); // Reject local / unspecified clocks before changing any state.
            var previous = LastDecayCalculationUtc;
            return previous.HasValue && previous.Value > now ? previous.Value : now;
        }

        private void Confirm(DateTime now)
        {
            var stamp = UtcStamp.Format(EffectiveTime(now));
            // A lesson must not turn already-forgotten knowledge into another lower
            // baseline while retaining a higher confirmed mastery (stacking the loss).
            decayBaselineKnowledge = Math.Max(currentKnowledge, mastery);
            decayBaselineUtc = stamp;
            lastDecayCalculationUtc = stamp;
        }

        internal void Assess(double score, DateTime now)
        {
            var bounded = Guard.Percent(score);
            EffectiveTime(now);
            currentKnowledge = mastery = bounded;
            isAssessed = true;
            Confirm(now);
        }

        internal void CompleteLesson(DateTime now)
        {
            EffectiveTime(now);
            var confirmsNewLearning = !isLessonCompleted || currentKnowledge < 50 || mastery < 50;
            currentKnowledge = Math.Max(50, currentKnowledge);
            mastery = Math.Max(50, mastery);
            isLessonCompleted = true;
            if (confirmsNewLearning) Confirm(now);
        }

        internal void CompleteTopic() { isTopicCompleted = true; }

        internal void Practice(KnowledgeEvaluation evaluation, bool successful, DateTime now)
        {
            var current = Guard.Percent(evaluation.CurrentKnowledge);
            var confirmed = Guard.Percent(evaluation.Mastery);
            var effective = EffectiveTime(now);
            currentKnowledge = current;
            mastery = confirmed;
            if (evaluation.ConfirmsAssessment) isAssessed = true;
            if (successful || evaluation.ConfirmsAssessment)
            {
                Confirm(effective);
                if (successful) lastPracticeUtc = UtcStamp.Format(effective);
            }
        }

        internal bool ApplyDecay(DateTime now, KnowledgeDecaySettings settings)
        {
            if (!isAssessed && !isLessonCompleted) return false;
            var effective = EffectiveTime(now);
            var baseline = UtcStamp.Parse(decayBaselineUtc).Value;
            var fullDays = Math.Max(0L, (effective.Ticks - baseline.Ticks) / TimeSpan.TicksPerDay);
            var steps = fullDays / settings.FullDaysPerStep;
            var loss = Math.Min(settings.MaximumLoss, steps * settings.PercentagePointsPerStep);
            var next = Math.Min(currentKnowledge, Guard.Percent(decayBaselineKnowledge - loss));
            var stamp = UtcStamp.Format(effective);
            var changed = next != currentKnowledge || stamp != lastDecayCalculationUtc;
            currentKnowledge = next;
            lastDecayCalculationUtc = stamp;
            return changed;
        }
    }
}
