#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    public sealed class FormationReserveBattleTests
    {
        [Test]
        public void DefeatWaitsUntilTheLastDepartureSquadDies()
        {
            var unit = new UnitDefinition(.1f, 1, UnitRole.Tank);
            var tower = new TowerDefinition(1, new Vector3(0, 0, 2), 6, 4, .1f,
                TargetRule.FurthestAlongRoute, basicDefense: BasicDefenseKind.Mage, minimumShotDamage: 4);
            var battle = new BattleSimulation(new BattleDefinition(
                new[] { Vector3.zero, Vector3.right * 20 }, new[] { tower },
                new[] { new MemberSpawnDefinition(1, 4, unit) }));
            battle.EnableFixedWaveDeployment(2);
            var slots = new UnitDefinition[9];
            slots[4] = unit;

            Assert.That(battle.TryQueueFixedWave(slots), Is.True);
            for (var i = 0; i < 100; i++) battle.Step(.01f);
            Assert.That(battle.AliveCount, Is.Zero);
            Assert.That(battle.DeparturesRemaining, Is.EqualTo(1));
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Running));

            Assert.That(battle.TryQueueFixedWave(slots), Is.True);
            Assert.That(battle.TryQueueFixedWave(slots), Is.False);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Running));
            for (var i = 0; i < 100; i++) battle.Step(.01f);
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Defeat));
        }

        [TestCase(BasicDefenseKind.Archer, true, 1, 1f)]
        [TestCase(BasicDefenseKind.Mage, true, 1, 1.5f)]
        [TestCase(BasicDefenseKind.Artillery, true, 0, 0f)]
        [TestCase(BasicDefenseKind.Artillery, false, 1, 2.5f)]
        public void BasicDefenseAppliesSingleShotAndCorrectProtection(
            BasicDefenseKind kind, bool flying, int expectedShots, float expectedDamage)
        {
            var unit = new UnitDefinition(100, 1, UnitRole.Tank, defense: .75f,
                magicResistance: .625f, isFlying: flying);
            var tower = new TowerDefinition(1, new Vector3(0, 0, 2), 6, 4, 3,
                TargetRule.FurthestAlongRoute, basicDefense: kind, minimumShotDamage: 4);
            var battle = new BattleSimulation(new BattleDefinition(
                new[] { Vector3.zero, Vector3.right * 20 }, new[] { tower },
                new[] { new MemberSpawnDefinition(1, 4, unit) }));
            battle.Step(.01f);
            Assert.That(battle.DefenseShots.Count, Is.EqualTo(expectedShots));
            for (var i = 0; i < 100; i++) battle.Step(.01f);
            var damage = 100 - battle.Units.Single().Hp;
            Assert.That(damage, Is.EqualTo(expectedDamage).Within(.02f));
        }

        [UnityTest]
        public IEnumerator BasicTowerTrialUsesThreeMacroSitesAndEachDefenseEngages()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(BasicTowerTrial.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            Assert.That(session.Towers.Count, Is.EqualTo(3));
            Assert.That(session.Towers.All(tower => tower.Footprint == new Vector2Int(3, 3)), Is.True);
            foreach (var tower in session.Towers)
            {
                var path = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tower);
                Assert.That(path, Does.EndWith($"/Tower_{tower.BasicDefense}_3x3.prefab"));
                var model = tower.transform.Find("Model");
                Assert.That(model, Is.Not.Null);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(model.localScale, Is.EqualTo(prefab.transform.Find("Model").localScale));
                var meshes = model.GetComponentsInChildren<MeshFilter>();
                Assert.That(meshes, Is.Not.Empty);
                Assert.That(meshes.All(filter => filter.sharedMesh != null), Is.True);
            }
            var simulation = session.Simulation;
            Assert.That(simulation, Is.Not.Null);
            Assert.That(simulation.BarracksGuards, Is.Empty);
            var impacts = new System.Collections.Generic.HashSet<BasicDefenseKind>();
            simulation.BasicDefenseImpact += (kind, _, _) => impacts.Add(kind);
            var slots = new UnitDefinition[9];
            slots[4] = new UnitDefinition(1000, session.CaptureDefinition().Unit.Speed, UnitRole.Attack, damage: .2f);
            Assert.That(simulation.TryQueueFixedWave(slots), Is.True);
            simulation.SetPaused(false);
            for (var i = 0; i < 6000 && impacts.Count < 3; i++) session.Advance(.02f);
            Assert.That(impacts, Is.EquivalentTo(new[] { BasicDefenseKind.Archer, BasicDefenseKind.Mage, BasicDefenseKind.Artillery }));
            Assert.That(simulation.Projectiles.Any(shot => shot.TargetKind == ProjectileTargetKind.Tower), Is.False);
            Assert.That(simulation.Towers.All(tower => tower.Hp == tower.Definition.MaxHp), Is.True);
        }

        [UnityTest]
        public IEnumerator DepartQueuesEachPreparedSquadWithoutChangingUnitsOnTheRoad()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(FormationReserveBattle.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var battle = Object.FindFirstObjectByType<FormationReserveBattle>();
            var session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(battle, Is.Not.Null);
            Assert.That(session.Simulation, Is.Not.Null);
            var sim = session.Simulation;
            Assert.That(sim.IsPaused, Is.True);
            Assert.That(sim.PendingSpawnCount, Is.Zero);
            Assert.That(sim.ElapsedTime, Is.Zero);
            yield return null;
            Assert.That(sim.Units, Is.Empty);
            var ready = (UnitDefinition[])typeof(FormationReserveBattle)
                .GetField("readySquad", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battle);
            var slot = System.Array.FindIndex(ready, unit => unit?.Role == UnitRole.Attack);
            Assert.That(ready.Count(unit => unit != null), Is.EqualTo(3));
            var buttons = battle.GetComponentsInChildren<Button>(true);
            var slider = battle.GetComponentsInChildren<Slider>().Single(control => control.name == "Speed Slider");
            var pause = buttons.Single(button => button.name == "Pause Battle");
            Assert.That(pause.interactable, Is.False);
            slider.value = 2;
            Assert.That(session.PlaybackSpeed, Is.EqualTo(3));
            var tankCard = buttons.Single(button => button.name.StartsWith("Reserve ") &&
                button.GetComponentInChildren<TMP_Text>().text == "TANK");
            Assert.That(buttons.Count(button => button.name.StartsWith("Reserve ")), Is.EqualTo(10));
            Drop(tankCard, buttons.Single(button => button.name == $"Slot {slot}"));
            Assert.That(sim.IsPaused, Is.True);
            Assert.That(ready[slot].Role, Is.EqualTo(UnitRole.Tank));
            Assert.That(tankCard.gameObject.activeSelf, Is.False);
            var vacant = System.Array.FindIndex(ready, unit => unit == null);
            var sniperCard = buttons.Single(button => button.name.StartsWith("Reserve ") &&
                button.GetComponentInChildren<TMP_Text>().text == "SNIPER");
            Drop(sniperCard, buttons.Single(button => button.name == $"Slot {vacant}"));
            Assert.That(ready[vacant].Role, Is.EqualTo(UnitRole.Sniper));
            yield return null;
            Assert.That(buttons.Where(button => button.name.StartsWith("Reserve ") && button.gameObject.activeInHierarchy)
                .All(button => button.interactable), Is.True);
            var launch = buttons.Single(button => button.name == "Launch");
            var firstPlan = ready.ToArray();
            Assert.That(firstPlan.Count(unit => unit != null), Is.EqualTo(4));
            launch.onClick.Invoke();
            Assert.That(sim.IsPaused, Is.False);
            Assert.That(sim.QueuedFixedWaves, Is.EqualTo(1));
            var countdown = launch.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Departure countdown");
            Assert.That(countdown.text, Is.EqualTo("8"));
            Assert.That(launch.interactable, Is.False);
            Assert.That(ready.Where(unit => unit != null).SequenceEqual(firstPlan.Where(unit => unit != null)), Is.False);
            session.Advance(.02f);
            Assert.That(sim.Units.Count, Is.EqualTo(4));
            var firstSquad = sim.Units.ToArray();
            var firstTank = firstSquad.Single(unit => unit.SlotId == slot);
            Assert.That(firstTank.Role, Is.EqualTo(UnitRole.Tank));
            launch.onClick.Invoke();
            Assert.That(sim.QueuedFixedWaves, Is.Zero);
            for (var i = 0; i < 400 && sim.FixedWaveClearanceDistance > 0; i++) session.Advance(.02f);
            yield return null;
            Assert.That(countdown.text, Is.EqualTo("0"));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(launch.interactable, Is.True);
            Assert.That(countdown.gameObject.activeSelf, Is.False);
            Assert.That(sim.Units.Count, Is.EqualTo(4));
            launch.onClick.Invoke();
            session.Advance(.02f);
            Assert.That(sim.Units.Select(unit => unit.SquadId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(firstTank.Role, Is.EqualTo(UnitRole.Tank));
            Assert.That(firstTank.RouteProgress, Is.GreaterThanOrEqualTo(3f));

        }

        private static void Drop(Button card, Button slot)
        {
            var pointer = new PointerEventData(EventSystem.current) { pointerDrag = card.gameObject };
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(slot.gameObject, pointer, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);
        }
    }
}
#endif
