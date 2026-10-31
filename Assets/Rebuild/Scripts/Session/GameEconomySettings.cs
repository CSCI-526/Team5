using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Session
{
    [Serializable]
    public sealed class LevelRewardConfiguration
    {
        [Min(1)] public int level = 1;
        [Min(0)] public int gold = 50;
    }

    [CreateAssetMenu(menuName = "Team5/Rebuild/Game Economy Settings")]
    public sealed class GameEconomySettings : ScriptableObject
    {
        [SerializeField, Min(0)] private int startingGold = 0;
        [SerializeField, Min(1)] private int baseSummonCost = 3;
        [SerializeField, Min(0)] private int summonCostIncreasePerPull = 1;
        [SerializeField] private List<LevelRewardConfiguration> levelRewards = new List<LevelRewardConfiguration>();

        public int StartingGold => Mathf.Max(0, startingGold);
        public int BaseSummonCost => Mathf.Max(1, baseSummonCost);
        public int SummonCostIncreasePerPull => Mathf.Max(0, summonCostIncreasePerPull);
        public IReadOnlyList<LevelRewardConfiguration> LevelRewards => levelRewards.AsReadOnly();

        public int GetStageClearGold(int level)
        {
            var match = RewardForLevel(level);
            if (match != null && match.gold > 0) return match.gold;
            return Mathf.Max(1, level) * 50; // Default fallback: 50g for Stage 1, 100g for Stage 2, etc.
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