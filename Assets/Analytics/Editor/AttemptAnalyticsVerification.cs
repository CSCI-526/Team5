using System;
using System.IO;
using System.Linq;
using ReverseTD.Analytics;
using UnityEditor;
using UnityEngine;

public static class AttemptAnalyticsVerification
{
    public static void Verify()
    {
        string path = AttemptAnalytics.SavePath;
        string backup = File.Exists(path) ? File.ReadAllText(path) : null;
        try
        {
            AttemptAnalytics.BeginRun();
            var data = ScriptableObject.CreateInstance<OffenseData>();
            data.ResetGame();
            string checkpoint = JsonUtility.ToJson(data);
            for (int i = 0; i < 3; i++)
            {
                AttemptAnalytics.BeginAttempt(1);
                AttemptAnalytics.FinishAttempt(false);
                AttemptAnalytics.FinishAttempt(false); // duplicate callbacks must be ignored
                data.RecordLevelDeath(1);
            }
            Check(AttemptAnalytics.Current.levels[0].Attempts == 3, "Three defeats counted once each");
            Check(data.IsLevelOutOfLives(1), "Level one is out of lives");
            Check(!data.IsLevelOutOfLives(2), "Level two lives are independent");
            data.gold = 10;
            Check(data.TryBuyLevelRetry(1), "Retry purchase succeeds");
            Check(data.GetLevelDeathCount(1) == 0, "Gameplay lives reset");
            Check(AttemptAnalytics.Current.levels[0].failures == 3, "Purchased lives preserve analytics");
            JsonUtility.FromJsonOverwrite(checkpoint, data);
            Check(AttemptAnalytics.Current.levels[0].failures == 3, "Tutorial restoration preserves analytics");
            AttemptAnalytics.BeginAttempt(1);
            AttemptAnalytics.FinishAttempt(true);
            AttemptAnalytics.FinishAttempt(true);
            Check(AttemptAnalytics.Current.levels[0].Attempts == 4, "Success adds one attempt exactly once");
            AttemptAnalytics.BeginAttempt(2);
            Check(AttemptAnalytics.Current.levels[1].Attempts == 0, "Unfinished attempt excluded");
            AttemptAnalytics.FinishAttempt(false);
            Check(AttemptAnalytics.Current.levels[0].Attempts == 4 && AttemptAnalytics.Current.levels[1].Attempts == 1, "Levels counted separately");
            Check(AttemptAnalytics.Current.levels[0].Attempts == 4, "Completed level shows failures plus successful attempt");
            Check(AttemptAnalytics.Current.levels[1].Attempts == 1, "Final failed attempt remains visible");
            Check(AttemptAnalytics.Current.levels[2].Attempts == 0, "Unreached level shows zero");
            Check(File.ReadAllText(path).Contains(AttemptAnalytics.Current.id), "Results saved to JSON");
            AttemptAnalytics.BeginRun();
            Check(AttemptAnalytics.Current.levels.All(x => x.Attempts == 0), "New run starts with fresh counts");
            UnityEngine.Object.DestroyImmediate(data);
            Debug.Log("ATTEMPT_ANALYTICS_VERIFICATION_PASSED");
        }
        finally
        {
            if (backup != null) File.WriteAllText(path, backup);
            else if (File.Exists(path)) File.Delete(path);
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Analytics verification failed: " + message);
        Debug.Log("PASS: " + message);
    }
}
