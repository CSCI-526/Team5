using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Layout copied from Team5 BattleSquadEditor. Only its owner/data adapter changes.
    public sealed class LegacyFormationEditor : MonoBehaviour
    {
        private BattleSimulation simulation;
        private LegacyFormationGrid grid;
        private LegacyBattleHud owner;
        private TextMeshProUGUI error;
        private bool wasPaused;
        public LegacyFormationGrid Grid => grid;
        public static LegacyFormationEditor Open(LegacyBattleHud owner, BattleSimulation simulation, RectTransform inspectionPanel = null)
        {
            var host = new GameObject("Battle Squad Editor", typeof(RectTransform));
            host.transform.SetParent(inspectionPanel != null ? inspectionPanel : owner.Session.transform, false);
            var editor = host.AddComponent<LegacyFormationEditor>();
            editor.owner = owner; editor.simulation = simulation; editor.wasPaused = simulation.IsPaused;
            simulation.SetPaused(true); editor.Build(inspectionPanel != null); return editor;
        }
        private void Build(bool embedded)
        {
            var root = (RectTransform)transform;
            if (embedded) LegacyUiKit.Stretch(root);
            else
            {
                LegacyUiKit.CreateCanvas(gameObject, 800, new Vector2(1280, 720), 1);
                var backdrop = LegacyUiKit.CreateImage(root, "Input Blocker", Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.15f));
                LegacyUiKit.Stretch(backdrop.rectTransform); backdrop.raycastTarget = true;
            }
            var panel = LegacyUiKit.CreateImage(root, "Panel", new Vector2(-20, -20), new Vector2(320, 410), LegacyUiKit.PanelColor, 12);
            if (embedded)
            {
                panel.rectTransform.anchoredPosition = Vector2.zero;
            }
            else
            {
                panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = panel.rectTransform.pivot = Vector2.one;
                panel.rectTransform.anchoredPosition = new Vector2(-20, -20);
            }
            panel.raycastTarget = true;
            var content = LegacyUiKit.CreateRect(panel.transform, "Content", Vector2.zero, Vector2.zero);
            LegacyUiKit.CreateText(content, "Title", "EDIT SQUAD", new Vector2(0, 174), new Vector2(280, 30), 24, LegacyUiKit.TextColor, FontStyles.Bold);
            LegacyUiKit.CreateText(content, "Paused", "Battle paused", new Vector2(0, 145), new Vector2(280, 24), 16, LegacyUiKit.HighlightColor);
            grid = LegacyFormationGrid.Create(content, root, simulation);
            var done = LegacyUiKit.CreateButton(content, "Apply And Resume", new Vector2(0, -170), new Vector2(260, 48), LegacyUiKit.NodeGreenColor, 8);
            LegacyUiKit.CreateText(done.transform, "Label", "APPLY & RESUME", Vector2.zero, new Vector2(250, 40), 20, Color.white, FontStyles.Bold);
            done.onClick.AddListener(() => ApplyAndResume());
            error = LegacyUiKit.CreateText(content, "Error", "", new Vector2(0, -136), new Vector2(290, 20), 11, Color.red);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        public bool ApplyAndResume()
        {
            if (owner.Session.Simulation != simulation || !simulation.TryApplyFormation(grid.CopySourceSlots()))
            { error.text = "Formation cannot be applied."; return false; }
            simulation.SetPaused(wasPaused);
            gameObject.SetActive(false); Destroy(gameObject); return true;
        }
        public void CancelAndClose()
        {
            if (owner != null && owner.Session.Simulation == simulation) simulation.SetPaused(wasPaused);
            gameObject.SetActive(false); Destroy(gameObject);
        }
        // Removing a view must not change the pause or battle state.
    }
}
