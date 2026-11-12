using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public sealed class BattleSimulation
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
        public ReadOnlyCollection<UnitState> Units { get; }
        public ReadOnlyCollection<TowerState> Towers { get; }
        public ReadOnlyCollection<ProjectileState> Projectiles { get; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Running;
        public BattleObjective Objective => definition.Objective;
        public bool IsPaused { get; private set; }
        public int SpawnedCount { get; private set; }
        public int PendingSpawnCount => definition.UnitCount - SpawnedCount;
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
            copy.time = time; copy.healTimer = healTimer; copy.projectileSequence = projectileSequence;
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

        public bool TryApplyFormation(IReadOnlyList<int> sourceSlots)
        {
            if (stepping || !IsPaused || Outcome != BattleOutcome.Running || definition.FormationSize != 3 ||
                sourceSlots == null || sourceSlots.Count != 9 || PendingSpawnCount != 0) return false;
            var seen = new HashSet<int>();
            var destinations = new int[9];
            for (var destination = 0; destination < 9; destination++)
            {
                var source = sourceSlots[destination];
                if (source < 0 || source > 8 || !seen.Add(source)) return false;
                destinations[source] = destination;
            }
            foreach (var unit in units)
                if (unit.IsAlive && !CanStand(unit.AnchorPosition + SquadCombatRules.Offset(destinations[unit.SlotId]))) return false;
            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                var anchor = unit.AnchorPosition;
                var destination = destinations[unit.SlotId];
                unit.FormationSlot = SquadCombatRules.Slot(destination);
                unit.FormationOffset = SquadCombatRules.Offset(destination);
                unit.Position = anchor + unit.FormationOffset;
            }
            FormationChanged?.Invoke();
            return true;
        }

        public void Step(float deltaTime)
        {
            if (!BattleNumbers.Finite(deltaTime) || deltaTime <= 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (stepping) throw new InvalidOperationException("Simulation.Step cannot be called from an event listener.");
            if (Outcome != BattleOutcome.Running || IsPaused) return;
            stepping = true;
            try
            {
                Spawn();
                var wallStop = WallStopProgress();
                foreach (var unit in units)
                {
                    if (!unit.IsAlive) continue;
                    Move(unit, deltaTime, wallStop);
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
                    UnitSpawned?.Invoke(new UnitSpawnedEvent(unit));
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
                    var unit = new UnitState(++SpawnedCount, definition.Unit, definition.Waypoints[0] + offset, squadId, coordinate, offset);
                    units.Add(unit); UnitSpawned?.Invoke(new UnitSpawnedEvent(unit));
                }
            }
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
                for (var i = 1; i < definition.Waypoints.Count; i++)
                {
                    var from = definition.Waypoints[i - 1];
                    var to = definition.Waypoints[i];
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
            while (remaining > 0 && unit.NextWaypoint < definition.Waypoints.Count)
            {
                var target = definition.Waypoints[unit.NextWaypoint] + unit.FormationOffset;
                var distance = Vector3.Distance(unit.Position, target);
                var travel = Math.Min(remaining, distance);
                unit.Position = Vector3.MoveTowards(unit.Position, target, travel);
                unit.RouteProgress += travel; remaining -= travel;
                if (travel >= distance) unit.NextWaypoint++; else break;
            }
            UnitMoved?.Invoke(new UnitMovedEvent(unit));
            if (unit.NextWaypoint < definition.Waypoints.Count) return;
            unit.LifeState = UnitLifeState.ReachedGoal; ReachedGoalCount++;
            if (Objective == BattleObjective.ReachGoal) Outcome = BattleOutcome.Victory;
            UnitReachedGoal?.Invoke(new UnitEndedEvent(unit));
            if (Objective == BattleObjective.ReachGoal) BattleEnded?.Invoke(new BattleEndedEvent(Outcome, KilledCount, ReachedGoalCount));
        }

        private void Attack(TowerState tower, float deltaTime)
        {
            if (!tower.IsAlive || tower.Definition.IsWall) { tower.TargetId = null; return; }
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
                Straight = tower.Definition.Area != AttackArea.Square,
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
                var distance = (AimPosition(tower, unit.Position) - unit.Position).sqrMagnitude;
                if (distance > nearest) continue;
                target = tower; nearest = distance;
            }
            if (target == null) return;
            unit.AttackCooldown = unit.Definition.AttackInterval;
            var effectiveTowerDamage = unit.Definition.Damage * (target.Definition.IsWall ? 1 : unit.Definition.TowerDamageMultiplier);
            projectiles.Add(new ProjectileState
            {
                Id = ++projectileSequence,
                SourceId = unit.Id,
                TargetKind = ProjectileTargetKind.Tower,
                TargetId = target.Id,
                Position = unit.Position,
                Damage = effectiveTowerDamage
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
                    if (tower != null) DamageTower(shot.SourceId, tower, shot.Damage);
                    else if (member != null) DamageUnit(shot.SourceId, member, shot.Damage);
                }
                shot.Age += deltaTime;
                if (shot.Age >= 5 || (shot.Straight && shot.Travel >= shot.Range)) shot.IsActive = false;
            }
        }

        private void DamageUnit(int sourceId, UnitState unit, float rawDamage)
        {
            if (!unit.IsAlive) return;
            var applied = Math.Min(unit.Hp, rawDamage * (1 - unit.Definition.Defense));
            unit.Hp = Math.Max(0, unit.Hp - applied);
            if (unit.Hp == 0) { unit.LifeState = UnitLifeState.Dead; KilledCount++; }
            UnitDamaged?.Invoke(new DamageEvent(sourceId, unit, applied));
            if (!unit.IsAlive) UnitDied?.Invoke(new UnitEndedEvent(unit));
        }

        private void DamageTower(int sourceId, TowerState tower, float rawDamage)
        {
            if (!tower.IsAlive) return;
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
            for (var i = 1; i < definition.Waypoints.Count; i++) length += Vector3.Distance(definition.Waypoints[i - 1], definition.Waypoints[i]);
            return length;
        }
        private int NextWaypoint(float distance)
        {
            for (var i = 1; i < definition.Waypoints.Count; i++)
            {
                var segment = Vector3.Distance(definition.Waypoints[i - 1], definition.Waypoints[i]);
                if (distance < segment) return i;
                distance -= segment;
            }
            return definition.Waypoints.Count;
        }
        private Vector3 SampleAnchor(float distance)
        {
            for (var i = 1; i < definition.Waypoints.Count; i++)
            {
                var segment = Vector3.Distance(definition.Waypoints[i - 1], definition.Waypoints[i]);
                if (distance <= segment) return Vector3.Lerp(definition.Waypoints[i - 1], definition.Waypoints[i], distance / segment);
                distance -= segment;
            }
            return definition.Waypoints[definition.Waypoints.Count - 1];
        }
        private bool CanStand(Vector3 position)
        {
            foreach (var cell in FormationClearance.SweptCells(position, position))
                if ((walkable != null && !walkable.Contains(cell)) || HasTowerFootprint(cell)) return false;
            return true;
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