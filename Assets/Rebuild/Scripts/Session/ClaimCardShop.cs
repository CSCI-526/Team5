using System;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;

namespace Team5.Rebuild.Session
{
    public enum ClaimCard { AddAttack, AddTank, AddHeal }

    // Team5 quotas, with a fixed Attack/Tank/Heal choice. Every purchase adds one warehouse member.
    public sealed class ClaimCardShop
    {
        private static readonly int[] prices = { 10, 100, 500 };
        private static readonly IReadOnlyList<ClaimCard> choices = Array.AsReadOnly(new[] { ClaimCard.AddAttack, ClaimCard.AddTank, ClaimCard.AddHeal });
        private readonly CampaignWallet wallet;
        private readonly SquadPreparation preparation;
        private bool offerOpen;
        public int CardsTaken { get; private set; }
        public bool HasNextCard => CardsTaken < prices.Length;
        public int NextPrice => HasNextCard ? prices[CardsTaken] : 0;
        public bool CanClaim => HasNextCard && wallet.Balance >= NextPrice && preparation.CanAddMember;

        public ClaimCardShop(CampaignWallet wallet, SquadPreparation preparation)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
        }

        public static UnitRole RoleOf(ClaimCard card)
        {
            switch (card)
            {
                case ClaimCard.AddAttack: return UnitRole.Attack;
                case ClaimCard.AddTank: return UnitRole.Tank;
                case ClaimCard.AddHeal: return UnitRole.Heal;
                default: throw new ArgumentOutOfRangeException(nameof(card));
            }
        }

        public IReadOnlyList<ClaimCard> BeginOffer()
        {
            if (!CanClaim) return Array.Empty<ClaimCard>();
            offerOpen = true;
            return choices;
        }

        public bool TryPurchase(ClaimCard card)
        {
            if (!CanClaim || !offerOpen || !Enum.IsDefined(typeof(ClaimCard), card)) return false;
            if (!wallet.TrySpend(NextPrice)) return false;
            CardsTaken++;
            offerOpen = false;
            preparation.TryAddRewardMember(RoleOf(card));
            return true;
        }
    }
}
