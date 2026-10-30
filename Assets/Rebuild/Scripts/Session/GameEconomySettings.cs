using System;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Session
{
    [Serializable]
    public sealed class UnitRewardConfiguration
    {
        public UnitRole role = UnitRole.Tank;
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public sealed class LevelRewardConfiguration
    {
        [Min(1)] public int level = 1;
        [Min(0)] public int gold = 10;
        public List<UnitRewardConfiguration> units = new List<UnitRewardConfiguration>();

        public UnitRole[] CaptureUnitRoles()
        {
            if (gold < 0) throw new InvalidOperationException("Level reward gold cannot be negative.");
            var result = new List<UnitRole>();
            foreach (var unit in units)
            {
                if (unit == null || unit.count < 1 || unit.role == UnitRole.Test || !Enum.IsDefined(typeof(UnitRole), unit.role))
                    throw new InvalidOperationException("Unit rewards require a playable role and positive count.");
                for (var i = 0; i < unit.count; i++) result.Add(unit.role);
            }
            return result.ToArray();
        }
    }

    [CreateAssetMenu(menuName = "Team5/Rebuild/Game Economy Settings")]
    public sealed class GameEconomySettings : ScriptableObject
    {
        [SerializeField, Min(0)] private int startingGold = 0;
        [SerializeField] private List<LevelRewardConfiguration> levelRewards = new List<LevelRewardConfiguration>();
        public int StartingGold => Mathf.Max(0, startingGold);
        public IReadOnlyList<LevelRewardConfiguration> LevelRewards => levelRewards.AsReadOnly();

        public LevelRewardConfiguration RewardForLevel(int level)
        {
            LevelRewardConfiguration match = null;
            foreach (var reward in levelRewards)
            {
                if (reward == null || reward.level < 1) throw new InvalidOperationException("Reward entries require a positive level number.");
                if (reward.level != level) continue;
                if (match != null) throw new InvalidOperationException($"Duplicate reward configuration for level {level}.");
                match = reward;
            }
            return match;
        }
    }
}
