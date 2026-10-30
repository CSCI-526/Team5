using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class SquadBattleSceneTests
    {
        [TearDown]
        public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        [Test]
        public void LevelOneUsesSavedSmallCellsAndLegacyRouteWithCurrentSessionAndHud()
        {
            EditorSceneManager.OpenScene(Team5.Rebuild.Session.BattleSceneFlow.BattleScene);
            var session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            Assert.That(session.Grid.SceneCells.Length, Is.EqualTo(20 * 14));
            Assert.That(session.Route.Cells.Count, Is.EqualTo(19));
            Assert.That(session.Route.Cells.First().TopCenter, Is.EqualTo(new Vector3(10, 0.25f, 16)));
            Assert.That(session.Route.Cells.Last().TopCenter, Is.EqualTo(new Vector3(19, 0.25f, 7)));
            Assert.That(session.Towers.Count, Is.EqualTo(1));
            var definition = session.CaptureDefinition();
            Assert.That(session.UsesSceneRosterOnly, Is.True);
            Assert.That(session.LevelTimeMultiplier, Is.EqualTo(2));
            Assert.That(definition.Members.Count, Is.EqualTo(1));
            Assert.That(definition.Members[0].Unit.Role, Is.EqualTo(UnitRole.Captain));
            Assert.That(definition.Members[0].SlotId, Is.EqualTo(4));
            Assert.That(definition.Towers[0].Position, Is.EqualTo(new Vector3(22, 0.25f, 16)));
            Assert.That(definition.Towers[0].Area, Is.EqualTo(AttackArea.NegativeX));
            Assert.That(definition.Towers[0].Range, Is.EqualTo(9));
            Assert.That(Object.FindFirstObjectByType<Team5.Rebuild.Presentation.LegacyUI.LegacyBattleHud>().Session, Is.SameAs(session));
            Assert.DoesNotThrow(() => new BattleSimulation(definition));
            var count = session.Grid.SceneCells.Length;
            EditorSceneManager.OpenScene(Team5.Rebuild.Session.BattleSceneFlow.BattleScene);
            session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            Assert.That(session.Grid.SceneCells.Length, Is.EqualTo(count));
        }

        [Test]
        public void MigrationScenePreservesEveryTerrainRouteAndCameraBlock()
        {
            Dictionary<string, string> Blocks(string path) => Regex.Split(File.ReadAllText(path), @"(?=^--- !u!)", RegexOptions.Multiline)
                .Where(b => b.StartsWith("---")).ToDictionary(b => Regex.Match(b, @"^--- !u!\d+ &(-?\d+)").Groups[1].Value);
            var before = Blocks(MacroPrototypeSceneBuilder.ScenePath);
            var after = Blocks(SquadBattleMenu.ScenePath);
            var changes = before.Where(pair => !after.TryGetValue(pair.Key, out var block) || block != pair.Value).ToArray();
            Assert.That(changes.Length, Is.EqualTo(7));
            Assert.That(changes.Count(pair => pair.Value.Contains("Team5.Rebuild.World.BattleTower")), Is.EqualTo(3));
            Assert.That(changes.Count(pair => pair.Value.Contains("Team5.Rebuild.Session.BattleSession")), Is.EqualTo(1));
            Assert.That(changes.Count(pair => pair.Value.StartsWith("--- !u!1 &")), Is.EqualTo(1));
            var sessionParent = changes.Single(pair => pair.Value.StartsWith("--- !u!4 &"));
            Assert.That(sessionParent.Value, Does.Contain("m_GameObject: {fileID: 2044225505}"));
            // Only parenting the authored HUD is allowed; every transform value is unchanged.
            var parentAfter = Regex.Replace(after[sessionParent.Key], @"  m_Children:\r?\n  - \{fileID: \d+\}", "  m_Children: []");
            Assert.That(parentAfter, Is.EqualTo(sessionParent.Value));
            // Saving an edited Scene now serializes the existing optional range toggle.
            var presentation = changes.Single(pair => pair.Value.Contains("Team5.Rebuild.Presentation.BattlePresentation"));
            Assert.That(Regex.Replace(after[presentation.Key], @"  showTowerRanges: [01]\r?\n", ""), Is.EqualTo(presentation.Value));
            // HUD minus the removed currency GameObject and its four component blocks.
            Assert.That(after.Count, Is.EqualTo(before.Count + 39));
        }

        [Test]
        public void DebugSceneProducesSixIndependentMembersAndSceneTransformsRemainAuthoritative()
        {
            EditorSceneManager.OpenScene(SquadBattleMenu.ScenePath);
            var session = Object.FindFirstObjectByType<BattleSession>();
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            var first = session.CaptureDefinition();
            Assert.That(first.Objective, Is.EqualTo(BattleObjective.ReachGoal));
            Assert.That(first.Members.Count, Is.EqualTo(6));
            Assert.That(first.Members.Select(m => m.MemberId).Distinct().Count(), Is.EqualTo(6));
            Assert.That(first.Members.Select(m => m.Unit.Role).Distinct().Count(), Is.EqualTo(3));
            var before = session.Grid.SceneCells.Select(c => c.transform.position).ToArray();
            session.Grid.transform.position += Vector3.right * 3;
            foreach (var tower in session.Towers) tower.transform.position += Vector3.right * 3;
            var next = session.CaptureDefinition();
            Assert.That(next.Waypoints[0], Is.EqualTo(first.Waypoints[0] + Vector3.right * 3));
            Assert.That(next.Towers[0].Position, Is.EqualTo(first.Towers[0].Position + Vector3.right * 3));
            Assert.DoesNotThrow(() => new BattleSimulation(next));
            Assert.That(session.Grid.SceneCells.Select(c => c.transform.position), Is.EqualTo(before.Select(p => p + Vector3.right * 3)));
        }
    }
}
