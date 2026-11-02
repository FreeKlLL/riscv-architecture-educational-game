using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace RiscVAdventure.Tests.Adventure
{
    // Invoked through Unity MCP eval, or a developer's Editor script. Never enters Play Mode.
    [InitializeOnLoad]
    public static class AdventureSliceTestExecution
    {
        public static string LastSummary { get; private set; } = "Not run";

        [Serializable] private sealed class CaseResult
        {
            public string name;
            public string state;
            public string message;
        }
        [Serializable] private sealed class Report
        {
            public string state;
            public int passed, failed, skipped, inconclusive;
            public List<CaseResult> cases = new List<CaseResult>();
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor test) { LastSummary = "Running EditMode tests"; }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                var report = new Report { state = result.ResultState, passed = result.PassCount,
                    failed = result.FailCount, skipped = result.SkipCount, inconclusive = result.InconclusiveCount };
                Collect(result, report.cases);
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "development"));
                Directory.CreateDirectory(directory);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(directory, "adventure-slice-test-results.xml"));
                LastSummary = JsonUtility.ToJson(report, true);
                File.WriteAllText(Path.Combine(directory, "adventure-slice-test-results.json"), LastSummary);
            }
            private static void Collect(ITestResultAdaptor node, List<CaseResult> cases)
            {
                if (node.HasChildren) { foreach (var child in node.Children) Collect(child, cases); }
                else cases.Add(new CaseResult { name = node.FullName, state = node.ResultState, message = node.Message });
            }
        }

        static AdventureSliceTestExecution() { TestRunnerApi.RegisterTestCallback(new PlayResults()); }
        private static TestRunnerApi playApi;
        public static string RunPlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run from Edit Mode after compilation.");
            playApi = ScriptableObject.CreateInstance<TestRunnerApi>();

            return playApi.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
                assemblyNames = new[] { "Adventure.Slice.PlayMode.Tests" } }));
        }
        private static bool ContainsPlayTests(ITestResultAdaptor result) {
            if (result.FullName.Contains("AdventureSlicePlayModeTests")) return true;
            if (result.HasChildren) foreach (var child in result.Children) if (ContainsPlayTests(child)) return true;
            return false;
        }
        private sealed class PlayResults : ICallbacks
        {
            public void RunStarted(ITestAdaptor test) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!ContainsPlayTests(result)) return;
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath,"..","docs","development"));
                Directory.CreateDirectory(directory);
                TestRunnerApi.SaveResultToFile(result,Path.Combine(directory,"adventure-slice-playmode-results.xml"));
                File.WriteAllText(Path.Combine(directory,"adventure-slice-playmode-results.txt"),
                    result.ResultState + ": passed=" + result.PassCount + ", failed=" + result.FailCount + ", skipped=" + result.SkipCount + "\n" + result.Message);
            }
        }
        public static string RunAllEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run in Edit Mode after compilation.");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callback = new Results();
            api.RegisterCallbacks(callback);
            try
            {
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
                    assemblyNames = new[] { "Adventure.Foundation.Tests", "Tests" } }) { runSynchronously = true });
                return LastSummary;
            }
            finally { api.UnregisterCallbacks(callback); UnityEngine.Object.DestroyImmediate(api); }
        }
    }
}
