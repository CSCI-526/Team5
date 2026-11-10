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
            var backdrop = LegacyUiKit.CreateImage(host.transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.94f));
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            // Main modal container frame
            LegacyUiKit.CreateImage(host.transform, "Modal Border", new Vector2(0, 6), new Vector2(1024, 654), LegacyUiKit.PanelLineColor, 18);
            var modal = LegacyUiKit.CreateImage(host.transform, "Modal Body", new Vector2(0, 6), new Vector2(1018, 648), new Color(0.06f, 0.08f, 0.13f, 0.98f), 16).rectTransform;

            var shop = BattleSceneFlow.CardShop;
            var tier = Mathf.Max(1, BattleSceneFlow.HighestClearedLevel);
            var (minLv, maxLv) = shop.GetLevelRange();

            LegacyUiKit.CreateText(host.transform, "Title", "SUMMON NEW UNIT", new Vector2(0, 286),
                new Vector2(900, 46), 34, Color.white, FontStyles.Bold, 3);

            // Tier & Level Range Pill + Color-Coded Rarity Boxes
            BuildTierAndOddsRow(modal, tier, minLv, maxLv, shop);

            // Gold Cost & Balance Pill
            var balance = BattleSceneFlow.HasUnlimitedGold ? "UNLIMITED" : $"{BattleSceneFlow.GoldBalance:N0} Gold";
            var pricePill = LegacyUiKit.CreateImage(modal, "Price Pill", new Vector2(0, 190), new Vector2(460, 34), LegacyUiKit.CellColor, 8);
            LegacyUiKit.CreateText(pricePill.transform, "Price", $"Summon Cost: {shop.NextPrice} Gold   |   Balance: {balance}",
                Vector2.zero, new Vector2(440, 30), 18, LegacyUiKit.HighlightColor, FontStyles.Bold);

            // Summon Cards (wider 310px spacing)
            for (var i = 0; i < offer.Count; i++)
                view.BuildSummonCard(i, offer[i], new Vector2((i - (offer.Count - 1) * 0.5f) * 310, -18), shop);

            var cancel = LegacyUiKit.CreateButton(host.transform, "Cancel", new Vector2(0, -268), new Vector2(240, 50), LegacyUiKit.PanelLineColor, 8);
            LegacyUiKit.CreateText(cancel.transform, "Label", "CANCEL", Vector2.zero, new Vector2(230, 46), 21, Color.white, FontStyles.Bold);
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
                new Vector2(-235, 0), new Vector2(180, 24), 15, LegacyUiKit.HighlightColor, FontStyles.Bold);
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
                var box = LegacyUiKit.CreateImage(container, $"{entry.name} Box", new Vector2(x, 0), new Vector2(16, 16), swatchColor, 4);
                var outline = box.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
                outline.effectDistance = new Vector2(1f, -1f);

                var text = $"{entry.name} {entry.pct}%";
                var label = LegacyUiKit.CreateText(container, $"{entry.name} Odds", text,
                    new Vector2(x + 14 + 52, 0), new Vector2(104, 22), 14,
                    entry.pct > 0 ? LegacyUiKit.TextColor : LegacyUiKit.MutedColor, FontStyles.Bold);
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
            var button = LegacyUiKit.CreateButton(transform, $"Summon Option {index}", position, new Vector2(276, 364), rarityColor, 14);
            var cardOutline = button.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.45f);
            cardOutline.effectDistance = new Vector2(3, -3);

            // Inner dark card body
            LegacyUiKit.CreateImage(button.transform, "Face", Vector2.zero, new Vector2(266, 354), LegacyUiKit.PanelColor, 11);

            // Top Rarity Header Ribbon
            var header = LegacyUiKit.CreateImage(button.transform, "Rarity Ribbon", new Vector2(0, 156), new Vector2(246, 28),
                new Color(rarityColor.r * 0.28f, rarityColor.g * 0.28f, rarityColor.b * 0.28f, 0.95f), 6);
            LegacyUiKit.CreateImage(header.transform, "Rarity Swatch", new Vector2(-98, 0), new Vector2(14, 14), rarityColor, 3);
            LegacyUiKit.CreateText(header.transform, "Rarity Banner", $"{rolledUnit.Rarity.ToString().ToUpperInvariant()} TIER",
                new Vector2(6, 0), new Vector2(200, 24), 13, rarityColor, FontStyles.Bold, 3);

            // Unit Introduction (preserves exact child hierarchy for tests)
            var introduction = LegacyUiKit.CreateRect(button.transform, "Introduction", new Vector2(0, 16), new Vector2(200, 262));
            introduction.localScale = Vector3.one * 1.06f;
            LegacyUnitIntroductionUI.Build(introduction, rolledUnit, false);

            // Bottom Recruit Action Pill
            var buyPill = LegacyUiKit.CreateImage(button.transform, "Buy Pill", new Vector2(0, -144), new Vector2(236, 42), LegacyUiKit.NodeGreenColor, 8);
            LegacyUiKit.CreateText(buyPill.transform, "Cost", $"RECRUIT  •  {shop.NextPrice}G", Vector2.zero,
                new Vector2(226, 36), 18, Color.white, FontStyles.Bold, 1);

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