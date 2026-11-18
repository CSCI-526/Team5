using System;
using Team5.Rebuild.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // "Choose your squad": shown by Start Game before the first battle of a run. Built at runtime with
    // LegacyUiKit like the other screens, in the layout of Paint Squad Wars' team select. Tapping a card
    // stores the set and continues.
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
            // Opaque hex paper like the Paint Squad menu ground; it also blocks the title screen's buttons.
            var backdrop = LegacyUiKit.CreateBackdrop(root, "Backdrop");
            backdrop.raycastTarget = true;

            LegacyUiKit.CreateRibbon(root, "Title Ribbon", Title, new Vector2(0, 425), new Vector2(980, 118), LegacyUiKit.Slate, 58);
            LegacyUiKit.CreateText(root, "Subtitle", "Pick the look of your squad. Units and stats are the same.", new Vector2(0, 340),
                new Vector2(1200, 40), 28, LegacyUiKit.Ink);

            var catalogs = CharacterSetCatalogs.Instance;
            var current = CharacterSetSelection.Current;
            BuildCard(root, CharacterSet.PaintSquad, new Vector2(-CardSpacing, -40), catalogs, current);
            BuildCard(root, CharacterSet.Catgirl, new Vector2(CardSpacing, -40), catalogs, current);

            var back = LegacyUiKit.CreateButton(root, "Back", new Vector2(-760, -455), new Vector2(220, 70), LegacyUiKit.ButtonSlate, 22);
            var arrow = LegacyUiKit.CreateIcon(back.transform, "Arrow", "arrow_left", new Vector2(-62, 4), 40);
            var backLabel = LegacyUiKit.CreateText(back.transform, "Label", "BACK", new Vector2(arrow != null ? 22 : 0, 4), new Vector2(150, 70), 32,
                LegacyUiKit.TextColor, FontStyles.Bold, 2);
            backLabel.raycastTarget = false;
            back.onClick.AddListener(Close);
        }

        // Paint Squad team cards: white card, a big accent disc behind the Captain, the name in the accent
        // color over an accent bar. The stored pick gets an accent rim and a green check badge.
        private void BuildCard(Transform root, CharacterSet set, Vector2 position, CharacterSetCatalogs catalogs, CharacterSet current)
        {
            CharacterSetEntry entry = null;
            if (catalogs != null) catalogs.TryGet(set, out entry);
            var picked = set == current;
            var accent = Accent(set);
            var accentDark = LegacyUiKit.Darker(accent, 0.75f);

            var frame = LegacyUiKit.CreateImage(root, "Card " + set, position, CardSize + Vector2.one * 16, picked ? accent : LegacyUiKit.CardRimColor, 30);
            LegacyUiKit.CreateShadow(frame.rectTransform, 26, 16, 0.28f);
            var button = LegacyUiKit.MakeButton(frame);
            button.onClick.AddListener(() => Choose(set));

            var card = LegacyUiKit.CreateImage(frame.transform, "Face", new Vector2(0, 5), CardSize - new Vector2(4, 10), LegacyUiKit.CardColor, 24);
            var well = LegacyUiKit.CreateCircle(card.transform, "Preview Well", new Vector2(0, 85), 410, accent);
            LegacyUiKit.CreateCircle(well.transform, "Preview Ring", Vector2.zero, 372, Color.Lerp(accent, Color.white, 0.18f));
            LegacyUiKit.CreateCircle(well.transform, "Preview Disc", Vector2.zero, 352, accent);
            if (entry != null && entry.captainPreview != null)
            {
                var preview = LegacyUiKit.CreateImage(well.transform, "Captain Preview", new Vector2(0, -6), new Vector2(430, 430), Color.white);
                preview.sprite = entry.captainPreview;
                preview.preserveAspect = true;
            }

            var name = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : DefaultName(set);
            LegacyUiKit.CreateText(card.transform, "Name", name.ToUpperInvariant(), new Vector2(0, -182), new Vector2(480, 64), 54,
                accentDark, FontStyles.Bold, 3);
            LegacyUiKit.CreateImage(card.transform, "Accent Bar", new Vector2(0, -224), new Vector2(150, 12), accent, 6);
            var tagline = entry != null ? entry.tagline : string.Empty;
            LegacyUiKit.CreateText(card.transform, "Tagline", tagline, new Vector2(0, -268), new Vector2(480, 36), 27, LegacyUiKit.InkMuted);
            if (picked)
            {
                var badge = LegacyUiKit.CreateCircle(frame.transform, "Selected Badge", new Vector2(CardSize.x / 2 - 6, CardSize.y / 2 - 6), 84, LegacyUiKit.Green);
                LegacyUiKit.Chunky(badge);
                LegacyUiKit.CreateIcon(badge.transform, "Check", "check", new Vector2(0, 4), 54);
                var tag = LegacyUiKit.CreatePill(frame.transform, "Selected Tag", new Vector2(0, CardSize.y / 2 + 6), new Vector2(190, 42), LegacyUiKit.Green);
                LegacyUiKit.CreateText(tag.transform, "Selected", "SELECTED", new Vector2(0, 1), new Vector2(180, 40), 26,
                    Color.white, FontStyles.Bold, 4);
            }
        }

        private static Color Accent(CharacterSet set) => set == CharacterSet.Catgirl ? LegacyUiKit.Hex(0xDB4793) : LegacyUiKit.Hex(0xDA4029);

        public static string DefaultName(CharacterSet set) => set == CharacterSet.Catgirl ? "Catgirl" : "Paint Squad";

        public Button CardButton(CharacterSet set)
        {
            var card = Canvas != null ? Canvas.transform.Find("Card " + set) : null;
            return card != null ? card.GetComponent<Button>() : null;
        }
    }
}
