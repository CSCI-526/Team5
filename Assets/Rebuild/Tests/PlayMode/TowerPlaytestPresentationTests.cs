using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class TowerPlaytestPresentationTests
    {
        private static IEnumerator WaitScene(string path)
        {
            var deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && SceneManager.GetActiveScene().path != path) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(path));
            for (var i = 0; i < 5; i++) yield return null;
        }
        private static IEnumerator Capture(string name)
        {
            yield return null;
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras = canvases.Select(c => c.worldCamera).ToArray();
            var oldDistances = canvases.Select(c => c.planeDistance).ToArray();
            var target = new RenderTexture(1280, 720, 24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            camera.targetTexture = target;
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 1f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            Directory.CreateDirectory("Logs/TowerPlaytestScreens");
            File.WriteAllBytes("Logs/TowerPlaytestScreens/" + name + ".png", texture.EncodeToPNG());
            RenderTexture.active = oldActive;
            camera.targetTexture = oldTarget;
            for (var i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].worldCamera = oldCameras[i];
                canvases[i].planeDistance = oldDistances[i];
            }
            Object.Destroy(texture); Object.Destroy(target);
        }
        [UnityTest]
        public IEnumerator BeginningArtworkButtonsAndBattleEntryWork()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.StartScene);
            yield return WaitScene(BattleSceneFlow.StartScene);
            Assert.That(Object.FindFirstObjectByType<BeginningPage>(), Is.Not.Null);
            Assert.That(GameObject.Find("Beginning artwork").GetComponent<Image>().sprite, Is.Not.Null);
            yield return Capture("Beginning");
            GameObject.Find("Levels").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("Campaign guide"), Is.Not.Null);
            GameObject.Find("Back").GetComponent<Button>().onClick.Invoke();
            yield return null;
            GameObject.Find("Settings").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("Audio"), Is.Not.Null);
            GameObject.Find("Back").GetComponent<Button>().onClick.Invoke();
            yield return null;
            GameObject.Find("Play").GetComponent<Button>().onClick.Invoke();
            yield return WaitScene(BattleSceneFlow.BattleScene);
            Assert.That(Object.FindFirstObjectByType<BattleSession>().Simulation, Is.Not.Null);
            yield return Capture("Level1");
        }
        [UnityTest]
        public IEnumerator PreviewUsesRealLevelFourWithBlueFrostBadgeAndModel()
        {
            BattleSceneFlow.BeginRun();
            yield return WaitScene(BattleSceneFlow.BattleScene);
            Object.FindFirstObjectByType<BattleSession>().enabled = false;
            typeof(BattleSceneFlow).GetMethod("Settle", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(Object.FindFirstObjectByType<BattleSceneFlow>(), new object[] { BattleOutcome.Victory });
            yield return WaitScene(BattleSceneFlow.SettlementScene);
            BattleSceneFlow.ContinueToUpgrade();
            yield return WaitScene(BattleSceneFlow.UpgradeScene);
            var flow = Object.FindFirstObjectByType<BattleSceneFlow>();
            typeof(BattleSceneFlow).GetField("nextBattleLevelNumber", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flow, 4);
            var upgrade = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            // Isolate the preview from the first-recruitment tutorial in this scene fixture.
            typeof(LegacyUpgradeScene).GetMethod("CloseSummonGuide", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(upgrade, null);
            yield return null;
            var preview = LegacyNextLevelPreview.Open(upgrade);
            Assert.That(preview, Is.Not.Null);
            var deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && !preview.IsReady) yield return null;
            Assert.That(preview.IsReady, Is.True);
            var data = NextMapData.Capture(preview.LoadedScene);
            Assert.That(data.Towers.Count, Is.EqualTo(7));
            var frost = data.Towers.Single(t => t.Definition.Kind == TowerKind.Frost);
            Assert.That(frost.Definition.Area, Is.EqualTo(AttackArea.Square));
            Assert.That(Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)
                .Any(t => t.text.Contains("FROST")), Is.True);
            yield return Capture("Level4Preview");
            preview.Close();
            for (var i = 0; i < 15; i++) yield return null;
        }
        [UnityTest]
        public IEnumerator LevelSixRetainsModelMaterialsAndAimsLaserAcrossRoad()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.LevelSixScene);
            yield return WaitScene(BattleSceneFlow.LevelSixScene);
            var session = Object.FindFirstObjectByType<BattleSession>();
            session.Simulation.SetPaused(true);
            var towers = session.Towers.Where(t => !t.IsWall).ToArray();
            var materials = towers.SelectMany(t => t.GetComponentsInChildren<MeshRenderer>())
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).ToArray();
            Assert.That(materials.Any(m => m.name.Contains("Gunmetal")), Is.True);
            Assert.That(materials.Any(m => m.name.Contains("Stone")), Is.True);
            Assert.That(materials.Select(m => m.name).Distinct().Count(), Is.GreaterThan(4));
            var laser = towers.Single(t => t.Kind == TowerKind.Laser).CaptureDefinition(1);
            Assert.That(laser.Area, Is.EqualTo(AttackArea.PositiveX));
            Assert.That(laser.Range, Is.GreaterThan(10));
            var effect = Resources.Load<Material>("TowerEffect");
            Assert.That(effect != null && effect.shader != null && effect.shader.isSupported, Is.True);
            yield return Capture("Level6RestoredModels");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var preview in Object.FindObjectsByType<LegacyNextLevelPreview>(FindObjectsSortMode.None)) preview.Close();
            for (var i = 0; i < 10; i++) yield return null;
            foreach (var flow in Object.FindObjectsByType<BattleSceneFlow>(FindObjectsSortMode.None)) Object.Destroy(flow.gameObject);
            yield return null;
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.StartScene);
        }
    }
}
