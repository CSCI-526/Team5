using System;
using Team5.Rebuild.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // "Choose your squad": shown by Start Game before the first battle of a run. Built at runtime with
    // LegacyUiKit like the other screens. Tapping a card stores the set and continues.
    public sealed class CharacterSelectScreen : MonoBehaviour
    {
        public const string Title = "CHOOSE YOUR SQUAD";
        private static readonly Vector2 Reference = new Vector2(1920, 1080);
        private static readonly Vector2 CardSize = new Vector2(520, 660);
        private const float CardSpacing = 300;

        private static CharacterSelectScreen active;
        private Action<CharacterSet> chosen;
        private bool done;

        public static CharacterSelectScreen Active => active;
        public Canvas Canvas { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active = null;

        public static CharacterSelectScreen Show(Action<CharacterSet> onChosen)
        {
            if (active != null) return active;
            var host = new GameObject("Character Select", typeof(RectTransform));
            var screen = host.AddComponent<CharacterSelectScreen>();
            screen.chosen = onChosen;
            screen.Build();
            active = screen;
            return screen;
        }

        public void Choose(CharacterSet set)
        {
            if (done) return;
            done = true;
            CharacterSetSelection.Set(set);
            var callback = chosen;
            Close();
            callback?.Invoke(set);
        }

        public void Close()
        {
            if (active == this) active = null;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy() { if (active == this) active = null; }

        private void Build()
        {
            Canvas = LegacyUiKit.CreateCanvas(gameObject, 50, Reference, 0.5f);
            var root = Canvas.transform;
            var backdrop = LegacyUiKit.CreateImage(root, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.05f, 0.07f, 0.11f, 0.97f));
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true; // Blocks the title screen's buttons underneath.

            LegacyUiKit.CreateText(root, "Title", Title, new Vector2(0, 425), new Vector2(1200, 80), 64,
                LegacyUiKit.TextColor, FontStyles.Bold, 6);
            LegacyUiKit.CreateText(root, "Subtitle", "Pick the look of your squad. Units and stats are the same.", new Vector2(0, 360),
                new Vector2(1200, 40), 26, LegacyUiKit.MutedColor);

            var catalogs = CharacterSetCatalogs.Instance;
            var current = CharacterSetSelection.Current;
            BuildCard(root, CharacterSet.PaintSquad, new Vector2(-CardSpacing, -40), catalogs, current);
            BuildCard(root, CharacterSet.Catgirl, new Vector2(CardSpacing, -40), catalogs, current);

            var back = LegacyUiKit.CreateButton(root, "Back", new Vector2(-760, -455), new Vector2(220, 70), LegacyUiKit.PanelColor, 14);
            LegacyUiKit.CreateText(back.transform, "Label", "BACK", Vector2.zero, new Vector2(220, 70), 28, LegacyUiKit.TextColor, FontStyles.Bold, 2);
            back.onClick.AddListener(Close);
        }

        private void BuildCard(Transform root, CharacterSet set, Vector2 position, CharacterSetCatalogs catalogs, CharacterSet current)
        {
            CharacterSetEntry entry = null;
            if (catalogs != null) catalogs.TryGet(set, out entry);
            var picked = set == current;
            var accent = picked ? LegacyUiKit.HighlightColor : LegacyUiKit.PanelLineColor;

            var frame = LegacyUiKit.CreateImage(root, "Card " + set, position, CardSize + Vector2.one * 8, accent, 26);
            var button = LegacyUiKit.MakeButton(frame);
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() => Choose(set));

            var card = LegacyUiKit.CreateImage(frame.transform, "Face", Vector2.zero, CardSize, LegacyUiKit.PanelColor, 22);
            var well = LegacyUiKit.CreateImage(card.transform, "Preview Well", new Vector2(0, 70), new Vector2(440, 440), LegacyUiKit.CellColor, 18);
            if (entry != null && entry.captainPreview != null)
            {
                var preview = LegacyUiKit.CreateImage(well.transform, "Captain Preview", Vector2.zero, new Vector2(420, 420), Color.white);
                preview.sprite = entry.captainPreview;
                preview.preserveAspect = true;
            }

            var name = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : DefaultName(set);
            LegacyUiKit.CreateText(card.transform, "Name", name.ToUpperInvariant(), new Vector2(0, -205), new Vector2(480, 56), 44,
                LegacyUiKit.TextColor, FontStyles.Bold, 3);
            var tagline = entry != null ? entry.tagline : string.Empty;
            LegacyUiKit.CreateText(card.transform, "Tagline", tagline, new Vector2(0, -255), new Vector2(480, 34), 24, LegacyUiKit.MutedColor);
            if (picked)
                LegacyUiKit.CreateText(card.transform, "Selected", "SELECTED", new Vector2(0, 300), new Vector2(240, 30), 20,
                    LegacyUiKit.HighlightColor, FontStyles.Bold, 4);
        }

        public static string DefaultName(CharacterSet set) => set == CharacterSet.Catgirl ? "Catgirl" : "Paint Squad";

        public Button CardButton(CharacterSet set)
        {
            var card = Canvas != null ? Canvas.transform.Find("Card " + set) : null;
            return card != null ? card.GetComponent<Button>() : null;
        }
    }
}
