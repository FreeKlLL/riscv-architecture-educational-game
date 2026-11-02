using System;
using System.Linq;

namespace RiscVAdventure.Adventure
{
    // One atomic profile write commits knowledge, route and the submission receipt together.
    public sealed class AdventureRegisseur
    {
        private readonly AdventureProfileService profiles;
        private readonly AdventureSliceConfiguration config;
        public AdventureRegisseur(AdventureProfileService profiles, AdventureSliceConfiguration config)
        { this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles)); this.config = config ?? throw new ArgumentNullException(nameof(config)); config.Validate(); }
        public AdventureExercise Exercise(string id) => config.exercises.First(e => e.id == id);
        public void Begin(string id) => profiles.Mutate(id, p => {
            profiles.ApplyDecay(p);
            var j = p.Journey;
            if (j.stage != AdventureStage.NotStarted) return;
            if (p.GetTopic(TopicIds.Registers).IsTopicCompleted) { Unavailable(j); return; }
            j.stage = AdventureStage.Diagnostic;
        });
        public void Answer(string id, int expectedIndex, int answer) => profiles.Mutate(id, p => {
            var j = p.Journey;
            if (j.stage != AdventureStage.Diagnostic || expectedIndex != j.diagnosticAnswers.Count) throw new InvalidOperationException("Stale diagnostic answer.");
            var q = config.questions[expectedIndex];
            if (answer < 0 || answer >= q.answersEnglish.Length) throw new ArgumentOutOfRangeException(nameof(answer));
            j.diagnosticAnswers.Add(answer);
            if (j.diagnosticAnswers.Count != config.questions.Length) return;
            j.diagnosticScore = 100.0 * j.diagnosticAnswers.Where((a, i) => a == config.questions[i].correctAnswer).Count() / config.questions.Length;
            var topic = p.GetTopic(TopicIds.Registers);
            topic.Assess(j.diagnosticScore, profiles.NowFor(p));
            var chapter = p.GetChapter(TopicIds.Registers); chapter.RecordDiagnostic();
            bool skip = j.diagnosticScore >= config.skipLessonThreshold;
            chapter.RecordChoice(skip ? LessonChoice.SkipLesson : LessonChoice.TakeLesson);
            if (skip) Assign(j, config.ExercisesFor(TopicIds.Registers)[0].id); else j.stage = AdventureStage.Lesson;
        });
        public void SaveLessonNode(string id, int node) => profiles.Mutate(id, p => {
            if (p.Journey.stage == AdventureStage.Lesson) p.Journey.lessonNode = Math.Max(0, node);
        });
        public void CompleteLesson(string id) => profiles.Mutate(id, p => {
            if (p.Journey.stage != AdventureStage.Lesson) return;
            p.GetTopic(TopicIds.Registers).CompleteLesson(profiles.NowFor(p));
            Assign(p.Journey, config.ExercisesFor(TopicIds.Registers)[0].id);
        });
        public void Hint(string id) => profiles.Mutate(id, p => { if (p.Journey.stage == AdventureStage.Practice) p.Journey.hints++; });
        public bool Submit(string id, string exerciseId, string attemptId, string submissionId, bool success)
        {
            Guard.Text(submissionId, nameof(submissionId)); bool accepted = false;
            profiles.Mutate(id, p => {
                var j = p.Journey;
                if (j.processedSubmissions.Contains(submissionId)) return;
                if (j.stage != AdventureStage.Practice || j.assignedExerciseId != exerciseId || j.attemptId != attemptId) return;
                accepted = true; j.processedSubmissions.Add(submissionId); profiles.ApplyDecay(p);
                var topic = p.GetTopic(TopicIds.Registers); var exercise = Exercise(exerciseId);
                double current = topic.CurrentKnowledge, mastery = topic.Mastery;
                if (!success) {
                    j.errors++;
                    double loss = Math.Min(config.failurePenalty, Math.Max(0, config.maxFailureLoss - j.failureLoss));
                    j.failureLoss += loss; current -= loss;
                } else {
                    j.lastReward = j.completedExercises.Contains(exerciseId) ? 0 : Math.Max(0, config.Reward(exercise.difficulty) - j.errors * config.errorPenalty - j.hints * config.hintPenalty);
                    current += j.lastReward; mastery = Math.Max(mastery, current);
                    if (!j.completedExercises.Contains(exerciseId)) j.completedExercises.Add(exerciseId);
                    j.lastExerciseId = exerciseId;
                    j.lastErrors = j.errors; j.lastHints = j.hints;
                }
                topic.Practice(new KnowledgeEvaluation(current, mastery), success, profiles.NowFor(p));
                p.AddPractice(new PracticeRecord(new PracticeEvidence(TopicIds.Registers, exerciseId, "classic", exercise.difficulty, j.errors, success, j.hints), profiles.NowFor(p)));
                if (!success) return;
                var remaining = config.ExercisesFor(TopicIds.Registers).Where(e => !j.completedExercises.Contains(e.id)).ToArray();
                if (remaining.Length == 0) { topic.CompleteTopic(); Unavailable(j); }
                else {
                    var next = topic.Mastery >= config.skipLessonThreshold ? remaining.OrderByDescending(e => e.difficulty).First() : remaining.OrderBy(e => e.difficulty).First();
                    Assign(j, next.id);
                }
                j.stage = AdventureStage.Result;
            });
            return accepted;
        }
        public void Continue(string id) => profiles.Mutate(id, p => {
            if (p.Journey.stage == AdventureStage.Result) p.Journey.stage = p.Journey.topicId == TopicIds.Registers ? AdventureStage.Practice : AdventureStage.Unavailable;
        });
        private static void Assign(AdventureJourney j, string exercise)
        { j.assignedExerciseId = exercise; j.attemptId = Guid.NewGuid().ToString("N"); j.errors = j.hints = 0; j.failureLoss = 0; j.stage = AdventureStage.Practice; }
        private void Unavailable(AdventureJourney j)
        { j.topicId = config.chapters[1].topicId; j.assignedExerciseId = null; j.attemptId = null; j.stage = AdventureStage.Unavailable; }
    }
}
