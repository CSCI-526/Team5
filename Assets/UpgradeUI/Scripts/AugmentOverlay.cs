using System;
using System.Collections;
using System.Collections.Generic;
using ReverseTD.Offense;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReverseTD.UpgradeUI
{
    public class AugmentOverlay : MonoBehaviour
    {
        private const int SortingOrder = 200;
        private const float BackdropAlpha = 0.85f;
        private const float TitleY = 318f;
        private const float SubtitleY = 280f;
        private const float CardY = 95f;
        private const float CardSpacing = 260f;
        private const float CardRadius = 12f;
        private const float CardBorder = 2f;
        private const float CardGlow = 8f;
        private const float CardGlowAlpha = 0.3f;
        private const float CardDimAlpha = 0.5f;
        private const float GridCell = 58f;
        private const float GridGap = 8f;
        private const float ZoomScale = 2.2f;
        private const float PickDelay = 0.25f;
        private const float ZoomTime = 0.3f;
        private const float HintY = -362f;

        private static readonly Vector2 referenceResolution = new Vector2(1280f, 800f);
        private static readonly Vector2 cardSize = new Vector2(230f, 310f);
        private static readonly Vector2 smallGridPosition = new Vector2(0f, -261f);
        private static readonly Vector2 bigGridPosition = new Vector2(0f, 20f);
        private static readonly Vector2 trayPosition = new Vector2(0f, -284f);
        private static readonly Vector2 traySize = new Vector2(140f, 110f);
        private static readonly Vector2 doneSize = new Vector2(200f, 56f);

        private readonly List<AugmentCard> offer = new List<AugmentCard>();
        private readonly List<GameObject> cards = new List<GameObject>();
        private readonly List<Image> cardGlows = new List<Image>();
        private readonly List<Image> cardBorders = new List<Image>();
        private readonly List<Image> cardDims = new List<Image>();
        private OffenseData data;
        private Action goldChanged;
        private Action closed;
        private RectTransform root;
        private TextMeshProUGUI title;
        private TextMeshProUGUI subtitle;
        private TextMeshProUGUI hint;
        private SquadGridUI grid;
        private UnitType placing = UnitType.None;
        private bool picked;
        private bool isLevelOneTankReward;

        public static AugmentOverlay Open(OffenseData data, Action goldChanged, Action closed, bool levelOneTankReward = false)
        {
            var host = new GameObject("Augment Overlay", typeof(RectTransform));
            var overlay = host.AddComponent<AugmentOverlay>();
            overlay.data = data;
            overlay.goldChanged = goldChanged;
            overlay.closed = closed;
            overlay.isLevelOneTankReward = levelOneTankReward;
            overlay.Build();
            return overlay;
        }

        private void Build()
        {
            UiKit.CreateCanvas(gameObject, SortingOrder, referenceResolution, 1f);
            root = (RectTransform)transform;

            Image backdrop = UiKit.CreateImage(root, "Backdrop", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, BackdropAlpha));
            UiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            double cardCost = data.NextQuota;
            title = UiKit.CreateText(root, "Title", "CHOOSE ONE", new Vector2(0f, TitleY), new Vector2(900f, 50f), 36f, UiKit.TextColor,
                FontStyles.Bold, 8f);
            subtitle = UiKit.CreateText(root, "Subtitle",
                $"Each card costs <color={UiKit.CostColorTag}>${cardCost:0}</color>  |  You have ${data.gold:0}",
                new Vector2(0f, SubtitleY), new Vector2(900f, 30f), 18f, UiKit.MutedColor);

            if (!isLevelOneTankReward) SquadRules.RollOffer(data.cardsTaken, data.IsSquadFull, offer);
            for (int i = 0; i < offer.Count; i++)
            {
                float x = (i - (offer.Count - 1) * 0.5f) * CardSpacing;
                BuildCard(offer[i], i, new Vector2(x, CardY));
            }

            grid = SquadGridUI.Create(root, root, data, smallGridPosition, GridCell, GridGap, "YOUR SQUAD", null, false, null);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            if (isLevelOneTankReward)
            {
                picked = true;
                subtitle.text = "LEVEL 1 REWARD - FREE TANK";
                BuildTankIntroduction();
                StartCoroutine(StartPlacement(UnitType.Tank));
            }
        }

        private void BuildTankIntroduction()
        {
            Image panel = UiKit.CreateImage(root, "Tank Introduction", new Vector2(-380f, 20f), new Vector2(280f, 300f),
                UiKit.NodeGreenColor, 12f);
            UiKit.CreateImage(panel.transform, "Background", Vector2.zero, new Vector2(276f, 296f), UiKit.PanelColor, 10f);
            UiKit.CreateText(panel.transform, "Title", "TANK UNIT", new Vector2(0f, 108f), new Vector2(248f, 36f),
                26f, UiKit.TextColor, FontStyles.Bold);
            UnitIconUI.Create(panel.transform, new Vector2(0f, 40f), 80f).SetType(UnitType.Tank);
            TextMeshProUGUI description = UiKit.CreateText(panel.transform, "Description",
                "Place at the front to absorb damage and protect other units.\n\nCannot attack.", new Vector2(0f, -68f), new Vector2(240f, 100f), 22f, UiKit.TextColor);
            description.textWrappingMode = TextWrappingModes.Normal;
        }

        private void BuildCard(AugmentCard card, int index, Vector2 position)
        {
            float width = cardSize.x;
            float height = cardSize.y;
            RectTransform cardRoot = UiKit.CreateRect(root, "Card " + card, position, cardSize);
            cards.Add(cardRoot.gameObject);

            Color glow = UiKit.GoldColor;
            glow.a = CardGlowAlpha;
            Image glowImage = UiKit.CreateImage(cardRoot, "Glow", Vector2.zero, cardSize + Vector2.one * CardGlow * 2f, glow, CardRadius + CardGlow);
            glowImage.enabled = false;
            cardGlows.Add(glowImage);
            cardBorders.Add(UiKit.CreateImage(cardRoot, "Border", Vector2.zero, cardSize + Vector2.one * CardBorder * 2f, UiKit.PanelLineColor,
                CardRadius + CardBorder));

            Image face = UiKit.CreateImage(cardRoot, "Face", Vector2.zero, cardSize, UiKit.PanelColor, CardRadius);
            UiKit.MakeButton(face).onClick.AddListener(() => Pick(card, index));

            UnitType unit = SquadRules.UnitOf(card);
            Color accent = unit == UnitType.None ? UiKit.GoldColor : UnitLooks.For(unit).Body;
            UiKit.CreateImage(cardRoot, "Band", new Vector2(0f, height * 0.5f - 20f), new Vector2(width - 32f, 8f), accent, 4f);

            float radius = Mathf.Min(width, height) * 0.2f;
            var emblem = new Vector2(0f, height * 0.23f);
            UiKit.CreateCircle(cardRoot, "Emblem Ring", emblem, radius * 2f + 6f, accent);
            UiKit.CreateCircle(cardRoot, "Emblem", emblem, radius * 2f, UiKit.CellColor);
            if (unit == UnitType.None)
            {
                UiKit.CreateCoin(cardRoot, emblem, radius * 0.62f);
            }
            else
            {
                UnitIconUI.Create(cardRoot, emblem, radius * 2.6f).SetType(unit);
            }

            UiKit.CreateText(cardRoot, "Title", CardTitle(card), Vector2.zero, new Vector2(width, 30f), height * 0.068f, UiKit.TextColor,
                FontStyles.Bold, 2f);
            UiKit.CreateText(cardRoot, "Stats", CardStats(card), new Vector2(0f, -height * 0.085f), new Vector2(width, 24f), height * 0.05f,
                unit == UnitType.None ? UiKit.GoldColor : UiKit.TextColor);
            string[] lines = CardLines(card);
            for (int i = 0; i < lines.Length; i++)
            {
                UiKit.CreateText(cardRoot, "Line", lines[i], new Vector2(0f, -height * (0.165f + 0.06f * i)), new Vector2(width - 16f, 20f),
                    height * 0.043f, UiKit.MutedColor);
            }

            double cardCost = data.NextQuota;
            var pillSize = new Vector2(width * 0.4f, height * 0.11f);
            var pillPosition = new Vector2(0f, -height * 0.5f + height * 0.06f + pillSize.y * 0.5f);
            UiKit.CreateImage(cardRoot, "Price Rim", pillPosition, pillSize + Vector2.one * 4f, UiKit.GoldColor, pillSize.y * 0.5f + 2f);
            UiKit.CreateImage(cardRoot, "Price", pillPosition, pillSize, new Color(0.18f, 0.15f, 0.06f), pillSize.y * 0.5f);
            UiKit.CreateText(cardRoot, "Price Text", $"${cardCost:0}", pillPosition, pillSize, height * 0.058f, UiKit.GoldColor, FontStyles.Bold);

            Image dim = UiKit.CreateImage(cardRoot, "Dim", Vector2.zero, cardSize, new Color(0f, 0f, 0f, CardDimAlpha), CardRadius);
            dim.enabled = false;
            cardDims.Add(dim);
        }

        private void Pick(AugmentCard card, int index)
        {
            if (picked)
            {
                return;
            }
            picked = true;

            for (int i = 0; i < cards.Count; i++)
            {
                bool chosen = i == index;
                cardGlows[i].enabled = chosen;
                cardBorders[i].color = chosen ? UiKit.GoldColor : UiKit.PanelLineColor;
                cardDims[i].enabled = !chosen;
            }

            if (card == AugmentCard.GoldBonus)
            {
                if (data.TryTakeGoldCard() && goldChanged != null)
                {
                    goldChanged();
                }
                StartCoroutine(CloseAfter(PickDelay));
            }
            else
            {
                StartCoroutine(StartPlacement(SquadRules.UnitOf(card)));
            }
        }

        private IEnumerator StartPlacement(UnitType type)
        {
            if (!isLevelOneTankReward) yield return new WaitForSecondsRealtime(PickDelay);

            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].SetActive(false);
            }
            subtitle.gameObject.SetActive(isLevelOneTankReward);
            title.text = "PLACE YOUR " + UnitLooks.Name(type);
            grid.SetTitleVisible(false);

            RectTransform gridRect = grid.Rect;
            Vector2 from = gridRect.anchoredPosition;
            float elapsed = 0f;
            while (!isLevelOneTankReward && elapsed < ZoomTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / ZoomTime));
                gridRect.anchoredPosition = Vector2.Lerp(from, bigGridPosition, t);
                gridRect.localScale = Vector3.one * Mathf.Lerp(1f, ZoomScale, t);
                yield return null;
            }
            gridRect.anchoredPosition = bigGridPosition;
            gridRect.localScale = Vector3.one * ZoomScale;

            bool replaceUnit = isLevelOneTankReward && data.IsSquadFull;
            hint = UiKit.CreateText(root, "Hint", replaceUnit ? "Squad full: place your free Tank on a cell to replace that unit" : "Drag the new unit into an empty cell", new Vector2(0f, HintY), new Vector2(1000f, 28f), 18f,
                UiKit.MutedColor);
            placing = type;
            grid.BeginPlacement(type, root, trayPosition, traySize, Place, replaceUnit);
        }

        private void Place(int cell)
        {
            if (placing == UnitType.None)
            {
                return;
            }
            bool placed = isLevelOneTankReward ? data.TryPlaceLevelOneRewardTank(cell) : data.TryTakeUnitCard(placing, cell);
            if (!placed) return;

            placing = UnitType.None;
            if (goldChanged != null)
            {
                goldChanged();
            }
            grid.EndPlacement(cell);
            hint.gameObject.SetActive(false);

            Button done = UiKit.CreateButton(root, "Done", trayPosition, doneSize, UiKit.NodeGreenColor, 8f);
            UiKit.CreateText(done.transform, "Label", "DONE", Vector2.zero, doneSize, 24f, Color.white, FontStyles.Bold, 4f);
            done.onClick.AddListener(Close);
        }

        private IEnumerator CloseAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Close();
        }

        private void Close()
        {
            Destroy(gameObject);
            if (closed != null)
            {
                closed();
            }
        }

        private string CardTitle(AugmentCard card)
        {
            UnitType unit = SquadRules.UnitOf(card);
            return unit == UnitType.None ? $"+{SquadRules.GoldBonusPerCard * 100:0}% GOLD" : "ADD " + UnitLooks.Name(unit);
        }

        private string CardStats(AugmentCard card)
        {
            int[] levels = data.upgradeLevels;
            switch (card)
            {
                case AugmentCard.AddTank:
                    return $"HP {SquadRules.MaxHealth(UnitType.Tank, levels):0.##}  |  DEF {SquadRules.Defense(UnitType.Tank, levels) * 100f:0}%";
                case AugmentCard.AddHeal:
                    return $"HP {SquadRules.MaxHealth(UnitType.Heal, levels):0.##}  |  HEAL {SquadRules.HealRate(levels):0.##}/s";
                case AugmentCard.AddAttack:
                    return $"HP {SquadRules.MaxHealth(UnitType.Attack, levels):0.##}  |  ATK {SquadRules.AttackDamage(levels):0.##}";
                default:
                    return $"Earn {data.GoldMultiplier + SquadRules.GoldBonusPerCard:0.0}x gold";
            }
        }

        private string[] CardLines(AugmentCard card)
        {
            switch (card)
            {
                case AugmentCard.AddTank:
                    return new[] { $"Takes {SquadRules.Defense(UnitType.Tank, data.upgradeLevels) * 100f:0}% less damage.", "Doesn't attack." };
                case AugmentCard.AddHeal:
                    return new[] { "Heals the units", "around it." };
                case AugmentCard.AddAttack:
                    return new[] { "Shoots towers", "and earns gold." };
                default:
                    return new[] { "Stacks if you", "pick it again." };
            }
        }
    }
}
