using System;
using System.Collections.Generic;
using System.Linq;
using Team5.BasicTowerTrial.Presentation.LegacyUI;
using Team5.BasicTowerTrial.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed partial class BattlegroundsUpgradeScene
    {
        [SerializeField] private Canvas courierCanvas;
#if UNITY_EDITOR
        [SerializeField] private bool applyTestGoldOnPlay;
        [SerializeField, Range(0, TavernRecruitment.GoldLimit)] private int testGold = 10;

        [ContextMenu("Apply Test Gold (Play Mode)")]
        private void ApplyTestGold()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || State == null) return;
            State.SetGoldForTesting(testGold);
            Draw();
        }
#endif
        private readonly List<RectTransform> courierOffers = new List<RectTransform>();
        private readonly List<RectTransform> courierHand = new List<RectTransform>();
        private TavernRecruitment.Follower[] courierHandUnits = Array.Empty<TavernRecruitment.Follower>();
        private readonly Dictionary<RectTransform, LegacyUnitIconUI> courierIcons = new Dictionary<RectTransform, LegacyUnitIconUI>();
        private RectTransform courierHandRoot;
        private Button courierUpgrade, courierRefresh, courierFreeze;
        private Button courierSkip;
        private RawImage courierTierStars;
        private TMP_Text courierGold, courierHandCount, courierCaptainHealth, courierUpgradeCost;

        private void InitializeCourierTavern()
        {
            Canvas = courierCanvas;
            root = (RectTransform)Canvas.transform;
            var controls = root.Find("02 - Tavern and shop controls");
            courierUpgrade = CourierButton((RectTransform)controls.Find("UPGRADE badge"), () => { State.Upgrade(); Draw(); });
            courierRefresh = CourierButton((RectTransform)controls.Find("REFRESH badge"), () => { State.Roll(); Draw(); });
            courierFreeze = CourierButton((RectTransform)controls.Find("FREEZE badge"), () => { State.ToggleFreeze(); Draw(); });
            courierUpgradeCost = controls.Find("UPGRADE cost").GetComponent<TMP_Text>();
            courierTierStars = controls.Find("Innkeeper/Tavern Tier Stars").GetComponent<RawImage>();
            courierGold = root.Find("08 - Currency/Gold count").GetComponent<TMP_Text>();
            var countdown = root.Find("05 - Round timer");
            timer = countdown.Find("Timer").GetComponent<TMP_Text>();

            var shop = root.Find("03 - Recruit shop");
            foreach (Transform child in shop) courierOffers.Add((RectTransform)child);
            while (courierOffers.Count < TavernRecruitment.MaxOfferCount)
                courierOffers.Add(Instantiate(courierOffers[0], shop, false));

            for (var i = 0; i < courierOffers.Count; i++)
            {
                var offerIndex = i;
                var offer = courierOffers[i];
                CourierButton(offer, () => BuyOffer(offerIndex));
                AttachOfferHover(offer, offerIndex);
                offer.Find("Unit name").gameObject.SetActive(false);
                var oldPrice = offer.Find("Recruit price").GetComponent<TMP_Text>();
                oldPrice.gameObject.SetActive(false);
                var icon = (RectTransform)offer.Find("Unit icon");
                AddOfferPrice(offer, icon.anchoredPosition + icon.rect.size * .46f, oldPrice.color);
            }

            courierHandRoot = (RectTransform)root.Find("07 - Player crest and fanned hand");
            courierHandCount = courierHandRoot.Find("Hand label").GetComponent<TMP_Text>();
            courierCaptainHealth = courierHandRoot.Find("Commander/Health").GetComponent<TMP_Text>();
            if (!standalone)
                courierSkip = AddSkipButton(countdown, -43, 79, 29);
            foreach (Transform child in courierHandRoot)
                if (child.name.StartsWith("Hand card ", StringComparison.Ordinal))
                    courierHand.Add((RectTransform)child);
            while (courierHand.Count < TavernRecruitment.HandLimit)
            {
                var card = Instantiate(courierHand[0], courierHandRoot, false);
                card.name = $"Hand card {courierHand.Count + 1}";
                courierHand.Add(card);
            }
            for (var i = 0; i < courierHand.Count; i++)
            {
                var index = i;
                var handCard = courierHand[index];
                if (handCard.GetComponent<Graphic>() == null)
                    handCard.gameObject.AddComponent<Image>().color = Color.clear;
                handCard.GetComponent<Graphic>().raycastTarget = true;
                AttachUnitHover(handCard, () => index < courierHandUnits.Length &&
                    courierHandUnits[index].Golden ? (courierHandUnits[index].Role, true) : null);
            }
        }

        private void DrawCourierTavern()
        {
            if (courierHandRoot == null) InitializeCourierTavern();
            timer.text = $"{Mathf.CeilToInt(seconds)} s";
            courierGold.text = $"{State.Gold} GOLD";
            courierCaptainHealth.text = State.CaptainHealth.ToString();
            courierUpgradeCost.text = State.Tier < TavernRecruitment.MaxTier ? State.UpgradeCost.ToString() : "MAX";
            courierTierStars.uvRect = new Rect((State.Tier - 1) / 6f, 0, 1f / 6f, 1);
            var shopping = !State.Finished;
            courierUpgrade.interactable = State.CanUpgrade;
            courierRefresh.interactable = shopping && State.Gold > 0;
            courierFreeze.interactable = shopping;
            if (courierSkip != null) courierSkip.interactable = State.Ready;
            courierFreeze.transform.Find("FREEZE Icon").GetComponent<Graphic>().color =
                State.Frozen ? new Color(.60f, .93f, 1f) : Color.white;

            for (var i = 0; i < courierOffers.Count; i++)
            {
                var card = courierOffers[i];
                var role = i < State.Offers.Count ? State.Offers[i] : null;
                card.gameObject.SetActive(role.HasValue);
                if (!role.HasValue) continue;
                card.anchoredPosition = new Vector2((i - (State.Offers.Count - 1) * .5f) * 81, 0);
                card.GetComponent<Button>().interactable = State.CanBuy(i);
                card.Find("Cost badge/Cost").GetComponent<TMP_Text>().text = TavernRecruitment.RecruitCost(role.Value).ToString();
                SetCourierUnit(card, role.Value);
            }

            var hand = courierHandUnits = State.Followers.Where(unit => unit.Slot < 0).ToArray();
            courierHandCount.text = $"HAND  {hand.Length} / {TavernRecruitment.HandLimit}";
            for (var i = 0; i < courierHand.Count; i++)
            {
                var card = courierHand[i];
                card.gameObject.SetActive(i < hand.Length);
                if (i < hand.Length) SetCourierUnit(card, hand[i].Role, hand[i].Golden);
            }
            courierHandRoot.SendMessage("Arrange", SendMessageOptions.RequireReceiver);

        }

        private void SetCourierUnit(RectTransform card, UnitRole role, bool golden = false)
        {
            if (!courierIcons.TryGetValue(card, out var icon))
            {
                var placeholder = (RectTransform)card.Find("Unit icon");
                placeholder.GetComponent<RawImage>().enabled = false;
                icon = LegacyUnitIconUI.Create(placeholder, Vector2.zero, placeholder.rect.width * 1.9f);
                courierIcons.Add(card, icon);
                card.Find("Attack").gameObject.SetActive(false);
                card.Find("Health").gameObject.SetActive(false);
            }
            icon.SetType(role, golden);
            icon.Rect.localScale = golden ? Vector3.one * 1.15f : Vector3.one;
        }

        private Button CourierButton(RectTransform target, Action action)
        {
            var graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                graphic = target.gameObject.AddComponent<Image>();
                graphic.color = Color.clear;
            }
            var button = Clickable(graphic);
            button.onClick.AddListener(() => action());
            return button;
        }
    }
}
