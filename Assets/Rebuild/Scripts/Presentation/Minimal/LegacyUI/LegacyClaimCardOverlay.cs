using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Team5 AugmentOverlay's choose-one card flow, adapted to warehouse ownership.
    public sealed class LegacyClaimCardOverlay : MonoBehaviour
    {
        private LegacyUpgradeScene owner;
        public static LegacyClaimCardOverlay Open(LegacyUpgradeScene owner)
        {
            var offer = BattleSceneFlow.OpenClaimCards();
            if (offer.Count == 0) return null;
            var host = new GameObject("Claim Card Offer", typeof(RectTransform));
            var view = host.AddComponent<LegacyClaimCardOverlay>();
            view.owner = owner;
            // Root canvas: do not inherit an authored Canvas's scale.
            LegacyUiKit.CreateCanvas(host, 200, new Vector2(1280, 720), 0.5f);
            var backdrop = LegacyUiKit.CreateImage(host.transform, "Backdrop", Vector2.zero, Vector2.zero, new Color(0.02f,0.03f,0.05f,0.96f));
            LegacyUiKit.Stretch(backdrop.rectTransform); backdrop.raycastTarget = true;
            LegacyUiKit.CreateText(host.transform, "Title", "CHOOSE ONE", new Vector2(0,260), new Vector2(900,55),36,Color.white,FontStyles.Bold);
            var shop = BattleSceneFlow.CardShop;
            LegacyUiKit.CreateText(host.transform, "Price", $"Each card costs {shop.NextPrice} gold  |  You have {BattleSceneFlow.GoldBalance}",
                new Vector2(0,210),new Vector2(900,35),22,LegacyUiKit.HighlightColor);
            for(var i=0;i<offer.Count;i++) view.BuildCard(offer[i],new Vector2((i-(offer.Count-1)*.5f)*285,0),shop);
            var cancel = LegacyUiKit.CreateButton(host.transform,"Cancel",new Vector2(0,-245),new Vector2(240,55),LegacyUiKit.PanelLineColor,6);
            LegacyUiKit.CreateText(cancel.transform,"Label","CANCEL",Vector2.zero,new Vector2(230,50),23,Color.white,FontStyles.Bold);
            cancel.onClick.AddListener(view.Close);
            return view;
        }

        private void BuildCard(ClaimCard card,Vector2 position,ClaimCardShop shop)
        {
            var role = ClaimCardShop.RoleOf(card);
            var button = LegacyUiKit.CreateButton(transform,card.ToString(),position,new Vector2(260,330),LegacyUiKit.PanelLineColor,12);
            LegacyUiKit.CreateImage(button.transform,"Face",Vector2.zero,new Vector2(254,324),LegacyUiKit.PanelColor,10);
            var introduction = LegacyUiKit.CreateRect(button.transform,"Introduction",new Vector2(0,20),new Vector2(200,262));
            introduction.localScale = Vector3.one * 1.1f;
            LegacyUnitIntroductionUI.Build(introduction,SquadCombatRules.Create(role),false);
            LegacyUiKit.CreateText(button.transform,"Cost",$"{shop.NextPrice} GOLD",new Vector2(0,-140),new Vector2(240,35),22,LegacyUiKit.HighlightColor,FontStyles.Bold);
            button.onClick.AddListener(()=> { if(BattleSceneFlow.TryClaimCard(card)) Close(); });
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if(owner != null) owner.OnClaimCardsClosed();
            Destroy(gameObject);
        }
    }
}
