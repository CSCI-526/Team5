using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public sealed partial class BattleSimulation
    {
        private readonly BattleDefinition definition;
        private readonly List<UnitState> units = new List<UnitState>();
        private readonly List<TowerState> towers = new List<TowerState>();
        private readonly List<ProjectileState> projectiles = new List<ProjectileState>();
        private readonly HashSet<int> spawnedMembers = new HashSet<int>();
        private readonly HashSet<Vector3Int> walkable;
        private double time;
        private bool stepping;
        private double healTimer;
        private int projectileSequence;
        private int extraMemberSequence;
        private List<Vector3> manualRoute;
        public const int ManualRouteStep = 3;
        public bool ManualMovement => manualRoute != null;
        private IReadOnlyList<Vector3> Waypoints => manualRoute != null ? (IReadOnlyList<Vector3>)manualRoute : definition.Waypoints;
        public void EnableManualMovement()
        {
            if (stepping || SpawnedCount != 0) throw new InvalidOperationException("Configure manual movement before spawning.");
            manualRoute = new List<Vector3> { definition.Waypoints[0] };
        }
        public bool CanPlaceSquadAt(Vector3 anchor)
        {
            if (!BattleNumbers.Finite(anchor)) return false;
            if (continuous != null) return continuous.CanPlaceFormation(anchor);
            foreach (var member in units)
                if (member.IsAlive && !CanStand(anchor + member.FormationOffset)) return false;
            return AliveCount > 0;
        }
        public bool TrySetManualRoute(IReadOnlyList<Vector3> points)
        {
            if (stepping || continuous != null || !ManualMovement || !IsPaused || Outcome != BattleOutcome.Running ||
                PendingSpawnCount != 0 || AliveCount == 0 || points == null || points.Count < 2) return false;
            UnitState first = units.Find(u => u.IsAlive);
            if (Vector3.Distance(first.AnchorPosition, points[0]) > .01f) return false;
            for (var i = 0; i < points.Count; i++)
            {
                if (!CanPlaceSquadAt(points[i])) return false;
                if (i == 0) continue;
                var delta = points[i] - points[i - 1];
                var length = delta.magnitude;
                if (Mathf.Abs(delta.y) > .001f || Mathf.Abs(delta.x) > .001f && Mathf.Abs(delta.z) > .001f ||
                    length < ManualRouteStep - .001f ||
                    Mathf.Abs(length - Mathf.Round(length / ManualRouteStep) * ManualRouteStep) > .001f) return false;
                foreach (var member in units)
                    if (member.IsAlive)
                        foreach (var cell in FormationClearance.SweptCells(points[i - 1] + member.FormationOffset, points[i] + member.FormationOffset))
                            if ((walkable != null && !walkable.Contains(cell)) || HasTowerFootprint(cell)) return false;
            }
            manualRoute = new List<Vector3>(points);
            foreach (var member in units)
                if (member.IsAlive) { member.RouteProgress = 0; member.NextWaypoint = 1; }
            FormationChanged?.Invoke();
            return true;
        }

        public ReadOnlyCollection<UnitState> Units { get; }
        public ReadOnlyCollection<TowerState> Towers { get; }
        public ReadOnlyCollection<ProjectileState> Projectiles { get; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Running;
        public BattleObjective Objective => definition.Objective;
        public bool IsPaused { get; private set; }
        public int SpawnedCount { get; private set; }
        public int PendingSpawnCount => definition.Members != null
            ? definition.Members.Count - spawnedMembers.Count : definition.UnitCount - SpawnedCount;
        public int KilledCount { get; private set; }
        public int ReachedGoalCount { get; private set; }
        public int TowersDestroyedCount { get; private set; }
        public int TowerBountyGoldEarned { get; private set; }
        public int LivesRemaining => Math.Max(0, definition.GoalLives - ReachedGoalCount);
        public int AliveCount => SpawnedCount - KilledCount - ReachedGoalCount;
        public double ElapsedTime => time;
        public event Action<UnitSpawnedEvent> UnitSpawned;
        public event Action<UnitMovedEvent> UnitMoved;
        public event Action<DamageEvent> UnitDamaged;
        public event Action<UnitEndedEvent> UnitDied;
        public event Action<UnitEndedEvent> UnitReachedGoal;
        public event Action<TowerAttackEvent> TowerAttacked;
        public event Action<BattleEndedEvent> BattleEnded;
        public event Action<TowerDamageEvent> TowerDamaged;
        public event Action<int> TowerDied;
        public event Action<TowerDestroyedEvent> TowerDestroyed;
        public event Action<HealEvent> UnitHealed;
        public event Action<int> HealerPulsed;
        public event Action FormationChanged;
        public event Action<UnitRole, int, Vector3> LegendarySkillUsed;
        public event Action<int, int> TowerHitBlocked;
        public IReadOnlyList<MemberSpawnDefinition> PlannedMembers => definition.Members;

        public sealed class Checkpoint
        {
            private readonly BattleSimulation saved;
            internal Checkpoint(BattleSimulation saved) { this.saved = saved; }
            public BattleSimulation Restore() => saved.CopyState();
        }

        public Checkpoint CaptureCheckpoint()
        {
            if (stepping || Outcome != BattleOutcome.Running)
                throw new InvalidOperationException("Capture a running battle outside tick callbacks.");
            return new Checkpoint(CopyState());
        }

        private BattleSimulation CopyState()
        {
            var copy = new BattleSimulation(definition);
            copy.towers.Clear();
            foreach (var tower in towers) copy.towers.Add(tower.Copy());
            foreach (var unit in units) copy.units.Add(unit.Copy());
            foreach (var shot in projectiles) copy.projectiles.Add(shot.Copy());
            copy.spawnedMembers.UnionWith(spawnedMembers);
            copy.manualRoute = manualRoute == null ? null : new List<Vector3>(manualRoute);
            if (continuous != null) copy.continuous = continuous.Copy(copy);
            copy.time = time; copy.healTimer = healTimer; copy.projectileSequence = projectileSequence;
            copy.extraMemberSequence = extraMemberSequence;
            copy.Outcome = Outcome; copy.IsPaused = IsPaused;
            copy.SpawnedCount = SpawnedCount; copy.KilledCount = KilledCount; copy.ReachedGoalCount = ReachedGoalCount;
            copy.TowersDestroyedCount = TowersDestroyedCount; copy.TowerBountyGoldEarned = TowerBountyGoldEarned;
            return copy;
        }

        public BattleSimulation(BattleDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            foreach (var tower in definition.Towers) towers.Add(new TowerState(tower));
            Units = units.AsReadOnly(); Towers = towers.AsReadOnly(); Projectiles = projectiles.AsReadOnly();
            extraMemberSequence = definition.Members != null ? 1000000 : definition.UnitCount + 1000000;
            if (definition.WalkableCells == null) return;
            walkable = new HashSet<Vector3Int>(definition.WalkableCells);
            for (var i = 1; i < definition.Waypoints.Count; i++)
                for (var slot = 0; slot < 9; slot++)
                    foreach (var cell in FormationClearance.SweptCells(definition.Waypoints[i - 1] + SquadCombatRules.Offset(slot),
                        definition.Waypoints[i] + SquadCombatRules.Offset(slot)))
                        if (!walkable.Contains(cell) || HasTowerFootprint(cell, false))
                            throw new ArgumentException($"Formation trajectory blocked at {cell}, slot {slot}, segment {i - 1}.");
        }

        public void SetTowerPosition(int towerId, Vector3 position)
        {
            if (!BattleNumbers.Finite(position)) throw new ArgumentOutOfRangeException(nameof(position));
            if (stepping) throw new InvalidOperationException("Cannot change tower position during a tick callback.");
            var tower = towers.Find(value => value.Id == towerId);
            if (tower == null) throw new ArgumentException("Unknown tower ID.");
            tower.Position = position;
        }
        public void SetPaused(bool paused)
        {
            if (stepping) throw new InvalidOperationException("Submit pause outside tick callbacks.");
            if (Outcome == BattleOutcome.Running) IsPaused = paused;
        }
        public UnitState GetMemberAtSlot(int slotId) => units.Find(unit => unit.IsAlive && unit.SlotId == slotId);

        public bool CanUseLegendarySkill(int memberId)
        {
            var caster = units.Find(unit => unit.MemberId == memberId);
            return !stepping && !IsPaused && Outcome == BattleOutcome.Running && caster != null && caster.IsAlive &&
                caster.Definition.Rarity == UnitRarity.Legendary && LegendarySkillRules.HasActiveSkill(caster.Role) &&
                caster.SkillCooldownRemaining <= 0.000001f;
        }

        // All abilities are support-only. This is the authoritative activation path for UI and tests.
        // Time Lock requires a tower near the clicked ground position; other auras center on the caster.
        public bool TryUseLegendarySkill(int memberId, Vector3 targetPoint)
        {
            if (!CanUseLegendarySkill(memberId) || !BattleNumbers.Finite(targetPoint)) return false;
            var caster = units.Find(unit => unit.MemberId == memberId);
            var affected = 0;
            switch (caster.Role)
            {
                case UnitRole.Captain:
                    // A RANDOM ordinary role at the campaign's currently unlocked summoning tier.
                    // Roll only after checking capacity; failed casts must not advance RNG or spend cooldown.
                    if (HasFreeReinforcementSlot() && TrySpawnExtra(RollCaptainReinforcement(), caster.Position)) affected++;
                    else
                    {
                        TowerState nearest = null;
                        var best = LegendarySkillRules.CaptainStrikeRange * LegendarySkillRules.CaptainStrikeRange;
                        foreach (var tower in towers)
                        {
                            if (!tower.IsAlive || tower.Definition.IsWoodenGate) continue;
                            if (tower.Definition.RequiresExplosiveDamage) continue;
                            var distance = HorizontalDistanceSquared(tower.Position, caster.Position);
                            if (distance >= best) continue;
                            nearest = tower; best = distance;
                        }
                        if (nearest != null)
                        {
                            DamageTower(caster.Id, nearest, LegendarySkillRules.CaptainStrikeDamage + caster.Definition.Level,
                                AttackDamageType.Normal);
                            affected++;
                        }
                    }
                    break;
                case UnitRole.Frostweaver:
                    foreach (var tower in towers)
                    {
                        if (!tower.IsAlive || (tower.Definition.IsWall && !tower.Definition.IsMinion) ||
                            HorizontalDistanceSquared(tower.Position, targetPoint) > LegendarySkillRules.FreezeRadius * LegendarySkillRules.FreezeRadius) continue;
                        tower.FrozenRemaining = Mathf.Max(tower.FrozenRemaining, LegendarySkillRules.FreezeTime(caster.Definition.Level));
                        tower.TargetId = null;
                        affected++;
                    }
                    break;
                case UnitRole.Warcaller:
                    foreach (var ally in units)
                    {
                        if (!ally.IsAlive || ally.SquadId != caster.SquadId ||
                            !SquadCombatRules.CanAttackTowers(ally.Role) ||
                            HorizontalDistanceSquared(ally.Position, caster.Position) > LegendarySkillRules.RallyRadius * LegendarySkillRules.RallyRadius) continue;
                        ally.DamageBuffRemaining = Mathf.Max(ally.DamageBuffRemaining, LegendarySkillRules.RallyDuration);
                        ally.DamageBuffMultiplier = Mathf.Max(ally.DamageBuffMultiplier, LegendarySkillRules.RallyMultiplier(caster.Definition.Level));
                        affected++;
                    }
                    break;
                case UnitRole.Aegis:
                    foreach (var ally in units)
                    {
                        if (!ally.IsAlive || ally.SquadId != caster.SquadId ||
                            HorizontalDistanceSquared(ally.Position, caster.Position) > LegendarySkillRules.BarrierRadius * LegendarySkillRules.BarrierRadius) continue;
                        ally.BarrierHits = LegendarySkillRules.BarrierCharges;
                        ally.BarrierRemaining = LegendarySkillRules.BarrierDuration;
                        affected++;
                    }
                    break;
            }
            if (affected == 0) return false; // Invalid target does not waste a skill.
            caster.SkillCooldownRemaining = LegendarySkillRules.Cooldown(caster.Role);
            LegendarySkillUsed?.Invoke(caster.Role, caster.MemberId, caster.Role == UnitRole.Frostweaver ? targetPoint : caster.Position);
            return true;
        }

        private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            var x = a.x - b.x;
            var z = a.z - b.z;
            return x * x + z * z;
        }

        private void TickLegendaryEffects(float deltaTime)
        {
            foreach (var tower in towers)
                tower.FrozenRemaining = Mathf.Max(0, tower.FrozenRemaining - deltaTime);
            foreach (var unit in units)
            {
                unit.SkillCooldownRemaining = Mathf.Max(0, unit.SkillCooldownRemaining - deltaTime);
                unit.DamageBuffRemaining = Mathf.Max(0, unit.DamageBuffRemaining - deltaTime);
                if (unit.DamageBuffRemaining <= 0) unit.DamageBuffMultiplier = 1f;
                unit.BarrierRemaining = Mathf.Max(0, unit.BarrierRemaining - deltaTime);
                if (unit.BarrierRemaining <= 0) unit.BarrierHits = 0;
            }
        }

        // Formations are locked after deployment; changes are made only in preparation.
        public bool TryApplyFormation(IReadOnlyList<int> sourceSlots) => false;



        public void Step(float deltaTime)
        {
            if (!BattleNumbers.Finite(deltaTime) || deltaTime <= 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (stepping) throw new InvalidOperationException("Simulation.Step cannot be called from an event listener.");
            if (Outcome != BattleOutcome.Running || IsPaused) return;
            stepping = true;
            try
            {
                Spawn();
                TickLegendaryEffects(deltaTime);
                if (continuous != null) continuous.Step(deltaTime);
                var wallStop = continuous == null ? WallStopProgress() : 0;
                foreach (var unit in units)
                {
                    if (!unit.IsAlive) continue;
                    if (continuous == null) Move(unit, deltaTime, wallStop);
                    else UnitMoved?.Invoke(new UnitMovedEvent(unit));
                    if (Outcome != BattleOutcome.Running) return;
                    if (Objective == BattleObjective.DefendGoal && ReachedGoalCount >= definition.GoalLives) break;
                }
                if (ReachedGoalCount < definition.GoalLives)
                {
                    foreach (var tower in towers) Attack(tower, deltaTime);
                    if (Objective == BattleObjective.ReachGoal)
                    {
                        foreach (var unit in units) AttackTower(unit, deltaTime);
                        TickProjectiles(deltaTime);
                        Heal(deltaTime);
                    }
                }
                time += deltaTime;
                CheckContinuousGoal();
                if (Outcome != BattleOutcome.Running) return;
                if (Objective == BattleObjective.ReachGoal)
                {
                    if (PendingSpawnCount == 0 && AliveCount == 0) End(BattleOutcome.Defeat);
                }
                else if (ReachedGoalCount >= definition.GoalLives) End(BattleOutcome.Defeat);
                else if (PendingSpawnCount == 0 && AliveCount == 0) End(BattleOutcome.Victory);
            }
            finally { stepping = false; }
        }

        private void Spawn()
        {
            if (definition.Members != null)
            {
                foreach (var member in definition.Members)
                {
                    if (spawnedMembers.Contains(member.MemberId) || member.SpawnTime > time + 0.000001) continue;
                    spawnedMembers.Add(member.MemberId);
                    var unit = new UnitState(member.MemberId, member.Unit,
                        SampleAnchor(SpawnProgress(member.Unit.Speed)) + SquadCombatRules.Offset(member.SlotId),
                        1, SquadCombatRules.Slot(member.SlotId), SquadCombatRules.Offset(member.SlotId));
                    unit.RouteProgress = SpawnProgress(member.Unit.Speed);
                    unit.NextWaypoint = NextWaypoint(unit.RouteProgress);
                    units.Add(unit); SpawnedCount++;
                    if (continuous != null) continuous.Spawn(unit);
                    UnitSpawned?.Invoke(new UnitSpawnedEvent(unit));
                    for (var extra = 1; extra < SquadCombatRules.StartingSummonCount(member.Unit.Role); extra++)
                        TrySpawnExtra(member.Unit, unit.Position);
                }
                return;
            }
            while (SpawnedCount < definition.UnitCount &&
                (SpawnedCount / definition.SquadCapacity) * (double)definition.SpawnInterval <= time + 0.000001)
            {
                var squadId = SpawnedCount / definition.SquadCapacity + 1;
                var count = Math.Min(definition.SquadCapacity, definition.UnitCount - SpawnedCount);
                for (var slot = 0; slot < count; slot++)
                {
                    var coordinate = new Vector2Int(slot % definition.FormationSize, slot / definition.FormationSize);
                    var radius = definition.FormationSize / 2;
                    var offset = new Vector3(coordinate.x - radius, 0, coordinate.y - radius);
                    var unit = new UnitState(++SpawnedCount, definition.Unit, Waypoints[0] + offset, squadId, coordinate, offset);
                    units.Add(unit); UnitSpawned?.Invoke(new UnitSpawnedEvent(unit));
                }
            }
        }

        private bool HasFreeReinforcementSlot()
        {
            if (units.FindAll(u => u.IsAlive).Count >= LegendarySkillRules.CaptainBattlefieldCapacity) return false;
            var occupied = new HashSet<int>();
            foreach (var unit in units) if (unit.IsAlive) occupied.Add(unit.SlotId);
            if (definition.Members != null)
                foreach (var planned in definition.Members)
                    if (!spawnedMembers.Contains(planned.MemberId)) occupied.Add(planned.SlotId);
            return occupied.Count < LegendarySkillRules.CaptainBattlefieldCapacity;
        }

        private static readonly UnitRole[] CaptainPool =
        { UnitRole.Attack, UnitRole.Tank, UnitRole.Heal, UnitRole.Sniper, UnitRole.Vanguard, UnitRole.Demolisher };

        private UnitDefinition RollCaptainReinforcement()
        {
            var tier = definition.SummonTier;
            var (common, rare, epic, _) = SquadCombatRules.GetRarityWeightsForTier(tier);
            // Unique legendary supports are deliberately excluded from reinforcements.
            var roll = UnityEngine.Random.Range(0, common + rare + epic);
            var rarity = roll < common ? UnitRarity.Common : roll < common + rare ? UnitRarity.Rare : UnitRarity.Epic;
            var role = CaptainPool[UnityEngine.Random.Range(0, CaptainPool.Length)];
            return SquadCombatRules.Create(role, rarity, tier);
        }

        // One physical unit per formation slot. Extras never change the six-card deployment.
        private bool TrySpawnExtra(UnitDefinition definition, Vector3 near)
        {
            if (units.FindAll(u => u.IsAlive).Count >= LegendarySkillRules.CaptainBattlefieldCapacity) return false;
            var reserved = new HashSet<int>();
            if (this.definition.Members != null)
                foreach (var planned in this.definition.Members)
                    if (!spawnedMembers.Contains(planned.MemberId)) reserved.Add(planned.SlotId);
            foreach (var existing in units) if (existing.IsAlive) reserved.Add(existing.SlotId);
            var prefer = new[] { 4, 1, 3, 5, 7, 0, 2, 6, 8 };
            foreach (var slot in prefer)
            {
                if (reserved.Contains(slot) || continuous != null && !continuous.CanSpawnInSlot(slot)) continue;
                var position = continuous != null ? continuous.Reference + SquadCombatRules.Offset(slot) *
                    continuous.FormationSpacing : near - (units.Count > 0 ? units[0].FormationOffset : Vector3.zero) + SquadCombatRules.Offset(slot);
                // For tight open maps, let continuous movement repair the initial shape later.
                var unit = new UnitState(++extraMemberSequence, definition, position, 1,
                    SquadCombatRules.Slot(slot), SquadCombatRules.Offset(slot));
                units.Add(unit); SpawnedCount++;
                if (continuous != null) continuous.Spawn(unit);
                UnitSpawned?.Invoke(new UnitSpawnedEvent(unit));
                return true;
            }
            return false;
        }

        public bool TryApplyEnvironmentalDamage(Vector3 location, float radius, float damage)
        {
            if (stepping || IsPaused || Outcome != BattleOutcome.Running || radius <= 0 || damage <= 0) return false;
            var hit = false;
            foreach (var unit in units)
            {
                if (!unit.IsAlive || HorizontalDistanceSquared(unit.Position, location) > radius * radius) continue;
                DamageUnit(-1, unit, damage);
                hit = true;
            }
            if (PendingSpawnCount == 0 && AliveCount == 0) End(BattleOutcome.Defeat);
            return hit;
        }

        public void ForceTimeLimitDefeat()
        {
            if (!stepping && Outcome == BattleOutcome.Running) End(BattleOutcome.Defeat);
        }

        private void CheckContinuousGoal()
        {
            if (continuous == null || Objective != BattleObjective.ReachGoal ||
                Outcome != BattleOutcome.Running || PendingSpawnCount > 0 || continuous.Moving) return;
            var goal = definition.Waypoints[definition.Waypoints.Count - 1];
            if (HorizontalDistanceSquared(continuous.Reference, goal) > 1.1f * 1.1f) return;
            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                unit.LifeState = UnitLifeState.ReachedGoal;
                ReachedGoalCount++;
                UnitReachedGoal?.Invoke(new UnitEndedEvent(unit));
            }
            End(BattleOutcome.Victory);
        }

        private float SpawnProgress(float speed)
        {
            // Reinforcements join the existing formation even when a wall has stopped it.
            foreach (var unit in units) if (unit.IsAlive) return unit.RouteProgress;
            return Mathf.Min((float)time * speed, Mathf.Min(RouteLength(), WallStopProgress()));
        }

        private float WallStopProgress()
        {
            var stop = float.PositiveInfinity;
            var members = new List<(float progress, Vector3 offset)>();
            foreach (var unit in units)
                if (unit.IsAlive) members.Add((unit.RouteProgress, unit.FormationOffset));
            // Reserve planned reinforcement slots too, so late spawns cannot appear inside a wall.
            if (definition.Members != null)
            {
                var anchorProgress = members.Count > 0 ? members[0].progress : 0;
                foreach (var member in definition.Members)
                    if (!spawnedMembers.Contains(member.MemberId)) members.Add((anchorProgress, SquadCombatRules.Offset(member.SlotId)));
            }
            if (members.Count == 0 && definition.Members == null)
                for (var slot = 0; slot < definition.SquadCapacity; slot++)
                    members.Add((0, definition.FormationSize == 3 ? SquadCombatRules.Offset(slot) : Vector3.zero));
            foreach (var wall in towers)
            {
                if (!wall.Definition.IsWall || !wall.IsAlive) continue;
                var distance = 0f;
                for (var i = 1; i < Waypoints.Count; i++)
                {
                    var from = Waypoints[i - 1];
                    var to = Waypoints[i];
                    var length = Vector3.Distance(from, to);
                    if (Mathf.Abs(from.y - wall.Position.y) < 0.1f)
                    foreach (var member in members)
                    {
                        // Ignore completed segments; a wall behind the squad cannot stop it.
                        if (member.progress >= distance + length) continue;
                        var start = Mathf.Max(distance, member.progress);
                        var along = length > 0 ? (start - distance) / length : 0;
                        var offset = member.offset;
                        // Include the unit's half-width so its model never intersects the wall.
                        var size = wall.Definition.Footprint;
                        var origin = Vector3.Lerp(from, to, along) + offset;
                        var delta = to + offset - origin;
                        var enter = 0f;
                        var exit = 1f;
                        if (ClipWallAxis(origin.x - wall.Position.x, delta.x, size.x * .5f + .44f, ref enter, ref exit) &&
                            ClipWallAxis(origin.z - wall.Position.z, delta.z, size.y * .5f + .44f, ref enter, ref exit))
                            stop = Mathf.Min(stop, start + Mathf.Max(0, enter * (distance + length - start) - 0.001f));
                    }
                    distance += length;
                }
            }
            return stop;
        }

        private static bool ClipWallAxis(float origin, float delta, float halfSize, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < 0.000001f) return Mathf.Abs(origin) <= halfSize;
            var first = (-halfSize - origin) / delta;
            var last = (halfSize - origin) / delta;
            enter = Mathf.Max(enter, Mathf.Min(first, last));
            exit = Mathf.Min(exit, Mathf.Max(first, last));
            return enter <= exit;
        }

        private void Move(UnitState unit, float deltaTime, float wallStop)
        {
            var remaining = Mathf.Min(unit.Speed * deltaTime, Mathf.Max(0, wallStop - unit.RouteProgress));
            while (remaining > 0 && unit.NextWaypoint < Waypoints.Count)
            {
                var target = Waypoints[unit.NextWaypoint] + unit.FormationOffset;
                var distance = Vector3.Distance(unit.Position, target);
                var travel = Math.Min(remaining, distance);
                unit.Position = Vector3.MoveTowards(unit.Position, target, travel);
                unit.RouteProgress += travel; remaining -= travel;
                if (travel >= distance) unit.NextWaypoint++; else break;
            }
            UnitMoved?.Invoke(new UnitMovedEvent(unit));
            if (unit.NextWaypoint < Waypoints.Count || ManualMovement) return;
            unit.LifeState = UnitLifeState.ReachedGoal; ReachedGoalCount++;
            if (Objective == BattleObjective.ReachGoal) Outcome = BattleOutcome.Victory;
            UnitReachedGoal?.Invoke(new UnitEndedEvent(unit));
            if (Objective == BattleObjective.ReachGoal) BattleEnded?.Invoke(new BattleEndedEvent(Outcome, KilledCount, ReachedGoalCount));
        }

        private void Attack(TowerState tower, float deltaTime)
        {
            if (!tower.IsAlive || (tower.Definition.IsWall && !tower.Definition.IsMinion)) { tower.TargetId = null; return; }
            if (tower.FrozenRemaining > 0) { tower.TargetId = null; return; }
            tower.CooldownRemaining = Math.Max(0, tower.CooldownRemaining - deltaTime);
            tower.TargetId = null;
            if (tower.CooldownRemaining > 0.000001f) return;

            // AOE Tower Mode: hits ALL living units inside its coverage area simultaneously
            if (tower.Definition.IsAoe)
            {
                var hitAny = false;
                foreach (var unit in units)
                {
                    if (!unit.IsAlive) continue;
                    var offset = unit.Position - tower.Position;
                    if (!SquadCombatRules.Contains(offset, tower.Definition.Area, tower.Definition.Range, tower.Definition.AttackWidth)) continue;
                    hitAny = true;
                    tower.TargetId = unit.Id;
                    TowerAttacked?.Invoke(new TowerAttackEvent(tower, unit));
                    if (Objective == BattleObjective.DefendGoal)
                    {
                        DamageUnit(tower.Id, unit, tower.Definition.Damage);
                    }
                    else
                    {
                        projectiles.Add(new ProjectileState
                        {
                            Id = ++projectileSequence,
                            SourceId = tower.Id,
                            TargetKind = ProjectileTargetKind.FormationSlot,
                            TargetId = unit.SlotId,
                            Position = tower.Position,
                            Origin = tower.Position,
                            Damage = tower.Definition.Damage,
                            Straight = false,
                            Range = tower.Definition.Range,
                            Width = tower.Definition.AttackWidth
                        });
                    }
                }
                if (hitAny) tower.CooldownRemaining = tower.Definition.AttackInterval;
                return;
            }

            // Standard Single-Target Mode
            UnitState target = null;
            var bestDistance = float.MaxValue;
            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                var offset = unit.Position - tower.Position;
                var distance = offset.sqrMagnitude;
                if (!SquadCombatRules.Contains(offset, tower.Definition.Area, tower.Definition.Range, tower.Definition.AttackWidth)) continue;
                var better = target == null || (tower.Definition.TargetRule == TargetRule.Nearest ? distance < bestDistance :
                    unit.RouteProgress > target.RouteProgress ||
                    (Objective == BattleObjective.ReachGoal && Mathf.Abs(unit.RouteProgress - target.RouteProgress) <= 0.001f && distance < bestDistance));
                if (!better) continue;
                target = unit; bestDistance = distance;
            }
            if (target == null) return;
            tower.TargetId = target.Id; tower.CooldownRemaining = tower.Definition.AttackInterval;
            TowerAttacked?.Invoke(new TowerAttackEvent(tower, target));
            if (Objective == BattleObjective.DefendGoal) DamageUnit(tower.Id, target, tower.Definition.Damage);
            else projectiles.Add(new ProjectileState
            {
                Id = ++projectileSequence,
                SourceId = tower.Id,
                TargetKind = ProjectileTargetKind.FormationSlot,
                TargetId = target.SlotId,
                Position = tower.Position,
                Origin = tower.Position,
                Damage = tower.Definition.Damage,
                Straight = SquadCombatRules.IsLaser(tower.Definition.Area),
                Direction = SquadCombatRules.Direction(tower.Definition.Area),
                Range = tower.Definition.Range,
                Width = tower.Definition.AttackWidth
            });
        }

        private static Vector3 AimPosition(TowerState target, Vector3 from)
        {
            if (!target.Definition.IsWall) return target.Position;
            var size = target.Definition.Footprint;
            return new Vector3(Mathf.Clamp(from.x, target.Position.x - size.x * .5f, target.Position.x + size.x * .5f),
                target.Position.y, Mathf.Clamp(from.z, target.Position.z - size.y * .5f, target.Position.z + size.y * .5f));
        }

        private void AttackTower(UnitState unit, float deltaTime)
        {
            if (!unit.IsAlive || !SquadCombatRules.CanAttackTowers(unit.Role)) return;
            unit.AttackCooldown = Math.Max(0, unit.AttackCooldown - deltaTime);
            if (unit.AttackCooldown > 0.000001f) return;
            TowerState target = null;
            var nearest = unit.Definition.Range * unit.Definition.Range;
            foreach (var tower in towers)
            {
                if (!tower.IsAlive) continue;
                if (tower.Definition.IsWoodenGate) continue;
                if (tower.Definition.RequiresExplosiveDamage && unit.Definition.DamageType != AttackDamageType.Explosive) continue;
                var distance = (AimPosition(tower, unit.Position) - unit.Position).sqrMagnitude;
                if (distance > nearest) continue;
                target = tower; nearest = distance;
            }
            if (target == null) return;
            unit.AttackCooldown = unit.Definition.AttackInterval;
            var effectiveTowerDamage = unit.Definition.Damage * (target.Definition.IsWall ? 1 : unit.Definition.TowerDamageMultiplier) *
                (unit.DamageBuffRemaining > 0 ? unit.DamageBuffMultiplier : 1f);
            projectiles.Add(new ProjectileState
            {
                Id = ++projectileSequence,
                SourceId = unit.Id,
                TargetKind = ProjectileTargetKind.Tower,
                TargetId = target.Id,
                Position = unit.Position,
                Damage = effectiveTowerDamage,
                DamageType = unit.Definition.DamageType
            });
        }

        private void TickProjectiles(float deltaTime)
        {
            foreach (var shot in projectiles)
            {
                if (!shot.IsActive) continue;
                UnitState member = null; TowerState tower = null;
                Vector3 aim;
                if (shot.TargetKind == ProjectileTargetKind.Tower)
                {
                    tower = towers.Find(value => value.Id == shot.TargetId && value.IsAlive);
                    if (tower == null) { shot.IsActive = false; continue; }
                    aim = AimPosition(tower, shot.Position);
                }
                else
                {
                    member = GetMemberAtSlot(shot.TargetId);
                    aim = member != null ? member.Position : SampleAnchor(((float)time + deltaTime) * definition.Unit.Speed) + SquadCombatRules.Offset(shot.TargetId);
                }
                bool hit;
                if (shot.Straight)
                {
                    var previous = shot.Travel;
                    shot.Travel = Mathf.Min(shot.Range, previous + SquadCombatRules.ProjectileSpeed * deltaTime);
                    shot.Position = shot.Origin + shot.Direction * shot.Travel;
                    var forward = Vector3.Dot(aim - shot.Origin, shot.Direction);
                    hit = member != null && SquadCombatRules.Contains(aim - shot.Origin,
                        DirectionArea(shot.Direction), shot.Range, shot.Width) && forward >= previous - 0.05f && forward <= shot.Travel + 0.05f;
                }
                else
                {
                    shot.Position = Vector3.MoveTowards(shot.Position, aim, SquadCombatRules.ProjectileSpeed * deltaTime);
                    hit = (shot.Position - aim).sqrMagnitude <= 0.0025f;
                }
                if (hit)
                {
                    shot.IsActive = false;
                    if (tower != null) DamageTower(shot.SourceId, tower, shot.Damage, shot.DamageType);
                    else if (member != null) DamageUnit(shot.SourceId, member, shot.Damage);
                }
                shot.Age += deltaTime;
                if (shot.Age >= 5 || (shot.Straight && shot.Travel >= shot.Range)) shot.IsActive = false;
            }
        }

        private void DamageUnit(int sourceId, UnitState unit, float rawDamage)
        {
            if (!unit.IsAlive) return;
            if (unit.BarrierRemaining > 0 && unit.BarrierHits > 0)
            {
                unit.BarrierHits--;
                if (unit.BarrierHits == 0) unit.BarrierRemaining = 0;
                TowerHitBlocked?.Invoke(sourceId, unit.Id);
                return;
            }
            var applied = Math.Min(unit.Hp, rawDamage * (1 - unit.Definition.Defense));
            unit.Hp = Math.Max(0, unit.Hp - applied);
            if (unit.Hp == 0) { unit.LifeState = UnitLifeState.Dead; KilledCount++; }
            UnitDamaged?.Invoke(new DamageEvent(sourceId, unit, applied));
            if (!unit.IsAlive) UnitDied?.Invoke(new UnitEndedEvent(unit));
        }

        // Vehicles use the same damage/death events as soldiers, restricted to contact with wooden gates.
        public bool TryRamWoodenGate(int sourceId, Vector3 contactProbe, float radius, float damage)
        {
            if (stepping || IsPaused || Outcome != BattleOutcome.Running || !BattleNumbers.Finite(contactProbe) ||
                !BattleNumbers.Finite(radius) || radius <= 0 || radius >= .5f || !BattleNumbers.Finite(damage) || damage <= 0) return false;
            foreach (var tower in towers)
            {
                if (!tower.IsAlive || !tower.Definition.IsWoodenGate || Mathf.Abs(contactProbe.y - tower.Position.y) > .01f) continue;
                var size = tower.Definition.Footprint;
                var dx = Mathf.Max(Mathf.Abs(contactProbe.x - tower.Position.x) - size.x * .5f, 0);
                var dz = Mathf.Max(Mathf.Abs(contactProbe.z - tower.Position.z) - size.y * .5f, 0);
                if (dx * dx + dz * dz > radius * radius) continue;
                DamageTower(sourceId, tower, damage, AttackDamageType.Normal, siegeRamContact: true);
                return true;
            }
            return false;
        }

        private void DamageTower(int sourceId, TowerState tower, float rawDamage, AttackDamageType damageType, bool siegeRamContact = false)
        {
            if (!tower.IsAlive) return;
            if (tower.Definition.IsWoodenGate && !siegeRamContact) return;
            if (tower.Definition.RequiresExplosiveDamage && damageType != AttackDamageType.Explosive) return;
            var applied = Math.Min(tower.Hp, rawDamage);
            tower.Hp = Math.Max(0, tower.Hp - applied);
            // Active suppression: hitting a tower slightly delays its next shot
            if (tower.IsAlive)
                tower.CooldownRemaining = Mathf.Min(tower.Definition.AttackInterval, tower.CooldownRemaining + 0.08f);

            TowerDamaged?.Invoke(new TowerDamageEvent(sourceId, tower, applied));
            if (!tower.IsAlive)
            {
                tower.TargetId = null;
                if (!tower.Definition.IsWall)
                {
                    TowersDestroyedCount++;
                    TowerBountyGoldEarned += tower.Definition.GoldBounty;
                    TowerDestroyed?.Invoke(new TowerDestroyedEvent(sourceId, tower));
                }
                TowerDied?.Invoke(tower.Id);
            }
        }

        private void Heal(float deltaTime)
        {
            healTimer += deltaTime;
            if (healTimer + 0.000001f < SquadCombatRules.HealInterval) return;
            healTimer -= SquadCombatRules.HealInterval;
            foreach (var healer in units)
            {
                if (!healer.IsAlive || healer.Role != UnitRole.Heal) continue;
                HealerPulsed?.Invoke(healer.Id);
                foreach (var member in units)
                {
                    if (!member.IsAlive || member.SquadId != healer.SquadId || !SquadCombatRules.AreNeighbors(healer.SlotId, member.SlotId)) continue;
                    var amount = Math.Min(member.MaxHp - member.Hp, healer.Definition.HealPerSecond * SquadCombatRules.HealInterval);
                    if (amount <= 0) continue;
                    member.Hp += amount; UnitHealed?.Invoke(new HealEvent(healer.Id, member, amount));
                }
            }
        }

        private void End(BattleOutcome outcome)
        {
            if (Outcome != BattleOutcome.Running) return;
            Outcome = outcome; BattleEnded?.Invoke(new BattleEndedEvent(outcome, KilledCount, ReachedGoalCount));
        }
        private float RouteLength()
        {
            var length = 0f;
            for (var i = 1; i < Waypoints.Count; i++) length += Vector3.Distance(Waypoints[i - 1], Waypoints[i]);
            return length;
        }
        private int NextWaypoint(float distance)
        {
            for (var i = 1; i < Waypoints.Count; i++)
            {
                var segment = Vector3.Distance(Waypoints[i - 1], Waypoints[i]);
                if (distance < segment) return i;
                distance -= segment;
            }
            return Waypoints.Count;
        }
        private Vector3 SampleAnchor(float distance)
        {
            for (var i = 1; i < Waypoints.Count; i++)
            {
                var segment = Vector3.Distance(Waypoints[i - 1], Waypoints[i]);
                if (distance <= segment) return Vector3.Lerp(Waypoints[i - 1], Waypoints[i], distance / segment);
                distance -= segment;
            }
            return Waypoints[Waypoints.Count - 1];
        }
        private bool CanStand(Vector3 position)
        {
            foreach (var cell in FormationClearance.SweptCells(position, position))
                if ((walkable != null && !walkable.Contains(cell)) || HasTowerFootprint(cell, false)) return false;
            return !TouchesLiveWall(position);
        }
        private bool TouchesLiveWall(Vector3 position)
        {
            var layer = SmallCellOccupancy.PositionToCell(position).y;
            foreach (var tower in towers)
            {
                if (!tower.Definition.IsWall || !tower.IsAlive || SmallCellOccupancy.PositionToCell(tower.Position).y != layer) continue;
                var size = tower.Definition.Footprint;
                if (Mathf.Abs(position.x - tower.Position.x) < size.x * .5f + .44f &&
                    Mathf.Abs(position.z - tower.Position.z) < size.y * .5f + .44f) return true;
            }
            return false;
        }
        private bool HasTowerFootprint(Vector3Int cell, bool includeWalls = true)
        {
            foreach (var tower in towers)
            {
                var center = SmallCellOccupancy.PositionToCell(tower.Position);
                if (tower.Definition.IsWall && (!includeWalls || !tower.IsAlive)) continue;
                var footprint = tower.Definition.Footprint;
                if (center.y == cell.y && Math.Abs(center.x - cell.x) <= footprint.x / 2 && Math.Abs(center.z - cell.z) <= footprint.y / 2) return true;
            }
            return false;
        }
        public SmallCellOccupancy GetCellOccupancy(Vector3Int cell)
        {
            var towerIds = new List<int>(); var unitIds = new List<int>();
            foreach (var tower in towers)
            {
                var center = SmallCellOccupancy.PositionToCell(tower.Position);
                if (tower.Definition.IsWall && !tower.IsAlive) continue;
                var footprint = tower.Definition.Footprint;
                if (center.y == cell.y && Math.Abs(center.x - cell.x) <= footprint.x / 2 && Math.Abs(center.z - cell.z) <= footprint.y / 2) towerIds.Add(tower.Id);
            }
            foreach (var unit in units) if (unit.IsAlive && unit.OccupiedCell == cell) unitIds.Add(unit.Id);
            return new SmallCellOccupancy(towerIds, unitIds);
        }
        private static AttackArea DirectionArea(Vector3 direction) => direction.x > 0 ? AttackArea.PositiveX : direction.x < 0 ? AttackArea.NegativeX :
            direction.z > 0 ? AttackArea.PositiveZ : AttackArea.NegativeZ;
    }
}
