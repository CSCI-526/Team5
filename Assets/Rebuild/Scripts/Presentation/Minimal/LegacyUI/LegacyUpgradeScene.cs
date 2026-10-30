using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Warehouse introductions reuse the old SquadScreen style; only selected IDs enter battle.
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
        public Button PreviewNextLevelButton => previewButton;
        public Button ClaimCardButton => claimButton;
        public TMP_Text GoldLabel => goldLabel;
        private readonly Dictionary<int, CanvasGroup> cardGroups = new Dictionary<int, CanvasGroup>();
        private readonly Dictionary<int, TMP_Text> cardStatuses = new Dictionary<int, TMP_Text>();
        public SquadRoster Roster => roster;
        public SquadPreparation Preparation { get; private set; }
        public LegacyFormationGrid Grid { get; private set; }
        public Button StartWaveButton => startGameButton;
        public TMP_Text LevelLabel => levelLabel;

        private void Start()
        {
            Preparation = BattleSceneFlow.PrepareSquad(roster);
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
            // Keep Team5's level label style, positioned above the authored Start Wave button.
            levelLabel = LegacyUiKit.CreateText(root, "Level Label", "", Vector2.zero, new Vector2(360, 60),
                36, Color.white, FontStyles.Bold);
            levelLabel.font = startGameButton.GetComponentInChildren<TMP_Text>(true).font;
            levelLabel.alignment = TextAlignmentOptions.Bottom;
            levelLabel.rectTransform.anchorMin = levelLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            levelLabel.rectTransform.pivot = new Vector2(0.5f, 0);
            LegacyUiKit.CreateHeartIcon(levelLabel.transform, Vector2.zero);
            levelHeart = (RectTransform)levelLabel.transform.Find("Heart Icon");
            levelHeart.anchorMin = levelHeart.anchorMax = levelHeart.pivot = new Vector2(0.5f, 0.5f);
            previewButton = LegacyUiKit.CreateButton(root,"Preview Next Level",Vector2.zero,new Vector2(240,48),LegacyUiKit.PanelLineColor,6);
            var previewLabel = LegacyUiKit.CreateText(previewButton.transform,"Label","Preview Next Level",Vector2.zero,Vector2.zero,24,Color.white);
            LegacyUiKit.Stretch(previewLabel.rectTransform);
            previewLabel.font = levelLabel.font; previewLabel.enableAutoSizing = true;
            previewLabel.fontSizeMin = 14; previewLabel.fontSizeMax = 24;
            var previewRect = (RectTransform)previewButton.transform;
            previewRect.anchorMin = previewRect.anchorMax = new Vector2(.5f,.5f); previewRect.pivot = new Vector2(.5f,0);
            previewButton.onClick.AddListener(OpenNextLevelPreview);
            RefreshLevelLabel();
            LegacyUiKit.CreateText(root, "Warehouse Title", "UNIT STORAGE", new Vector2(-165, 300), new Vector2(780, 30),
                24, LegacyUiKit.TextColor, FontStyles.Bold, 3);
            for (var index = 0; index < SquadPreparation.WarehouseCapacity; index++) BuildWarehouseCell(root, index);
            renderedMembers = Preparation.Members.ToArray();
            var quota = LegacyUiKit.CreateImage(root,"Card Quota",new Vector2(-165,-160),new Vector2(330,10),LegacyUiKit.CellColor,5);
            quotaFill = LegacyUiKit.CreateImage(quota.transform,"Fill",Vector2.zero,new Vector2(330,10),LegacyUiKit.HighlightColor,5);
            quotaFill.rectTransform.anchorMin = quotaFill.rectTransform.anchorMax = quotaFill.rectTransform.pivot = new Vector2(0,.5f);
            quotaLabel = LegacyUiKit.CreateText(root,"Card Quota Text","",new Vector2(-165,-140),new Vector2(600,26),18,LegacyUiKit.HighlightColor);
            claimButton = LegacyUiKit.CreateButton(root,"Claim Card Button",new Vector2(-165,-210),new Vector2(330,58),LegacyUiKit.NodeGreenColor,6);
            claimLabel = LegacyUiKit.CreateText(claimButton.transform,"Label","CLAIM CARD",Vector2.zero,new Vector2(320,54),22,Color.white,FontStyles.Bold);
            claimButton.onClick.AddListener(OpenClaimCards);
            warehouseHint = LegacyUiKit.CreateText(root, "Warehouse Hint", "Drag units into the squad", new Vector2(-165, -115),
                new Vector2(780, 26), 16, LegacyUiKit.MutedColor);
            Grid = LegacyFormationGrid.CreateForPreparation(root, root, Preparation);
            ((RectTransform)Grid.transform).anchoredPosition = new Vector2(465, 43);
            LegacyUiKit.CreateText(Grid.transform, "Title", "SQUAD", new Vector2(0, 151), new Vector2(310, 26),
                18, LegacyUiKit.TextColor, FontStyles.Bold, 3);
            countLabel = LegacyUiKit.CreateText(Grid.transform, "Squad Count", "", new Vector2(0, 189), new Vector2(310, 30),
                26, LegacyUiKit.TextColor, FontStyles.Bold);
            Preparation.Changed += Refresh;
            startGameButton.onClick.AddListener(StartWave);
            Refresh();
        }

        private void Update()
        {
            RefreshLevelLabel(); RefreshGoldLabel(); RefreshClaimCards();
            if (startGameButton != null && Preparation != null)
                startGameButton.interactable = Preparation.CanStart && !BattleSceneFlow.IsOutOfLives && claimOverlay == null && levelPreview == null;
            if(previewButton != null) previewButton.interactable = levelPreview == null && claimOverlay == null && !BattleSceneFlow.IsOutOfLives;
        }

        public void OpenNextLevelPreview()
        {
            if(levelPreview != null || claimOverlay != null || BattleSceneFlow.IsOutOfLives) return;
            levelPreview = LegacyNextLevelPreview.Open(this);
        }
        public void OnNextLevelPreviewClosed() { levelPreview = null; Refresh(); }

        public void OpenClaimCards()
        {
            if(claimOverlay != null || levelPreview != null || BattleSceneFlow.IsOutOfLives) return;
            claimOverlay = LegacyClaimCardOverlay.Open(this);
            RefreshClaimCards();
        }
        public void OnClaimCardsClosed() { claimOverlay = null; Refresh(); RefreshClaimCards(); }
        private void RefreshClaimCards()
        {
            var shop = BattleSceneFlow.CardShop;
            if(claimButton == null || shop == null) return;
            claimButton.interactable = shop.CanClaim && claimOverlay == null && levelPreview == null && !BattleSceneFlow.IsOutOfLives;
            claimButton.image.color = claimButton.interactable ? LegacyUiKit.NodeGreenColor : LegacyUiKit.PanelLineColor;
            claimLabel.text = shop.HasNextCard ? $"CLAIM CARD ({shop.NextPrice} GOLD)" : "ALL CARDS CLAIMED";
            quotaLabel.text = shop.HasNextCard ? $"{BattleSceneFlow.GoldBalance} / {shop.NextPrice} gold in bank" : "All cards taken";
            quotaFill.enabled = shop.HasNextCard && BattleSceneFlow.GoldBalance > 0;
            quotaFill.rectTransform.sizeDelta = new Vector2(shop.HasNextCard ? 330 * Mathf.Clamp01((float)BattleSceneFlow.GoldBalance / shop.NextPrice) : 0,10);
        }

        private void RefreshWarehouseCards()
        {
            if(renderedMembers != null && Preparation.Members.SequenceEqual(renderedMembers)) return;
            foreach(var cell in warehouseCells) { cell.SetActive(false); Destroy(cell); }
            warehouseCells.Clear(); cardGroups.Clear(); cardStatuses.Clear();
            for(var index=0; index<SquadPreparation.WarehouseCapacity; index++) BuildWarehouseCell(screenRoot,index);
            renderedMembers = Preparation.Members.ToArray();
        }

        private void RefreshGoldLabel()
        {
            if (goldLabel != null) goldLabel.text = $"$ {BattleSceneFlow.GoldBalance:N0}";
        }

        private void RefreshLevelLabel()
        {
            if (levelLabel == null) return;
            var lives = $"{BattleSceneFlow.CurrentLevelLivesRemaining}/{BattleSession.DefaultLivesPerLevel}";
            levelLabel.text = $"LEVEL {Mathf.Max(1, BattleSceneFlow.NextBattleLevelNumber)}  <space=30>{lives}";
            var buttonRect = (RectTransform)startGameButton.transform;
            var top = buttonRect.TransformPoint(new Vector3(buttonRect.rect.center.x, buttonRect.rect.yMax, 0));
            var point = levelLabel.rectTransform.parent.InverseTransformPoint(top);
            var labelBottom = point.y + 12;
            if(previewButton != null)
            {
                var parent = levelLabel.rectTransform.parent;
                var left = parent.InverseTransformPoint(buttonRect.TransformPoint(new Vector3(buttonRect.rect.xMin,buttonRect.rect.center.y,0)));
                var right = parent.InverseTransformPoint(buttonRect.TransformPoint(new Vector3(buttonRect.rect.xMax,buttonRect.rect.center.y,0)));
                var bottom = parent.InverseTransformPoint(buttonRect.TransformPoint(new Vector3(buttonRect.rect.center.x,buttonRect.rect.yMin,0)));
                var rect = (RectTransform)previewButton.transform;
                rect.sizeDelta = new Vector2(Vector3.Distance(left,right)*.85f,Vector3.Distance(point,bottom)*.75f);
                rect.anchoredPosition = new Vector2(point.x,point.y+12);
                labelBottom = rect.anchoredPosition.y + rect.sizeDelta.y + 12;
            }
            levelLabel.rectTransform.anchoredPosition = new Vector2(point.x,labelBottom);
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
            // Six columns, two rows. Keep the old introduction panel at 60% scale inside each cell.
            var position = new Vector2(-500 + index % SquadPreparation.WarehouseColumns * 134, 185 - index / SquadPreparation.WarehouseColumns * 180);
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
            panelImage.raycastTarget = true;
            panel.gameObject.AddComponent<WarehouseUnitCardUI>().Initialize(this, root, member);
            cardGroups[member.MemberId] = cell.gameObject.AddComponent<CanvasGroup>();
            cardStatuses[member.MemberId] = LegacyUnitIntroductionUI.Build(panel,member.Unit);
        }

        public void StartWave()
        {
            if (Grid == null || !Preparation.CanStart || BattleSceneFlow.IsOutOfLives || claimOverlay != null || levelPreview != null) return;
            BattleSceneFlow.StartPreparedBattle(Preparation);
        }

        public bool TryPlaceMember(int memberId, PointerEventData data)
        {
            if (Grid.TryPlaceWarehouseMember(memberId, data)) return true;
            warehouseHint.text = Preparation.DeployedCount >= SquadPreparation.MaxDeployed
                ? "Squad full (6/6). Replace or return a unit." : "Drop the unit onto a squad slot.";
            return false;
        }

        private void Refresh()
        {
            RefreshWarehouseCards();
            countLabel.text = $"{Preparation.DeployedCount}/{SquadPreparation.MaxDeployed}";
            startGameButton.interactable = Preparation.CanStart && !BattleSceneFlow.IsOutOfLives && claimOverlay == null && levelPreview == null;
            warehouseHint.text = BattleSceneFlow.PendingUnitRewardCount > 0
                ? $"Storage full: {BattleSceneFlow.PendingUnitRewardCount} reward units waiting."
                : "Drag units into the squad";
            foreach (var member in Preparation.Members)
            {
                var deployed = Preparation.IsDeployed(member.MemberId);
                cardGroups[member.MemberId].alpha = deployed ? 0.45f : 1;
                cardStatuses[member.MemberId].text = deployed ? "IN SQUAD" : "AVAILABLE";
            }
        }

        private void OnDestroy()
        {
            if(levelPreview != null) Destroy(levelPreview.gameObject);
            if(claimOverlay != null) Destroy(claimOverlay.gameObject);
            if (startGameButton != null) startGameButton.onClick.RemoveListener(StartWave);
            if (Preparation != null) Preparation.Changed -= Refresh;
            if (screen != null) Destroy(screen);
        }
    }
}
