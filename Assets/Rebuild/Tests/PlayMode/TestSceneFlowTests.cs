using System.Collections;
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
    public sealed class TestSceneFlowTests
    {
        private static IEnumerator WaitForScene(string path)
        {
            for (var frame = 0; frame < 600 && SceneManager.GetActiveScene().path != path; frame++) yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(path));
            yield return null;
        }

        private static IEnumerator OpenTestPreparation()
        {
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.TestStartScene);
            yield return null;
            Object.FindFirstObjectByType<Button>().onClick.Invoke();
            yield return WaitForScene(BattleSceneFlow.TestUpdateScene);
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            foreach (var flow in Object.FindObjectsByType<BattleSceneFlow>(FindObjectsSortMode.None)) Object.Destroy(flow.gameObject);
            yield return null;
            yield return SceneManager.LoadSceneAsync(BattleSceneFlow.StartScene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestTitlePreparationAndEmptyLevelStaySeparateFromCampaign()
        {
            yield return OpenTestPreparation();
            Assert.That(BattleSceneFlow.IsTestRun, Is.True);
            Assert.That(BattleSceneFlow.HasUnlimitedGold, Is.True);
            Assert.That(BattleSceneFlow.NextBattleScenePath, Is.EqualTo(BattleSceneFlow.TestLevelScene));
            var preparation = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            Assert.That(preparation.GoldLabel.text, Does.Contain("UNLIMITED"));
            var gold = BattleSceneFlow.GoldBalance;
            Assert.That(BattleSceneFlow.TrySpendGold(int.MaxValue), Is.True);
            Assert.That(BattleSceneFlow.TrySpendGold(int.MaxValue), Is.True);
            Assert.That(BattleSceneFlow.GoldBalance, Is.EqualTo(gold));
            var member = preparation.Preparation.Members[0];
            Assert.That(preparation.Preparation.TryAssign(member.MemberId, 4), Is.True);
            preparation.StartWave();
            yield return WaitForScene(BattleSceneFlow.TestLevelScene);
            var session = Object.FindFirstObjectByType<BattleSession>();
            var tools = session.GetComponent<TestLevelControls>();
            Assert.That(session.Grid.SceneCells, Is.Empty);
            Assert.That(session.Route.Cells, Is.Empty);
            Assert.That(session.Towers, Is.Empty);
            Assert.That(session.Simulation, Is.Null);
            Assert.That(tools.ConfigurationMessage, Does.Contain("Map not configured"));
            Assert.That(BattleSceneFlow.CompletedLevelNumber, Is.Zero);
            BattleSceneFlow.ContinueToUpgrade();
            yield return WaitForScene(BattleSceneFlow.TestUpdateScene);
            Assert.That(Object.FindFirstObjectByType<LegacyUpgradeScene>().Preparation.MemberAtSlot(4), Is.EqualTo(member.MemberId));
            Assert.That(BattleSceneFlow.SceneForLevel(4), Is.EqualTo(BattleSceneFlow.LevelFourScene));
        }

        [UnityTest]
        public IEnumerator TestPreparationUsesNewSummonOverlayAndUnlimitedWallet()
        {
            yield return OpenTestPreparation();
            var preparation = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            var count = preparation.Preparation.Members.Count;
            var price = BattleSceneFlow.CardShop.NextPrice;
            preparation.OpenClaimCards();
            yield return null;
            var overlay = Object.FindFirstObjectByType<LegacyClaimCardOverlay>();
            Assert.That(overlay, Is.Not.Null);
            var choices = overlay.GetComponentsInChildren<Button>();
            Assert.That(System.Array.FindAll(choices, button => button.name.StartsWith("Summon Option ")).Length, Is.EqualTo(3));
            Assert.That(System.Array.Exists(overlay.GetComponentsInChildren<TMPro.TMP_Text>(), label => label.text.Contains("UNLIMITED")), Is.True);
            var selected = BattleSceneFlow.CardShop.BeginOffer()[0];
            System.Array.Find(choices, button => button.name == "Summon Option 0").onClick.Invoke();
            yield return null;
            Assert.That(preparation.Preparation.Members.Count, Is.EqualTo(count + 1));
            Assert.That(preparation.Preparation.Members[count].Unit, Is.SameAs(selected));
            Assert.That(BattleSceneFlow.GoldBalance, Is.EqualTo(int.MaxValue));
            Assert.That(BattleSceneFlow.CardShop.NextPrice, Is.EqualTo(price + Resources.Load<GameEconomySettings>("RebuildEconomy").SummonCostIncreasePerPull));
            Assert.That(BattleSceneFlow.HighestClearedLevel, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CampaignVictoriesKeepNewRewardsAndAdvanceSummonPool()
        {
            BattleSceneFlow.BeginRun();
            yield return WaitForScene(BattleSceneFlow.BattleScene);
            var settings = Resources.Load<GameEconomySettings>("RebuildEconomy");
            var expectedGold = settings.StartingGold;
            for (var level = 1; level <= 2; level++)
            {
                var session = Object.FindFirstObjectByType<BattleSession>();
                session.enabled = false;
                // Exercise the scene-flow victory handler without changing the combat system.
                typeof(BattleSceneFlow).GetMethod("Settle", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(Object.FindFirstObjectByType<BattleSceneFlow>(), new object[] { BattleOutcome.Victory });
                yield return WaitForScene(BattleSceneFlow.SettlementScene);
                expectedGold += settings.GetStageClearGold(level);
                Assert.That(BattleSceneFlow.IsTestRun, Is.False);
                Assert.That(BattleSceneFlow.HasUnlimitedGold, Is.False);
                Assert.That(BattleSceneFlow.GoldBalance, Is.EqualTo(expectedGold));
                Assert.That(BattleSceneFlow.LastRewardGold, Is.EqualTo(settings.GetStageClearGold(level)));
                Assert.That(BattleSceneFlow.HighestClearedLevel, Is.EqualTo(level));
                Assert.That(BattleSceneFlow.NextBattleLevelNumber, Is.EqualTo(level + 1));
                BattleSceneFlow.ContinueToUpgrade();
                yield return WaitForScene(BattleSceneFlow.UpgradeScene);
                Assert.That(BattleSceneFlow.CardShop.HighestStageCleared, Is.EqualTo(level));
                if (level == 2)
                {
                    Assert.That(BattleSceneFlow.CardShop.GetRarityWeights(), Is.EqualTo((50, 35, 15, 0)));
                    break;
                }
                var preparation = Object.FindFirstObjectByType<LegacyUpgradeScene>();
                var member = preparation.Preparation.Members[0];
                Assert.That(preparation.Preparation.TryAssign(member.MemberId, 4), Is.True);
                preparation.StartWave();
                yield return WaitForScene(BattleSceneFlow.LevelTwoScene);
            }
        }

        [UnityTest]
        public IEnumerator StartingCampaignAfterTestRestoresConfiguredFiniteGold()
        {
            yield return OpenTestPreparation();
            Assert.That(BattleSceneFlow.HasUnlimitedGold, Is.True);
            BattleSceneFlow.BeginRun();
            yield return WaitForScene(BattleSceneFlow.BattleScene);
            Assert.That(BattleSceneFlow.IsTestRun, Is.False);
            Assert.That(BattleSceneFlow.HasUnlimitedGold, Is.False);
            Assert.That(BattleSceneFlow.GoldBalance, Is.EqualTo(Resources.Load<GameEconomySettings>("RebuildEconomy").StartingGold));
        }

        [UnityTest]
        public IEnumerator CompletedTestDoesNotAwardCampaignRewardsOrLeaveTestLevel()
        {
            yield return OpenTestPreparation();
            var preparation = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            var member = preparation.Preparation.Members[0];
            preparation.Preparation.TryAssign(member.MemberId, 4);
            preparation.StartWave();
            yield return WaitForScene(BattleSceneFlow.TestLevelScene);
            var session = Object.FindFirstObjectByType<BattleSession>();
            session.enabled = false;
            var gold = BattleSceneFlow.GoldBalance;
            var cells = new BattleCell[6, 3];
            // Temporary test fixtures only; the saved TestLevel remains completely empty.
            for (var x = 0; x < 6; x++)
            for (var z = 0; z < 3; z++)
            {
                var cell = new GameObject("Test Cell").AddComponent<BattleCell>();
                cell.transform.SetParent(session.Grid.transform, false);
                // BattleCell validates world coordinates; the user's saved rig may be offset.
                cell.transform.position = new Vector3(x, 0, z);
                typeof(BattleCell).GetField("kind", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(cell, x < 3 ? CellKind.Spawn : CellKind.Goal);
                cells[x, z] = cell;
            }
            typeof(SceneRoute).GetField("cells", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(session.Route, new System.Collections.Generic.List<BattleCell> { cells[1,1], cells[2,1], cells[3,1], cells[4,1] });
            Assert.That(session.ValidateConfiguration(), Is.Empty);
            Assert.That(session.GetComponent<TestLevelControls>().RestartTest(), Is.True);
            Assert.That(session.Simulation.Units.Count, Is.Zero);
            for (var tick = 0; tick < 1000 && session.Simulation.Outcome == BattleOutcome.Running; tick++) session.Advance(.02f);
            Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(session.Simulation.Units[0].MemberId, Is.EqualTo(member.MemberId));
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.TestLevelScene));
            Assert.That(BattleSceneFlow.GoldBalance, Is.EqualTo(gold));
            Assert.That(BattleSceneFlow.LastRewardGold, Is.Zero);
            Assert.That(BattleSceneFlow.HighestClearedLevel, Is.Zero);
            Assert.That(BattleSceneFlow.CompletedLevelNumber, Is.Zero);
            Assert.That(session.AttemptsRemaining, Is.EqualTo(BattleSession.DefaultLivesPerLevel));
            Assert.That(session.GetComponent<TestLevelControls>().RestartTest(), Is.True);
            Assert.That(session.Simulation.Outcome, Is.EqualTo(BattleOutcome.Running));
        }

        [UnityTest]
        public IEnumerator PreviewTargetsTestMapWithoutStartingItsToolsOrSimulation()
        {
            yield return OpenTestPreparation();
            var preparation = Object.FindFirstObjectByType<LegacyUpgradeScene>();
            preparation.OpenNextLevelPreview();
            var preview = Object.FindFirstObjectByType<LegacyNextLevelPreview>();
            Assert.That(preview, Is.Not.Null);
            for (var frame = 0; frame < 600 && !preview.LoadedScene.isLoaded; frame++) yield return null;
            Assert.That(preview.LoadedScene.path, Is.EqualTo(BattleSceneFlow.TestLevelScene));
            foreach (var root in preview.LoadedScene.GetRootGameObjects())
            {
                var session = root.GetComponentInChildren<BattleSession>(true);
                if (session == null) continue;
                Assert.That(session.Simulation, Is.Null);
                Assert.That(session.GetComponent<TestLevelControls>().ConfigurationMessage, Is.Null);
            }
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(BattleSceneFlow.TestUpdateScene));
            preview.Close();
            for (var frame = 0; frame < 600 && SceneManager.GetSceneByPath(BattleSceneFlow.TestLevelScene).isLoaded; frame++) yield return null;
            Assert.That(SceneManager.GetSceneByPath(BattleSceneFlow.TestLevelScene).isLoaded, Is.False);
            Assert.That(BattleSceneFlow.NextBattleScenePath, Is.EqualTo(BattleSceneFlow.TestLevelScene));
        }
    }
}
