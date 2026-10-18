using ReverseTD.Offense;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReverseTD.UpgradeUI
{
    public class BattleSquadEditor : MonoBehaviour
    {
        private OffenseWaveManager owner;
        private OffenseData draft;
        private readonly int[] sourceCells = new int[SquadRules.CellCount];
        private bool ownsPause;

        public static BattleSquadEditor Open(OffenseWaveManager owner, OffenseData data)
        {
            var host = new GameObject("Battle Squad Editor", typeof(RectTransform));
            host.transform.SetParent(owner.transform, false);
            var editor = host.AddComponent<BattleSquadEditor>();
            editor.owner = owner;
            editor.ownsPause = true;
            // Work on a copy until Done; do not change the live roster while dragging.
            editor.draft = Instantiate(data);
            editor.draft.squad = (UnitType[])data.squad.Clone();
            for (int cell = 0; cell < editor.sourceCells.Length; cell++)
            {
                editor.sourceCells[cell] = cell;
                // Hide fallen units in the battle editor without removing them from the roster.
                if (!owner.IsSoldierAliveAt(cell)) editor.draft.squad[cell] = UnitType.None;
            }
            editor.Build();
            return editor;
        }

        private void Build()
        {
            UiKit.CreateCanvas(gameObject, 800, new Vector2(1280f, 720f), 1f);
            var root = (RectTransform)transform;

            // Block clicks on battle controls while the squad is being edited.
            Image backdrop = UiKit.CreateImage(root, "Input Blocker", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.15f));
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            Image panel = UiKit.CreateImage(root, "Panel", new Vector2(-20f, -20f), new Vector2(320f, 410f), UiKit.PanelColor, 12f);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = Vector2.one;
            panelRect.pivot = Vector2.one;
            panelRect.anchoredPosition = new Vector2(-20f, -20f);
            // Children use the panel's center as origin, independently of its top-right pivot.
            RectTransform content = UiKit.CreateRect(panelRect, "Content", Vector2.zero, Vector2.zero);
            panel.raycastTarget = true;

            UiKit.CreateText(content, "Title", "EDIT SQUAD", new Vector2(0f, 174f), new Vector2(280f, 30f), 24f,
                UiKit.TextColor, FontStyles.Bold);
            UiKit.CreateText(content, "Paused", "Battle paused", new Vector2(0f, 145f), new Vector2(280f, 24f), 16f, UiKit.GoldColor);

            SquadGridUI.Create(content, root, draft, Vector2.zero, 58f, 8f, null,
                "Drag to swap", true, null, SwapSources);

            Button done = UiKit.CreateButton(content, "Apply And Resume", new Vector2(0f, -170f), new Vector2(260f, 48f), UiKit.NodeGreenColor, 8f);
            UiKit.CreateText(done.transform, "Label", "APPLY & RESUME", Vector2.zero, new Vector2(250f, 40f), 20f, Color.white, FontStyles.Bold);
            done.onClick.AddListener(ApplyAndResume);

            if (OffenseWaveManager.IsTutorialPaused && owner.AllowTutorialSquadEditing)
            {
                BuildMoveDownTutorialHint(panelRect);
            }

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void BuildMoveDownTutorialHint(RectTransform panel)
        {
            Image card = UiKit.CreateImage(panel, "Move Down Tutorial Hint", Vector2.zero, new Vector2(320f, 156f),
                new Color(0.08f, 0.11f, 0.17f, 0.96f));
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            card.rectTransform.pivot = new Vector2(0.5f, 1f);
            card.rectTransform.anchoredPosition = new Vector2(0f, -12f);

            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.85f, 0.4f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            RectTransform content = UiKit.CreateRect(card.transform, "Content", Vector2.zero, Vector2.zero);
            UiKit.CreateText(content, "Title", "ADJUST YOUR FORMATION", new Vector2(0f, 52f), new Vector2(296f, 30f),
                20f, new Color(0.35f, 0.95f, 0.45f), FontStyles.Bold);
            var body = UiKit.CreateText(content, "Body", "Drag your unit <b>down by one cell to avoid attack</b>.", new Vector2(0f, 8f),
                new Vector2(284f, 58f), 19f, Color.white);
            body.textWrappingMode = TextWrappingModes.Normal;
            UiKit.CreateText(content, "Prompt", "Then click APPLY & RESUME.", new Vector2(0f, -49f), new Vector2(296f, 24f),
                15f, new Color(0.75f, 0.82f, 0.9f), FontStyles.Italic);
        }

        private void SwapSources(int first, int second)
        {
            int moved = sourceCells[first];
            sourceCells[first] = sourceCells[second];
            sourceCells[second] = moved;
        }

        public void ApplyAndResume()
        {
            if (owner == null || !owner.ApplySquadFormation(sourceCells)) return;

            // Disable immediately so no additional UI event can edit the committed layout.
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (!ownsPause) return;
            ownsPause = false;
            if (owner != null) owner.EndSquadEdit();
        }

        private void OnDestroy()
        {
            if (draft != null) Destroy(draft);
        }
    }
}
