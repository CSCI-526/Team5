using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public enum BasicDefenseKind { None, Archer, Mage, Artillery, Barracks }

    public sealed partial class BattleSimulation
    {
        public sealed class DefenseShot
        {
            public BasicDefenseKind Kind { get; internal set; }
            public Vector3 Position { get; internal set; }
            internal Vector3 From, Landing;
            internal int TowerId, MemberId;
            internal float Age, Duration, LowDamage, HighDamage, BlastRadius, BlastEdgeMultiplier;
            internal DefenseShot Copy() => (DefenseShot)MemberwiseClone();
        }

        public sealed class BarracksGuard
        {
            public int Id { get; internal set; }
            public int TowerId { get; internal set; }
            public Vector3 Position { get; internal set; }
            public Vector3 Rally { get; internal set; }
            public float Hp { get; internal set; } = 7;
            public bool Visible { get; internal set; } = true;
            internal float Cooldown, Respawn;
            internal int TargetId;
            internal BarracksGuard Copy() => (BarracksGuard)MemberwiseClone();
        }

        private readonly List<DefenseShot> defenseShots = new List<DefenseShot>();
        private readonly List<BarracksGuard> barracksGuards = new List<BarracksGuard>();
        private readonly HashSet<int> interceptedSquads = new HashSet<int>();
        private readonly HashSet<int> interceptedMembers = new HashSet<int>();
        private uint basicDefenseSeed = 526;
        public IReadOnlyList<DefenseShot> DefenseShots => defenseShots;
        public IReadOnlyList<BarracksGuard> BarracksGuards => barracksGuards;
        public event Action<BasicDefenseKind, Vector3, float> BasicDefenseImpact;

        private void InitializeBasicDefenses()
        {
            foreach (var tower in towers)
            {
                if (tower.Definition.BasicDefense != BasicDefenseKind.Barracks) continue;
                var closest = Mathf.Min(1, Waypoints.Count - 1);
                for (var node = 2; node < Waypoints.Count - 1; node++)
                    if ((Waypoints[node] - tower.Position).sqrMagnitude < (Waypoints[closest] - tower.Position).sqrMagnitude)
                        closest = node;
                var direction = (Waypoints[closest] - Waypoints[Mathf.Max(0, closest - 1)]).normalized;
                var across = new Vector3(direction.z, 0, -direction.x);
                for (var lane = -1; lane <= 1; lane++)
                    barracksGuards.Add(new BarracksGuard { Id = barracksGuards.Count + 1, TowerId = tower.Id,
                        Position = tower.Position, Rally = Waypoints[closest] + across * lane });
            }
        }

        private void CopyBasicDefensesTo(BattleSimulation copy)
        {
            copy.barracksGuards.Clear();
            copy.basicDefenseSeed = basicDefenseSeed;
            foreach (var guard in barracksGuards) copy.barracksGuards.Add(guard.Copy());
            foreach (var shot in defenseShots) copy.defenseShots.Add(shot.Copy());
        }

        private float RollDefenseDamage(float low, float high)
        {
            basicDefenseSeed = unchecked(1664525u * basicDefenseSeed + 1013904223u);
            return Mathf.Lerp(low, high, (basicDefenseSeed >> 8) / 16777215f);
        }

        private bool AttackWithBasicDefense(TowerState tower, float deltaTime)
        {
            var kind = tower.Definition.BasicDefense;
            if (kind == BasicDefenseKind.None) return false;
            if (!tower.IsAlive || tower.FrozenRemaining > 0 || kind == BasicDefenseKind.Barracks)
            {
                tower.TargetId = null;
                return true;
            }
            tower.CooldownRemaining = Mathf.Max(0, tower.CooldownRemaining - deltaTime);
            if (tower.CooldownRemaining > .000001f) return true;
            var range = tower.Definition.Range;
            var target = units.Where(unit => unit.IsAlive &&
                    (kind != BasicDefenseKind.Artillery || !unit.Definition.IsFlying) &&
                    HorizontalDistanceSquared(unit.Position, tower.Position) <= range * range)
                .OrderByDescending(unit => unit.RouteProgress).ThenBy(unit => unit.Id).FirstOrDefault();
            tower.TargetId = target?.Id;
            if (target == null) return true;
            tower.CooldownRemaining = tower.Definition.AttackInterval;
            TowerAttacked?.Invoke(new TowerAttackEvent(tower, target));
            var height = kind == BasicDefenseKind.Archer ? 2.4f : 1.4f;
            var duration = kind == BasicDefenseKind.Artillery ? .85f : .36f;
            var origin = tower.Position + Vector3.up * height;
            var landing = SampleAnchor(target.RouteProgress + (interceptedSquads.Contains(target.SquadId) ? 0 : target.Speed * duration))
                + target.FormationOffset;
            defenseShots.Add(new DefenseShot
            {
                Kind = kind, Position = origin, From = origin, Landing = landing, TowerId = tower.Id,
                MemberId = target.Id, Duration = duration,
                LowDamage = tower.Definition.MinimumShotDamage, HighDamage = tower.Definition.Damage,
                BlastRadius = tower.Definition.BlastRadius, BlastEdgeMultiplier = tower.Definition.BlastEdgeMultiplier
            });
            return true;
        }

        private void TickBasicDefenseShots(float deltaTime)
        {
            for (var i = defenseShots.Count - 1; i >= 0; i--)
            {
                var shot = defenseShots[i];
                shot.Age += deltaTime;
                var target = units.Find(unit => unit.Id == shot.MemberId && unit.IsAlive);
                if (shot.Kind == BasicDefenseKind.Mage)
                {
                    if (target != null)
                    {
                        var aim = target.Position + Vector3.up * .45f;
                        shot.Position = Vector3.MoveTowards(shot.Position, aim, 7f * deltaTime);
                        if ((shot.Position - aim).sqrMagnitude > .00001f && shot.Age < 5f) continue;
                        if ((shot.Position - aim).sqrMagnitude <= .00001f)
                            DamageUnit(shot.TowerId, target, RollDefenseDamage(shot.LowDamage, shot.HighDamage), magical: true);
                    }
                }
                else
                {
                    var t = Mathf.Clamp01(shot.Age / shot.Duration);
                    var arc = shot.Kind == BasicDefenseKind.Artillery ? 3.6f : .72f;
                    shot.Position = Vector3.Lerp(shot.From, shot.Landing, t) + Vector3.up * (arc * 4f * t * (1f - t));
                    if (t < 1f) continue;
                    if (shot.Kind == BasicDefenseKind.Artillery)
                    {
                        foreach (var unit in units)
                        {
                            if (!unit.IsAlive || unit.Definition.IsFlying) continue;
                            var distance = Mathf.Sqrt(HorizontalDistanceSquared(unit.Position, shot.Landing));
                            if (distance > shot.BlastRadius) continue;
                            var multiplier = Mathf.Lerp(1f, shot.BlastEdgeMultiplier, distance / shot.BlastRadius);
                            DamageUnit(shot.TowerId, unit, shot.HighDamage * multiplier, .5f);
                        }
                    }
                    else if (target != null && HorizontalDistanceSquared(target.Position, shot.Landing) <= .24f * .24f)
                        DamageUnit(shot.TowerId, target, RollDefenseDamage(shot.LowDamage, shot.HighDamage));
                }
                BasicDefenseImpact?.Invoke(shot.Kind, shot.Position, shot.Kind == BasicDefenseKind.Artillery ? shot.BlastRadius : .22f);
                defenseShots.RemoveAt(i);
            }
        }

        private void TickBarracks(float deltaTime)
        {
            if (barracksGuards.Count == 0) return;
            interceptedSquads.Clear();
            interceptedMembers.Clear();
            foreach (var guard in barracksGuards)
            {
                var owner = towers.Find(tower => tower.Id == guard.TowerId);
                if (owner == null || !owner.IsAlive)
                {
                    guard.Visible = false;
                    continue;
                }
                if (guard.Hp <= 0)
                {
                    guard.Respawn -= deltaTime;
                    if (guard.Respawn > 0) continue;
                    guard.Hp = 7;
                    guard.Position = owner.Position;
                    guard.Cooldown = 0;
                }
                guard.Visible = true;
                if (owner.FrozenRemaining > 0) continue;
                guard.Cooldown = Mathf.Max(0, guard.Cooldown - deltaTime);
                var target = units.Where(unit => unit.IsAlive && !interceptedMembers.Contains(unit.Id) &&
                        HorizontalDistanceSquared(unit.Position, guard.Rally) <= .85f * .85f)
                    .OrderBy(unit => unit.Id == guard.TargetId ? 0 : 1)
                    .ThenBy(unit => HorizontalDistanceSquared(unit.Position, guard.Rally)).ThenBy(unit => unit.Id).FirstOrDefault();
                guard.Position = Vector3.MoveTowards(guard.Position, guard.Rally, 1.8f * deltaTime);
                if (target == null || (guard.Position - guard.Rally).sqrMagnitude > .03f)
                {
                    guard.TargetId = 0;
                    guard.Hp = Mathf.Min(7, guard.Hp + .6f * deltaTime);
                    continue;
                }
                guard.TargetId = target.Id;
                interceptedMembers.Add(target.Id);
                interceptedSquads.Add(target.SquadId);
                if (guard.Cooldown <= .000001f)
                {
                    guard.Cooldown = 1f;
                    DamageUnit(owner.Id, target, RollDefenseDamage(.25f, .5f));
                    BasicDefenseImpact?.Invoke(BasicDefenseKind.Barracks, target.Position, .2f);
                }
                // Attack-capable members of the intercepted squad can clear their own blocker.
                foreach (var ally in units)
                {
                    if (!ally.IsAlive || ally.SquadId != target.SquadId || !SquadCombatRules.CanAttackTowers(ally.Role) ||
                        HorizontalDistanceSquared(ally.Position, guard.Position) > ally.Definition.Range * ally.Definition.Range ||
                        ally.AttackCooldown > deltaTime) continue;
                    ally.AttackCooldown = ally.Definition.AttackInterval + deltaTime;
                    guard.Hp -= ally.Definition.Damage * .7f;
                    if (guard.Hp <= 0)
                    {
                        guard.Visible = false;
                        guard.TargetId = 0;
                        guard.Respawn = 14;
                        break;
                    }
                }
            }
        }
    }
}
