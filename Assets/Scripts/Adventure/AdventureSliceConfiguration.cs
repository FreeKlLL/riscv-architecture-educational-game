using System;
using System.Linq;
using UnityEngine;

namespace RiscVAdventure.Adventure
{
    [Serializable] public sealed class DiagnosticQuestion
    {
        public string english, german;
        public string[] answersEnglish, answersGerman;
        public int correctAnswer;
    }
    [Serializable] public sealed class AdventureExercise
    {
        public string id, topicId, scenePath;
        public ExerciseDifficulty difficulty;
    }
    [Serializable] public sealed class AdventureChapterDefinition
    {
        public string topicId;
        public bool implemented;
        public string[] exerciseIds;
    }
    [CreateAssetMenu(menuName = "Adventure/Playable slice")]
    public sealed class AdventureSliceConfiguration : ScriptableObject
    {
        public double skipLessonThreshold = 80;
        public double easyReward = 5, mediumReward = 8, hardReward = 12;
        public double errorPenalty = 1, hintPenalty = 2, failurePenalty = 2, maxFailureLoss = 10;
        public Sprite charlie;
        public AdventureChapterDefinition[] chapters = {
            new AdventureChapterDefinition {topicId=TopicIds.Registers,implemented=true,exerciseIds=new[]{"registers.transfer","registers.hold","registers.negative"}},
            new AdventureChapterDefinition {topicId=TopicIds.Multiplexer}, new AdventureChapterDefinition {topicId=TopicIds.Alu},
            new AdventureChapterDefinition {topicId=TopicIds.Memory}, new AdventureChapterDefinition {topicId=TopicIds.Extender},
            new AdventureChapterDefinition {topicId=TopicIds.RegisterFile}, new AdventureChapterDefinition {topicId=TopicIds.Eintaktprozessor},
            new AdventureChapterDefinition {topicId=TopicIds.Mehrtaktprozessor}
        };
        public DiagnosticQuestion[] questions = {
            new DiagnosticQuestion { english = "Charlie: Before we begin, what does a register do at a clock edge when WE = 0?", german = "Charlie: Bevor wir anfangen: Was macht ein Register an der Taktflanke bei WE = 0?", answersEnglish = new[] { "It keeps its previous value.", "It always becomes zero.", "It copies its input." }, answersGerman = new[] { "Es behält seinen bisherigen Wert.", "Es wird immer null.", "Es übernimmt den Eingang." }, correctAnswer = 0 },
            new DiagnosticQuestion { english = "Three registers form a chain: R1 = 4, R2 = 0, R3 = 0. All WE signals are 1. What is R3 immediately after one clock edge?", german = "Drei Register bilden eine Kette: R1 = 4, R2 = 0, R3 = 0. Alle WE-Signale sind 1. Welchen Wert hat R3 direkt nach einer Taktflanke?", answersEnglish = new[] { "4", "0: it takes the old value of R2.", "The value is undefined." }, answersGerman = new[] { "4", "0: Es übernimmt den alten Wert von R2.", "Der Wert ist undefiniert." }, correctAnswer = 1 },
            new DiagnosticQuestion { english = "With the same initial chain, how many clock edges transfer the original 4 from R1 into R3?", german = "Wie viele Taktflanken braucht dieselbe Kette, um die ursprüngliche 4 von R1 nach R3 zu übertragen?", answersEnglish = new[] { "None", "One", "Two" }, answersGerman = new[] { "Keine", "Eine", "Zwei" }, correctAnswer = 2 }
        };
        public AdventureExercise[] exercises = {
            new AdventureExercise { id = "registers.transfer", topicId = TopicIds.Registers, scenePath = "Assets/Scenes/Levels/RegisterLevels/Level 0.unity", difficulty = ExerciseDifficulty.Introductory },
            new AdventureExercise { id = "registers.hold", topicId = TopicIds.Registers, scenePath = "Assets/Scenes/Levels/RegisterLevels/Level 0-1.unity", difficulty = ExerciseDifficulty.Standard },
            new AdventureExercise { id = "registers.negative", topicId = TopicIds.Registers, scenePath = "Assets/Scenes/Levels/RegisterLevels/Level 0-2.unity", difficulty = ExerciseDifficulty.Advanced }
        };
        public double Reward(ExerciseDifficulty difficulty) => difficulty == ExerciseDifficulty.Introductory ? easyReward : difficulty == ExerciseDifficulty.Standard ? mediumReward : hardReward;
        public AdventureExercise[] ExercisesFor(string topicId) => chapters.First(c => c.topicId == topicId).exerciseIds.Select(id => exercises.First(e => e.id == id)).ToArray();
        public void Validate()
        {
            if (questions == null || questions.Length == 0 || exercises == null || exercises.Length < 2) throw new InvalidOperationException("Adventure configuration is incomplete.");
            foreach (var q in questions)
                if (q == null || q.answersEnglish == null || q.answersGerman == null || q.answersEnglish.Length != q.answersGerman.Length || q.correctAnswer < 0 || q.correctAnswer >= q.answersEnglish.Length)
                    throw new InvalidOperationException("Invalid diagnostic question.");
            if (Guard.Percent(skipLessonThreshold) != skipLessonThreshold) throw new InvalidOperationException("Invalid diagnostic threshold.");
            foreach (var number in new[] {easyReward, mediumReward, hardReward, errorPenalty, hintPenalty, failurePenalty, maxFailureLoss})
                if (double.IsNaN(number) || double.IsInfinity(number) || number < 0) throw new InvalidOperationException("Invalid scoring parameter.");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var e in exercises)
                if (e == null || !ids.Add(Guard.Text(e.id, "exercise")) || string.IsNullOrWhiteSpace(e.scenePath) || string.IsNullOrWhiteSpace(e.topicId))
                    throw new InvalidOperationException("Invalid exercise catalog.");
            if (chapters == null || chapters.Length < 2 || chapters[0].topicId != TopicIds.Registers || !chapters[0].implemented || chapters.Select(c=>c.topicId).Distinct().Count()!=chapters.Length)
                throw new InvalidOperationException("Invalid chapter route.");
            foreach (var chapter in chapters.Where(c=>c.implemented))
                if (chapter.exerciseIds == null || chapter.exerciseIds.Length == 0 || chapter.exerciseIds.Distinct().Count()!=chapter.exerciseIds.Length || chapter.exerciseIds.Any(id=>!exercises.Any(e=>e.id==id&&e.topicId==chapter.topicId)))
                    throw new InvalidOperationException("Chapter has missing or duplicate exercises.");
        }
    }
}
