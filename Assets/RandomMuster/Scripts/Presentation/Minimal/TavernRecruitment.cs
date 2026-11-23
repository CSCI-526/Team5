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
        public static readonly UnitRole[] Roles = { UnitRole.Attack, UnitRole.Tank, UnitRole.Heal,
            UnitRole.Vanguard, UnitRole.Sniper, UnitRole.Demolisher, UnitRole.Frostweaver,
            UnitRole.Warcaller, UnitRole.Aegis };
        public static int RoleTier(UnitRole role) => role == UnitRole.Attack || role == UnitRole.Tank ? 1 :
            role == UnitRole.Heal ? 2 : role == UnitRole.Vanguard ? 3 : role == UnitRole.Sniper ? 4 :
            role == UnitRole.Demolisher || role == UnitRole.Frostweaver ? 5 : 6;
        public readonly List<Follower> Followers = new List<Follower>();
        public readonly List<UnitRole?> Offers = new List<UnitRole?>();
        public readonly List<UnitRole> Discover = new List<UnitRole>();
        private readonly Queue<int> rewards = new Queue<int>();
        private readonly Random random;
        private int sequence = 2000;
        public int Turn { get; private set; } = 1;
        public int Tier { get; private set; } = 1;
        public int Gold { get; private set; } = 3;
        public int Lives { get; private set; } = 3;
        public int LastAddedId { get; private set; }
        public bool Frozen { get; private set; }
        public bool Finished => Lives == 0 || Turn > 8;
        public bool Ready => !Finished && Discover.Count == 0 && Followers.Any(f => f.Slot >= 0);
        public int OfferCount => 3 + (Tier - 1) / 2;
        public int UpgradeCost => Tier >= 6 ? 0 : Math.Max(0, 4 + Tier - (Turn - 1));
        public int BenchCount => Followers.Count(f => f.Slot < 0);
        public int DeployedCount => Followers.Count(f => f.Slot >= 0);
        public string Message { get; private set; } = "Recruit units, collect triples and prepare your formation.";

        public TavernRecruitment(int seed = -1)
        {
            random = seed < 0 ? new Random() : new Random(seed);
            Followers.Add(new Follower { Id = ++sequence, Role = UnitRole.Captain, Slot = 4 });
            Followers.Add(new Follower { Id = ++sequence, Role = UnitRole.Attack, Slot = 7 });
            Roll(false);
        }
        private UnitRole RandomRole(int maxTier)
        {
            var pool = Roles.Where(r => RoleTier(r) <= maxTier &&
                (!SquadCombatRules.IsLegendaryRole(r) || !Followers.Any(f => f.Role == r))).ToArray();
            return pool[random.Next(pool.Length)];
        }
        public bool Roll(bool paid = true)
        {
            if (Finished || Discover.Count > 0 || paid && Gold < 1) return false;
            if (paid) Gold--;
            Offers.Clear();for (var i = 0; i < OfferCount; i++) Offers.Add(RandomRole(Tier));
            Frozen = false;return true;
        }
        public bool ToggleFreeze()
        {
            if (Finished || Discover.Count > 0) return false;
            Frozen = !Frozen;return true;
        }
        public bool Upgrade()
        {
            if (Finished || Discover.Count > 0 || Tier >= 6 || Gold < UpgradeCost) return false;
            Gold -= UpgradeCost;Tier++;
            // Existing offers stay in place; the larger shop arrives with the next refresh.
            Message = "Tavern upgraded. Refresh to recruit higher-tier units.";return true;
        }
        private bool CanReceive(UnitRole role) => BenchCount < 7 ||
            !SquadCombatRules.IsLegendaryRole(role) && Followers.Count(f => f.Role == role && !f.Golden) >= 2;
        public bool CanBuy(int index) => !Finished && Discover.Count == 0 && Gold >= 3 && index >= 0 && index < Offers.Count &&
            Offers[index].HasValue && CanReceive(Offers[index].Value) &&
            (!SquadCombatRules.IsLegendaryRole(Offers[index].Value) || !Followers.Any(f => f.Role == Offers[index].Value));
        public bool Buy(int index)
        {
            if (!CanBuy(index)) return false;
            var role = Offers[index].Value;Gold -= 3;Offers[index] = null;Receive(role);return true;
        }
        private void Receive(UnitRole role)
        {
            var follower = new Follower { Id = ++sequence, Role = role };Followers.Add(follower);LastAddedId = follower.Id;
            var matching = Followers.Where(f => f.Role == role && !f.Golden).OrderBy(f => f.Slot < 0 ? 1 : 0).ThenBy(f => f.Id).Take(3).ToArray();
            if (matching.Length == 3 && !SquadCombatRules.IsLegendaryRole(role))
            {
                var keep = matching[0];keep.Golden = true;
                foreach (var other in matching.Skip(1)) Followers.Remove(other);
                LastAddedId = keep.Id;rewards.Enqueue(Math.Min(6, Tier + 1));
                Message = "TRIPLE! A golden unit and a free discovery reward.";
                OpenDiscovery();
            }
            else Message = "Unit recruited to your bench. Place it in the formation.";
        }
        private void OpenDiscovery()
        {
            if (Discover.Count > 0 || rewards.Count == 0) return;
            var target = rewards.Dequeue();
            var choices = Roles.Where(r => RoleTier(r) == target && (!SquadCombatRules.IsLegendaryRole(r) || !Followers.Any(f => f.Role == r))).ToList();
            if (choices.Count == 0) choices = Roles.Where(r => RoleTier(r) <= target && !SquadCombatRules.IsLegendaryRole(r)).ToList();
            // Small existing catalog: offer distinct roles rather than three duplicate portraits.
            foreach (var role in choices.Concat(Roles.Where(r => RoleTier(r) < target && !SquadCombatRules.IsLegendaryRole(r))).Distinct().OrderBy(r => RoleTier(r) == target ? 0 : 1).ThenBy(_ => random.Next()).Take(3)) Discover.Add(role);
        }
        public bool ChooseDiscovery(int index)
        {
            if (index < 0 || index >= Discover.Count || !CanReceive(Discover[index])) return false;
            var role = Discover[index];Discover.Clear();Receive(role);OpenDiscovery();return true;
        }
        public bool Place(int id, int slot)
        {
            if (Finished || slot < 0 || slot > 8) return false;
            var unit = Followers.Find(f => f.Id == id);if (unit == null) return false;
            var other = Followers.Find(f => f.Slot == slot);
            if (unit.Slot < 0 && (other == null ? DeployedCount >= 6 : false)) return false;
            if (other != null) other.Slot = unit.Slot;
            unit.Slot = slot;return true;
        }
        public bool Bench(int id)
        {
            var unit = Followers.Find(f => f.Id == id);
            if (Finished || unit == null || unit.Slot < 0 || BenchCount >= 7 || DeployedCount <= 1) return false;
            unit.Slot = -1;return true;
        }
        public bool Sell(int id)
        {
            var unit = Followers.Find(f => f.Id == id);
            if (Finished || unit == null || unit.Role == UnitRole.Captain || unit.Slot >= 0 && DeployedCount <= 1) return false;
            Followers.Remove(unit);Gold = Math.Min(10, Gold + 1);Message = "Sold for 1 gold.";return true;
        }
        public UnitDefinition Stats(Follower unit)
        {
            var stats = SquadCombatRules.Create(unit.Role);
            if (!unit.Golden) return stats;
            return new UnitDefinition(stats.MaxHp * 2, stats.Speed, stats.Role, stats.Damage * 2,
                stats.Range, stats.AttackInterval, stats.Defense, stats.HealPerSecond * 2,
                stats.Rarity, 2, stats.TowerDamageMultiplier, stats.DamageType);
        }
        public List<MemberSpawnDefinition> Deployment() => Followers.Where(f => f.Slot >= 0)
            .Select(f => new MemberSpawnDefinition(f.Id, f.Slot, Stats(f))).ToList();
        public void Resolve(bool victory)
        {
            if (Finished) return;
            if (!victory) Lives--;Turn++;
            Message = victory ? "Defense breached! Prepare for the next assault." : "Assault lost. Your units recover; one life lost.";
            if (Finished) return;
            Gold = Math.Min(10, Turn + 2); // New turn allowance, not accumulated interest.
            if (!Frozen) Roll(false);
            else { while (Offers.Count < OfferCount) Offers.Add(null);for (var i = 0; i < Offers.Count; i++) if (!Offers[i].HasValue) Offers[i] = RandomRole(Tier); }
            Frozen = false;
        }
    }
}
