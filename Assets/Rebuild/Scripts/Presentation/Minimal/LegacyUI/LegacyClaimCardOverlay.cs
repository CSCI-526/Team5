using System.Collections.Generic;
using Team5.Rebuild.Analytics;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class LegacyClaimCardOverlay : MonoBehaviour
    {
        private LegacyUpgradeScene owner;

        public static LegacyClaimCardOverlay Open(LegacyUpgradeScene owner)
        {
            var offer = BattleSceneFlow.OpenSummonOffer();
            if (offer.Count == 0) return null;

            var host = new GameObject("Unit Summon Offer", typeof(RectTransform));
            var view = host.AddComponent<LegacyClaimCardOverlay>();
            view.owner = owner;

            LegacyUiKit.CreateCanvas(host, 200, new Vector2(1280, 720), 0.5f);
            var backdrop = LegacyUiKit.CreateImage(host.transform, "Backdrop", Vector2.zero, Vector2.zero, LegacyUiKit.DimColor);
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            // Main modal: a cream Paint Squad board with a red title ribbon across its top edge.
            var border = LegacyUiKit.CreateImage(host.transform, "Modal Border", new Vector2(0, -4), new Vector2(1030, 640), LegacyUiKit.CardRimColor, 30);
            if (LegacyUiKit.Skin != null && LegacyUiKit.Skin.panel != null) { border.sprite = LegacyUiKit.Skin.panel; border.pixelsPerUnitMultiplier = LegacyUiKit.SkinCornerRadius / 30f; }
            LegacyUiKit.CreateShadow(border.rectTransform);
            var modal = LegacyUiKit.CreateImage(host.transform, "Modal Body", new Vector2(0, 0), new Vector2(1018, 624), LegacyUiKit.CardColor, 26).rectTransform;

            var shop = BattleSceneFlow.CardShop;
            var tier = Mathf.Max(1, BattleSceneFlow.HighestClearedLevel);
            var (minLv, maxLv) = shop.GetLevelRange();

            LegacyUiKit.CreateRibbon(host.transform, "Title Ribbon", string.Empty, new Vector2(0, 300), new Vector2(520, 80), LegacyUiKit.Red);
            LegacyUiKit.CreateText(host.transform, "Title", "SUMMON NEW UNIT", new Vector2(0, 306),
                new Vector2(900, 46), 38, Color.white, FontStyles.Bold, 3);

            // Tier & Level Range Pill + Color-Coded Rarity Boxes
            BuildTierAndOddsRow(modal, tier, minLv, maxLv, shop);

            // Gold Cost & Balance Pill
            var balance = BattleSceneFlow.HasUnlimitedGold ? "UNLIMITED" : $"{BattleSceneFlow.GoldBalance:N0} Gold";
            var pricePill = LegacyUiKit.CreatePill(modal, "Price Pill", new Vector2(0, 190), new Vector2(500, 38), LegacyUiKit.CardInnerColor);
            LegacyUiKit.CreateIcon(pricePill.transform, "Coin", "coin", new Vector2(-232, 0), 44);
            LegacyUiKit.CreateText(pricePill.transform, "Price", $"Summon Cost: {shop.NextPrice} Gold   |   Balance: {balance}",
                new Vector2(10, 0), new Vector2(440, 30), 19, LegacyUiKit.Ink, FontStyles.Bold);

            // Summon Cards (wider 310px spacing)
            for (var i = 0; i < offer.Count; i++)
                view.BuildSummonCard(i, offer[i], new Vector2((i - (offer.Count - 1) * 0.5f) * 310, -18), shop);

            var cancel = LegacyUiKit.CreateButton(host.transform, "Cancel", new Vector2(0, -262), new Vector2(240, 56), LegacyUiKit.ButtonSlate, 14);
            LegacyUiKit.CreateText(cancel.transform, "Label", "CANCEL", new Vector2(0, 3), new Vector2(230, 46), 24, Color.white, FontStyles.Bold);
            cancel.onClick.AddListener(() =>
            {
                GameAnalytics.Track("summon_cancel", ("price", shop.NextPrice), ("gold", BattleSceneFlow.GoldBalance));
                view.Close();
            });

            GameAnalytics.Track("summon_offer", ("price", shop.NextPrice), ("gold", BattleSceneFlow.GoldBalance),
                ("offer_0", PrepAnalytics.Describe(offer.Count > 0 ? offer[0] : null)),
                ("offer_1", PrepAnalytics.Describe(offer.Count > 1 ? offer[1] : null)),
                ("offer_2", PrepAnalytics.Describe(offer.Count > 2 ? offer[2] : null)));
            return view;
        }

        public static RectTransform BuildRarityOddsChips(Transform parent, Vector2 position, ClaimCardShop shop, float scale = 1f)
        {
            var container = LegacyUiKit.CreateRect(parent, "Rarity Odds Row", position, new Vector2(760, 28));
            container.localScale = Vector3.one * scale;

            var (tierMinLv, tierMaxLv) = shop.GetLevelRange();
            var tierNum = Mathf.Max(1, shop.HighestStageCleared);
            var tierLabel = LegacyUiKit.CreateText(container, "TierText", $"TIER {tierNum} (Lv.{tierMinLv}-{tierMaxLv})",
                new Vector2(-235, 0), new Vector2(180, 24), 16, LegacyUiKit.GoldDark, FontStyles.Bold);
            tierLabel.alignment = TextAlignmentOptions.Right;

            var (c, r, e, l) = shop.GetRarityWeights();
            var entries = new (UnitRarity rarity, string name, int pct)[]
            {
                (UnitRarity.Common, "Common", c),
                (UnitRarity.Rare, "Rare", r),
                (UnitRarity.Epic, "Epic", e),
                (UnitRarity.Legendary, "Legendary", l)
            };

            var x = -125f;
            foreach (var entry in entries)
            {
                var color = LegacyUnitLooks.RarityColor(entry.rarity);
                var alpha = entry.pct > 0 ? 1f : 0.35f;
                var swatchColor = new Color(color.r, color.g, color.b, alpha);

                // Colored rarity box instead of "C:" / "R:" text prefix
                LegacyUiKit.CreateCircle(container, $"{entry.name} Box", new Vector2(x, 0), 18, swatchColor);

                var text = $"{entry.name} {entry.pct}%";
                var label = LegacyUiKit.CreateText(container, $"{entry.name} Odds", text,
                    new Vector2(x + 14 + 52, 0), new Vector2(104, 22), 15,
                    entry.pct > 0 ? LegacyUiKit.Ink : LegacyUiKit.InkMuted, FontStyles.Bold);
                label.alignment = TextAlignmentOptions.Left;

                x += 22f + label.GetPreferredValues(text).x + 18f;
            }
            return container;
        }

        private static void BuildTierAndOddsRow(Transform parent, int tier, int minLv, int maxLv, ClaimCardShop shop)
        {
            BuildRarityOddsChips(parent, new Vector2(10, 232), shop, 1.05f);
        }

        private void BuildSummonCard(int index, UnitDefinition rolledUnit, Vector2 position, ClaimCardShop shop)
        {
            var rarityColor = LegacyUnitLooks.RarityColor(rolledUnit.Rarity);

            // Outer rarity-colored frame
            // Paint Squad card: rarity rim with a chunky bottom edge around a cream face.
            var button = LegacyUiKit.CreateButton(transform, $"Summon Option {index}", position, new Vector2(276, 364), rarityColor, 22);
            LegacyUiKit.CreateShadow((RectTransform)button.transform, 16, 10, 0.22f);

            LegacyUiKit.CreateImage(button.transform, "Face", new Vector2(0, 5), new Vector2(264, 340), LegacyUiKit.CardColor, 17);

            // Top rarity header: a solid rarity strip with an outlined label.
            var header = LegacyUiKit.CreateImage(button.transform, "Rarity Ribbon", new Vector2(0, 154), new Vector2(246, 30),
                rarityColor, 12);
            LegacyUiKit.CreateCircle(header.transform, "Rarity Swatch", new Vector2(-104, 0), 16, Color.white);
            LegacyUiKit.CreateText(header.transform, "Rarity Banner", $"{rolledUnit.Rarity.ToString().ToUpperInvariant()} TIER",
                new Vector2(6, 1), new Vector2(200, 24), 16, Color.white, FontStyles.Bold, 3);

            // Unit Introduction (preserves exact child hierarchy for tests)
            var introduction = LegacyUiKit.CreateRect(button.transform, "Introduction", new Vector2(0, 16), new Vector2(200, 262));
            introduction.localScale = Vector3.one * 1.06f;
            LegacyUnitIntroductionUI.Build(introduction, rolledUnit, false);

            // Bottom Recruit Action Pill
            var buyPill = LegacyUiKit.CreateImage(button.transform, "Buy Pill", new Vector2(0, -140), new Vector2(236, 46), LegacyUiKit.NodeGreenColor, 14);
            LegacyUiKit.Chunky(buyPill);
            LegacyUiKit.CreateText(buyPill.transform, "Cost", $"RECRUIT  •  {shop.NextPrice}G", new Vector2(0, 3),
                new Vector2(226, 36), 21, Color.white, FontStyles.Bold, 1);

            button.onClick.AddListener(() =>
            {
                var price = shop.NextPrice;
                var gold = BattleSceneFlow.GoldBalance;
                var newRole = !owner.Preparation.HasOwnedRole(rolledUnit.Role);
                if (!BattleSceneFlow.TrySummonUnit(index)) return;
                GameAnalytics.Track("summon_purchase", ("chosen_index", index), ("role", rolledUnit.Role.ToString()),
                    ("rarity", rolledUnit.Rarity.ToString()), ("unit_level", rolledUnit.Level), ("price", price),
                    ("gold_before", gold), ("gold_after", BattleSceneFlow.GoldBalance));
                Close();
                if (newRole && owner != null) owner.ShowUnitIntroduction(rolledUnit);
            });
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (owner != null) owner.OnClaimCardsClosed();
            Destroy(gameObject);
        }
    }
}