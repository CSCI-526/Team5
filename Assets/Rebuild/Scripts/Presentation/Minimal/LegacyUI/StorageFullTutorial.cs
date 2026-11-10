using Team5.Rebuild.Analytics;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    /// <summary>
    /// Guides the player to right-click a unit card to dismiss it the first time unit storage becomes full.
    /// </summary>
    public sealed class StorageFullTutorial : MonoBehaviour
    {
        private static bool hasCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => hasCompleted = false;

        private readonly WarehouseCardHighlighter cardHighlighter = new WarehouseCardHighlighter();
        private readonly StorageTutorialView view = new StorageTutorialView();

        private LegacyUpgradeScene owner;
        private RectTransform root;
        private bool isShowing;
        private bool finished;

        public static StorageFullTutorial Attach(LegacyUpgradeScene owner, RectTransform root)
        {
            if (hasCompleted || owner == null || root == null || owner.Preparation == null)
                return null;

            var rect = LegacyUiKit.CreateRect(root, "Storage Full Tutorial", Vector2.zero, Vector2.zero);
            LegacyUiKit.Stretch(rect);
            var tutorial = rect.gameObject.AddComponent<StorageFullTutorial>();
            tutorial.owner = owner;
            tutorial.root = root;
            tutorial.view.Build(rect, tutorial.AcknowledgeAndClose);
            tutorial.view.SetVisible(false);
            return tutorial;
        }

        private void Update()
        {
            if (finished) return;
            var preparation = owner != null ? owner.Preparation : null;
            if (preparation == null)
            {
                Finish(markCompleted: false);
                return;
            }

            var isStorageFull = !preparation.CanAddMember;

            // If the tutorial was active and the player right-clicked to dismiss a unit, complete the tutorial!
            if (isShowing && !isStorageFull)
            {
                GameAnalytics.Track("tutorial_storage_full_complete", ("action", "dismissed_unit"));
                Finish(markCompleted: true);
                return;
            }

            var shouldShow = isStorageFull && !owner.IsOverlayOpen && !BattleSceneFlow.IsOutOfLives;
            if (!shouldShow)
            {
                if (isShowing)
                {
                    cardHighlighter.Restore();
                    view.SetVisible(false);
                    isShowing = false;
                }
                return;
            }

            var targetIndex = FindCandidateCardIndex(preparation);
            if (targetIndex < 0) return;

            if (!isShowing)
            {
                isShowing = true;
                view.SetVisible(true);
                GameAnalytics.Track("tutorial_storage_full_shown");
            }

            if (transform.GetSiblingIndex() != root.childCount - 1)
                transform.SetAsLastSibling();

            var now = Time.unscaledTime;
            cardHighlighter.PulseCard(root, targetIndex, now);
            view.UpdateChipPosition(LegacyUpgradeScene.WarehouseCellPosition(targetIndex), now);
        }

        private void AcknowledgeAndClose()
        {
            GameAnalytics.Track("tutorial_storage_full_complete", ("action", "acknowledged"));
            Finish(markCompleted: true);
        }

        private static int FindCandidateCardIndex(SquadPreparation preparation)
        {
            var fallbackIndex = -1;
            for (var i = 0; i < preparation.Members.Count; i++)
            {
                var member = preparation.Members[i];
                if (member.Unit.Role == UnitRole.Captain) continue;
                if (fallbackIndex < 0) fallbackIndex = i;

                // Prefer highlighting an undeployed unit in storage
                if (!preparation.IsDeployed(member.MemberId))
                    return i;
            }
            return fallbackIndex;
        }

        private void Finish(bool markCompleted)
        {
            if (finished) return;
            finished = true;
            if (markCompleted) hasCompleted = true;
            cardHighlighter.Restore();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            cardHighlighter.Restore();
        }
    }

    /// <summary>
    /// Pulses the border of the target warehouse unit card and restores its original rarity color.
    /// </summary>
    internal sealed class WarehouseCardHighlighter
    {
        private static readonly Color PulseLow = new Color(1f, 0.64f, 0.08f);
        private static readonly Color PulseHigh = new Color(1f, 0.94f, 0.48f);

        private Image highlightedCard;
        private Color savedColor;
        private int highlightedIndex = -1;

        public void PulseCard(RectTransform root, int cardIndex, float time)
        {
            var cell = cardIndex >= 0 ? root.Find("Warehouse Cell " + cardIndex) : null;
            var frame = cell != null ? cell.GetComponent<Image>() : null;

            if (frame != highlightedCard || cardIndex != highlightedIndex)
            {
                Restore();
                highlightedCard = frame;
                highlightedIndex = cardIndex;
                if (highlightedCard != null)
                    savedColor = highlightedCard.color;
            }

            if (highlightedCard != null)
            {
                var t = (Mathf.Sin(time * 5f) + 1f) * 0.5f;
                highlightedCard.color = Color.Lerp(PulseLow, PulseHigh, t);
            }
        }

        public void Restore()
        {
            if (highlightedCard != null)
                highlightedCard.color = savedColor;

            highlightedCard = null;
            highlightedIndex = -1;
        }
    }

    /// <summary>
    /// Renders the floating callout chip above the target card and the instructional banner at the bottom.
    /// </summary>
    internal sealed class StorageTutorialView
    {
        private static readonly Color Gold = new Color(1f, 0.84f, 0.25f);
        private static readonly Color ChipInk = new Color(0.1f, 0.08f, 0f);
        private static readonly Color BannerBg = new Color(0.08f, 0.11f, 0.17f, 0.96f);
        private static readonly Color BannerOutline = new Color(0.3f, 0.85f, 0.4f, 1f);
        private static readonly Color TitleGreen = new Color(0.35f, 0.95f, 0.45f);

        private CanvasGroup group;
        private RectTransform chip;
        private RectTransform arrow;

        public void Build(RectTransform parent, UnityEngine.Events.UnityAction onGotIt)
        {
            group = parent.gameObject.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = false; // Allow right-clicking directly through to the warehouse cards

            BuildFloatingChip(parent);
            BuildBanner(parent, onGotIt);
        }

        private void BuildFloatingChip(RectTransform parent)
        {
            chip = LegacyUiKit.CreateRect(parent, "Dismiss Guide Chip", Vector2.zero, Vector2.zero);
            const string chipText = "RIGHT-CLICK TO DISMISS";
            var label = LegacyUiKit.CreateText(chip, "Label", chipText, Vector2.zero, new Vector2(240f, 26f), 13f, ChipInk, FontStyles.Bold, 2f);
            var back = LegacyUiKit.CreateImage(chip, "Back", Vector2.zero, new Vector2(label.GetPreferredValues(chipText).x + 24f, 28f), Gold, 14f);
            back.transform.SetSiblingIndex(label.transform.GetSiblingIndex());

            arrow = LegacyUiKit.CreateRect(chip, "Down Arrow", new Vector2(0f, -20f), new Vector2(18f, 18f));
            var head = LegacyUiKit.CreateImage(arrow, "Head", Vector2.zero, new Vector2(18f, 18f), Gold);
            head.sprite = NextMapShapes.Triangle;
            head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        private void BuildBanner(RectTransform parent, UnityEngine.Events.UnityAction onGotIt)
        {
            var panel = LegacyUiKit.CreateImage(parent, "Storage Tutorial Banner", Vector2.zero, new Vector2(760f, 130f), BannerBg, 10f);
            panel.raycastTarget = true;
            var banner = panel.rectTransform;
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0f);
            banner.pivot = new Vector2(0.5f, 0f);
            banner.anchoredPosition = new Vector2(-115f, 18f);

            var bannerCanvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            bannerCanvasGroup.blocksRaycasts = true;

            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = BannerOutline;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var title = LegacyUiKit.CreateText(banner, "Title", "STORAGE FULL — DISMISS UNITS", new Vector2(0f, 34f), new Vector2(700f, 32f),
                24f, TitleGreen, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;

            var body = LegacyUiKit.CreateText(banner, "Body",
                "Unit storage is full (12/12)! <b>Right-click</b> any non-Captain unit card in storage\nto dismiss it and make room for new summons.",
                new Vector2(-65f, -14f), new Vector2(560f, 56f), 17f, Color.white);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.alignment = TextAlignmentOptions.Center;

            var gotItButton = LegacyUiKit.CreateButton(banner, "Got It", new Vector2(290f, -14f), new Vector2(124f, 40f), LegacyUiKit.NodeGreenColor, 6f);
            LegacyUiKit.CreateText(gotItButton.transform, "Label", "GOT IT", Vector2.zero, new Vector2(116f, 36f), 17f, Color.white, FontStyles.Bold);
            gotItButton.onClick.AddListener(onGotIt);
        }

        public void UpdateChipPosition(Vector2 cellPosition, float time)
        {
            if (chip == null) return;
            var bob = Mathf.Sin(time * 6f) * 4f;
            chip.anchoredPosition = cellPosition + new Vector2(0f, 96f + bob);
        }

        public void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
        }
    }
}