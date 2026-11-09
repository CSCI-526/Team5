using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Team5.Rebuild.Analytics
{
    // WebGL hands each event to Analytics.jslib, which batches and flushes on page exit.
    // Other players POST batches as text/plain so Apps Script needs no CORS preflight.
    public sealed class AnalyticsDispatcher : MonoBehaviour, IAnalyticsSink
    {
        private const int MaxTries = 2, MaxQueued = 1000, MaxBatch = 100;
        private readonly List<string> queue = new List<string>();
        private string url;
        private float interval, timer;
        private bool send, log, posting;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AnalyticsInit(string url, int intervalMs);
        [DllImport("__Internal")] private static extern void AnalyticsEnqueue(string json);
        [DllImport("__Internal")] private static extern void AnalyticsSetContext(string json);
        private static readonly bool UsesPage = true;
#else
        private static void AnalyticsInit(string url, int intervalMs) { }
        private static void AnalyticsEnqueue(string json) { }
        private static void AnalyticsSetContext(string json) { }
        private static readonly bool UsesPage = false;
#endif

        internal static AnalyticsDispatcher Create(AnalyticsSettings settings, bool send, bool log)
        {
            var host = new GameObject("Analytics Dispatcher");
            DontDestroyOnLoad(host);
            var dispatcher = host.AddComponent<AnalyticsDispatcher>();
            dispatcher.url = settings.EndpointUrl;
            dispatcher.interval = settings.FlushIntervalSeconds;
            dispatcher.send = send;
            dispatcher.log = log;
            if (send && UsesPage) AnalyticsInit(dispatcher.url, Mathf.RoundToInt(dispatcher.interval * 1000));
            return dispatcher;
        }

        public void Emit(AnalyticsEvent value)
        {
            var json = value.ToJson();
            if (log) Debug.Log("[Analytics] " + json);
            if (!send) return;
            if (UsesPage)
            {
                AnalyticsEnqueue(json);
                AnalyticsSetContext(GameAnalytics.ContextJson());
            }
            else if (queue.Count < MaxQueued) queue.Add(json);
        }

        private void LateUpdate()
        {
            BattleAnalytics.LateTick();
            if (!send || UsesPage) return;
            timer += Time.unscaledDeltaTime;
            if (timer >= interval) Flush();
        }

        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationQuit() => Flush();

        private void Flush()
        {
            timer = 0;
            if (!send || UsesPage || posting || queue.Count == 0) return;
            var count = Mathf.Min(MaxBatch, queue.Count);
            var batch = queue.GetRange(0, count);
            queue.RemoveRange(0, count);
            StartCoroutine(Post(AnalyticsJson.Batch(batch)));
        }

        private IEnumerator Post(string body)
        {
            posting = true;
            for (var tries = 1; tries <= MaxTries; tries++)
            {
                using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)) { contentType = "text/plain;charset=utf-8" };
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");
                    request.timeout = 15;
                    yield return request.SendWebRequest();
                    // Any HTTP answer means Apps Script ran; retrying would duplicate rows.
                    if (request.responseCode != 0) break;
                }
            }
            posting = false;
        }
    }
}
