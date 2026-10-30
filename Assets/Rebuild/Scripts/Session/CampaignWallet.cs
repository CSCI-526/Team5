using System;
using System.Collections.Generic;

namespace Team5.Rebuild.Session
{
    // Per-run currency. No Unity, combat, Scene or shared-asset mutation dependencies.
    public sealed class CampaignWallet
    {
        private readonly HashSet<int> rewardedLevels = new HashSet<int>();
        public int Balance { get; private set; }

        public CampaignWallet(int startingGold = 0)
        {
            if (startingGold < 0) throw new ArgumentOutOfRangeException(nameof(startingGold));
            Balance = startingGold;
        }

        public bool TryRewardLevel(int level, int amount)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (rewardedLevels.Contains(level)) return false;
            var total = checked(Balance + amount);
            rewardedLevels.Add(level);
            Balance = total;
            return true;
        }

        public bool TrySpend(int price)
        {
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
            if (Balance < price) return false;
            Balance -= price;
            return true;
        }
    }
}
