using System;
using System.Collections.Generic;
using System.Globalization;
using Team5.Rebuild.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Analytics
{
    public interface IAnalyticsSink
    {
        void Emit(AnalyticsEvent value);
    }

    public sealed class AnalyticsEvent
    {
        private readonly List<KeyValuePair<string, object>> fields = new List<KeyValuePair<string, object>>();
        public string Name { get; }
        public IReadOnlyList<KeyValuePair<string, object>> Fields => fields;
        public object this[string key] => TryGet(key, out var value) ? value : null;

        internal AnalyticsEvent(string name)
        {
            Name = name;
            fields.Add(new KeyValuePair<string, object>("event", name));
        }

        internal void Set(string key, object value)
        {
            for (var i = 0; i < fields.Count; i++)
                if (fields[i].Key == key) { fields[i] = new KeyValuePair<string, object>(key, value); return; }
            fields.Add(new KeyValuePair<string, object>(key, value));
        }

        public bool TryGet(string key, out object value)
        {
            foreach (var field in fields)
                if (field.Key == key) { value = field.Value; return true; }
            value = null;
            return false;
        }

        public string ToJson() => AnalyticsJson.Object(fields);
    }

    // Static facade. Every entry point swallows its own failures; analytics never changes gameplay.
    public static class GameAnalytics
    {
        private const string PlayerKey = "team5.analytics.player";
        private const string SessionCountKey = "team5.analytics.sessions";
        private static IAnalyticsSink sink;
        private static string playerId = "", sessionId = "", runId = "", build = "", sceneName = "";
        private static int level, attempt;
        private static long sequence;
        private static bool failureLogged;

        public static bool IsActive => sink != null;
        public static string RunId => runId;
        public static int Level => level;
        public static int Attempt => attempt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sink = null;
            playerId = sessionId = runId = build = sceneName = "";
            level = attempt = 0;
            sequence = 0;
            failureLogged = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            try
            {
                var settings = Resources.Load<AnalyticsSettings>(AnalyticsSettings.ResourceName);
                if (settings == null || !settings.Enabled) return;
                var send = settings.EndpointUrl.Length > 0 && (!Application.isEditor || settings.SendFromEditor);
                var log = Application.isEditor && settings.LogToConsole;
                if (!send && !log) return;
                var returning = PlayerPrefs.HasKey(PlayerKey);
                playerId = returning ? PlayerPrefs.GetString(PlayerKey) : Guid.NewGuid().ToString("N");
                var sessions = PlayerPrefs.GetInt(SessionCountKey, 0) + 1;
                PlayerPrefs.SetString(PlayerKey, playerId);
                PlayerPrefs.SetInt(SessionCountKey, sessions);
                PlayerPrefs.Save();
                sessionId = Guid.NewGuid().ToString("N");
                build = settings.BuildLabel.Length > 0 ? settings.BuildLabel + "-" + Application.version : Application.version;
                sceneName = SceneManager.GetActiveScene().name;
                sink = AnalyticsDispatcher.Create(settings, send, log);
                BattleAnalytics.Install();
                Track("session_start", ("returning_player", returning), ("session_count", sessions),
                    ("screen_w", Screen.width), ("screen_h", Screen.height));
            }
            catch (Exception error) { Fail(error); }
        }

        // Tests and tools inject a sink; null disables tracking.
        public static void SetSink(IAnalyticsSink value) => sink = value;

        public static void Track(string name, params (string key, object value)[] fields)
        {
            if (sink == null || string.IsNullOrEmpty(name)) return;
            try
            {
                var value = new AnalyticsEvent(name);
                AddCommonFields(value);
                if (fields != null)
                    foreach (var (key, field) in fields)
                        if (!string.IsNullOrEmpty(key)) value.Set(key, field);
                BattleAnalytics.Observe(value);
                PrepAnalytics.Observe(value);
                sink.Emit(value);
            }
            catch (Exception error) { Fail(error); }
        }

        // Battle-scoped events also carry the tracked attempt's sim time and route progress.
        public static void TrackBattle(string name, params (string key, object value)[] fields)
        {
            if (sink == null) return;
            try
            {
                var all = new (string key, object value)[(fields?.Length ?? 0) + 2];
                all[0] = ("sim_s", BattleAnalytics.SimSeconds);
                all[1] = ("progress_pct", BattleAnalytics.ProgressPct);
                if (fields != null) Array.Copy(fields, 0, all, 2, fields.Length);
                Track(name, all);
            }
            catch (Exception error) { Fail(error); }
        }

        public static void SetLevelContext(int levelNumber, int attemptNumber)
        {
            level = Math.Max(0, levelNumber);
            attempt = Math.Max(0, attemptNumber);
        }

        internal static void BeginRun() => runId = Guid.NewGuid().ToString("N");
        internal static void SetScene(string name) => sceneName = name ?? "";

        // Last known context, handed to the WebGL page so it can describe a tab close.
        internal static string ContextJson()
        {
            var value = new AnalyticsEvent("context");
            AddContextFields(value);
            value.Set("seq", sequence);
            return value.ToJson();
        }

        private static void AddCommonFields(AnalyticsEvent value)
        {
            value.Set("ts", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            AddContextFields(value);
            value.Set("seq", ++sequence);
        }

        private static void AddContextFields(AnalyticsEvent value)
        {
            value.Set("player_id", playerId);
            value.Set("session_id", sessionId);
            value.Set("run_id", runId);
            value.Set("build", build);
            value.Set("platform", Application.platform.ToString());
            value.Set("is_editor", Application.isEditor);
            value.Set("is_test_run", BattleSceneFlow.IsTestRun);
            value.Set("scene", sceneName.Length > 0 ? sceneName : SceneManager.GetActiveScene().name);
            value.Set("level", level);
            value.Set("attempt", attempt);
        }

        internal static void Fail(Exception error)
        {
            if (failureLogged) return;
            failureLogged = true;
            if (Application.isEditor) Debug.LogWarning("[Analytics] Dropped an event after an error; later errors stay silent.\n" + error);
        }
    }
}
