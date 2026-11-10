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
            screen = new GameObject("Squad Screen", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(screen, 10, new Vector2(1280, 720), 0);
            var root = (RectTransform)screen.transform;
            screenRoot = root;

            goldLabel = LegacyUiKit.CreateText(root, "Gold Balance", "", Vector2.zero, new Vector2(260, 48),
                30, LegacyUiKit.HighlightColor, FontStyles.Bold);
            goldLabel.font = startGameButton.GetComponentInChildren<TMP_Text>(true).font;
            goldLabel.alignment = TextAlignmentOptions.Right;
            goldLabel.rectTransform.anchorMin = goldLabel.rectTransform.anchorMax = new Vector2(1, 1);
            goldLabel.rectTransform.pivot = new Vector2(1, 1);
            goldLabel.rectTransform.anchoredPosition = new Vector2(-28, -20);
            RefreshGoldLabel();

            levelLabel = LegacyUiKit.CreateText(root, "Level Label", "", Vector2.zero, new Vector2(360, 60),
                36, Color.white, FontStyles.Bold);
            levelLabel.font = startGameButton.GetComponentInChildren<TMP_Text>(true).font;
            levelLabel.alignment = TextAlignmentOptions.Bottom;
            levelLabel.rectTransform.anchorMin = levelLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            levelLabel.rectTransform.pivot = new Vector2(0.5f, 0);
            LegacyUiKit.CreateHeartIcon(levelLabel.transform, Vector2.zero);
            levelHeart = (RectTransform)levelLabel.transform.Find("Heart Icon");
            levelHeart.anchorMin = levelHeart.anchorMax = levelHeart.pivot = new Vector2(0.5f, 0.5f);

            previewButton = LegacyUiKit.CreateButton(root, "Preview Next Level", Vector2.zero, new Vector2(240, 48), LegacyUiKit.PanelLineColor, 6);
            var previewLabel = LegacyUiKit.CreateText(previewButton.transform, "Label", "Preview Next Level", Vector2.zero, Vector2.zero, 24, Color.white);
            LegacyUiKit.Stretch(previewLabel.rectTransform);
            previewLabel.font = levelLabel.font;
            previewLabel.enableAutoSizing = true;
            previewLabel.fontSizeMin = 14;
            previewLabel.fontSizeMax = 24;
            var previewRect = (RectTransform)previewButton.transform;
            previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0);
            previewButton.onClick.AddListener(OpenNextLevelPreview);
            RefreshLevelLabel();

            LegacyUiKit.CreateText(root, "Warehouse Title", "UNIT STORAGE", new Vector2(-165, 300), new Vector2(780, 30),
                24, LegacyUiKit.TextColor, FontStyles.Bold, 3);
            for (var index = 0; index < SquadPreparation.WarehouseCapacity; index++)
                BuildWarehouseCell(root, index);
            renderedMembers = Preparation.Members.ToArray();

            var quota = LegacyUiKit.CreateImage(root, "Card Quota", new Vector2(-165, -160), new Vector2(330, 10), LegacyUiKit.CellColor, 5);
            quotaFill = LegacyUiKit.CreateImage(quota.transform, "Fill", Vector2.zero, new Vector2(330, 10), LegacyUiKit.HighlightColor, 5);
            quotaFill.rectTransform.anchorMin = quotaFill.rectTransform.anchorMax = quotaFill.rectTransform.pivot = new Vector2(0, 0.5f);

            quotaLabel = LegacyUiKit.CreateText(root, "Card Quota Text", "", new Vector2(-165, -140), new Vector2(740, 26), 16, LegacyUiKit.HighlightColor);
            claimButton = LegacyUiKit.CreateButton(root, "Claim Card Button", new Vector2(-165, -210), new Vector2(330, 58), LegacyUiKit.NodeGreenColor, 6);
            claimLabel = LegacyUiKit.CreateText(claimButton.transform, "Label", "SUMMON UNIT", Vector2.zero, new Vector2(320, 54), 22, Color.white, FontStyles.Bold);
            claimButton.onClick.AddListener(OpenClaimCards);

            warehouseHint = LegacyUiKit.CreateText(root, "Warehouse Hint", "Drag units to squad  |  Click X on a card to dismiss",
                new Vector2(-165, -115), new Vector2(780, 26), 16, LegacyUiKit.MutedColor);

            Grid = LegacyFormationGrid.CreateForPreparation(root, root, Preparation);
            ((RectTransform)Grid.transform).anchoredPosition = new Vector2(465, 43);
            LegacyUiKit.CreateText(Grid.transform, "Title", "SQUAD", new Vector2(0, 151), new Vector2(310, 26),
                18, LegacyUiKit.TextColor, FontStyles.Bold, 3);
            countLabel = LegacyUiKit.CreateText(Grid.transform, "Squad Count", "", new Vector2(0, 189), new Vector2(310, 30),
                26, LegacyUiKit.TextColor, FontStyles.Bold);

            Preparation.Changed += Refresh;
            startGameButton.onClick.AddListener(StartWave);
            Refresh();
            CaptainGuide.Attach(this, root);
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
                previewButton.interactable = levelPreview == null && claimOverlay == null && dismissOverlay == null && unitIntroductionOverlay == null && summonGuideOverlay == null && !BattleSceneFlow.IsOutOfLives && !startPending;
        }

        public bool CanStartWave =>
            Preparation != null && Preparation.CanStart && !BattleSceneFlow.IsOutOfLives && claimOverlay == null && dismissOverlay == null && unitIntroductionOverlay == null && summonGuideOverlay == null && !startPending;

        public bool IsOverlayOpen => claimOverlay != null || levelPreview != null || dismissOverlay != null || unitIntroductionOverlay != null || summonGuideOverlay != null;

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
                dismissOverlay != null || unitIntroductionOverlay != null) return;
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
            StartWaveNotice.Attach(this, screenRoot);
        }

        private void ShowSummonGuide()
        {
            summonGuideOverlay = new GameObject("First Summon Guide", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(summonGuideOverlay, 900, new Vector2(1280, 720), 0.5f);
            var shade = LegacyUiKit.CreateImage(summonGuideOverlay.transform, "Input Blocker", Vector2.zero,
                Vector2.zero, new Color(0, 0, 0, 0.78f));
            LegacyUiKit.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            var banner = LegacyUiKit.CreateImage(summonGuideOverlay.transform, "Tutorial Banner", Vector2.zero,
                new Vector2(880, 140), new Color(0.08f, 0.11f, 0.17f, 0.96f));
            banner.raycastTarget = true;
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(0.5f, 1);
            banner.rectTransform.pivot = new Vector2(0.5f, 1);
            banner.rectTransform.anchoredPosition = new Vector2(0, -24);
            var outline = banner.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.85f, 0.4f, 1);
            outline.effectDistance = new Vector2(3, -3);
            LegacyUiKit.CreateText(banner.transform, "Title", "RECRUIT YOUR SQUAD", new Vector2(0, 32),
                new Vector2(820, 42), 30, new Color(0.35f, 0.95f, 0.45f), FontStyles.Bold);
            LegacyUiKit.CreateText(banner.transform, "Body", "Click SUMMON UNIT to recruit a new teammate.",
                new Vector2(0, -22), new Vector2(820, 46), 23, Color.white);
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
            claimButton.interactable = shop.CanClaim && claimOverlay == null && levelPreview == null && dismissOverlay == null && unitIntroductionOverlay == null && !BattleSceneFlow.IsOutOfLives;
            claimButton.image.color = claimButton.interactable ? LegacyUiKit.NodeGreenColor : LegacyUiKit.PanelLineColor;
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
            if (goldLabel != null) goldLabel.text = BattleSceneFlow.HasUnlimitedGold ? "$ UNLIMITED" : $"$ {BattleSceneFlow.GoldBalance:N0}";
        }

        private void RefreshLevelLabel()
        {
            if (levelLabel == null) return;
            var lives = $"{BattleSceneFlow.CurrentLevelLivesRemaining}/{BattleSession.DefaultLivesPerLevel}";
            var levelName = BattleSceneFlow.IsTestRun ? "TEST LEVEL" : $"LEVEL {Mathf.Max(1, BattleSceneFlow.NextBattleLevelNumber)}";
            levelLabel.text = $"{levelName}  <space=30>{lives}";
            var buttonRect = (RectTransform)startGameButton.transform;
            var top = buttonRect.TransformPoint(new Vector3(buttonRect.rect.center.x, buttonRect.rect.yMax, 0));
            var point = levelLabel.rectTransform.parent.InverseTransformPoint(top);
            var labelBottom = point.y + 12;
            if (previewButton != null)
            {
                var parent = levelLabel.rectTransform.parent;
                var left = parent.InverseTransformPoint(buttonRect.TransformPoint(new Vector3(buttonRect.rect.xMin, buttonRect.rect.center.y, 0)));
                var right = parent.InverseTransformPoint(buttonRect.TransformPoint(new Vector3(buttonRect.rect.xMax, buttonRect.rect.center.y, 0)));
                var bottom = parent.InverseTransformPoint(buttonRect.TransformPoint(new Vector3(buttonRect.rect.center.x, buttonRect.rect.yMin, 0)));
                var rect = (RectTransform)previewButton.transform;
                rect.sizeDelta = new Vector2(Vector3.Distance(left, right) * 0.85f, Vector3.Distance(point, bottom) * 0.75f);
                rect.anchoredPosition = new Vector2(point.x, point.y + 12);
                labelBottom = rect.anchoredPosition.y + rect.sizeDelta.y + 12;
            }
            levelLabel.rectTransform.anchoredPosition = new Vector2(point.x, labelBottom);
            if (levelHeart != null)
            {
                levelLabel.ForceMeshUpdate();
                var info = levelLabel.textInfo;
                if (info.characterCount >= lives.Length)
                {
                    var first = info.characterInfo[info.characterCount - lives.Length];
                    levelHeart.localPosition = new Vector3(first.bottomLeft.x - 18,
                        (first.topRight.y + first.bottomLeft.y) / 2, 0);
                }
            }
        }

        private void BuildWarehouseCell(RectTransform root, int index)
        {
            var position = WarehouseCellPosition(index);
            var size = new Vector2(200, 262);
            var cell = LegacyUiKit.CreateImage(root, "Warehouse Cell " + index, position, size + Vector2.one * 4, LegacyUiKit.PanelLineColor, 14).rectTransform;
            warehouseCells.Add(cell.gameObject);
            cell.localScale = Vector3.one * 0.6f;
            var panelImage = LegacyUiKit.CreateImage(cell, "Introduction", Vector2.zero, size, LegacyUiKit.PanelColor, 12);
            var panel = panelImage.rectTransform;
            if (index >= Preparation.Members.Count)
            {
                LegacyUiKit.CreateText(panel, "Empty", "EMPTY", Vector2.zero, size, 18, LegacyUiKit.MutedColor);
                return;
            }
            var member = Preparation.Members[index];
            cell.GetComponent<Image>().color = LegacyUnitLooks.RarityColor(member.Unit.Rarity);
            panelImage.raycastTarget = true;
            panel.gameObject.AddComponent<WarehouseUnitCardUI>().Initialize(this, root, member);
            cardGroups[member.MemberId] = cell.gameObject.AddComponent<CanvasGroup>();
            cardStatuses[member.MemberId] = LegacyUnitIntroductionUI.Build(panel, member.Unit);
            var remove = LegacyUiKit.MakeButton(LegacyUiKit.CreateCircle(panel, "Dismiss Unit", Vector2.zero, 42,
                new Color(0.72f, 0.18f, 0.22f)));
            var removeRect = (RectTransform)remove.transform;
            removeRect.anchorMin = removeRect.anchorMax = new Vector2(1, 1);
            removeRect.anchoredPosition = new Vector2(-23, -23);
            // Draw the cross with UI geometry so it does not depend on emoji font support.
            for (var i = 0; i < 2; i++)
            {
                var stroke = LegacyUiKit.CreateImage(remove.transform, "Cross " + i, Vector2.zero,
                    new Vector2(20, 4), Color.white);
                stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45 : -45);
            }
            remove.interactable = member.Unit.Role != UnitRole.Captain && Preparation.Members.Count > 1;
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
            if (member == null || member.Unit.Role == UnitRole.Captain || Preparation.Members.Count <= 1) return;
            pendingDismissMemberId = memberId;
            dismissOverlay = new GameObject("Dismiss Unit Confirmation", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(dismissOverlay, 600, new Vector2(1280, 720), 0.5f);
            var backdrop = LegacyUiKit.CreateImage(dismissOverlay.transform, "Input Blocker", Vector2.zero,
                Vector2.zero, new Color(0, 0, 0, 0.78f));
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            var panel = LegacyUiKit.CreateImage(dismissOverlay.transform, "Panel", Vector2.zero,
                new Vector2(480, 390), LegacyUiKit.PanelColor, 14).rectTransform;
            panel.GetComponent<Image>().raycastTarget = true;
            LegacyUiKit.CreateText(panel, "Title", "DISMISS THIS UNIT?", new Vector2(0, 145),
                new Vector2(440, 44), 28, Color.white, FontStyles.Bold);
            LegacyUnitIconUI.Create(panel, new Vector2(0, 75), 150).SetType(member.Unit.Role);
            LegacyUiKit.CreateText(panel, "Unit Name", $"{LegacyUnitLooks.Name(member.Unit.Role)}  Lv.{member.Unit.Level}",
                new Vector2(0, 12), new Vector2(440, 34), 24, Color.white, FontStyles.Bold);
            LegacyUiKit.CreateText(panel, "Rarity", member.Unit.Rarity.ToString().ToUpperInvariant(),
                new Vector2(0, -24), new Vector2(440, 28), 19, LegacyUnitLooks.RarityColor(member.Unit.Rarity));
            LegacyUiKit.CreateText(panel, "No Refund", "No Gold will be refunded.", new Vector2(0, -72),
                new Vector2(440, 26), 17, LegacyUiKit.MutedColor);
            var cancel = LegacyUiKit.CreateButton(panel, "Cancel", new Vector2(-112, -140),
                new Vector2(192, 52), LegacyUiKit.PanelLineColor, 6);
            LegacyUiKit.CreateText(cancel.transform, "Label", "CANCEL", Vector2.zero,
                new Vector2(184, 48), 21, Color.white, FontStyles.Bold);
            cancel.onClick.AddListener(CloseDismissConfirmation);
            var confirm = LegacyUiKit.CreateButton(panel, "Confirm", new Vector2(112, -140),
                new Vector2(192, 52), new Color(0.72f, 0.18f, 0.22f), 6);
            LegacyUiKit.CreateText(confirm.transform, "Label", "CONFIRM", Vector2.zero,
                new Vector2(184, 48), 21, Color.white, FontStyles.Bold);
            confirm.onClick.AddListener(ConfirmDismissMember);
        }

        public void ShowUnitIntroduction(UnitDefinition unit)
        {
            if (unit == null || IsOverlayOpen) return;
            unitIntroductionOverlay = new GameObject("New Unit Introduction", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(unitIntroductionOverlay, 600, new Vector2(1280, 720), 0.5f);
            var backdrop = LegacyUiKit.CreateImage(unitIntroductionOverlay.transform, "Input Blocker", Vector2.zero,
                Vector2.zero, new Color(0, 0, 0, 0.8f));
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            var panel = LegacyUiKit.CreateImage(unitIntroductionOverlay.transform, "Panel", Vector2.zero,
                new Vector2(600, 490), LegacyUiKit.PanelColor, 16).rectTransform;
            panel.GetComponent<Image>().raycastTarget = true;
            LegacyUiKit.CreateText(panel, "Title", "NEW UNIT DISCOVERED", new Vector2(0, 194),
                new Vector2(550, 42), 28, LegacyUiKit.HighlightColor, FontStyles.Bold);
            LegacyUnitIconUI.Create(panel, new Vector2(0, 120), 170).SetType(unit.Role);
            LegacyUiKit.CreateText(panel, "Unit Name", $"{LegacyUnitLooks.Name(unit.Role)}  Lv.{unit.Level}",
                new Vector2(0, 51), new Vector2(550, 36), 26, Color.white, FontStyles.Bold);
            LegacyUiKit.CreateText(panel, "Rarity", unit.Rarity.ToString().ToUpperInvariant(),
                new Vector2(0, 16), new Vector2(550, 28), 18, LegacyUnitLooks.RarityColor(unit.Rarity));
            var box = LegacyUiKit.CreateImage(panel, "Description Box", new Vector2(0, -82),
                new Vector2(532, 144), LegacyUiKit.CellColor, 8);
            var description = unit.Role switch
            {
                UnitRole.Attack => "A steady tower breaker with fast attacks and 1.5x damage against towers. Place it where it can fire while tougher teammates take the hits.",
                UnitRole.Tank => "High health and damage reduction help this unit absorb incoming fire. Protect teammates through positioning. It cannot attack or taunt enemies.",
                UnitRole.Heal => "Heals teammates in the eight neighboring formation slots every 2 seconds. Cannot heal itself. A central position lets it cover more allies.",
                UnitRole.Sniper => "The longest attack range in the squad, with powerful shots and 1.85x damage against towers. Its low health makes safe positioning essential.",
                UnitRole.Vanguard => "An armored fighter that combines damage reduction with tower attacks. Useful in positions that need to absorb damage while helping break towers.",
                UnitRole.Demolisher => "A siege specialist dealing 2.25x damage against towers. Its attacks hit one tower at a time. Keep it protected and within range to make the most of its damage.",
                UnitRole.Captain => "A durable leader that can attack towers. Use Edit Formation to pause the battle and reposition your squad around enemy firing lanes.",
                _ => "Arrange this unit in your squad before starting the next battle."
            };
            var text = LegacyUiKit.CreateText(box.transform, "Description", description, Vector2.zero,
                new Vector2(492, 116), 20, LegacyUiKit.TextColor);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 17;
            text.fontSizeMax = 20;
            var close = LegacyUiKit.CreateButton(panel, "Got It", new Vector2(0, -197),
                new Vector2(240, 52), LegacyUiKit.NodeGreenColor, 6);
            LegacyUiKit.CreateText(close.transform, "Label", "GOT IT", Vector2.zero,
                new Vector2(232, 48), 22, Color.white, FontStyles.Bold);
            close.onClick.AddListener(CloseUnitIntroduction);
            RefreshClaimCards();
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
            warehouseHint.text = "Drag units into squad  |  Click X on a card to dismiss";
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
            CloseDismissConfirmation();
            if (levelPreview != null) Destroy(levelPreview.gameObject);
            if (claimOverlay != null) Destroy(claimOverlay.gameObject);
            if (startGameButton != null) startGameButton.onClick.RemoveListener(StartWave);
            if (Preparation != null) Preparation.Changed -= Refresh;
            if (screen != null) Destroy(screen);
        }
    }
}
