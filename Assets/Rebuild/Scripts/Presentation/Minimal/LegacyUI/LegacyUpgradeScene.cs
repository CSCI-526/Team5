using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Analytics;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class LegacyUpgradeScene : MonoBehaviour
    {
        [SerializeField] private SquadRoster roster;
        [SerializeField] private Button startGameButton;
        private GameObject screen;
        private TMP_Text countLabel, warehouseHint, levelLabel, goldLabel;
        private RectTransform levelHeart;
        private RectTransform screenRoot;
        private readonly List<GameObject> warehouseCells = new List<GameObject>();
        private MemberSpawnDefinition[] renderedMembers;
        private Button claimButton;
        private TMP_Text claimLabel, quotaLabel;
        private Image quotaFill;
        private LegacyClaimCardOverlay claimOverlay;
        private Button previewButton;
        private LegacyNextLevelPreview levelPreview;
        private bool startPending;
        private GameObject dismissOverlay;
        private GameObject unitIntroductionOverlay;
        private GameObject unitDetailsOverlay;
        private GameObject summonGuideOverlay;
        private Canvas summonGuideButtonCanvas;
        private GraphicRaycaster summonGuideButtonRaycaster;
        private bool preparationGuidesAttached;
        private bool IsFirstCampaignPreparation => !BattleSceneFlow.IsTestRun &&
            BattleSceneFlow.HighestClearedLevel == 1 && BattleSceneFlow.NextBattleLevelNumber == 2;
        private int pendingDismissMemberId;
        private readonly Dictionary<int, CanvasGroup> cardGroups = new Dictionary<int, CanvasGroup>();
        private readonly Dictionary<int, TMP_Text> cardStatuses = new Dictionary<int, TMP_Text>();

        public Button PreviewNextLevelButton => previewButton;
        public Button ClaimCardButton => claimButton;
        public TMP_Text GoldLabel => goldLabel;
        public SquadRoster Roster => roster;
        public SquadPreparation Preparation { get; private set; }
        public LegacyFormationGrid Grid { get; private set; }
        public Button StartWaveButton => startGameButton;
        public TMP_Text LevelLabel => levelLabel;

        private void Start()
        {
            Preparation = BattleSceneFlow.PrepareSquad(roster);
            PrepAnalytics.Enter(Preparation);
            PaintSquadSceneSkin.StyleUpgradeScene(startGameButton);
            screen = new GameObject("Squad Screen", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(screen, 10, new Vector2(1280, 720), 0);
            var root = (RectTransform)screen.transform;
            screenRoot = root;
            BuildPanels(startGameButton.GetComponentInParent<Canvas>().rootCanvas.transform);
            // Keep all wave controls in the same coordinate space as the squad board.
            var startRect = (RectTransform)startGameButton.transform;
            startRect.SetParent(root, false);
            PlaceWaveControl(startRect, new Vector2(465, -248), new Vector2(276, 58));
            foreach (var label in startGameButton.GetComponentsInChildren<TMP_Text>(true))
            {
                LegacyUiKit.Stretch(label.rectTransform);
                label.alignment = TextAlignmentOptions.Center;
                label.margin = new Vector4(10, 0, 10, 4);
                label.enableAutoSizing = true;
                label.fontSizeMin = 18; label.fontSizeMax = 32;
                label.raycastTarget = false;
            }

            // Paint Squad currency chip: dark pill, big coin over its left end, white number.
            var chip = LegacyUiKit.CreatePill(root, "Gold Chip", Vector2.zero, new Vector2(196, 44), new Color(LegacyUiKit.Ink.r, LegacyUiKit.Ink.g, LegacyUiKit.Ink.b, 0.85f));
            chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = chip.rectTransform.pivot = new Vector2(1, 1);
            chip.rectTransform.anchoredPosition = new Vector2(-22, -22);
            LegacyUiKit.CreateIcon(chip.transform, "Coin", "coin", new Vector2(-92, 0), 56);
            goldLabel = LegacyUiKit.CreateText(root, "Gold Balance", "", Vector2.zero, new Vector2(260, 48),
                30, Color.white, FontStyles.Bold);
            goldLabel.alignment = TextAlignmentOptions.Right;
            goldLabel.rectTransform.anchorMin = goldLabel.rectTransform.anchorMax = new Vector2(1, 1);
            goldLabel.rectTransform.pivot = new Vector2(1, 1);
            goldLabel.rectTransform.anchoredPosition = new Vector2(-40, -20);
            RefreshGoldLabel();

            levelLabel = LegacyUiKit.CreateText(root, "Level Label", "", Vector2.zero, new Vector2(360, 60),
                36, Color.white, FontStyles.Bold);
            levelLabel.alignment = TextAlignmentOptions.Center;
            levelLabel.rectTransform.anchorMin = levelLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            PlaceWaveControl(levelLabel.rectTransform, new Vector2(465, -126), new Vector2(276, 42));
            levelLabel.fontSize = 30; levelLabel.enableAutoSizing = true; levelLabel.fontSizeMin = 18; levelLabel.fontSizeMax = 30;
            LegacyUiKit.CreateHeartIcon(levelLabel.transform, Vector2.zero);
            levelHeart = (RectTransform)levelLabel.transform.Find("Heart Icon");
            levelHeart.anchorMin = levelHeart.anchorMax = levelHeart.pivot = new Vector2(0.5f, 0.5f);

            previewButton = LegacyUiKit.CreateButton(root, "Preview Next Level", Vector2.zero, new Vector2(240, 48), LegacyUiKit.ButtonSlate, 12);
            var previewLabel = LegacyUiKit.CreateText(previewButton.transform, "Label", "PREVIEW NEXT LEVEL", Vector2.zero, Vector2.zero, 24, Color.white, FontStyles.Bold);
            LegacyUiKit.Stretch(previewLabel.rectTransform);
            previewLabel.margin = new Vector4(10, 0, 10, 4);
            previewLabel.enableAutoSizing = true;
            previewLabel.fontSizeMin = 14;
            previewLabel.fontSizeMax = 24;
            var previewRect = (RectTransform)previewButton.transform;
            previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            PlaceWaveControl(previewRect, new Vector2(465, -184), new Vector2(276, 48));
            previewButton.onClick.AddListener(OpenNextLevelPreview);
            RefreshLevelLabel();

            LegacyUiKit.CreateRibbon(root, "Warehouse Ribbon", string.Empty, new Vector2(-165, 296), new Vector2(330, 58), LegacyUiKit.Red);
            LegacyUiKit.CreateText(root, "Warehouse Title", "UNIT STORAGE", new Vector2(-165, 300), new Vector2(780, 30),
                28, LegacyUiKit.TextColor, FontStyles.Bold, 3);
            for (var index = 0; index < SquadPreparation.WarehouseCapacity; index++)
                BuildWarehouseCell(root, index);
            renderedMembers = Preparation.Members.ToArray();

            var quota = LegacyUiKit.CreatePill(root, "Card Quota", new Vector2(-165, -160), new Vector2(330, 10), new Color(LegacyUiKit.Ink.r, LegacyUiKit.Ink.g, LegacyUiKit.Ink.b, 0.16f));
            quotaFill = LegacyUiKit.CreatePill(quota.transform, "Fill", Vector2.zero, new Vector2(330, 10), LegacyUiKit.Gold);
            quotaFill.rectTransform.anchorMin = quotaFill.rectTransform.anchorMax = quotaFill.rectTransform.pivot = new Vector2(0, 0.5f);

            quotaLabel = LegacyUiKit.CreateText(root, "Card Quota Text", "", new Vector2(-165, -140), new Vector2(740, 26), 16, LegacyUiKit.Ink);
            claimButton = LegacyUiKit.CreateButton(root, "Claim Card Button", new Vector2(-165, -210), new Vector2(330, 58), LegacyUiKit.NodeGreenColor, 14);
            claimLabel = LegacyUiKit.CreateText(claimButton.transform, "Label", "SUMMON UNIT", new Vector2(0, 3), new Vector2(320, 54), 23, Color.white, FontStyles.Bold);
            claimButton.onClick.AddListener(OpenClaimCards);

            warehouseHint = LegacyUiKit.CreateText(root, "Warehouse Hint", "Drag units to squad  |  Click a card for details  |  X to dismiss",
                new Vector2(-165, -115), new Vector2(780, 26), 16, LegacyUiKit.InkMuted);

            Grid = LegacyFormationGrid.CreateForPreparation(root, root, Preparation);
            ((RectTransform)Grid.transform).anchoredPosition = new Vector2(465, 43);
            LegacyUiKit.CreateText(Grid.transform, "Title", "SQUAD", new Vector2(0, 151), new Vector2(310, 26),
                20, LegacyUiKit.Ink, FontStyles.Bold, 3);
            countLabel = LegacyUiKit.CreateText(Grid.transform, "Squad Count", "", new Vector2(0, 189), new Vector2(310, 30),
                30, LegacyUiKit.Ink, FontStyles.Bold);

            Preparation.Changed += Refresh;
            startGameButton.onClick.AddListener(StartWave);
            Refresh();
            StartWaveNotice.Attach(this, root);
            StorageFullTutorial.Attach(this, root);
            if (IsFirstCampaignPreparation && !Preparation.FirstSummonPromptCompleted)
                ShowSummonGuide();
            TryAttachPreparationGuides();
        }

        private void Update()
        {
            TryAttachPreparationGuides();
            RefreshLevelLabel();
            RefreshGoldLabel();
            RefreshClaimCards();
            if (startGameButton != null && Preparation != null)
                startGameButton.interactable = CanStartWave && levelPreview == null;
            if (previewButton != null)
                previewButton.interactable = levelPreview == null && claimOverlay == null && dismissOverlay == null && unitIntroductionOverlay == null && unitDetailsOverlay == null && summonGuideOverlay == null && !BattleSceneFlow.IsOutOfLives && !startPending;
        }

        public bool CanStartWave =>
            Preparation != null && Preparation.CanStart && !BattleSceneFlow.IsOutOfLives && claimOverlay == null && dismissOverlay == null && unitIntroductionOverlay == null && unitDetailsOverlay == null && summonGuideOverlay == null && !startPending;

        public bool IsOverlayOpen => claimOverlay != null || levelPreview != null || dismissOverlay != null || unitIntroductionOverlay != null || unitDetailsOverlay != null || summonGuideOverlay != null;

        public TMP_Text SquadCountLabel => countLabel;

        public static Vector2 WarehouseCellPosition(int index) =>
            new Vector2(-500 + index % SquadPreparation.WarehouseColumns * 134, 185 - index / SquadPreparation.WarehouseColumns * 180);

        public void OpenNextLevelPreview()
        {
            if (IsOverlayOpen || BattleSceneFlow.IsOutOfLives || startPending) return;
            levelPreview = LegacyNextLevelPreview.Open(this);
            if (levelPreview != null) GameAnalytics.Track("map_preview_open");
        }

        public void OnNextLevelPreviewClosed()
        {
            levelPreview = null;
            var start = startPending;
            startPending = false;
            Refresh();
            if (start && Preparation.CanStart && !BattleSceneFlow.IsOutOfLives) BattleSceneFlow.StartPreparedBattle(Preparation);
        }

        public void OpenClaimCards()
        {
            // The highlighted Summon button is the only permitted action during this prompt.
            if (BattleSceneFlow.IsOutOfLives || claimOverlay != null || levelPreview != null ||
                dismissOverlay != null || unitIntroductionOverlay != null || unitDetailsOverlay != null) return;
            if (summonGuideOverlay != null)
            {
                Preparation.CompleteFirstSummonPrompt();
                CloseSummonGuide();
            }
            claimOverlay = LegacyClaimCardOverlay.Open(this);
            RefreshClaimCards();
        }

        private void TryAttachPreparationGuides()
        {
            if (preparationGuidesAttached || Preparation == null || IsOverlayOpen) return;
            // Wait until the offer and any new-role introduction have both closed.
            // Opening Summon is required; purchasing is optional. Cancel also unlocks deployment guidance.
            if (IsFirstCampaignPreparation && !Preparation.FirstSummonPromptCompleted) return;
            preparationGuidesAttached = true;
            CaptainGuide.Attach(this, screenRoot);
            // The empty-squad notice is already attached during Start.
        }

        private void ShowSummonGuide()
        {
            summonGuideOverlay = new GameObject("First Summon Guide", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(summonGuideOverlay, 900, new Vector2(1280, 720), 0.5f);
            var shade = LegacyUiKit.CreateImage(summonGuideOverlay.transform, "Input Blocker", Vector2.zero,
                Vector2.zero, new Color(LegacyUiKit.SlateDark.r, LegacyUiKit.SlateDark.g, LegacyUiKit.SlateDark.b, 0.72f));
            LegacyUiKit.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            // Paint Squad tutorial bar: dark slate plate, outlined title, white body.
            var banner = LegacyUiKit.CreateImage(summonGuideOverlay.transform, "Tutorial Banner", Vector2.zero,
                new Vector2(880, 140), new Color(LegacyUiKit.Slate.r, LegacyUiKit.Slate.g, LegacyUiKit.Slate.b, 0.96f), 26);
            banner.raycastTarget = true;
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(0.5f, 1);
            banner.rectTransform.pivot = new Vector2(0.5f, 1);
            banner.rectTransform.anchoredPosition = new Vector2(0, -24);
            LegacyUiKit.CreateIcon(banner.transform, "Icon", "users", new Vector2(-380, 0), 72);
            LegacyUiKit.CreateText(banner.transform, "Title", "RECRUIT YOUR SQUAD", new Vector2(0, 32),
                new Vector2(820, 42), 32, LegacyUiKit.Gold, FontStyles.Bold);
            LegacyUiKit.CreateText(banner.transform, "Body", "Click SUMMON UNIT to recruit a new teammate.",
                new Vector2(0, -22), new Vector2(820, 46), 24, Color.white);
            // Lift the real button above the dimmer without moving it or copying its listeners.
            summonGuideButtonCanvas = claimButton.gameObject.AddComponent<Canvas>();
            summonGuideButtonCanvas.overrideSorting = true;
            summonGuideButtonCanvas.sortingOrder = 901;
            summonGuideButtonRaycaster = claimButton.gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void CloseSummonGuide()
        {
            if (summonGuideOverlay != null)
            {
                summonGuideOverlay.SetActive(false);
                Destroy(summonGuideOverlay);
                summonGuideOverlay = null;
            }
            if (summonGuideButtonRaycaster != null)
            {
                summonGuideButtonRaycaster.enabled = false;
                Destroy(summonGuideButtonRaycaster);
                summonGuideButtonRaycaster = null;
            }
            if (summonGuideButtonCanvas != null)
            {
                summonGuideButtonCanvas.overrideSorting = false;
                Destroy(summonGuideButtonCanvas);
                summonGuideButtonCanvas = null;
            }
        }

        public void OnClaimCardsClosed() { claimOverlay = null; Refresh(); RefreshClaimCards(); }

        private void RefreshClaimCards()
        {
            var shop = BattleSceneFlow.CardShop;
            if (claimButton == null || shop == null) return;
            claimButton.interactable = shop.CanClaim && claimOverlay == null && levelPreview == null && dismissOverlay == null && unitIntroductionOverlay == null && unitDetailsOverlay == null && !BattleSceneFlow.IsOutOfLives;
            claimButton.image.color = claimButton.interactable ? LegacyUiKit.NodeGreenColor : LegacyUiKit.GreyDark;
            claimLabel.text = Preparation.CanAddMember ? $"SUMMON UNIT ({shop.NextPrice} GOLD)" : "STORAGE FULL";
            quotaLabel.text = shop.GetBannerSummary();
            quotaFill.enabled = BattleSceneFlow.GoldBalance > 0;
            quotaFill.rectTransform.sizeDelta = new Vector2(330 * Mathf.Clamp01((float)BattleSceneFlow.GoldBalance / shop.NextPrice), 10);
        }

        private void RefreshWarehouseCards()
        {
            if (renderedMembers != null && Preparation.Members.SequenceEqual(renderedMembers)) return;
            foreach (var cell in warehouseCells) { cell.SetActive(false); Destroy(cell); }
            warehouseCells.Clear();
            cardGroups.Clear();
            cardStatuses.Clear();
            for (var index = 0; index < SquadPreparation.WarehouseCapacity; index++)
                BuildWarehouseCell(screenRoot, index);
            renderedMembers = Preparation.Members.ToArray();
        }

        private void RefreshGoldLabel()
        {
            if (goldLabel != null) goldLabel.text = BattleSceneFlow.HasUnlimitedGold ? "UNLIMITED" : $"{BattleSceneFlow.GoldBalance:N0}";
        }

        private static void PlaceWaveControl(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void RefreshLevelLabel()
        {
            if (levelLabel == null) return;
            var lives = $"{BattleSceneFlow.CurrentLevelLivesRemaining}/{BattleSession.DefaultLivesPerLevel}";
            var levelName = BattleSceneFlow.IsTestRun ? "TEST LEVEL" : $"LEVEL {Mathf.Max(1, BattleSceneFlow.NextBattleLevelNumber)}";
            levelLabel.text = $"{levelName}  <space=30>{lives}";
            if (levelHeart != null)
            {
                levelLabel.ForceMeshUpdate();
                var info = levelLabel.textInfo;
                if (info.characterCount >= lives.Length)
                {
                    var first = info.characterInfo[info.characterCount - lives.Length];
                    levelHeart.localPosition = new Vector3(first.bottomLeft.x - 20,
                        (first.topRight.y + first.bottomLeft.y) / 2, 0);
                }
            }
        }

        private void BuildWarehouseCell(RectTransform root, int index)
        {
            var position = WarehouseCellPosition(index);
            var size = new Vector2(200, 262);
            // Paint Squad card: rim (rarity color for units) with a thicker bottom edge around a cream face.
            var empty = index >= Preparation.Members.Count;
            var cellImage = LegacyUiKit.CreateImage(root, "Warehouse Cell " + index, position, size + Vector2.one * 10, LegacyUiKit.CardLineColor, 22);
            if (LegacyUiKit.Skin != null && LegacyUiKit.Skin.panel != null) { cellImage.sprite = LegacyUiKit.Skin.panel; cellImage.pixelsPerUnitMultiplier = LegacyUiKit.SkinCornerRadius / 22f; }
            var cell = cellImage.rectTransform;
            warehouseCells.Add(cell.gameObject);
            cell.localScale = Vector3.one * 0.6f;
            var panelImage = LegacyUiKit.CreateImage(cell, "Introduction", new Vector2(0, 3), size - new Vector2(0, 6), empty ? LegacyUiKit.CardInnerColor : LegacyUiKit.CardColor, 18);
            var panel = panelImage.rectTransform;
            if (empty)
            {
                LegacyUiKit.CreateText(panel, "Empty", "EMPTY", Vector2.zero, size, 22, LegacyUiKit.CardRimColor, FontStyles.Bold, 2);
                return;
            }
            var member = Preparation.Members[index];
            cell.GetComponent<Image>().color = LegacyUnitLooks.RarityColor(member.Unit.Rarity);
            panelImage.raycastTarget = true;
            panel.gameObject.AddComponent<WarehouseUnitCardUI>().Initialize(this, root, member);
            cardGroups[member.MemberId] = cell.gameObject.AddComponent<CanvasGroup>();
            cardStatuses[member.MemberId] = LegacyUnitIntroductionUI.Build(panel, member.Unit, compact: true);
            var remove = LegacyUiKit.MakeButton(LegacyUiKit.CreateCircle(panel, "Dismiss Unit", Vector2.zero, 46,
                LegacyUiKit.Red));
            var removeRect = (RectTransform)remove.transform;
            removeRect.anchorMin = removeRect.anchorMax = new Vector2(1, 1);
            removeRect.anchoredPosition = new Vector2(-18, -18);
            // Draw the cross with the Paint Squad icon, or UI geometry when the skin is missing.
            if (LegacyUiKit.CreateIcon(remove.transform, "Cross", "close", new Vector2(0, 2), 30) == null)
                for (var i = 0; i < 2; i++)
                {
                    var stroke = LegacyUiKit.CreateImage(remove.transform, "Cross " + i, Vector2.zero,
                        new Vector2(20, 4), Color.white);
                    stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45 : -45);
                }
            remove.interactable = member.Unit.Rarity != UnitRarity.Legendary && Preparation.Members.Count > 1;
            remove.onClick.AddListener(() => TryDismissMember(member.MemberId));
        }

        public void StartWave()
        {
            if (Grid == null || !CanStartWave) return;
            if (levelPreview != null)
            {
                startPending = true;
                levelPreview.Close();
                return;
            }
            BattleSceneFlow.StartPreparedBattle(Preparation);
        }

        public bool TryPlaceMember(int memberId, PointerEventData data)
        {
            if (IsOverlayOpen) return false;
            if (Grid.TryPlaceWarehouseMember(memberId, data)) return true;
            warehouseHint.text = Preparation.DeployedCount >= SquadPreparation.MaxDeployed
                ? "Squad full (6/6). Replace or return a unit."
                : "Drop the unit onto a squad slot.";
            return false;
        }

        public void TryDismissMember(int memberId)
        {
            if (IsOverlayOpen || startPending || BattleSceneFlow.IsOutOfLives) return;
            var member = Preparation.FindMember(memberId);
            if (member == null || member.Unit.Rarity == UnitRarity.Legendary || Preparation.Members.Count <= 1) return;
            pendingDismissMemberId = memberId;
            dismissOverlay = new GameObject("Dismiss Unit Confirmation", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(dismissOverlay, 600, new Vector2(1280, 720), 0.5f);
            var backdrop = LegacyUiKit.CreateImage(dismissOverlay.transform, "Input Blocker", Vector2.zero,
                Vector2.zero, LegacyUiKit.DimColor);
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            var panel = PopupCard(dismissOverlay.transform, new Vector2(480, 390), "DISMISS THIS UNIT?", LegacyUiKit.Red);
            UnitDisc(panel, new Vector2(0, 70), 132, member.Unit);
            LegacyUiKit.CreateText(panel, "Unit Name", $"{LegacyUnitLooks.Name(member.Unit.Role)}  Lv.{member.Unit.Level}",
                new Vector2(0, -12), new Vector2(440, 34), 28, LegacyUiKit.Ink, FontStyles.Bold);
            LegacyUiKit.CreateText(panel, "Rarity", member.Unit.Rarity.ToString().ToUpperInvariant(),
                new Vector2(0, -42), new Vector2(440, 28), 19, LegacyUiKit.Darker(LegacyUnitLooks.RarityColor(member.Unit.Rarity), 0.85f), FontStyles.Bold, 2);
            LegacyUiKit.CreateText(panel, "No Refund", "No Gold will be refunded.", new Vector2(0, -78),
                new Vector2(440, 26), 18, LegacyUiKit.InkMuted);
            var cancel = LegacyUiKit.CreateButton(panel, "Cancel", new Vector2(-112, -140),
                new Vector2(192, 56), LegacyUiKit.ButtonSlate, 14);
            LegacyUiKit.CreateText(cancel.transform, "Label", "CANCEL", new Vector2(0, 3),
                new Vector2(184, 48), 23, Color.white, FontStyles.Bold);
            cancel.onClick.AddListener(CloseDismissConfirmation);
            var confirm = LegacyUiKit.CreateButton(panel, "Confirm", new Vector2(112, -140),
                new Vector2(192, 56), LegacyUiKit.Red, 14);
            LegacyUiKit.CreateText(confirm.transform, "Label", "CONFIRM", new Vector2(0, 3),
                new Vector2(184, 48), 23, Color.white, FontStyles.Bold);
            confirm.onClick.AddListener(ConfirmDismissMember);
        }

        public void ShowUnitIntroduction(UnitDefinition unit)
        {
            if (unit == null || IsOverlayOpen) return;
            unitIntroductionOverlay = new GameObject("New Unit Introduction", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(unitIntroductionOverlay, 600, new Vector2(1280, 720), 0.5f);
            var backdrop = LegacyUiKit.CreateImage(unitIntroductionOverlay.transform, "Input Blocker", Vector2.zero,
                Vector2.zero, LegacyUiKit.DimColor);
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            var panel = PopupCard(unitIntroductionOverlay.transform, new Vector2(600, 490), "NEW UNIT DISCOVERED!", LegacyUiKit.Gold);
            UnitDisc(panel, new Vector2(0, 118), 140, unit);
            LegacyUiKit.CreateText(panel, "Unit Name", $"{LegacyUnitLooks.Name(unit.Role)}  Lv.{unit.Level}",
                new Vector2(0, 30), new Vector2(550, 36), 30, LegacyUiKit.Ink, FontStyles.Bold);
            LegacyUiKit.CreateText(panel, "Rarity", unit.Rarity.ToString().ToUpperInvariant(),
                new Vector2(0, 0), new Vector2(550, 28), 19, LegacyUiKit.Darker(LegacyUnitLooks.RarityColor(unit.Rarity), 0.85f), FontStyles.Bold, 2);
            var box = LegacyUiKit.CreateImage(panel, "Description Box", new Vector2(0, -88),
                new Vector2(532, 132), LegacyUiKit.CardInnerColor, 16);
            var description = LegacyUnitIntroductionUI.LongDescription(unit);
            var text = LegacyUiKit.CreateText(box.transform, "Description", description, Vector2.zero,
                new Vector2(492, 112), 20, LegacyUiKit.Ink);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 17;
            text.fontSizeMax = 20;
            var close = LegacyUiKit.CreateButton(panel, "Got It", new Vector2(0, -197),
                new Vector2(240, 58), LegacyUiKit.NodeGreenColor, 14);
            LegacyUiKit.CreateIcon(close.transform, "Check", "check", new Vector2(-62, 3), 34);
            LegacyUiKit.CreateText(close.transform, "Label", "GOT IT", new Vector2(18, 3),
                new Vector2(180, 48), 26, Color.white, FontStyles.Bold);
            close.onClick.AddListener(CloseUnitIntroduction);
            RefreshClaimCards();
        }

        public bool IsUnitDetailsOpen => unitDetailsOverlay != null;

        /// <summary>Opens the full stat sheet for a storage card. Cards only show identity at a glance.</summary>
        public void ShowUnitDetails(int memberId)
        {
            if (IsOverlayOpen || startPending) return;
            var member = Preparation.FindMember(memberId);
            if (member == null) return;
            var unit = member.Unit;
            var rarityColor = LegacyUnitLooks.RarityColor(unit.Rarity);

            unitDetailsOverlay = new GameObject("Unit Details", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(unitDetailsOverlay, 600, new Vector2(1280, 720), 0.5f);
            // Clicking the dimmed backdrop closes the panel, like tapping outside a popup.
            var backdrop = LegacyUiKit.CreateImage(unitDetailsOverlay.transform, "Input Blocker", Vector2.zero,
                Vector2.zero, LegacyUiKit.DimColor);
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(CloseUnitDetails);

            var face = LegacyUiKit.CreateCard(unitDetailsOverlay.transform, "Frame", Vector2.zero,
                new Vector2(624, 524), 24, LegacyUiKit.CardColor, rarityColor, 6);
            var frame = (RectTransform)face.transform.parent;
            LegacyUiKit.CreateShadow(frame);
            frame.GetComponent<Image>().raycastTarget = true;
            face.raycastTarget = true;
            var panel = LegacyUiKit.CreateRect(frame, "Panel", Vector2.zero, new Vector2(620, 520));

            UnitDisc(panel, new Vector2(-210, 170), 128, unit);

            var name = LegacyUiKit.CreateText(panel, "Unit Name", $"{LegacyUnitLooks.Name(unit.Role)}  Lv.{unit.Level}",
                new Vector2(60, 200), new Vector2(400, 40), 34, LegacyUiKit.Ink, FontStyles.Bold);
            name.alignment = TextAlignmentOptions.Left;
            var rarity = LegacyUiKit.CreateText(panel, "Rarity", unit.Rarity.ToString().ToUpperInvariant(),
                new Vector2(60, 164), new Vector2(400, 28), 20, LegacyUiKit.Darker(rarityColor, 0.85f), FontStyles.Bold, 2);
            rarity.alignment = TextAlignmentOptions.Left;
            var deployed = Preparation.IsDeployed(member.MemberId);
            var status = LegacyUiKit.CreateText(panel, "Availability", deployed ? "IN SQUAD" : "AVAILABLE",
                new Vector2(60, 134), new Vector2(400, 24), 17, deployed ? LegacyUiKit.GreenDark : LegacyUiKit.InkMuted, FontStyles.Bold, 1);
            status.alignment = TextAlignmentOptions.Left;

            var box = LegacyUiKit.CreateImage(panel, "Description Box", new Vector2(0, 40),
                new Vector2(572, 112), LegacyUiKit.CardInnerColor, 16);
            var text = LegacyUiKit.CreateText(box.transform, "Description", LegacyUnitIntroductionUI.LongDescription(unit),
                Vector2.zero, new Vector2(532, 96), 19, LegacyUiKit.Ink);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14;
            text.fontSizeMax = 19;

            var stats = UnitDetailStats(unit);
            for (var i = 0; i < stats.Count; i++)
            {
                // Two columns of label/value rows under the description.
                var column = i % 2;
                var row = i / 2;
                var position = new Vector2(column == 0 ? -146 : 146, -48 - row * 38);
                var cellBg = LegacyUiKit.CreateImage(panel, "Stat " + stats[i].label, position, new Vector2(272, 32), LegacyUiKit.CardInnerColor, 10);
                var label = LegacyUiKit.CreateText(cellBg.transform, "Label", stats[i].label, Vector2.zero, new Vector2(248, 28),
                    16, LegacyUiKit.InkMuted, FontStyles.Bold, 2);
                label.alignment = TextAlignmentOptions.Left;
                var value = LegacyUiKit.CreateText(cellBg.transform, "Value", stats[i].value, Vector2.zero, new Vector2(248, 28),
                    20, LegacyUiKit.Ink, FontStyles.Bold);
                value.alignment = TextAlignmentOptions.Right;
            }

            var close = LegacyUiKit.CreateButton(panel, "Close", new Vector2(0, -214),
                new Vector2(220, 56), LegacyUiKit.ButtonSlate, 14);
            LegacyUiKit.CreateText(close.transform, "Label", "CLOSE", new Vector2(0, 3),
                new Vector2(212, 48), 24, Color.white, FontStyles.Bold);
            close.onClick.AddListener(CloseUnitDetails);

            GameAnalytics.Track("unit_details_open", ("role", unit.Role.ToString()), ("rarity", unit.Rarity.ToString()),
                ("unit_level", unit.Level), ("deployed", deployed));
        }

        // Two cream boards like the Paint Squad Army screen: unit storage on the left, the squad on the right.
        // They live on the scene canvas (same 1280x720 width-matched space as the squad screen), right above its
        // backdrop; wave controls are in the foreground squad canvas.
        private static void BuildPanels(Transform sceneCanvas)
        {
            if (sceneCanvas.Find("Storage Board") != null) return;
            var storage = LegacyUiKit.CreateCard(sceneCanvas, "Storage Board", new Vector2(-165, 18), new Vector2(860, 566), 26);
            var squad = LegacyUiKit.CreateCard(sceneCanvas, "Squad Board", new Vector2(465, 86), new Vector2(276, 366), 26);
            var index = sceneCanvas.Find(PaintSquadSceneSkin.BackdropName) != null ? 1 : 0;
            foreach (var board in new[] { (RectTransform)storage.transform.parent, (RectTransform)squad.transform.parent })
            {
                board.SetSiblingIndex(index++);
                var shadow = LegacyUiKit.CreateShadow(board);
                if (shadow != null) index++;
            }
        }

        // A cream popup card with a title ribbon across its top edge; returns the content rect.
        private static RectTransform PopupCard(Transform parent, Vector2 size, string title, Color ribbon)
        {
            var face = LegacyUiKit.CreateCard(parent, "Panel", Vector2.zero, size, 26, LegacyUiKit.CardColor, LegacyUiKit.CardRimColor, 6);
            var card = (RectTransform)face.transform.parent;
            LegacyUiKit.CreateShadow(card);
            card.GetComponent<Image>().raycastTarget = true;
            face.raycastTarget = true;
            LegacyUiKit.CreateRibbon(card, "Title", title, new Vector2(0, size.y / 2 + 4), new Vector2(Mathf.Min(size.x + 60, 560), 76), ribbon, 34);
            return card;
        }

        // The unit's portrait on a big rarity-colored disc with a white ring (Paint Squad mercenary cards).
        private static void UnitDisc(Transform parent, Vector2 position, float diameter, UnitDefinition unit)
        {
            var rarity = LegacyUnitLooks.RarityColor(unit.Rarity);
            LegacyUiKit.CreateCircle(parent, "Emblem Ring", position, diameter, rarity);
            var disc = LegacyUiKit.CreateCircle(parent, "Emblem", position, diameter * 0.9f, Color.Lerp(rarity, Color.white, 0.55f));
            LegacyUnitIconUI.Create(disc.transform, new Vector2(0, -diameter * 0.04f), diameter * 2.1f).SetType(unit.Role);
        }

        /// <summary>Only the stats that mean something for this role, so a Tank never shows a 0 ATK row.</summary>
        public static List<(string label, string value)> UnitDetailStats(UnitDefinition unit)
        {
            var stats = new List<(string label, string value)> { ("HP", $"{unit.MaxHp:0.##}") };
            if (SquadCombatRules.IsLegendaryRole(unit.Role)) stats.Add(("SKILL", "ACTIVE"));
            if (SquadCombatRules.CanAttackTowers(unit.Role))
            {
                stats.Add(("ATK", $"{unit.Damage:0.##}"));
                stats.Add(("ATK SPEED", $"{unit.AttackInterval:0.##}s"));
                stats.Add(("RANGE", $"{unit.Range:0.##}"));
                if (!Mathf.Approximately(unit.TowerDamageMultiplier, 1f))
                    stats.Add(("VS TOWERS", $"x{unit.TowerDamageMultiplier:0.##}"));
            }
            if (unit.Defense > 0) stats.Add(("DEF", $"{unit.Defense * 100:0}%"));
            if (unit.Role == UnitRole.Heal) stats.Add(("HEAL", $"{unit.HealPerSecond:0.##}/s"));
            return stats;
        }

        public void CloseUnitDetails()
        {
            if (unitDetailsOverlay == null) return;
            unitDetailsOverlay.SetActive(false);
            Destroy(unitDetailsOverlay);
            unitDetailsOverlay = null;
        }

        private void CloseUnitIntroduction()
        {
            if (unitIntroductionOverlay == null) return;
            unitIntroductionOverlay.SetActive(false);
            Destroy(unitIntroductionOverlay);
            unitIntroductionOverlay = null;
            Refresh();
            RefreshClaimCards();
        }

        private void CloseDismissConfirmation()
        {
            if (dismissOverlay != null)
            {
                dismissOverlay.SetActive(false);
                Destroy(dismissOverlay);
                dismissOverlay = null;
            }
            pendingDismissMemberId = 0;
        }

        private void ConfirmDismissMember()
        {
            if (dismissOverlay == null) return;
            var member = Preparation.FindMember(pendingDismissMemberId);
            CloseDismissConfirmation();
            if (member == null || !Preparation.TryDismissMember(member.MemberId)) return;
            warehouseHint.text = $"Dismissed {LegacyUnitLooks.Name(member.Unit.Role)} Lv.{member.Unit.Level}.";
            GameAnalytics.Track("unit_dismiss", ("role", member.Unit.Role.ToString()), ("rarity", member.Unit.Rarity.ToString()),
                ("unit_level", member.Unit.Level));
        }

        private void Refresh()
        {
            RefreshWarehouseCards();
            countLabel.text = $"{Preparation.DeployedCount}/{SquadPreparation.MaxDeployed}";
            startGameButton.interactable = CanStartWave && levelPreview == null;
            warehouseHint.text = "Drag units to squad  |  Click a card for details  |  X to dismiss";
            foreach (var member in Preparation.Members)
            {
                var deployed = Preparation.IsDeployed(member.MemberId);
                cardGroups[member.MemberId].alpha = deployed ? 0.45f : 1f;
                cardStatuses[member.MemberId].text = deployed ? "IN SQUAD" : "AVAILABLE";
            }
        }

        private void OnDestroy()
        {
            CloseSummonGuide();
            if (unitIntroductionOverlay != null) Destroy(unitIntroductionOverlay);
            CloseUnitDetails();
            CloseDismissConfirmation();
            if (levelPreview != null) Destroy(levelPreview.gameObject);
            if (claimOverlay != null) Destroy(claimOverlay.gameObject);
            if (startGameButton != null) startGameButton.onClick.RemoveListener(StartWave);
            if (Preparation != null) Preparation.Changed -= Refresh;
            if (screen != null) Destroy(screen);
        }
    }
}
