using Team5.Rebuild.Analytics;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;

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
            var backdrop = LegacyUiKit.CreateImage(host.transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.96f));
            LegacyUiKit.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            LegacyUiKit.CreateText(host.transform, "Title", "SUMMON NEW UNIT", new Vector2(0, 270), new Vector2(900, 50), 36, Color.white, FontStyles.Bold);
            var shop = BattleSceneFlow.CardShop;
            LegacyUiKit.CreateText(host.transform, "BannerInfo", shop.GetBannerSummary(),
                new Vector2(0, 228), new Vector2(900, 30), 18, LegacyUiKit.MutedColor);
            var balance = BattleSceneFlow.HasUnlimitedGold ? "UNLIMITED" : BattleSceneFlow.GoldBalance.ToString();
            LegacyUiKit.CreateText(host.transform, "Price", $"Cost: {shop.NextPrice} Gold  |  Balance: {balance}",
                new Vector2(0, 198), new Vector2(900, 32), 22, LegacyUiKit.HighlightColor);

            for (var i = 0; i < offer.Count; i++)
                view.BuildSummonCard(i, offer[i], new Vector2((i - (offer.Count - 1) * 0.5f) * 285, -10), shop);

            var cancel = LegacyUiKit.CreateButton(host.transform, "Cancel", new Vector2(0, -255), new Vector2(240, 55), LegacyUiKit.PanelLineColor, 6);
            LegacyUiKit.CreateText(cancel.transform, "Label", "CANCEL", Vector2.zero, new Vector2(230, 50), 23, Color.white, FontStyles.Bold);
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

        private void BuildSummonCard(int index, UnitDefinition rolledUnit, Vector2 position, ClaimCardShop shop)
        {
            var borderColor = LegacyUnitLooks.RarityColor(rolledUnit.Rarity);
            var button = LegacyUiKit.CreateButton(transform, $"Summon Option {index}", position, new Vector2(260, 330), borderColor, 12);
            LegacyUiKit.CreateImage(button.transform, "Face", Vector2.zero, new Vector2(252, 322), LegacyUiKit.PanelColor, 10);

            var introduction = LegacyUiKit.CreateRect(button.transform, "Introduction", new Vector2(0, 20), new Vector2(200, 262));
            introduction.localScale = Vector3.one * 1.1f;
            LegacyUnitIntroductionUI.Build(introduction, rolledUnit, false);

            LegacyUiKit.CreateText(button.transform, "Cost", $"SUMMON ({shop.NextPrice}G)", new Vector2(0, -140),
                new Vector2(240, 35), 20, LegacyUiKit.HighlightColor, FontStyles.Bold);

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
