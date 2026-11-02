using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RiscVAdventure.Adventure
{
    public sealed class SaveLoadIssue
    {
        public string Path { get; }
        public string Reason { get; }
        internal SaveLoadIssue(string path, string reason) { Path = path; Reason = reason; }
    }

    public sealed class UnsupportedSaveVersionException : IOException
    {
        public UnsupportedSaveVersionException(int version) : base("Unsupported Adventure save version: " + version) { }
    }

    public sealed class AdventureProfileStore
    {
        public const int SchemaVersion = 1;
        public string RootDirectory { get; }
        public IReadOnlyList<SaveLoadIssue> LoadIssues => issues.AsReadOnly();
        private readonly List<SaveLoadIssue> issues = new List<SaveLoadIssue>();
        private bool indexIsFutureVersion;

        [Serializable] private sealed class ProfileDocument
        {
            public int schemaVersion;
            public AdventureProfile profile;
        }
        [Serializable] private sealed class IndexDocument
        {
            public int schemaVersion;
            public string activeProfileId;
        }

        public AdventureProfileStore(string rootDirectory)
        {
            RootDirectory = Path.GetFullPath(Guard.Text(rootDirectory, nameof(rootDirectory)));
            Directory.CreateDirectory(RootDirectory);
        }

        public string GetProfilePath(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid profile ID.", nameof(id));
            return Path.Combine(RootDirectory, id + ".json");
        }

        private string IndexPath => Path.Combine(RootDirectory, "index.json");

        internal List<AdventureProfile> Load(TopicCatalog catalog, out string activeId)
        {
            issues.Clear();
            indexIsFutureVersion = false;
            var profiles = new List<AdventureProfile>();
            // Ignore temporary writes. A valid .bak also allows recovery of an absent primary file.
            var ids = Directory.EnumerateFiles(RootDirectory, "*.json*")
                .Select(Path.GetFileName).Where(n => n.EndsWith(".json", StringComparison.Ordinal) ||
                    n.EndsWith(".json.bak", StringComparison.Ordinal))
                .Select(n => n.Substring(0, n.IndexOf('.')))
                .Where(n => Guid.TryParseExact(n, "N", out _)).Distinct().OrderBy(n => n, StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var path = GetProfilePath(id);
                var profile = ReadWithBackup(path, text =>
                {
                    var doc = JsonUtility.FromJson<ProfileDocument>(text);
                    ValidateVersion(doc?.schemaVersion ?? 0);
                    if (doc.profile == null) throw new InvalidDataException("Missing profile.");
                    doc.profile.Validate(id, catalog);
                    return doc.profile;
                });
                if (profile != null) profiles.Add(profile);
            }
            var index = ReadWithBackup(IndexPath, text =>
            {
                var doc = JsonUtility.FromJson<IndexDocument>(text);
                ValidateVersion(doc?.schemaVersion ?? 0);
                if (!string.IsNullOrEmpty(doc.activeProfileId) && !Guid.TryParseExact(doc.activeProfileId, "N", out _))
                    throw new InvalidDataException("Invalid active profile ID.");
                if (string.IsNullOrEmpty(doc.activeProfileId)) doc.activeProfileId = null;
                return doc;
            }, true);
            activeId = profiles.Any(p => p.Id == index?.activeProfileId) ? index.activeProfileId :
                profiles.OrderBy(p => p.CreatedUtc).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;
            return profiles;
        }

        private T ReadWithBackup<T>(string path, Func<string, T> read, bool index = false) where T : class
        {
            foreach (var candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try { return read(File.ReadAllText(candidate, Encoding.UTF8)); }
                catch (UnsupportedSaveVersionException ex)
                {
                    // Never fall back to an older save and later overwrite a newer schema.
                    if (index) indexIsFutureVersion = true;
                    issues.Add(new SaveLoadIssue(candidate, ex.Message));
                    return null;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is IOException ||
                                           ex is UnauthorizedAccessException || ex is OverflowException)
                {
                    issues.Add(new SaveLoadIssue(candidate, ex.Message));
                }
            }
            return null;
        }

        private static void ValidateVersion(int version)
        {
            if (version > SchemaVersion) throw new UnsupportedSaveVersionException(version);
            if (version != SchemaVersion) throw new InvalidDataException("Missing/invalid Adventure schema version.");
        }

        internal void Save(AdventureProfile profile)
        {
            var path = GetProfilePath(profile.Id);
            AtomicWrite(path, JsonUtility.ToJson(new ProfileDocument { schemaVersion = SchemaVersion, profile = profile }, true));
        }

        internal void SaveActive(string id)
        {
            EnsureIndexWritable();
            AtomicWrite(IndexPath, JsonUtility.ToJson(new IndexDocument { schemaVersion = SchemaVersion, activeProfileId = id }, true));
        }

        internal void EnsureIndexWritable()
        {
            if (indexIsFutureVersion) throw new UnsupportedSaveVersionException(SchemaVersion + 1);
        }

        internal bool CanAutoSave(string id) => File.Exists(GetProfilePath(id)) &&
            issues.All(i => i.Path != GetProfilePath(id));

        internal void Delete(string id)
        {
            var path = GetProfilePath(id);
            // Remove the backup as well, otherwise discovery would resurrect the deleted profile.
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path)) File.Delete(path);
        }

        private static void AtomicWrite(string path, string json)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var bytes = new UTF8Encoding(false).GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
