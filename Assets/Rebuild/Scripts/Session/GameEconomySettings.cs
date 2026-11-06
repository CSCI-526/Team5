using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Session
{
    [Serializable]
    public sealed class LevelRewardConfiguration
    {
        [Min(1)] public int level = 1;
        [Min(0)] public int gold = 45;
    }

    [CreateAssetMenu(menuName = "Team5/Rebuild/Game Economy Settings")]
    public sealed class GameEconomySettings : ScriptableObject
    {
        private static readonly int[] BalancedStageRewards = { 45, 65, 90, 120, 155, 195, 240 };

        [SerializeField, Min(0)] private int startingGold = 35;
        [SerializeField, Min(1)] private int baseSummonCost = 30;
        [SerializeField, Min(0)] private int summonCostIncreasePerPull = 15;
        [SerializeField, Min(0)] private int baseTowerBounty = 25;
        [SerializeField, Min(0)] private int aoeTowerBounty = 45;
        [SerializeField] private List<LevelRewardConfiguration> levelRewards = new List<LevelRewardConfiguration>();

        public int StartingGold => Mathf.Max(0, startingGold);
        public int BaseSummonCost => Mathf.Max(1, baseSummonCost);
        public int SummonCostIncreasePerPull => Mathf.Max(0, summonCostIncreasePerPull);
        public int BaseTowerBounty => Mathf.Max(5, baseTowerBounty);
        public int AoeTowerBounty => Mathf.Max(10, aoeTowerBounty);
        public IReadOnlyList<LevelRewardConfiguration> LevelRewards => levelRewards.AsReadOnly();

        public int GetStageClearGold(int level)
        {
            var match = RewardForLevel(level);
            if (match != null && match.gold > 0) return match.gold;
            var index = Mathf.Clamp(level - 1, 0, BalancedStageRewards.Length - 1);
            return BalancedStageRewards[index];
        }

        public int GetTowerDestroyGold(bool isAoe, int level)
        {
            var baseBounty = isAoe ? AoeTowerBounty : BaseTowerBounty;
            var stageScale = 1f + Mathf.Max(0, level - 1) * 0.20f;
            return Mathf.RoundToInt(baseBounty * stageScale);
        }

        public LevelRewardConfiguration RewardForLevel(int level)
        {
            foreach (var reward in levelRewards)
            {
                if (reward != null && reward.level == level) return reward;
            }
            return null;
        }
    }
}