using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using Team5.Rebuild.Session;
using Team5.Rebuild.Editor.LevelAuthoring;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class WallTests
    {
        [TestCase(false, 0, 3, 1)]
        [TestCase(false, -90, 1, 3)]
        [TestCase(false, 180, 3, 1)]
        [TestCase(false, 270, 1, 3)]
        [TestCase(true, 90, 3, 1)]
        [TestCase(true, 180, 1, 3)]
        public void WallFootprintFollowsCardinalRotation(bool alongZ, float yaw, int width, int depth)
        {
            Assert.That(BattleTower.WallFootprintForYaw(alongZ, yaw), Is.EqualTo(new Vector2Int(width, depth)));
        }

        private static BattleSimulation Create(UnitRole role, bool alongZ = true)
        {
            var wall = new TowerDefinition(1, new Vector3(5, .25f, 0), 0, 0, 1, TargetRule.Nearest,
                maxHp: 4, goldBounty: 100, isWall: true, wallAlongZ: alongZ);
            var cells = from x in Enumerable.Range(-1, 15) from z in Enumerable.Range(-1, 3)
                        select new Vector3Int(x, 0, z);
            return new BattleSimulation(new BattleDefinition(new[] { new Vector3(0, .25f, 0), new Vector3(12, .25f, 0) },
                new[] { wall }, new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(role)) }, cells));
        }

        [TestCase(true, 4.06f)]
        [TestCase(false, 3.06f)]
        public void WallStopsFormationAndNeverAttacks(bool alongZ, float stop)
        {
            var simulation = Create(UnitRole.Tank, alongZ);
            var attacks = 0;
            simulation.TowerAttacked += _ => attacks++;
            for (var i = 0; i < 1500; i++) simulation.Step(.02f);
            Assert.That(simulation.Units[0].RouteProgress, Is.EqualTo(stop).Within(.005f));
            Assert.That(simulation.Units[0].Hp, Is.EqualTo(simulation.Units[0].MaxHp));
            Assert.That(simulation.Towers[0].Hp, Is.EqualTo(4));
            Assert.That(attacks, Is.Zero);
            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Running));
            Assert.That(simulation.GetCellOccupancy(new Vector3Int(5, 0, 0)).TowerIds, Does.Contain(1));
        }

        [Test]
        public void DestroyingWallClearsOccupancyAndLetsSquadWinWithoutBounty()
        {
            var simulation = Create(UnitRole.Attack);
            for (var i = 0; i < 2000 && simulation.Outcome == BattleOutcome.Running; i++) simulation.Step(.02f);
            Assert.That(simulation.Towers[0].Hp, Is.Zero);
            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(simulation.TowerBountyGoldEarned, Is.Zero);
            Assert.That(simulation.TowersDestroyedCount, Is.Zero);
            Assert.That(simulation.GetCellOccupancy(new Vector3Int(5, 0, 0)).TowerIds, Is.Empty);
        }

        [Test]
        public void UnitsNeverPassALivingWallAndResumeAfterItsDestruction()
        {
            var simulation = Create(UnitRole.Attack);
            var destroyed = false;
            var stoppedAt = 0f;
            for (var i = 0; i < 2000 && simulation.Outcome == BattleOutcome.Running; i++)
            {
                var wallWasAlive = simulation.Towers[0].IsAlive;
                simulation.Step(.02f);
                if (wallWasAlive)
                {
                    Assert.That(simulation.Units[0].RouteProgress, Is.LessThanOrEqualTo(4.06f));
                    if (!simulation.Towers[0].IsAlive)
                    { destroyed = true; stoppedAt = simulation.Units[0].RouteProgress; }
                }
            }
            Assert.That(destroyed, Is.True);
            Assert.That(simulation.Units[0].RouteProgress, Is.GreaterThan(stoppedAt));
            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void DelayedReinforcementsCannotSpawnPastALivingWall()
        {
            var wall = new TowerDefinition(1, new Vector3(5, .25f, 0), 0, 0, 1, TargetRule.Nearest,
                isWall: true, wallAlongZ: true);
            var tank = SquadCombatRules.Create(UnitRole.Tank);
            var simulation = new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(12, .25f, 0) }, new[] { wall },
                new[] { new MemberSpawnDefinition(1, 4, tank), new MemberSpawnDefinition(2, 8, tank, 15) }));
            for (var i = 0; i < 1200; i++) simulation.Step(.02f);
            Assert.That(simulation.SpawnedCount, Is.EqualTo(2));
            Assert.That(simulation.Towers[0].IsAlive, Is.True);
            foreach (var unit in simulation.Units)
            {
                Assert.That(unit.RouteProgress, Is.LessThanOrEqualTo(3.06f));
                Assert.That(unit.Position.x + .44f, Is.LessThan(4.5f));
            }
        }

        [Test]
        public void SceneWallsAreIncludedWithoutManualTowerRegistration()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Wall_3x1_Z.prefab");
            var wall = Object.Instantiate(prefab);
            var host = new GameObject("Wall registration test");
            try
            {
                var session = host.AddComponent<BattleSession>();
                Assert.That(session.Towers, Does.Contain(wall.GetComponent<BattleTower>()));
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(wall); }
        }

        [Test]
        public void WallNextToAnEmptySlotDoesNotStopTheActualUnit()
        {
            var wall = new TowerDefinition(1, new Vector3(5, .25f, 1), 0, 0, 1, TargetRule.Nearest, isWall: true);
            var simulation = new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(12, .25f, 0) }, new[] { wall },
                new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Tank)) }));
            for (var i = 0; i < 1500 && simulation.Outcome == BattleOutcome.Running; i++) simulation.Step(.02f);
            Assert.That(simulation.Towers[0].IsAlive, Is.True);
            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void MovingToAClearLaneReleasesTheWallStop()
        {
            var wall = new TowerDefinition(1, new Vector3(5, .25f, -1), 0, 0, 1, TargetRule.Nearest, isWall: true);
            var simulation = new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(12, .25f, 0) }, new[] { wall },
                new[] { new MemberSpawnDefinition(1, 1, SquadCombatRules.Create(UnitRole.Tank)) }));
            for (var i = 0; i < 700; i++) simulation.Step(.02f);
            Assert.That(simulation.Units[0].RouteProgress, Is.EqualTo(3.059f).Within(.005f));
            simulation.SetPaused(true);
            var slots = Enumerable.Range(0, 9).ToArray();
            slots[1] = 7; slots[7] = 1;
            Assert.That(simulation.TryApplyFormation(slots), Is.True);
            simulation.SetPaused(false);
            for (var i = 0; i < 1500 && simulation.Outcome == BattleOutcome.Running; i++) simulation.Step(.02f);
            Assert.That(simulation.Towers[0].IsAlive, Is.True);
            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void OrdinaryTowerAndItsAttackRangeDoNotStopMarching()
        {
            var tower = new TowerDefinition(1, new Vector3(5, .25f, 3), 100, .001f, 1, TargetRule.Nearest);
            var simulation = new BattleSimulation(new BattleDefinition(
                new[] { new Vector3(0, .25f, 0), new Vector3(12, .25f, 0) }, new[] { tower },
                new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Tank)) }));
            var attacks = 0;
            simulation.TowerAttacked += _ => attacks++;
            for (var i = 0; i < 1500 && simulation.Outcome == BattleOutcome.Running; i++) simulation.Step(.02f);
            Assert.That(attacks, Is.GreaterThan(0));
            Assert.That(simulation.Towers[0].IsAlive, Is.True);
            Assert.That(simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void CheckpointRestoresWallHealthAndBlockedCells()
        {
            var simulation = Create(UnitRole.Attack);
            simulation.Step(.02f);
            var checkpoint = simulation.CaptureCheckpoint();
            for (var i = 0; i < 2000 && simulation.Towers[0].IsAlive; i++) simulation.Step(.02f);
            Assert.That(simulation.Towers[0].IsAlive, Is.False);
            var restored = checkpoint.Restore();
            Assert.That(restored.Towers[0].Hp, Is.EqualTo(4));
            Assert.That(restored.GetCellOccupancy(new Vector3Int(5, 0, 0)).TowerIds, Does.Contain(1));
        }

        [TestCase("X", 3, 1)]
        [TestCase("Z", 1, 3)]
        public void WallPrefabHasStaticModelAndFitsThreeRoadCells(string axis, int width, int depth)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapBuilderUtility.Kit + "Towers/Wall_3x1_" + axis + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            var wall = prefab.GetComponent<BattleTower>();
            Assert.That(wall.IsWall, Is.True);
            Assert.That(wall.Footprint, Is.EqualTo(new Vector2Int(width, depth)));
            Assert.That(wall.CaptureDefinition(1).Damage, Is.Zero);
            Assert.That(wall.GoldBounty, Is.Zero);
            Assert.That(prefab.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(3));
            var draft = ScriptableObject.CreateInstance<MapDraft>();
            try
            {
                draft.ResetGrid(9, 9);
                for (var i = 0; i < draft.tiles.Length; i++) draft.tiles[i] = (int)CellKind.Road;
                draft.PlaceTower(new Vector2Int(4, 4), prefab, AttackArea.Square);
                Assert.That(draft.towers.Count, Is.EqualTo(1));
                Assert.Throws<System.ArgumentException>(() => draft.PlaceTower(new Vector2Int(4, 4), prefab, AttackArea.Square));
            }
            finally { Object.DestroyImmediate(draft); }
        }
    }
}
