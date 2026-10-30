using Team5.Rebuild.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Adapted from Team5 HowToPlay.ShowLevelOneDefeat and OutOfLivesScreen.
    public sealed class LegacyLevelOneRecovery : MonoBehaviour
    {
        private BattleSession session;
        public static GameObject Open(BattleSession session)
        {
            var tutorial = session.GetComponent<LevelOneTutorial>();
            if (tutorial != null) tutorial.SuspendForDefeat();
            var host = new GameObject("Level Recovery", typeof(RectTransform));
            // A nested Canvas inherits the HUD's reference scale and ignores its own scaler.
            // Parent under the non-UI session so this is a root Canvas with its own sizing.
            host.transform.SetParent(session.transform, false);
            var view = host.AddComponent<LegacyLevelOneRecovery>();
            view.session = session;
            view.Build();
            return host;
        }

        private void Build()
        {
            LegacyUiKit.CreateCanvas(gameObject, 2000, new Vector2(1280, 720), 0.5f);
            var backdrop = LegacyUiKit.CreateImage(transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.025f, 0.035f, 0.055f, 0.95f));
            LegacyUiKit.Stretch(backdrop.rectTransform); backdrop.raycastTarget = true;
            var card = LegacyUiKit.CreateImage(transform, "Recovery Card", Vector2.zero, new Vector2(760, 400), LegacyUiKit.PanelColor, 12).transform;
            var exhausted = session.AttemptsRemaining == 0;
            LegacyUiKit.CreateText(card, "Title", exhausted ? "OUT OF LIVES" : "SQUAD DEFEATED", new Vector2(0, 140), new Vector2(700, 55), 34, Color.white, FontStyles.Bold);
            LegacyUiKit.CreateText(card, "Description", exhausted
                ? $"You have used all {session.AttemptsPerLevel} lives.\nExit and restart the game to try again."
                : $"Lives: {session.AttemptsRemaining}/{session.AttemptsPerLevel}\nRestart, or return to the last tutorial hint.\nRewinding does not refund a lost life.",
                new Vector2(0, 45), new Vector2(700, 110), 23, LegacyUiKit.TextColor);
            if (!exhausted)
            {
                Button(card, "Restart", "RESTART LEVEL", new Vector2(-170, -50), BattleSceneFlow.RestartCurrentLevel);
                var rewind = Button(card, "Rewind", "BACK TO LAST HINT", new Vector2(170, -50), () => BattleSceneFlow.RewindLevelOne());
                rewind.interactable = session.HasTutorialCheckpoint;
            }
            Button(card, "Exit", "EXIT & RESTART GAME", new Vector2(0, -140), BattleSceneFlow.ReturnToStart);
        }

        private static Button Button(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var button = LegacyUiKit.CreateButton(parent, name, position, new Vector2(320, 58), LegacyUiKit.NodeBlueColor, 6);
            LegacyUiKit.CreateText(button.transform, "Label", label, Vector2.zero, new Vector2(310, 54), 21, Color.white, FontStyles.Bold);
            button.onClick.AddListener(action);
            return button;
        }

        private void Update()
        {
            if (session != null && session.RecoveryRequired) return;
            gameObject.SetActive(false); Destroy(gameObject);
        }
    }
}
