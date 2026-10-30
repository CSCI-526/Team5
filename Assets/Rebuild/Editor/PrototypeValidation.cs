using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    // A normal Unity Test Framework entry point for an already-open Editor.
    [InitializeOnLoad]
    public static class PrototypeValidation
    {
        private const string PendingKey = "Team5.Rebuild.ValidationPending";
        private static readonly TestRunnerApi Runner;
        static PrototypeValidation()
        {
            Runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            Runner.RegisterCallbacks(new Results());
        }

        [MenuItem("Team5/Rebuild/Run Prototype EditMode Tests")]
        public static void RunEditMode() => Run(TestMode.EditMode);

        [MenuItem("Team5/Rebuild/Run Prototype PlayMode Tests")]
        public static void RunPlayMode() => Run(TestMode.PlayMode);

        private static void Run(TestMode mode)
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before running tests.");
            SessionState.SetBool(PendingKey, true);
            SessionState.SetString(PendingKey + ".Report", $"Logs/MinimalPresentation{mode}Results.xml");
            Runner.Execute(new ExecutionSettings(new Filter
            {
                testMode = mode,
                assemblyNames = new[] { "Team5.Rebuild.EditModeTests", "Team5.Rebuild.PlayModeTests" }
            }));
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                if (!SessionState.GetBool(PendingKey, false)) return;
                Directory.CreateDirectory("Logs");
                TestRunnerApi.SaveResultToFile(result, SessionState.GetString(PendingKey + ".Report", "Logs/MinimalPresentationResults.xml"));
                SessionState.SetBool(PendingKey, false);
                Debug.Log($"Prototype validation: {result.ResultState}, passed {result.PassCount}, failed {result.FailCount}.");
            }
        }
    }
}
