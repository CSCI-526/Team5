using System.Collections.Generic;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Test-only authoring controls. An unfinished empty map is an intentional state.
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class TestLevelControls : MonoBehaviour
    {
        [SerializeField] private BattleSession session;
        public string ConfigurationMessage { get; private set; }

        private void Start()
        {
            if (gameObject.scene.path != BattleSceneFlow.TestLevelScene || BattleSceneFlow.IsPreviewScene(gameObject.scene)) return;
            if (session == null) session = GetComponent<BattleSession>();
            var host = new GameObject("Test Level Tools", typeof(RectTransform));
            host.transform.SetParent(transform, false);
            LegacyUiKit.CreateCanvas(host, 100, new Vector2(1280, 720), 0.5f);
            var panel = LegacyUiKit.CreateImage(host.transform, "Panel", Vector2.zero, new Vector2(410, 128), LegacyUiKit.PanelColor, 8).rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-16, -16);
            var content = LegacyUiKit.CreateRect(panel, "Content", Vector2.zero, Vector2.zero);
            var status = LegacyUiKit.CreateText(content, "Status", "", new Vector2(0, 30), new Vector2(378, 54), 16, Color.white);
            status.textWrappingMode = TMPro.TextWrappingModes.Normal;
            var restart = LegacyUiKit.CreateButton(content, "Restart Test", new Vector2(-100, -32), new Vector2(184, 36), LegacyUiKit.NodeGreenColor, 4);
            LegacyUiKit.CreateText(restart.transform, "Label", "RESTART TEST", Vector2.zero, new Vector2(180, 32), 15, Color.white);
            restart.onClick.AddListener(() => { RestartTest(); status.text = ConfigurationMessage; });
            var prepare = LegacyUiKit.CreateButton(content, "Test Preparation", new Vector2(100, -32), new Vector2(184, 36), LegacyUiKit.NodeBlueColor, 4);
            LegacyUiKit.CreateText(prepare.transform, "Label", "TEST PREPARATION", Vector2.zero, new Vector2(180, 32), 15, Color.white);
            prepare.onClick.AddListener(BattleSceneFlow.ContinueToUpgrade);
            RestartTest();
            status.text = ConfigurationMessage;
        }

        public bool RestartTest()
        {
            if (session == null || BattleSceneFlow.IsPreviewScene(gameObject.scene)) return false;
            List<string> errors = session.ValidateConfiguration();
            if (errors.Count > 0)
            {
                ConfigurationMessage = "Map not configured yet. Build GameplayGrid, set SceneRoute Cells and register Towers.";
                return false;
            }
            session.StartBattle();
            ConfigurationMessage = "TEST LEVEL — standalone battle. Return to preparation to change deployment.";
            return session.Simulation != null;
        }
    }
}
