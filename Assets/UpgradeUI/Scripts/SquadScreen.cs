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
        private const float PanelX = -304f;
        private const float PanelY = 165f;
        private const float PanelSpacing = 224f;
        private const float PanelRadius = 12f;
        private const float PanelBorder = 2f;
        private const float EmblemY = 75f;
        private const float EmblemSize = 76f;
        private const float EmblemRing = 3f;
        private const float EmblemIconScale = 1.15f;
        private const float LockShade = 0.72f;
        private const float NameY = 15f;
        private const float CountY = -8f;
        private const float RoleY = -32f;
        private const float DividerY = -52f;
        private const float DividerInset = 20f;
        private const float StatY = -76f;
        private const float StatGap = 28f;
        private const float StatInset = 26f;
        private const float GridCell = 58f;
        private const float GridGap = 8f;
        private const float ButtonRadius = 6f;
        private const float QuotaLabelY = -30f;
        private const float QuotaBarY = -55f;
        private const float QuotaTextY = -82f;
        private const float QuotaHintY = -106f;
        private const float ClaimButtonY = -146f;

        private static readonly Vector2 referenceResolution = new Vector2(1280f, 720f);
        private static readonly Vector2 panelSize = new Vector2(200f, 262f);
        private static readonly Vector2 gridPosition = new Vector2(465f, 43f);
        private static readonly Vector2 quotaBarSize = new Vector2(400f, 14f);
        private static readonly Vector2 claimButtonSize = new Vector2(240f, 48f);
        private static readonly UnitType[] panelTypes = { UnitType.Attack, UnitType.Tank, UnitType.Heal };
        private static readonly UnitStat[] attackStats = { UnitStat.AttackDamage, UnitStat.AttackHealth };
        private static readonly UnitStat[] tankStats = { UnitStat.TankDefense, UnitStat.TankHealth };
        private static readonly UnitStat[] healStats = { UnitStat.HealRate, UnitStat.HealHealth };

        private readonly GameObject[] panelLocks = new GameObject[3];
        private readonly TextMeshProUGUI[] panelNames = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] panelCounts = new TextMeshProUGUI[3];
        private readonly TextMeshProUGUI[] statValues = new TextMeshProUGUI[SquadRules.StatCount];
        private OffenseData data;
        private Action goldChanged;
        private RectTransform root;
        private SquadGridUI grid;
        private Image quotaBack;
        private Image quotaFill;
        private TextMeshProUGUI quotaText;
        private TextMeshProUGUI quotaHint;
        private Button claimButton;
        private Image claimButtonImage;
        private TextMeshProUGUI claimButtonLabel;
        private bool overlayOpen;

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
            screen.OpenLevelOneTankReward();
            return screen;
        }

        public void Refresh()
        {
            data.EnsureSquad();
            for (int i = 0; i < panelTypes.Length; i++)
            {
                UnitType type = panelTypes[i];
                bool unlocked = data.IsUnlocked(type);
                int count = data.CountUnits(type);

                panelLocks[i].SetActive(!unlocked);
                panelNames[i].color = unlocked ? UiKit.TextColor : UiKit.MutedColor;
                panelCounts[i].text = !unlocked ? "LOCKED" : count == 1 ? "1 unit" : count + " units";
            }

            for (int i = 0; i < statValues.Length; i++)
            {
                var stat = (UnitStat)i;
                bool unlocked = data.IsUnlocked(SquadRules.OwnerOf(stat));
                statValues[i].text = StatText.Value(stat, data.upgradeLevels);
                statValues[i].color = !unlocked ? UiKit.MutedColor : data.GetLevel(stat) > 0 ? UiKit.GoldColor : UiKit.TextColor;
            }

            grid.Refresh();
            RefreshQuota();
        }

        private void Build()
        {
            UiKit.CreateCanvas(gameObject, SortingOrder, referenceResolution, 0f);
            root = (RectTransform)transform;
            data.EnsureSquad();

            for (int i = 0; i < panelTypes.Length; i++)
            {
                BuildPanel(i);
            }
            grid = SquadGridUI.Create(root, root, data, gridPosition, GridCell, GridGap, "SQUAD", "Drag to swap", true, Refresh);
            BuildQuota();
            Refresh();
        }

        private void BuildPanel(int index)
        {
            UnitType type = panelTypes[index];
            var position = new Vector2(PanelX + index * PanelSpacing, PanelY);

            UiKit.CreateImage(root, "Panel Border", position, panelSize + Vector2.one * PanelBorder * 2f, UiKit.PanelLineColor,
                PanelRadius + PanelBorder);
            RectTransform panel = UiKit.CreateImage(root, "Panel " + UnitLooks.Name(type), position, panelSize, UiKit.PanelColor,
                PanelRadius).rectTransform;

            var emblemPosition = new Vector2(0f, EmblemY);
            UiKit.CreateCircle(panel, "Emblem Ring", emblemPosition, EmblemSize, UiKit.PanelLineColor);
            Image face = UiKit.CreateCircle(panel, "Emblem", emblemPosition, EmblemSize - EmblemRing * 2f, UiKit.PanelColor);
            UnitIconUI.Create(face.transform, Vector2.zero, EmblemSize * EmblemIconScale).SetType(type);

            Color shade = UiKit.PanelColor;
            shade.a = LockShade;
            Image locked = UiKit.CreateCircle(face.transform, "Locked", Vector2.zero, EmblemSize - EmblemRing * 2f, shade);
            UiKit.CreateLock(locked.transform, Vector2.zero, 1f);
            panelLocks[index] = locked.gameObject;

            panelNames[index] = UiKit.CreateText(panel, "Name", UnitLooks.Name(type), new Vector2(0f, NameY), new Vector2(panelSize.x, 24f), 18f,
                UiKit.TextColor, FontStyles.Bold, 4f);
            panelCounts[index] = UiKit.CreateText(panel, "Count", string.Empty, new Vector2(0f, CountY), new Vector2(panelSize.x, 20f), 14f,
                UiKit.MutedColor);
            UiKit.CreateText(panel, "Role", RoleText(type), new Vector2(0f, RoleY), new Vector2(panelSize.x, 20f), 13f, UiKit.MutedColor);
            UiKit.CreateImage(panel, "Divider", new Vector2(0f, DividerY), new Vector2(panelSize.x - DividerInset * 2f, 2f), UiKit.PanelLineColor);

            UnitStat[] stats = StatsOf(type);
            var rowSize = new Vector2(panelSize.x - StatInset * 2f, 24f);
            for (int i = 0; i < stats.Length; i++)
            {
                var rowPosition = new Vector2(0f, StatY - i * StatGap);
                TextMeshProUGUI statName = UiKit.CreateText(panel, "Stat", StatText.Name(stats[i]), rowPosition, rowSize, 14f, UiKit.MutedColor,
                    FontStyles.Bold, 2f);
                statName.alignment = TextAlignmentOptions.Left;
                TextMeshProUGUI value = UiKit.CreateText(panel, "Value", string.Empty, rowPosition, rowSize, 18f, UiKit.TextColor, FontStyles.Bold);
                value.alignment = TextAlignmentOptions.Right;
                statValues[(int)stats[i]] = value;
            }
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

            claimButton = UiKit.CreateButton(root, "Claim Card Button", new Vector2(0f, ClaimButtonY), claimButtonSize, UiKit.NodeGreyColor, ButtonRadius);
            claimButtonImage = claimButton.GetComponent<Image>();
            claimButtonLabel = UiKit.CreateText(claimButton.transform, "Label", string.Empty, Vector2.zero, claimButtonSize, 18f, Color.white, FontStyles.Bold);
            claimButton.onClick.AddListener(ClaimCard);
        }

        private void RefreshQuota()
        {
            string text;
            if (data.HasNextQuota)
            {
                double goal = data.NextQuota;
                float fraction = goal > 0 ? Mathf.Clamp01((float)(data.gold / goal)) : 1f;
                quotaBack.gameObject.SetActive(true);
                quotaFill.enabled = fraction > 0f;
                quotaFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(quotaBarSize.y, quotaBarSize.x * fraction), quotaBarSize.y);
                text = $"${data.gold:0} / ${goal:0} in bank";
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

            if (!data.HasNextQuota)
            {
                quotaHint.text = string.Empty;
                claimButton.interactable = false;
                claimButtonImage.color = UiKit.NodeBlueColor;
                claimButtonLabel.text = "ALL CARDS CLAIMED";
            }
            else if (data.IsCardReady)
            {
                quotaHint.text = "Card ready! Click below to claim a card";
                claimButton.interactable = !overlayOpen;
                claimButtonImage.color = UiKit.NodeGreenColor;
                claimButtonLabel.text = $"CLAIM CARD (${data.NextQuota:0})";
            }
            else
            {
                quotaHint.text = $"Save ${data.NextQuota:0} in your bank account to unlock the next card";
                claimButton.interactable = false;
                claimButtonImage.color = UiKit.NodeGreyColor;
                claimButtonLabel.text = $"CLAIM CARD (<color={UiKit.CostColorTag}>${data.NextQuota:0}</color>)";
            }
        }

        private void OpenLevelOneTankReward()
        {
            if (overlayOpen || data.currentLevel != 2 || !data.levelOnePracticePending || !data.HasPendingLevelOneTankReward) return;
            overlayOpen = true;
            RefreshQuota();
            AugmentOverlay.Open(data, OnGoldChanged, OnOverlayClosed, true);
        }

        private void ClaimCard()
        {
            if (overlayOpen || !data.IsCardReady || !data.CanAffordCard)
            {
                return;
            }

            overlayOpen = true;
            RefreshQuota();
            AugmentOverlay.Open(data, OnGoldChanged, OnOverlayClosed);
        }

        private void OnOverlayClosed()
        {
            overlayOpen = false;
            Refresh();
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

        private static string RoleText(UnitType type)
        {
            switch (type)
            {
                case UnitType.Tank:
                    return "Takes less damage";
                case UnitType.Heal:
                    return "Heals neighbors";
                default:
                    return "Shoots towers";
            }
        }
    }
}
