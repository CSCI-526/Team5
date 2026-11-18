using Team5.Rebuild.Analytics;
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
            var backdrop = LegacyUiKit.CreateImage(transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(LegacyUiKit.SlateDark.r, LegacyUiKit.SlateDark.g, LegacyUiKit.SlateDark.b, 0.82f));
            LegacyUiKit.Stretch(backdrop.rectTransform); backdrop.raycastTarget = true;
            // Paint Squad defeat card: cream board under a slate ribbon.
            var face = LegacyUiKit.CreateCard(transform, "Recovery Card", Vector2.zero, new Vector2(760, 400), 26);
            var card = face.transform.parent;
            LegacyUiKit.CreateShadow((RectTransform)card);
            var exhausted = session.AttemptsRemaining == 0;
            var ribbon = LegacyUiKit.CreateRibbon(card, "Title", exhausted ? "OUT OF LIVES" : "SQUAD DEFEATED", new Vector2(0, 204), new Vector2(560, 84), LegacyUiKit.Slate, 40);
            ribbon.name = "Title Ribbon";
            LegacyUiKit.CreateText(card, "Description", exhausted
                ? $"You have used all {session.AttemptsPerLevel} lives.\nExit and restart the game to try again."
                : $"Lives: {session.AttemptsRemaining}/{session.AttemptsPerLevel}\nRestart, or return to the last tutorial hint.\nRewinding does not refund a lost life.",
                new Vector2(0, 58), new Vector2(700, 110), 24, LegacyUiKit.Ink);
            if (!exhausted)
            {
                Button(card, "Restart", "RESTART LEVEL", new Vector2(-170, -50), BattleSceneFlow.RestartCurrentLevel);
                var rewind = Button(card, "Rewind", "BACK TO LAST HINT", new Vector2(170, -50), () => BattleSceneFlow.RewindLevelOne());
                rewind.interactable = session.HasTutorialCheckpoint;
            }
            Button(card, "Exit", "EXIT & RESTART GAME", new Vector2(0, -140), BattleSceneFlow.ReturnToStart, LegacyUiKit.ButtonSlate);
        }

        private static Button Button(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action, Color? face = null)
        {
            var button = LegacyUiKit.CreateButton(parent, name, position, new Vector2(320, 62), face ?? (name == "Restart" ? LegacyUiKit.Green : LegacyUiKit.Gold), 16);
            LegacyUiKit.CreateText(button.transform, "Label", label, new Vector2(0, 3), new Vector2(310, 54), 24, Color.white, FontStyles.Bold);
            button.onClick.AddListener(() => GameAnalytics.Track("recovery_choice", ("choice", name.ToLowerInvariant())));
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
