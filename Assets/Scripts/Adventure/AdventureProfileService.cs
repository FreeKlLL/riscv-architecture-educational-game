using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RiscVAdventure.Adventure
{
    // Synchronous API intended for the Unity main thread / one application instance.
    // Every successful mutation is saved; callers receive read-only domain objects.
    public sealed class AdventureProfileService
    {
        private readonly AdventureProfileStore store;
        private readonly IUtcClock clock;
        private readonly KnowledgeDecaySettings decay;
        private readonly TopicCatalog catalog;
        private readonly Dictionary<string, AdventureProfile> profiles;
        public string ActiveProfileId { get; private set; }
        public AdventureProfile ActiveProfile => ActiveProfileId == null ? null : GetProfile(ActiveProfileId);
        public IReadOnlyList<AdventureProfile> Profiles => Array.AsReadOnly(profiles.Values
            .OrderBy(p => p.CreatedUtc).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray());
        public IReadOnlyList<SaveLoadIssue> LoadIssues => store.LoadIssues;

        public AdventureProfileService(AdventureProfileStore store, IUtcClock clock = null,
            KnowledgeDecaySettings decay = null, TopicCatalog catalog = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? new SystemUtcClock();
            this.decay = decay ?? new KnowledgeDecaySettings();
            this.catalog = catalog ?? TopicCatalog.Default;
            UtcStamp.Format(this.clock.UtcNow);
            profiles = store.Load(this.catalog, out var active).ToDictionary(p => p.Id, StringComparer.Ordinal);
            ActiveProfileId = active;
            // Cumulative anchored calculation makes loading idempotent. Do not overwrite
            // a damaged primary merely because a backup was recovered during startup.
            foreach (var profile in profiles.Values)
            {
                var changed = ApplyDecay(profile);
                if (changed && store.CanAutoSave(profile.Id)) store.Save(profile);
            }
        }

        public AdventureProfile GetProfile(string id) => profiles.TryGetValue(id, out var profile) ? profile :
            throw new KeyNotFoundException("Unknown profile: " + id);

        public AdventureProfile CreateProfile(string name)
        {
            store.EnsureIndexWritable();
            var profile = new AdventureProfile(name, clock.UtcNow, catalog);
            store.Save(profile);
            if (ActiveProfileId == null) store.SaveActive(profile.Id);
            profiles.Add(profile.Id, profile);
            if (ActiveProfileId == null) ActiveProfileId = profile.Id;
            return profile;
        }

        public void SelectProfile(string id)
        {
            GetProfile(id);
            store.EnsureIndexWritable();
            RefreshKnowledge(id);
            store.SaveActive(id);
            ActiveProfileId = id;
        }

        public void RenameProfile(string id, string name) => Mutate(id, p => p.Rename(name));

        public bool DeleteProfile(string id, bool confirmDeletion)
        {
            if (!confirmDeletion) return false;
            GetProfile(id);
            store.EnsureIndexWritable();
            var next = ActiveProfileId == id ? Profiles.FirstOrDefault(p => p.Id != id)?.Id : ActiveProfileId;
            store.SaveActive(next);
            store.Delete(id);
            profiles.Remove(id);
            ActiveProfileId = next;
            return true;
        }

        public void SaveProfile(string id) => store.Save(GetProfile(id));

        public void RefreshKnowledge(string id) => Mutate(id, p => ApplyDecay(p));

        public void RecordAssessment(string id, string chapterId, string topicId, double score) => Mutate(id, p =>
        {
            var topic = p.GetTopic(topicId);
            var chapter = p.EnsureChapter(chapterId, topicId);
            topic.Assess(score, NowFor(p));
            chapter.RecordDiagnostic();
        });

        public void CompleteLesson(string id, string topicId) =>
            Mutate(id, p => p.GetTopic(topicId).CompleteLesson(NowFor(p)));

        // Completion is an explicit decision by the future Regisseur, never a side effect of decay.
        public void CompleteTopic(string id, string topicId) => Mutate(id, p => p.GetTopic(topicId).CompleteTopic());

        public void RecordLessonChoice(string id, string chapterId, LessonChoice choice) =>
            Mutate(id, p => p.GetChapter(chapterId).RecordChoice(choice));

        public void RecordPractice(string id, PracticeEvidence evidence, IKnowledgeEvaluator evaluator)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));
            Mutate(id, p =>
            {
                ApplyDecay(p);
                var topic = p.GetTopic(evidence.TopicId);
                var evaluation = evaluator.Evaluate(topic, evidence)
                    ?? throw new InvalidOperationException("Evaluator returned no result.");
                var now = NowFor(p);
                topic.Practice(evaluation, evidence.IsSuccessful, now);
                var recorded = topic.LastDecayCalculationUtc.HasValue && topic.LastDecayCalculationUtc.Value > now
                    ? topic.LastDecayCalculationUtc.Value : now;
                p.AddPractice(new PracticeRecord(evidence, recorded));
            });
        }

        public bool HasCompletedAdventure(string id) => GetProfile(id).HasCompletedAdventure(catalog);

        internal bool ApplyDecay(AdventureProfile profile)
        {
            var changed = false;
            var now = NowFor(profile);
            foreach (var topic in profile.Topics) changed |= topic.ApplyDecay(now, decay);
            return changed;
        }

        internal DateTime NowFor(AdventureProfile profile)
        {
            var now = clock.UtcNow;
            UtcStamp.Format(now);
            if (profile.CreatedUtc > now) now = profile.CreatedUtc;
            foreach (var topic in profile.Topics)
                if (topic.LastDecayCalculationUtc.HasValue && topic.LastDecayCalculationUtc.Value > now)
                    now = topic.LastDecayCalculationUtc.Value;
            return now;
        }

        internal void Mutate(string id, Action<AdventureProfile> action)
        {
            var profile = GetProfile(id);
            var snapshot = JsonUtility.ToJson(profile);
            try { action(profile); store.Save(profile); }
            catch
            {
                // A failed mutation must not leak half-applied chapter / topic state through the service.
                profiles[id] = JsonUtility.FromJson<AdventureProfile>(snapshot);
                throw;
            }
        }
    }
}
