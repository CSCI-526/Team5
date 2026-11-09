using UnityEngine;

namespace Team5.Rebuild.Analytics
{
    // Loaded from Resources/RebuildAnalytics. An empty endpoint disables network sends.
    [CreateAssetMenu(menuName = "Team5/Rebuild/Analytics Settings")]
    public sealed class AnalyticsSettings : ScriptableObject
    {
        public const string ResourceName = "RebuildAnalytics";
        [SerializeField] private string endpointUrl = "";
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool sendFromEditor;
        [SerializeField] private bool logToConsole = true;
        [SerializeField] private string buildLabel = "beta";
        [SerializeField, Min(1)] private float flushIntervalSeconds = 5;
        public string EndpointUrl => endpointUrl != null ? endpointUrl.Trim() : "";
        public bool Enabled => enabled;
        public bool SendFromEditor => sendFromEditor;
        public bool LogToConsole => logToConsole;
        public string BuildLabel => buildLabel ?? "";
        public float FlushIntervalSeconds => Mathf.Max(1, flushIntervalSeconds);
    }
}
