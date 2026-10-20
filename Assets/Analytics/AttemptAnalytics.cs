using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ReverseTD.Analytics
{
    // Separate from OffenseData: checkpoints and purchased lives cannot reset history.
    public static class AttemptAnalytics
    {
        [Serializable] public class LevelResult
        {
            public int level;
            public int failures;
            public bool completed;
            public int Attempts => failures + (completed ? 1 : 0);
        }
        [Serializable] public class Run
        {
            public string id;
            public List<LevelResult> levels = new List<LevelResult>();
        }
        [Serializable] private class Archive { public List<Run> runs = new List<Run>(); }
        private static Archive archive;
        private static Run current;
        private static int activeLevel;
        private static bool resolved;
        public static string SavePath => Path.Combine(Application.persistentDataPath, "attempts-per-level.json");
        public static Run Current => current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { archive = null; current = null; activeLevel = 0; resolved = true; }

        public static void BeginRun()
        {
            Load();
            current = new Run { id = Guid.NewGuid().ToString("N") };
            for (int i = 1; i <= 3; i++) current.levels.Add(new LevelResult { level = i });
            archive.runs.Add(current);
            resolved = true;
            Save();
        }
        public static void BeginAttempt(int level)
        {
            if (current == null) BeginRun();
            activeLevel = level;
            resolved = false;

            if (Application.isPlaying)
            {
                FirebaseAnalyticsSender.Instance.StartNewAttempt(level);
            }
        }

        public static void FinishAttempt(bool success)
        {
            if (current == null || resolved || activeLevel < 1 || activeLevel > 3) return;
            resolved = true;
            LevelResult result = current.levels[activeLevel - 1];
            if (result.completed) return;
            if (success) result.completed = true;
            else result.failures++;
            Save();

            if (Application.isPlaying)
            {
                FirebaseAnalyticsSender.Instance.MarkCompleted(success, current.id, result.Attempts);
            }
        }
        private static void Load()
        {
            if (archive != null) return;
            try { if (File.Exists(SavePath)) archive = JsonUtility.FromJson<Archive>(File.ReadAllText(SavePath)); }
            catch (Exception e) { Debug.LogWarning("Could not read attempts history: " + e.Message); }
            if (archive == null) archive = new Archive();
        }
        private static void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(archive, true)); }
            catch (Exception e) { Debug.LogWarning("Could not save attempts history: " + e.Message); }
        }

    }
}
