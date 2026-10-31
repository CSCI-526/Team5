using System;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Session
{
    // Stage-scaling Summon Shop. Generates rolled UnitDefinitions whose Level and Rarity
    // improve as the player clears more stages.
    public sealed class ClaimCardShop
    {
        private static readonly UnitRole[] SummonableRoles = { UnitRole.Attack, UnitRole.Tank, UnitRole.Heal };

        private readonly CampaignWallet wallet;
        private readonly SquadPreparation preparation;
        private readonly int baseCost;
        private readonly int costStep;
        private readonly List<UnitDefinition> currentOffer = new List<UnitDefinition>();

        public int SummonsCompleted { get; private set; }
        public int HighestStageCleared { get; private set; }
        public int NextPrice => baseCost + SummonsCompleted * costStep;
        public bool CanClaim => wallet.Balance >= NextPrice && preparation.CanAddMember;

        public ClaimCardShop(CampaignWallet wallet, SquadPreparation preparation, int baseCost = 25, int costStep = 10)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            this.baseCost = Math.Max(1, baseCost);
            this.costStep = Math.Max(0, costStep);
        }

        public void SetStageProgress(int highestStageCleared)
        {
            HighestStageCleared = Math.Max(HighestStageCleared, highestStageCleared);
            currentOffer.Clear(); // Refresh offer pool when stage progression advances
        }

        public (int minLevel, int maxLevel) GetLevelRange()
        {
            var stage = Math.Max(1, HighestStageCleared);
            return (stage, stage + 1);
        }

        public (int common, int rare, int epic, int legendary) GetRarityWeights()
        {
            return HighestStageCleared switch
            {
                <= 1 => (80, 20, 0, 0),
                2 => (50, 35, 15, 0),
                3 => (30, 40, 25, 5),
                _ => (15, 35, 35, 15)
            };
        }

        public string GetBannerSummary()
        {
            var (minLv, maxLv) = GetLevelRange();
            var (c, r, e, l) = GetRarityWeights();
            return $"Summon Tier {Math.Max(1, HighestStageCleared)}  |  Lv.{minLv}-{maxLv}  |  C:{c}% R:{r}% E:{e}% L:{l}%";
        }

        public IReadOnlyList<UnitDefinition> BeginOffer()
        {
            if (!CanClaim) return Array.Empty<UnitDefinition>();
            if (currentOffer.Count == 0)
            {
                for (var i = 0; i < 3; i++)
                    currentOffer.Add(RollUnit());
            }
            return currentOffer.AsReadOnly();
        }

        public bool TrySummon(int offerIndex)
        {
            if (!CanClaim || offerIndex < 0 || offerIndex >= currentOffer.Count) return false;
            var chosen = currentOffer[offerIndex];
            if (!wallet.TrySpend(NextPrice)) return false;

            SummonsCompleted++;
            currentOffer.Clear();
            preparation.TryAddSummonedUnit(chosen);
            return true;
        }

        private UnitDefinition RollUnit()
        {
            var role = SummonableRoles[UnityEngine.Random.Range(0, SummonableRoles.Length)];
            var (minLv, maxLv) = GetLevelRange();
            var level = UnityEngine.Random.Range(minLv, maxLv + 1);
            var rarity = RollRarity();
            return SquadCombatRules.Create(role, rarity, level);
        }

        private UnitRarity RollRarity()
        {
            var (c, r, e, l) = GetRarityWeights();
            var roll = UnityEngine.Random.Range(0, c + r + e + l);
            if (roll < c) return UnitRarity.Common;
            roll -= c;
            if (roll < r) return UnitRarity.Rare;
            roll -= r;
            if (roll < e) return UnitRarity.Epic;
            return UnitRarity.Legendary;
        }
    }
}