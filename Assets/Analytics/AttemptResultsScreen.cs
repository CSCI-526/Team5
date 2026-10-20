using ReverseTD.UpgradeUI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReverseTD.Analytics
{
    public sealed class AttemptResultsScreen : MonoBehaviour
    {
        public const string SceneName = "AttemptResults";
        [SerializeField] private OffenseData offenseData;

        public static void Open()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneName);
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Build();
        }

        private void ReturnToStart()
        {
            if (offenseData != null) offenseData.ResetGame();
            SceneManager.LoadScene("StartGame");
        }

        private void Build()
        {
            UiKit.CreateCanvas(gameObject, 3000, new Vector2(1280, 720), 0.5f);
            var backdrop = UiKit.CreateImage(transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(.025f,.035f,.055f,.98f));
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            UiKit.CreateText(transform, "Title", "ATTEMPTS PER LEVEL", new Vector2(0,245), new Vector2(1100,60), 40, Color.white, FontStyles.Bold);
            var run = AttemptAnalytics.Current;
            int max = 1;
            if (run != null) foreach (var level in run.levels) max = Mathf.Max(max, level.Attempts);
            for (int i = 0; i < 3; i++)
            {
                var result = run != null ? run.levels[i] : null;
                int attempts = result != null ? result.Attempts : 0;
                bool complete = result != null && result.completed;
                string status = complete ? "Completed" : result != null && result.failures > 0 ? "Not Completed" : "Not Reached";
                float y = 90 - i * 95;
                UiKit.CreateText(transform, "Level", "Level " + (i+1), new Vector2(-470,y), new Vector2(160,50), 26, Color.white);
                UiKit.CreateImage(transform, "Track", new Vector2(-40,y), new Vector2(570,38), UiKit.CellColor);
                float width = 570f * attempts / max;
                if (attempts > 0) UiKit.CreateImage(transform, "Attempts", new Vector2(-325 + width/2,y), new Vector2(width,38), complete ? UiKit.NodeGreenColor : UiKit.GoldColor);
                UiKit.CreateText(transform, "Count", attempts.ToString(), new Vector2(285,y), new Vector2(65,50), 28, Color.white);
                UiKit.CreateText(transform, "Status", status, new Vector2(440,y), new Vector2(230,50), 23, complete ? UiKit.DropColor : UiKit.MutedColor);
            }
            var button = UiKit.CreateImage(transform, "Continue", new Vector2(0,-295), new Vector2(300,55), UiKit.NodeBlueColor, 6);
            UiKit.CreateText(button.transform, "Label", "BACK TO START", Vector2.zero, new Vector2(290,50), 24, Color.white, FontStyles.Bold);
            UiKit.MakeButton(button).onClick.AddListener(ReturnToStart);
        }
    }
}
