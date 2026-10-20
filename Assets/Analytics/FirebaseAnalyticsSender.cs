using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseAnalyticsSender : MonoBehaviour
{
    private static FirebaseAnalyticsSender _instance;
    public static FirebaseAnalyticsSender Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("FirebaseAnalyticsSender");
                _instance = go.AddComponent<FirebaseAnalyticsSender>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Firebase Configuration")]
    [SerializeField] private string firebaseProjectId = "reverse-td-7a708";

    private int _currentLevel = 1;
    private float _sessionStartTime;
    private int _unitsSpawned;
    private int _towersDestroyed;
    private bool _attemptActive;
    private bool _dataAlreadySent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        _sessionStartTime = Time.realtimeSinceStartup;
    }

    private static string PlayerId
    {
        get
        {
            const string key = "ReverseTD_PlayerId";
            string id = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(key, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    public void StartNewAttempt(int level)
    {
        _currentLevel = level;
        _sessionStartTime = Time.realtimeSinceStartup;
        _unitsSpawned = 0;
        _towersDestroyed = 0;
        _attemptActive = true;
        _dataAlreadySent = false;
    }

    public void UpdateCurrentAttemptMetrics(int level, int unitsSpawned, int towersDestroyed)
    {
        _currentLevel = level;
        _unitsSpawned = unitsSpawned;
        _towersDestroyed = towersDestroyed;
    }

    public void MarkCompleted(bool victory, string runId = "", int attemptNumber = 1)
    {
        if (_dataAlreadySent) return;
        _dataAlreadySent = true;
        _attemptActive = false;

        float duration = Time.realtimeSinceStartup - _sessionStartTime;
        SendAttemptData(_currentLevel, victory, duration, _unitsSpawned, _towersDestroyed, "completed", runId, attemptNumber);
    }

    private void OnApplicationQuit()
    {
        FlushQuitData();
    }

    private void FlushQuitData()
    {
        if (!_attemptActive || _dataAlreadySent) return;
        _dataAlreadySent = true;
        _attemptActive = false;

        float duration = Time.realtimeSinceStartup - _sessionStartTime;
        string runId = ReverseTD.Analytics.AttemptAnalytics.Current?.id ?? "";
        int attempts = 0;
        if (ReverseTD.Analytics.AttemptAnalytics.Current != null &&
            _currentLevel >= 1 &&
            _currentLevel <= ReverseTD.Analytics.AttemptAnalytics.Current.levels.Count)
        {
            attempts = ReverseTD.Analytics.AttemptAnalytics.Current.levels[_currentLevel - 1].Attempts + 1;
        }

        // Fire request directly without a `using` block so quitting does not immediately abort/dispose the upload
        UnityWebRequest request = CreateFirestoreRequest(_currentLevel, false, duration, _unitsSpawned, _towersDestroyed, "quit_midway", runId, attempts);
        request.SendWebRequest();
    }

    public void SendAttemptData(int levelIndex, bool victory, float durationSeconds, int totalDeployedUnits, int towersDestroyed, string exitStatus, string runId = "", int attemptNumber = 1)
    {
        StartCoroutine(PostAttemptCoroutine(levelIndex, victory, durationSeconds, totalDeployedUnits, towersDestroyed, exitStatus, runId, attemptNumber));
    }

    private IEnumerator PostAttemptCoroutine(int levelIndex, bool victory, float durationSeconds, int totalDeployedUnits, int towersDestroyed, string exitStatus, string runId, int attemptNumber)
    {
        using (UnityWebRequest request = CreateFirestoreRequest(levelIndex, victory, durationSeconds, totalDeployedUnits, towersDestroyed, exitStatus, runId, attemptNumber))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[FirebaseAnalytics] Failed ({request.responseCode}): {request.error}\nResponse: {request.downloadHandler.text}");
            }
            else
            {
                Debug.Log($"[FirebaseAnalytics] Sent attempt data for Level {levelIndex} (victory={victory}, attempt={attemptNumber}).");
            }
        }
    }

    private UnityWebRequest CreateFirestoreRequest(int levelIndex, bool victory, float durationSeconds, int totalDeployedUnits, int towersDestroyed, string exitStatus, string runId, int attemptNumber)
    {
        string url = $"https://firestore.googleapis.com/v1/projects/{firebaseProjectId}/databases/(default)/documents/attempt_analytics";

        string durationStr = durationSeconds.ToString("F2", CultureInfo.InvariantCulture);
        string timestampStr = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        string boolStr = victory ? "true" : "false";

        string runId2 = ReverseTD.Analytics.AttemptAnalytics.Current?.id ?? "unknown_run";
        string isEditorStr = Application.isEditor ? "true" : "false";

        string jsonBody = "{"
            + "\"fields\": {"
            + $"\"playerId\": {{\"stringValue\": \"{PlayerId}\"}},"
            + $"\"runId\": {{\"stringValue\": \"{runId2}\"}},"
            + $"\"isEditor\": {{\"booleanValue\": {isEditorStr}}},"
            + $"\"level\": {{\"integerValue\": \"{levelIndex}\"}},"
            + $"\"victory\": {{\"booleanValue\": {victory.ToString().ToLower()}}},"
            + $"\"durationSeconds\": {{\"doubleValue\": {durationSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}}},"
            + $"\"totalDeployedUnits\": {{\"integerValue\": \"{totalDeployedUnits}\"}},"
            + $"\"towersDestroyed\": {{\"integerValue\": \"{towersDestroyed}\"}},"
            + $"\"status\": {{\"stringValue\": \"{exitStatus}\"}},"
            + $"\"clientTimestamp\": {{\"timestampValue\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\"}}"
            + "}"
            + "}";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }
}