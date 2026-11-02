using System.IO;
using UnityEngine;

namespace RiscVAdventure.Adventure
{
    public static class AdventureRuntime
    {
        private static AdventureProfileService profiles;
        public static string StartupError { get; private set; }
        public static AdventureProfileService Profiles => profiles ?? (profiles = CreateDefault());

        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "AdventureMode", "profiles");

        private static AdventureProfileService CreateDefault() =>
            new AdventureProfileService(new AdventureProfileStore(SaveDirectory));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { profiles = null; StartupError = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadAtStartup()
        {
            try
            {
                var loaded = Profiles;
                foreach (var issue in loaded.LoadIssues)
                    Debug.LogWarning("Adventure save skipped/recovered: " + issue.Path + ": " + issue.Reason);
            }
            catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
            {
                // An unavailable Adventure directory must not prevent Classic scenes starting.
                StartupError = ex.Message;
                Debug.LogWarning("Adventure storage is unavailable: " + StartupError);
            }
        }
    }
}
