using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class BattleSimulationTests
    {
        private static readonly Vector3[] LongRoute = { Vector3.zero, new Vector3(20, 0, 0) };
        private static BattleSimulation Create(IEnumerable<TowerDefinition> towers = null, int count = 1,
            float hp = 100, float speed = 1, float interval = 1, int lives = 10, IEnumerable<Vector3> route = null)
            => new BattleSimulation(new BattleDefinition(route ?? LongRoute, towers ?? Array.Empty<TowerDefinition>(),
                new UnitDefinition(hp, speed), count, interval, lives));
        private static TowerDefinition Tower(int id = 1, float damage = 10, float interval = 0.5f,
            TargetRule rule = TargetRule.FurthestAlongRoute, Vector3? position = null, float range = 30)
            => new TowerDefinition(id, position ?? Vector3.zero, range, damage, interval, rule);
        private static void Finish(BattleSimulation simulation, int ticks = 20000)
        {
            for (var i = 0; i < ticks && simulation.Outcome == BattleOutcome.Running; i++) simulation.Step(0.02f);
            Assert.That(simulation.Outcome, Is.Not.EqualTo(BattleOutcome.Running), "Battle must terminate.");
        }

        [Test]
        public void MovementIsContinuousAndConsumesDistanceAcrossCorners()
        {
            var sim = Create(speed: 2, route: new[] { Vector3.zero, Vector3.right, new Vector3(1, 0, 1), new Vector3(2, 0, 1) });
            sim.Step(0.25f);
            Assert.That(sim.Units[0].Position, Is.EqualTo(new Vector3(0.5f, 0, 0)));
            sim.Step(0.5f);
            Assert.That(sim.Units[0].Position, Is.EqualTo(new Vector3(1, 0, 0.5f)));
            Assert.That(sim.Units[0].RouteProgress, Is.EqualTo(1.5f).Within(0.001));
        }

        [Test]
        public void DamageCooldownDeathAndVictoryEmitExactlyOnceInOrder()
        {
            var sim = Create(new[] { Tower(damage: 30) }, hp: 50, speed: 0.01f);
            var events = new List<string>();
            var damage = new List<DamageEvent>();
            sim.UnitSpawned += _ => events.Add("spawn");
            sim.TowerAttacked += _ => events.Add("attack");
            sim.UnitDamaged += value => { events.Add("damage"); damage.Add(value); };
            sim.UnitDied += _ => events.Add("death");
            sim.BattleEnded += _ => events.Add("end");
            sim.Step(0.1f);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(20));
            sim.Step(0.2f);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(20));
            sim.Step(0.3f);
            sim.Step(1);
            Assert.That(sim.Units[0].Hp, Is.Zero);
            Assert.That(sim.Units[0].LifeState, Is.EqualTo(UnitLifeState.Dead));
            Assert.That(sim.KilledCount, Is.EqualTo(1));
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(damage.Select(value => value.Amount), Is.EqualTo(new[] { 30f, 20f }));
            Assert.That(damage[0].RemainingHp, Is.EqualTo(20));
            Assert.That(events, Is.EqualTo(new[] { "spawn", "attack", "damage", "attack", "damage", "death", "end" }));
        }

        [Test]
        public void MultipleTowersDoNotAttackAlreadyDeadUnits()
        {
            var sim = Create(new[] { Tower(1, 100), Tower(2, 100) }, hp: 10);
            var attacks = 0;
            sim.TowerAttacked += _ => attacks++;
            sim.Step(0.02f);
            Assert.That(attacks, Is.EqualTo(1));
            Assert.That(sim.KilledCount, Is.EqualTo(1));
        }

        [TestCase(TargetRule.FurthestAlongRoute, 1)]
        [TestCase(TargetRule.Nearest, 2)]
        public void TargetSelectionUsesRequestedRule(TargetRule rule, int expectedId)
        {
            var sim = Create(new[] { Tower(rule: rule, position: new Vector3(100, 0, 0)) }, count: 2);
            sim.Step(1);
            sim.SetTowerPosition(1, Vector3.zero);
            sim.Step(0.1f);
            Assert.That(sim.Towers[0].TargetId, Is.EqualTo(expectedId));
        }

        [Test]
        public void SquareRangeUsesWorldXZCoordinatesAndLiveTowerPosition()
        {
            var sim = Create(new[] { Tower(range: 1, position: new Vector3(2, 0, 0)) });
            sim.Step(0.02f);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(100));
            sim.SetTowerPosition(1, sim.Units[0].Position);
            sim.Step(0.02f);
            Assert.That(sim.Units[0].Hp, Is.EqualTo(90));
        }

        [Test]
        public void GoalEventAndDefeatOccurOnceAndStopFutureSpawns()
        {
            var sim = Create(count: 100, speed: 100, lives: 1);
            var goals = 0;
            var ends = 0;
            sim.UnitReachedGoal += _ => goals++;
            sim.BattleEnded += _ => ends++;
            sim.Step(1);
            sim.Step(1);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(sim.Units[0].LifeState, Is.EqualTo(UnitLifeState.ReachedGoal));
            Assert.That(sim.Units[0].Hp, Is.EqualTo(100));
            Assert.That(goals, Is.EqualTo(1));
            Assert.That(ends, Is.EqualTo(1));
            Assert.That(sim.SpawnedCount, Is.EqualTo(1));
            Assert.That(sim.LivesRemaining, Is.Zero);
        }

        [Test]
        public void LeaksBelowLifeLimitStillAllowVictoryAfterWaveCompletes()
        {
            var sim = Create(count: 3, speed: 20, lives: 4);
            Finish(sim);
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(sim.ReachedGoalCount, Is.EqualTo(3));
            Assert.That(sim.AliveCount, Is.Zero);
            Assert.That(sim.LivesRemaining, Is.EqualTo(1));
        }

        [Test]
        public void FiveHundredTwelveConcurrentUnitsCompleteWithoutVisualObjects()
        {
            var sim = Create(count: 512, speed: 1, interval: 0.02f, lives: 513);
            var maximumAlive = 0;
            for (var i = 0; i < 3000 && sim.Outcome == BattleOutcome.Running; i++)
            {
                sim.Step(0.02f);
                maximumAlive = Math.Max(maximumAlive, sim.AliveCount);
            }
            Assert.That(maximumAlive, Is.EqualTo(512));
            Assert.That(sim.ReachedGoalCount, Is.EqualTo(512));
            Assert.That(sim.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void DefinitionCopiesInputsAndRuntimeDoesNotMutateConfiguration()
        {
            var points = new List<Vector3>(LongRoute);
            var tower = Tower();
            var definition = new BattleDefinition(points, new[] { tower }, new UnitDefinition(100, 1), 1, 1, 1);
            points[0] = Vector3.one * 99;
            var sim = new BattleSimulation(definition);
            sim.SetTowerPosition(1, Vector3.one);
            sim.Step(0.02f);
            Assert.That(definition.Waypoints[0], Is.EqualTo(Vector3.zero));
            Assert.That(tower.Position, Is.EqualTo(Vector3.zero));
            Assert.That(definition.Unit.MaxHp, Is.EqualTo(100));
        }

        [Test]
        public void SameInputsAndFixedTicksProduceSameResults()
        {
            var a = Create(new[] { Tower() }, count: 20, interval: 0.25f, lives: 5);
            var b = Create(new[] { Tower() }, count: 20, interval: 0.25f, lives: 5);
            Finish(a);
            Finish(b);
            Assert.That(a.Outcome, Is.EqualTo(b.Outcome));
            Assert.That(a.KilledCount, Is.EqualTo(b.KilledCount));
            Assert.That(a.ElapsedTime, Is.EqualTo(b.ElapsedTime));
            Assert.That(a.Units.Select(unit => (unit.Hp, unit.Position, unit.LifeState)),
                Is.EqualTo(b.Units.Select(unit => (unit.Hp, unit.Position, unit.LifeState))));
        }

        [Test]
        public void InvalidNumbersAndDuplicateTowerIdsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitDefinition(float.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Tower(interval: 0));
            Assert.Throws<ArgumentException>(() => Create(new[] { Tower(), Tower() }));
            var sim = Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => sim.Step(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => sim.Step(0));
        }

        [Test]
        public void SimulationAssemblyHasNoComponentTypesOrPresentationReferences()
        {
            var assembly = typeof(BattleSimulation).Assembly;
            Assert.That(assembly.GetTypes().Any(type => typeof(Component).IsAssignableFrom(type)), Is.False);
            Assert.That(assembly.GetReferencedAssemblies().Any(name =>
                name.Name.Contains("World") || name.Name.Contains("Session") || name.Name.Contains("Presentation") ||
                name.Name.Contains("Animation") || name.Name.Contains("Particle") || name.Name.Contains("UI")), Is.False);
            foreach (var type in assembly.GetTypes())
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                Assert.That(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType), Is.False, $"{type.Name}.{field.Name}");
        }
    }
}
