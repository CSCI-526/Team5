using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    public sealed class GameMenuSceneTests
    {
        [TearDown] public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        [TestCase(BattleSceneFlow.StartScene, "StartGame", "Reverse Tower Defense")]
        [TestCase(BattleSceneFlow.EndScene, "GoToStart", "Thanks for playing")]
        public void ImportedMenuKeepsLegacyTextAndHasValidPersistentButton(string path, string method, string heading)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var menu = Object.FindFirstObjectByType<LegacySceneMenu>();
            Assert.That(menu, Is.Not.Null);
            var button = Object.FindFirstObjectByType<Button>();
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(menu));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(method));
            var labels = Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
            Assert.That(labels.Any(label => label.text.Trim() == heading), Is.True);
            Assert.That(labels.All(label => label.font != null), Is.True);
            Assert.That(Object.FindFirstObjectByType<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(500, 500)));
            foreach (var root in scene.GetRootGameObjects())
                Assert.That(root.GetComponentsInChildren<Component>(true).All(component => component != null), Is.True, root.name);
            Assert.That(scene.GetRootGameObjects().Any(root => root.name.Contains("Analytics")), Is.False);
        }

        [Test]
        public void BuildStartsAtTitleAndIncludesBattleAndEnd()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            Assert.That(scenes[0], Is.EqualTo(BattleSceneFlow.StartScene));
            Assert.That(scenes, Does.Contain(BattleSceneFlow.BattleScene));
            Assert.That(scenes, Does.Contain(BattleSceneFlow.LevelTwoScene));
            Assert.That(scenes, Does.Contain(BattleSceneFlow.LevelThreeScene));
            Assert.That(scenes, Does.Contain(BattleSceneFlow.LevelFourScene));
            Assert.That(scenes, Does.Contain(BattleSceneFlow.SettlementScene));
            Assert.That(scenes, Does.Contain(BattleSceneFlow.UpgradeScene));
            Assert.That(scenes, Does.Contain(BattleSceneFlow.EndScene));
            Assert.That(scenes.Distinct().Count(), Is.EqualTo(scenes.Length));
        }

        [Test]
        public void SettlementHasPortedLabelsAndValidContinueCallback()
        {
            var scene = EditorSceneManager.OpenScene(BattleSceneFlow.SettlementScene);
            var controller = Object.FindFirstObjectByType<LegacyLevelSettlement>();
            Assert.That(controller, Is.Not.Null);
            var button = Object.FindFirstObjectByType<Button>();
            Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(controller));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("ContinueToUpgrade"));
            foreach (var root in scene.GetRootGameObjects())
                Assert.That(root.GetComponentsInChildren<Component>(true).All(component => component != null), Is.True);
            Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).All(label => label.font != null), Is.True);
        }
    }
}
