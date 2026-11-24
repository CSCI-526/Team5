using System;
using System.Collections.Generic;
using System.Linq;
using Team5.BasicTowerTrial.Simulation;

namespace Team5.BasicTowerTrial.Presentation
{
    // Tavern rules are isolated from both campaign gold and the earlier SiegeTavern shop.
    public sealed class TavernRecruitment
    {
        public sealed class Follower
        {
            public int Id, Slot = -1;
            public UnitRole Role;
            public bool Golden;
        }
        public static readonly UnitRole[] Roles =
            { UnitRole.Zombie, UnitRole.BruteZombie, UnitRole.ShamanZombie, UnitRole.TankZombie };
        public static int RoleTier(UnitRole role) => MusterZombieRules.Tier(role);
        public readonly List<Follower> Followers = new List<Follower>();
        public readonly List<UnitRole?> Offers = new List<UnitRole?>();
        private readonly Random random;
        private int sequence = 2000;
        public int Turn { get; private set; } = 1;
        public const int MaxRounds = 8;
        public int Tier { get; private set; } = 1;
        public const int MaxTier = 2;
        public const int MaxOfferCount = 4;
        public const int GoldLimit = 99;
        public int Gold { get; private set; } = 3;
#if UNITY_EDITOR
        internal void SetGoldForTesting(int amount) => Gold = Math.Clamp(amount, 0, GoldLimit);
#endif
        public int CaptainHealth { get; private set; } = 30;
        public int LastAddedId { get; private set; }
        public bool Frozen { get; private set; }
        private bool expeditionCompleted;
        private int musterGold;
        public bool Finished => expeditionCompleted || CaptainHealth == 0 || Turn > MaxRounds;

        internal void RecordMusterProgress(int cells) => musterGold = Math.Max(musterGold, cells / 2);

        internal void CompleteExpedition()
        {
            Gold = Math.Min(GoldLimit, Gold + musterGold);
            musterGold = 0;
            expeditionCompleted = true;
        }
        public bool Ready => !Finished && Followers.Any(f => f.Slot >= 0);
        public int OfferCount => Tier == 1 ? 3 : MaxOfferCount;
        public bool CanUpgrade => !Finished && Tier < MaxTier && Gold >= UpgradeCost;
        public int UpgradeCost => Tier >= MaxTier ? 0 : Math.Max(0, 4 + Tier - (Turn - 1));
        public const int HandLimit = 10;
        public int BenchCount => Followers.Count(f => f.Slot < 0);
        public int DeployedCount => Followers.Count(f => f.Slot >= 0);
        public string Message { get; private set; } = "Recruit units to your hand.";

        public TavernRecruitment(int seed = -1)
        {
            random = seed < 0 ? new Random() : new Random(seed);
            Followers.Add(new Follower { Id = ++sequence, Role = UnitRole.Captain, Slot = 4 });
            Roll(false);
        }
        private const int RecruitRollRange = 100;

        internal static UnitRole RecruitRole(int tier, int roll)
        {
            if (tier < 2) return roll < 75 ? UnitRole.Zombie : UnitRole.BruteZombie;
            if (roll < 55) return UnitRole.Zombie;
            if (roll < 83) return UnitRole.BruteZombie;
            if (roll < 96) return UnitRole.ShamanZombie;
            return UnitRole.TankZombie;
        }

        private UnitRole RandomRole() => RecruitRole(Tier, random.Next(RecruitRollRange));
        public bool Roll(bool paid = true)
        {
            if (Finished || paid && Gold < 1) return false;
            if (paid) Gold--;
            Offers.Clear();for (var i = 0; i < OfferCount; i++) Offers.Add(RandomRole());
            Frozen = false;return true;
        }
        public bool ToggleFreeze()
        {
            if (Finished) return false;
            Frozen = !Frozen;return true;
        }
        public bool Upgrade()
        {
            if (!CanUpgrade) return false;
            Gold -= UpgradeCost;
            Tier++;
            while (Offers.Count < OfferCount) Offers.Add(RandomRole());
            Message = "Tavern upgraded. A new recruit is available.";
            return true;
        }
        internal static int RecruitCost(UnitRole role) => role switch
        {
            UnitRole.Zombie => 2,
            UnitRole.ShamanZombie => 5,
            UnitRole.TankZombie => 10,
            _ => 3
        };

        public bool CanBuy(int index) => !Finished && index >= 0 && index < Offers.Count &&
            Offers[index] is UnitRole role && RoleTier(role) <= Tier && Gold >= RecruitCost(role) && BenchCount < HandLimit;
        public bool Buy(int index)
        {
            if (!CanBuy(index)) return false;
            var role = Offers[index].Value;Gold -= RecruitCost(role);Offers[index] = null;Receive(role);return true;
        }
        private void Receive(UnitRole role)
        {
            var follower = new Follower { Id = ++sequence, Role = role };
            Followers.Add(follower);
            LastAddedId = follower.Id;
            Message = "Unit recruited to your hand.";
            if (SquadCombatRules.IsLegendaryRole(role)) return;

            var copies = Followers.Where(unit => unit.Role == role && !unit.Golden)
                .OrderBy(unit => unit.Slot < 0).ThenBy(unit => unit.Id).Take(3).ToArray();
            if (copies.Length != 3) return;
            var upgraded = copies[0];
            upgraded.Golden = true;
            Followers.Remove(copies[1]);
            Followers.Remove(copies[2]);
            LastAddedId = upgraded.Id;
            Message = "Three copies combined into a stronger unit.";
        }
        public bool Place(int id, int slot)
        {
            if (Finished || slot < 0 || slot > 8) return false;
            var unit = Followers.Find(f => f.Id == id);if (unit == null) return false;
            var other = Followers.Find(f => f.Slot == slot);
            if (unit.Slot < 0 && other?.Role == UnitRole.Captain) return false;
            if (unit.Slot < 0 && (other == null ? DeployedCount >= 6 : false)) return false;
            if (other != null) other.Slot = unit.Slot;
            unit.Slot = slot;return true;
        }
        public bool Bench(int id)
        {
            var unit = Followers.Find(f => f.Id == id);
            if (Finished || unit == null || unit.Role == UnitRole.Captain || unit.Slot < 0 ||
                BenchCount >= HandLimit || DeployedCount <= 1) return false;
            unit.Slot = -1;return true;
        }
        public bool Sell(int id)
        {
            var unit = Followers.Find(f => f.Id == id);
            if (Finished || unit == null || unit.Role == UnitRole.Captain || unit.Slot >= 0 && DeployedCount <= 1) return false;
            Followers.Remove(unit);Gold = Math.Min(GoldLimit, Gold + 1);Message = "Sold for 1 gold.";return true;
        }
        public UnitDefinition Stats(Follower unit)
        {
            if (MusterZombieRules.IsZombie(unit.Role)) return MusterZombieRules.Create(unit.Role, unit.Golden);
            var stats = SquadCombatRules.Create(unit.Role);
            if (!unit.Golden) return stats;
            return new UnitDefinition(stats.MaxHp * 2, stats.Speed, stats.Role, stats.Damage * 2,
                stats.Range, stats.AttackInterval, stats.Defense, stats.HealPerSecond * 2,
                stats.Rarity, 2, stats.TowerDamageMultiplier, stats.DamageType, stats.MagicResistance, stats.IsFlying);
        }
        public List<MemberSpawnDefinition> Deployment() => Followers.Where(f => f.Slot >= 0)
            .Select(f => new MemberSpawnDefinition(f.Id, f.Slot, Stats(f))).ToList();
        public void Resolve(bool victory) => ResolveRound(victory ? 0 : 1,
            victory ? "Defense breached! Prepare for the next assault." : "Assault lost. Captain lost 1 HP.");

        public static int MusterDamage(int towers, int arrivals) => Math.Max(0, towers - arrivals);

        public void ResolveMuster(int towers, int arrivals)
        {
            var damage = MusterDamage(towers, arrivals);
            ResolveRound(damage, $"Last assault: {arrivals}/{towers} reached. Damage taken: {damage}.", musterGold);
            musterGold = 0;
        }

        private void ResolveRound(int damage, string message, int bonusGold = 0)
        {
            if (Finished) return;
            CaptainHealth = Math.Max(0, CaptainHealth - damage);
            Turn++;
            Message = message;
            if (Finished)
            {
                Gold = Math.Min(GoldLimit, Gold + bonusGold);
                return;
            }
            Gold = Math.Min(GoldLimit, Turn + 2 + bonusGold);
            if (!Frozen) Roll(false);
            else { while (Offers.Count < OfferCount) Offers.Add(null);for (var i = 0; i < Offers.Count; i++) if (!Offers[i].HasValue) Offers[i] = RandomRole(); }
            Frozen = false;
        }
    }
}
