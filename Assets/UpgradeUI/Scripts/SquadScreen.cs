using System;
using ReverseTD.Offense;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReverseTD.UpgradeUI
{
    public class SquadScreen : MonoBehaviour
    {
        private const int SortingOrder = 10;
        private const float EmblemSize = 96f;
        private const float EmblemRing = 4f;
        private const float EmblemGlow = 18f;
        private const float EmblemGlowAlpha = 0.22f;
        private const float EmblemIconScale = 1.15f;
        private const float LockShade = 0.72f;
        private const float EmblemY = 240f;
        private const float EmblemSpacing = 140f;
        private const float NameY = 162f;
        private const float CountY = 139f;
        private const float BranchY = 122f;
        private const float NodeY = 20f;
        private const float NodeOffsetX = 105f;
        private const float NodeRadius = 6f;
        private const float LineThickness = 4f;
        private const float StatsY = -56f;
        private const float GridCell = 58f;
        private const float GridGap = 8f;
        private const float QuotaLabelY = -148f;
        private const float QuotaBarY = -173f;
        private const float QuotaTextY = -200f;
        private const float QuotaHintY = -224f;

        private static readonly Vector2 referenceResolution = new Vector2(1280f, 720f);
        private static readonly Vector2 nodeSize = new Vector2(210f, 92f);
        private static readonly Vector2 gridPosition = new Vector2(465f, 43f);
        private static readonly Vector2 quotaBarSize = new Vector2(400f, 14f);
        private static readonly Color lineColor = new Color(1f, 1f, 1f, 0.4f);
        private static readonly UnitType[] emblemTypes = { UnitType.Attack, UnitType.Tank, UnitType.Heal };
        private static readonly UnitStat[] attackStats = { UnitStat.AttackDamage, UnitStat.AttackHealth };
        private static readonly UnitStat[] tankStats = { UnitStat.TankDefense, UnitStat.TankHealth };
        private static readonly UnitStat[] healStats = { UnitStat.HealRate, UnitStat.HealHealth };

        private readonly Image[] emblemRings = new Image[3];
        private readonly Image[] emblemGlows = new Image[3];
        private readonly GameObject[] emblemLocks = new GameObject[3];
        private readonly TextMeshProUGUI[] emblemNames = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] emblemCounts = new TextMeshProUGUI[3];
        private OffenseData data;
        private Action goldChanged;
        private RectTransform root;
        private RectTransform branch;
        private SquadGridUI grid;
        private Image quotaBack;
        private Image quotaFill;
        private TextMeshProUGUI quotaText;
        private TextMeshProUGUI quotaHint;
        private UnitType selected = UnitType.Attack;
        private bool offerShown;

        public static SquadScreen Create(OffenseData data, Action goldChanged)
        {
            if (data == null)
            {
                Debug.LogError("SquadScreen needs OffenseData.");
                return null;
            }

            var host = new GameObject("Squad Screen", typeof(RectTransform));
            var screen = host.AddComponent<SquadScreen>();
            screen.data = data;
            screen.goldChanged = goldChanged;
            screen.Build();
            return screen;
        }

        public void Refresh()
        {
            data.EnsureSquad();
            if (!data.IsUnlocked(selected))
            {
                selected = UnitType.Attack;
            }

            for (int i = 0; i < emblemTypes.Length; i++)
            {
                UnitType type = emblemTypes[i];
                bool unlocked = data.IsUnlocked(type);
                bool isSelected = type == selected;
                int count = data.CountUnits(type);

                emblemGlows[i].enabled = isSelected;
                emblemRings[i].color = isSelected ? UiKit.GoldColor : UiKit.PanelLineColor;
                emblemLocks[i].SetActive(!unlocked);
                emblemNames[i].color = unlocked ? UiKit.TextColor : UiKit.MutedColor;
                emblemCounts[i].text = !unlocked ? "LOCKED" : count == 1 ? "1 unit" : count + " units";
                emblemCounts[i].color = isSelected ? UiKit.GoldColor : UiKit.MutedColor;
            }

            BuildBranch();
            grid.Refresh();
            RefreshQuota();
        }

        private void Start()
        {
            if (!offerShown && data.IsCardReady && data.CanAffordCard)
            {
                offerShown = true;
                AugmentOverlay.Open(data, OnGoldChanged, Refresh);
            }
        }

        private void Build()
        {
            UiKit.CreateCanvas(gameObject, SortingOrder, referenceResolution, 0f);
            root = (RectTransform)transform;
            data.EnsureSquad();

            for (int i = 0; i < emblemTypes.Length; i++)
            {
                BuildEmblem(i);
            }
            grid = SquadGridUI.Create(root, root, data, gridPosition, GridCell, GridGap, "SQUAD", "Drag to swap", true, Refresh);
            BuildQuota();
            Refresh();
        }

        private void BuildEmblem(int index)
        {
            UnitType type = emblemTypes[index];
            float x = (index - 1) * EmblemSpacing;
            var position = new Vector2(x, EmblemY);

            Color glow = UiKit.GoldColor;
            glow.a = EmblemGlowAlpha;
            emblemGlows[index] = UiKit.CreateCircle(root, "Emblem Glow", position, EmblemSize + EmblemGlow, glow);
            emblemRings[index] = UiKit.CreateCircle(root, "Emblem Ring", position, EmblemSize, UiKit.PanelLineColor);

            Image face = UiKit.CreateCircle(root, "Emblem " + UnitLooks.Name(type), position, EmblemSize - EmblemRing * 2f, UiKit.PanelColor);
            UiKit.MakeButton(face).onClick.AddListener(() => Select(type));
            UnitIconUI.Create(face.transform, Vector2.zero, EmblemSize * EmblemIconScale).SetType(type);

            Color shade = UiKit.PanelColor;
            shade.a = LockShade;
            Image locked = UiKit.CreateCircle(face.transform, "Locked", Vector2.zero, EmblemSize - EmblemRing * 2f, shade);
            UiKit.CreateLock(locked.transform, Vector2.zero, 1f);
            emblemLocks[index] = locked.gameObject;

            emblemNames[index] = UiKit.CreateText(root, "Name", UnitLooks.Name(type), new Vector2(x, NameY), new Vector2(EmblemSpacing, 24f), 18f,
                UiKit.TextColor, FontStyles.Bold, 4f);
            emblemCounts[index] = UiKit.CreateText(root, "Count", string.Empty, new Vector2(x, CountY), new Vector2(EmblemSpacing, 20f), 14f,
                UiKit.MutedColor);
        }

        private void BuildBranch()
        {
            if (branch != null)
            {
                Destroy(branch.gameObject);
            }

            float x = (Array.IndexOf(emblemTypes, selected) - 1) * EmblemSpacing;
            branch = UiKit.CreateRect(root, "Branch", Vector2.zero, Vector2.zero);
            branch.SetAsFirstSibling();
            RectTransform start = UiKit.CreateRect(branch, "Branch Start", new Vector2(x, BranchY), Vector2.zero);

            UnitStat[] stats = StatsOf(selected);
            for (int i = 0; i < stats.Length; i++)
            {
                var position = new Vector2(x + (i == 0 ? -NodeOffsetX : NodeOffsetX), NodeY);
                RectTransform node = BuildNode(stats[i], position);
                UiKit.Connect(branch, start, node, LineThickness, lineColor);
            }

            UiKit.CreateText(branch, "Now", NowText(selected), new Vector2(x, StatsY), new Vector2(nodeSize.x * 2f, 24f), 16f, UiKit.MutedColor);
        }

        private RectTransform BuildNode(UnitStat stat, Vector2 position)
        {
            int level = data.GetLevel(stat);
            int maxLevel = SquadRules.MaxLevel(stat);
            bool maxed = data.IsMaxed(stat);
            double cost = data.GetUpgradeCost(stat);
            bool affordable = !maxed && data.gold >= cost;
            Color color = maxed ? UiKit.NodeBlueColor : affordable ? UiKit.NodeGreenColor : UiKit.NodeGreyColor;

            Button button = UiKit.CreateButton(branch, "Node " + stat, position, nodeSize, color, NodeRadius);
            button.onClick.AddListener(() => Buy(stat));

            string levelText = maxed
                ? $"Lv {level}/{maxLevel} | MAXED"
                : $"Lv {level}/{maxLevel} | <color={UiKit.CostColorTag}>Cost: ${cost:0}</color>";
            UiKit.CreateText(button.transform, "Title", StepText(stat), new Vector2(0f, 15f), new Vector2(nodeSize.x, 30f), 22f, Color.white, FontStyles.Bold);
            UiKit.CreateText(button.transform, "Level", levelText, new Vector2(0f, -18f), new Vector2(nodeSize.x, 24f), 17f, Color.white);
            return (RectTransform)button.transform;
        }

        private void BuildQuota()
        {
            UiKit.CreateText(root, "Quota Label", "NEXT CARD", new Vector2(0f, QuotaLabelY), new Vector2(300f, 20f), 14f, UiKit.MutedColor,
                FontStyles.Bold, 6f);
            quotaBack = UiKit.CreateImage(root, "Quota Bar", new Vector2(0f, QuotaBarY), quotaBarSize, UiKit.CellColor, quotaBarSize.y * 0.5f);
            quotaFill = UiKit.CreateImage(quotaBack.transform, "Quota Fill", Vector2.zero, quotaBarSize, UiKit.GoldColor, quotaBarSize.y * 0.5f);
            RectTransform fill = quotaFill.rectTransform;
            fill.anchorMin = new Vector2(0f, 0.5f);
            fill.anchorMax = new Vector2(0f, 0.5f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            quotaText = UiKit.CreateText(root, "Quota Text", string.Empty, new Vector2(0f, QuotaTextY), new Vector2(600f, 22f), 16f, UiKit.GoldColor);
            quotaHint = UiKit.CreateText(root, "Quota Hint", string.Empty, new Vector2(0f, QuotaHintY), new Vector2(600f, 22f), 15f, UiKit.TextColor);
        }

        private void RefreshQuota()
        {
            string text;
            if (data.HasNextQuota)
            {
                double goal = data.NextQuota;
                float fraction = goal > 0 ? Mathf.Clamp01((float)(data.goldEarned / goal)) : 1f;
                quotaBack.gameObject.SetActive(true);
                quotaFill.enabled = fraction > 0f;
                quotaFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(quotaBarSize.y, quotaBarSize.x * fraction), quotaBarSize.y);
                text = $"${data.goldEarned:0} / ${goal:0} gold earned";
            }
            else
            {
                quotaBack.gameObject.SetActive(false);
                text = "All cards taken";
            }

            if (data.goldCardsTaken > 0)
            {
                text += $"   |   Gold x{data.GoldMultiplier:0.0}";
            }
            quotaText.text = text;

            bool waiting = data.IsCardReady && (offerShown || !data.CanAffordCard);
            quotaHint.text = waiting ? $"Card ready! Come back with ${SquadRules.CardCost:0} to pick it" : string.Empty;
        }

        private void Select(UnitType type)
        {
            if (data.IsUnlocked(type) && type != selected)
            {
                selected = type;
                Refresh();
            }
        }

        private void Buy(UnitStat stat)
        {
            if (data.TryBuyUpgrade(stat))
            {
                OnGoldChanged();
            }
        }

        private void OnGoldChanged()
        {
            if (goldChanged != null)
            {
                goldChanged();
            }
            Refresh();
        }

        private static UnitStat[] StatsOf(UnitType type)
        {
            switch (type)
            {
                case UnitType.Tank:
                    return tankStats;
                case UnitType.Heal:
                    return healStats;
                default:
                    return attackStats;
            }
        }

        private static string StepText(UnitStat stat)
        {
            float step = SquadRules.Step(stat);
            switch (stat)
            {
                case UnitStat.AttackDamage:
                    return $"+{step:0.##} ATK";
                case UnitStat.TankDefense:
                    return $"+{step * 100f:0}% DEF";
                case UnitStat.HealRate:
                    return $"+{step:0.##} HEAL";
                default:
                    return $"+{step:0.##} HP";
            }
        }

        private string NowText(UnitType type)
        {
            int[] levels = data.upgradeLevels;
            string health = $"HP {SquadRules.MaxHealth(type, levels):0.##}";
            switch (type)
            {
                case UnitType.Tank:
                    return $"Now: DEF {SquadRules.Defense(type, levels) * 100f:0}%  |  {health}";
                case UnitType.Heal:
                    return $"Now: HEAL {SquadRules.HealRate(levels):0.##}/s  |  {health}";
                default:
                    return $"Now: ATK {SquadRules.AttackDamage(levels):0.##}  |  {health}";
            }
        }
    }
}
