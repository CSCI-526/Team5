using System;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Session
{
    public sealed class ClaimCardShop
    {
        private static readonly UnitRole[] SummonableRoles =
        {
            UnitRole.Attack,
            UnitRole.Tank,
            UnitRole.Heal,
            UnitRole.Sniper,
            UnitRole.Vanguard,
            UnitRole.Demolisher
        };

        private readonly CampaignWallet wallet;
        private readonly SquadPreparation preparation;
        private readonly int baseCost;
        private readonly int costStep;
        private readonly List<UnitDefinition> currentOffer = new List<UnitDefinition>();

        public int SummonsCompleted { get; private set; }
        public int HighestStageCleared { get; private set; }
        public int NextPrice => baseCost + SummonsCompleted * costStep;
        public bool CanClaim => wallet.Balance >= NextPrice && preparation.CanAddMember;

        public ClaimCardShop(CampaignWallet wallet, SquadPreparation preparation, int baseCost = 30, int costStep = 15)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            this.baseCost = Math.Max(1, baseCost);
            this.costStep = Math.Max(0, costStep);
        }

        public void SetStageProgress(int highestStageCleared)
        {
            HighestStageCleared = Math.Max(HighestStageCleared, highestStageCleared);
            currentOffer.Clear();
        }

        public (int minLevel, int maxLevel) GetLevelRange()
        {
            var stage = Math.Max(1, HighestStageCleared);
            return (stage, stage + 1);
        }

        // Higher rarity units also roll higher level ranges based on stage + rarity tier
        public (int minLevel, int maxLevel) GetLevelRangeForRarity(UnitRarity rarity)
        {
            var stage = Math.Max(1, HighestStageCleared);
            var rarityBonus = rarity switch
            {
                UnitRarity.Common => 0,
                UnitRarity.Rare => 1,
                UnitRarity.Epic => 2,
                UnitRarity.Legendary => 3,
                _ => 0
            };
            return (stage + rarityBonus, stage + 1 + rarityBonus);
        }

        // Frontloads Commons early and keeps high rarities genuinely scarce
        public (int common, int rare, int epic, int legendary) GetRarityWeights()
        {
            return HighestStageCleared switch
            {
                <= 1 => (90, 10, 0, 0),
                2 => (80, 20, 0, 0),
                3 => (70, 30, 0, 0),
                4 => (50, 45, 5, 0),
                5 => (40, 50, 9, 1),
                6 => (25, 55, 16, 4),
                _ => (0, 70, 24, 6)
            };
        }

        private static string RarityBox(string hex) =>
            $"<size=72%><voffset=0.12em><mark=#{hex}FF><color=#{hex}>M</color></mark></voffset></size>";

        public string GetBannerSummary()
        {
            var (minLv, maxLv) = GetLevelRange();
            var (c, r, e, l) = GetRarityWeights();

            var cBox = RarityBox("BFBFBF"); // Common (Silver)
            var rBox = RarityBox("338CE6"); // Rare (Blue)
            var eBox = RarityBox("9440D9"); // Epic (Purple)
            var lBox = RarityBox("FFBF26"); // Legendary (Gold)

            return $"Summon Tier {Math.Max(1, HighestStageCleared)}  |  Base Lv.{minLv}-{maxLv} (+Rarity Lv Bonus)  |  {cBox} {c}%  {rBox} {r}%  {eBox} {e}%  {lBox} {l}%";
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
            var rarity = RollRarity();
            var (minLv, maxLv) = GetLevelRangeForRarity(rarity);
            var level = UnityEngine.Random.Range(minLv, maxLv + 1);
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