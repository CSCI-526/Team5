using ReverseTD.Offense;
using ReverseTD.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ReverseTD.UpgradeUI
{
    public sealed class OutOfLivesScreen : MonoBehaviour
    {
        private OffenseWaveManager owner;
        private OffenseData data;
        private int level;
        private Button retryButton;
        private TMP_Text balance;
        private bool submitted;

        public static void Open(OffenseWaveManager owner, OffenseData data, int level)
        {
            HowToPlay.SuspendBattleTutorial();
            var host = new GameObject("Out Of Lives Screen", typeof(RectTransform));
            host.transform.SetParent(owner.transform, false);
            var screen = host.AddComponent<OutOfLivesScreen>();
            screen.owner = owner;
            screen.data = data;
            screen.level = level;
            screen.Build();
        }

        private void Build()
        {
            UiKit.CreateCanvas(gameObject, 2000, new Vector2(1280f, 720f), 0.5f);
            Image backdrop = UiKit.CreateImage(transform, "Backdrop", Vector2.zero, Vector2.zero,
                new Color(0.025f, 0.035f, 0.055f, 0.98f));
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            Image card = UiKit.CreateImage(transform, "Out Of Lives Card", Vector2.zero, new Vector2(760f, 440f), UiKit.PanelColor, 12f);
            UiKit.CreateText(card.transform, "Title", "OUT OF LIVES", new Vector2(0f, 155f), new Vector2(680f, 55f),
                38f, new Color(1f, 0.3f, 0.35f), FontStyles.Bold);
            UiKit.CreateText(card.transform, "Message", $"LEVEL {level}\nYou have used all {OffenseData.LivesPerLevel} lives.",
                new Vector2(0f, 85f), new Vector2(680f, 70f), 25f, Color.white);
            balance = UiKit.CreateText(card.transform, "Balance", "", new Vector2(0f, 15f), new Vector2(680f, 55f),
                21f, UiKit.GoldColor);

            Image retryImage = UiKit.CreateImage(card.transform, "Retry Level", new Vector2(0f, -65f), new Vector2(620f, 60f),
                UiKit.NodeGreenColor, 6f);
            retryButton = UiKit.MakeButton(retryImage);
            UiKit.CreateText(retryImage.transform, "Label", $"RETRY LEVEL - {OffenseData.LevelRetryCost} GOLD",
                Vector2.zero, new Vector2(600f, 55f), 25f, Color.white, FontStyles.Bold);
            retryButton.onClick.AddListener(RetryLevel);

            Image exitImage = UiKit.CreateImage(card.transform, "Exit", new Vector2(0f, -145f), new Vector2(620f, 60f),
                UiKit.NodeGreyColor, 6f);
            Button exitButton = UiKit.MakeButton(exitImage);
            UiKit.CreateText(exitImage.transform, "Label", "EXIT & RESTART GAME", Vector2.zero, new Vector2(600f, 55f),
                25f, Color.white, FontStyles.Bold);
            exitButton.onClick.AddListener(ExitGame);
            RefreshBalance();
        }

        private void Update()
        {
            if (!submitted) RefreshBalance();
        }

        private void RefreshBalance()
        {
            bool canRetry = data != null && data.IsLevelOutOfLives(level) && data.gold >= OffenseData.LevelRetryCost;
            retryButton.interactable = canRetry;
            string text = $"Gold: {(data != null ? data.gold : 0):N0}   |   Retry cost: {OffenseData.LevelRetryCost}";
            if (!canRetry) text += "\nNot enough gold. Restart the game to try again.";
            if (balance.text != text) balance.text = text;
        }

        private void RetryLevel()
        {
            if (submitted || data == null || !data.TryBuyLevelRetry(level)) return;
            submitted = true;
            if (owner.gameObject.scene.name == "DefenseSandbox") HowToPlay.ResetLevelOneTutorial();
            gameObject.SetActive(false);
            SceneManager.LoadScene(owner.gameObject.scene.name);
        }

        private void ExitGame()
        {
            if (submitted) return;
            submitted = true;
            if (data != null) data.ResetGame();
            gameObject.SetActive(false);
            SceneManager.LoadScene("StartGame");
        }
    }
}
