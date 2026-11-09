using System;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Analytics
{
    // One battle attempt. Observes simulation events only; never calls back into the simulation.
    public sealed class BattleAnalyticsTracker
    {
        private readonly BattleSimulation simulation;
        private readonly float routeLength;
        private readonly float startedAt;
        private readonly Dictionary<int, int> lastHitBy = new Dictionary<int, int>();
        private readonly Dictionary<int, float> towerDamage = new Dictionary<int, float>();
        private readonly Dictionary<int, int> towerKills = new Dictionary<int, int>();
        private readonly Dictionary<int, double> towerDestroyedAt = new Dictionary<int, double>();
        private readonly double[] speedSeconds = new double[4];
        private double sampledTime;
        private float damageTaken, damageToTowers, healingDone;
        private bool started;
        internal int formationEdits, pauses, speedChanges, inspections;

        public BattleSimulation Simulation => simulation;
        public int Attempt { get; }
        public bool Resumed { get; }
        public bool IsFinished { get; private set; }

        public BattleAnalyticsTracker(BattleSimulation simulation, float routeLength, int attempt, bool resumed)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.routeLength = routeLength;
            Attempt = attempt;
            Resumed = resumed;
            startedAt = Time.realtimeSinceStartup;
            sampledTime = simulation.ElapsedTime;
            simulation.UnitSpawned += OnSpawned;
            simulation.UnitDamaged += OnDamaged;
            simulation.UnitDied += OnDied;
            simulation.TowerDamaged += OnTowerDamaged;
            simulation.TowerDestroyed += OnTowerDestroyed;
            simulation.UnitHealed += OnHealed;
            TryStart(false);
        }

        public float CurrentProgressPct
        {
            get
            {
                var best = -1f;
                foreach (var unit in simulation.Units) if (unit.IsAlive) best = Mathf.Max(best, ProgressPct(unit));
                return best >= 0 ? best : FurthestProgressPct;
            }
        }

        public float FurthestProgressPct
        {
            get
            {
                var best = 0f;
                foreach (var unit in simulation.Units) best = Mathf.Max(best, ProgressPct(unit));
                return best;
            }
        }

        public int Survivors => simulation.Units.Count(unit => unit.LifeState != UnitLifeState.Dead);

        public float ProgressPct(UnitState unit)
        {
            if (unit.LifeState == UnitLifeState.ReachedGoal) return 100;
            return routeLength > 0 ? Mathf.Clamp(unit.RouteProgress / routeLength * 100, 0, 100) : 0;
        }

        // Attributes simulated time to the playback speed active since the last sample.
        public void Sample(int playbackSpeed)
        {
            var delta = simulation.ElapsedTime - sampledTime;
            if (delta > 0) speedSeconds[Mathf.Clamp(playbackSpeed, 1, 3)] += delta;
            sampledTime = simulation.ElapsedTime;
        }

        public void Finish(int goldReward, int livesLeft, bool gameOver)
        {
            if (IsFinished) return;
            TryStart(true);
            Unsubscribe();
            var units = simulation.Units;
            var maxHp = units.Sum(unit => unit.MaxHp);
            var outcome = simulation.Outcome == BattleOutcome.Running ? "running" : simulation.Outcome.ToString().ToLowerInvariant();
            GameAnalytics.Track("level_end",
                ("outcome", outcome),
                ("resumed", Resumed),
                ("sim_s", simulation.ElapsedTime),
                ("real_s", Time.realtimeSinceStartup - startedAt),
                ("spawned", simulation.SpawnedCount),
                ("killed", simulation.KilledCount),
                ("reached_goal", simulation.ReachedGoalCount),
                ("survivors", Survivors),
                ("towers_destroyed", simulation.TowersDestroyedCount),
                ("tower_count", simulation.Towers.Count),
                ("furthest_progress_pct", FurthestProgressPct),
                ("squad_hp_left_pct", maxHp > 0 ? units.Sum(unit => unit.Hp) / maxHp * 100 : 0),
                ("damage_taken", damageTaken),
                ("damage_to_towers", damageToTowers),
                ("healing_done", healingDone),
                ("formation_edits", formationEdits),
                ("pauses", pauses),
                ("speed_changes", speedChanges),
                ("sim_s_at_1x", speedSeconds[1]),
                ("sim_s_at_2x", speedSeconds[2]),
                ("sim_s_at_3x", speedSeconds[3]),
                ("inspections", inspections),
                ("gold_reward", goldReward),
                ("lives_left", livesLeft),
                ("game_over", gameOver));
            foreach (var tower in simulation.Towers)
            {
                towerDamage.TryGetValue(tower.Id, out var damage);
                towerKills.TryGetValue(tower.Id, out var kills);
                var destroyed = towerDestroyedAt.TryGetValue(tower.Id, out var destroyedAt);
                GameAnalytics.Track("tower_stats",
                    ("outcome", outcome),
                    ("tower_id", tower.Id),
                    ("is_aoe", tower.Definition.IsAoe),
                    ("max_hp", tower.Definition.MaxHp),
                    ("damage_dealt", damage),
                    ("kills", kills),
                    ("destroyed", !tower.IsAlive),
                    ("destroyed_at_s", destroyed ? destroyedAt : -1),
                    ("hp_left_pct", tower.Hp / tower.Definition.MaxHp * 100));
            }
            IsFinished = true;
        }

        public void Abandon()
        {
            if (IsFinished) return;
            Unsubscribe();
            GameAnalytics.Track("level_abandon",
                ("resumed", Resumed),
                ("sim_s", simulation.ElapsedTime),
                ("real_s", Time.realtimeSinceStartup - startedAt),
                ("progress_pct", CurrentProgressPct),
                ("survivors", Survivors));
            IsFinished = true;
        }

        // The squad is known once every planned member has spawned (the first tick for prepared squads).
        private void TryStart(bool force)
        {
            if (started || (!force && simulation.PendingSpawnCount > 0)) return;
            started = true;
            var units = simulation.Units;
            GameAnalytics.Track("level_start",
                ("resumed", Resumed),
                ("squad_size", units.Count),
                ("roles", Roles(units)),
                ("slots", string.Join("|", units.OrderBy(unit => unit.SlotId).Select(unit => $"{unit.SlotId}:{unit.Role}"))),
                ("captain_present", units.Any(unit => unit.Role == UnitRole.Captain)),
                ("squad_total_hp", units.Sum(unit => unit.MaxHp)),
                ("avg_unit_level", units.Count > 0 ? units.Average(unit => unit.Definition.Level) : 0),
                ("max_rarity", MaxRarity(units.Select(unit => unit.Definition))),
                ("tower_count", simulation.Towers.Count));
        }

        public static string Roles(IEnumerable<UnitState> units) => Roles(units.Select(unit => unit.Definition));

        public static string Roles(IEnumerable<UnitDefinition> units) =>
            string.Join("|", units.GroupBy(unit => unit.Role).OrderBy(group => group.Key).Select(group => $"{group.Key}:{group.Count()}"));

        public static string MaxRarity(IEnumerable<UnitDefinition> units)
        {
            var list = units.ToList();
            return list.Count > 0 ? list.Max(unit => unit.Rarity).ToString() : "";
        }

        private void Unsubscribe()
        {
            simulation.UnitSpawned -= OnSpawned;
            simulation.UnitDamaged -= OnDamaged;
            simulation.UnitDied -= OnDied;
            simulation.TowerDamaged -= OnTowerDamaged;
            simulation.TowerDestroyed -= OnTowerDestroyed;
            simulation.UnitHealed -= OnHealed;
        }

        // Handlers run inside BattleSimulation.Step, so each one swallows its own failures.
        private void OnSpawned(UnitSpawnedEvent value)
        {
            try { TryStart(false); }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private void OnDamaged(DamageEvent value)
        {
            try
            {
                damageTaken += value.Amount;
                lastHitBy[value.UnitId] = value.TowerId;
                towerDamage.TryGetValue(value.TowerId, out var total);
                towerDamage[value.TowerId] = total + value.Amount;
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private void OnDied(UnitEndedEvent value)
        {
            try
            {
                var killer = lastHitBy.TryGetValue(value.UnitId, out var towerId) ? towerId : 0;
                if (killer != 0) { towerKills.TryGetValue(killer, out var kills); towerKills[killer] = kills + 1; }
                UnitState unit = null;
                foreach (var candidate in simulation.Units) if (candidate.Id == value.UnitId) { unit = candidate; break; }
                if (unit == null) return;
                GameAnalytics.Track("unit_death",
                    ("role", unit.Role.ToString()),
                    ("rarity", unit.Definition.Rarity.ToString()),
                    ("unit_level", unit.Definition.Level),
                    ("slot", unit.SlotId),
                    ("sim_s", simulation.ElapsedTime),
                    ("progress_pct", ProgressPct(unit)),
                    ("killer_tower_id", killer),
                    ("x", value.Position.x),
                    ("z", value.Position.z));
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private void OnTowerDamaged(TowerDamageEvent value)
        {
            try { damageToTowers += value.Amount; }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private void OnTowerDestroyed(TowerDestroyedEvent value)
        {
            try { towerDestroyedAt[value.TowerId] = simulation.ElapsedTime; }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        private void OnHealed(HealEvent value)
        {
            try { healingDone += value.Amount; }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }
    }
}
