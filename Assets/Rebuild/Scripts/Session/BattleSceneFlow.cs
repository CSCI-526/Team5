using System.Collections;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Session
{
    public sealed class BattleSceneFlow : MonoBehaviour
    {
        public const string StartScene = "Assets/Rebuild/Scenes/StartGame.unity";
        public const string UpgradeScene = "Assets/Rebuild/Scenes/UpgradeScene.unity";
        public const string PrototypeBattleScene = "Assets/Rebuild/Scenes/SquadBattlePrototype.unity";
        public const string BattleScene = "Assets/Rebuild/Scenes/Level1GridBattle.unity";
        public const string LevelTwoScene = "Assets/Rebuild/Scenes/Level2GridBattle.unity";
        public const string LevelThreeScene = "Assets/Rebuild/Scenes/Level3GridBattle.unity";
        public const string LevelFourScene = "Assets/Rebuild/Scenes/Level4GridBattle.unity";
        public const string LevelFiveScene = "Assets/Rebuild/Scenes/Level5GridBattle.unity";
        public const string LevelSixScene = "Assets/Rebuild/Scenes/Level6GridBattle.unity";
        public const string LevelSevenScene = "Assets/Rebuild/Scenes/Level7GridBattle.unity";
        public const string TestStartScene = "Assets/Rebuild/Scenes/TestStartScene.unity";
        public const string TestUpdateScene = "Assets/Rebuild/Scenes/TestUpdateScene.unity";
        public const string TestLevelScene = "Assets/Rebuild/Scenes/TestLevel.unity";
        public const string SettlementScene = "Assets/Rebuild/Scenes/LevelSettlement.unity";
        public const string EndScene = "Assets/Rebuild/Scenes/EndGame.unity";

        private static readonly string[] battleScenes =
        {
            BattleScene,
            LevelTwoScene,
            LevelThreeScene,
            LevelFourScene,
            LevelFiveScene,
            LevelSixScene,
            LevelSevenScene
        };

        public static int TotalCampaignLevels => battleScenes.Length;

        public static string SceneForLevel(int level)
        {
            if (level < 1 || level > battleScenes.Length) throw new System.ArgumentOutOfRangeException(nameof(level));
            return battleScenes[level - 1];
        }

        private static bool IsCampaignScene(string path) => System.Array.IndexOf(battleScenes, path) >= 0;

        private static string previewScenePath;
        public static string NextBattleScenePath => IsTestRun ? TestLevelScene : SceneForLevel(NextBattleLevelNumber);
        private string PreparationScenePath => testRun ? TestUpdateScene : UpgradeScene;
        private static bool IsInPreparationScene => current != null && SceneManager.GetActiveScene().path == current.PreparationScenePath;
        private static bool IsTestScenePath(string path) => path == TestStartScene || path == TestUpdateScene || path == TestLevelScene;
        public static bool IsTestRun => current != null && current.testRun;
        public static bool IsPreviewScene(Scene scene) => previewScenePath != null && scene.path == previewScenePath;

        public static bool TryBeginLevelPreview()
        {
            if (current == null || current.loading || current.livesExhausted || previewScenePath != null || !IsInPreparationScene)
                return false;
            previewScenePath = NextBattleScenePath;
            return true;
        }

        public static void EndLevelPreview(string path)
        {
            if (previewScenePath == path) previewScenePath = null;
        }

        private static BattleSceneFlow current;
        private bool testRun;
        private BattleSession session;
        private BattleSimulation simulation;
        private bool loading;
        private SquadPreparation preparation;
        private ClaimCardShop claimCards;
        private IReadOnlyList<MemberSpawnDefinition> deployment;
        private int nextBattleLevelNumber = 1;
        private int highestClearedLevel = 0;
        private int failedAttempts;
        private bool settled;
        private bool livesExhausted;
        private CampaignWallet wallet;
        private GameEconomySettings economySettings;

        public static ClaimCardShop CardShop => current != null ? current.claimCards : null;
        public static int HighestClearedLevel => current != null ? current.highestClearedLevel : 0;
        public static bool IsOutOfLives => current != null && current.livesExhausted;
        public static int LastRewardGold { get; private set; }
        public static int LastStageClearGold { get; private set; }
        public static int LastTowerBonusGold { get; private set; }
        public static int LastTowersDestroyedCount { get; private set; }
        public static int GoldBalance => current != null && current.wallet != null ? current.wallet.Balance : 0;
        public static bool HasUnlimitedGold => current != null && current.wallet != null && current.wallet.IsUnlimited;

        public static bool TrySpendGold(int price)
        {
            if (price <= 0) throw new System.ArgumentOutOfRangeException(nameof(price));
            return current != null && !current.loading && !current.livesExhausted && IsInPreparationScene
                && current.wallet.TrySpend(price);
        }

        public static BattleOutcome LastOutcome { get; private set; }
        public static int CompletedLevelNumber { get; private set; }
        public static bool IsRunActive => current != null;
        public static int NextBattleLevelNumber => current != null ? current.nextBattleLevelNumber : 1;
        public static int CurrentLevelLivesRemaining => System.Math.Max(0, BattleSession.DefaultLivesPerLevel - (current != null ? current.failedAttempts : 0));

        public static IReadOnlyList<UnitDefinition> OpenSummonOffer()
        {
            if (current == null || current.loading || current.livesExhausted || !IsInPreparationScene || current.claimCards == null)
                return System.Array.Empty<UnitDefinition>();
            return current.claimCards.BeginOffer();
        }

        public static bool TrySummonUnit(int offerIndex) =>
            current != null && !current.loading && !current.livesExhausted &&
            IsInPreparationScene &&
            current.claimCards != null && current.claimCards.TrySummon(offerIndex);

        public static void RestartCurrentLevel()
        {
            if (current == null || current.loading || current.livesExhausted || current.session == null || !current.session.RecoveryRequired || current.failedAttempts >= current.session.AttemptsPerLevel)
                return;
            current.QueueScene(current.session.gameObject.scene.path);
        }

        public static bool RewindLevelOne() =>
            current != null && !current.loading && current.session != null && current.session.RewindTutorialCheckpoint();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= EnsureLevelFlow;
            current = null;
            previewScenePath = null;
            LastOutcome = BattleOutcome.Running;
            CompletedLevelNumber = 0;
            LastRewardGold = 0;
            LastStageClearGold = 0;
            LastTowerBonusGold = 0;
            LastTowersDestroyedCount = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallLevelFlow()
        {
            SceneManager.sceneLoaded -= EnsureLevelFlow;
            SceneManager.sceneLoaded += EnsureLevelFlow;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BindInitialLevel() => EnsureLevelFlow(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        private static void EnsureLevelFlow(Scene scene, LoadSceneMode mode)
        {
            if (IsPreviewScene(scene)) return;
            if ((!IsCampaignScene(scene.path) && scene.path != TestLevelScene) || current != null) return;
            GetOrCreate().OnSceneLoaded(scene, mode);
        }

        public static void BeginRun() => BeginRun(false);
        public static void BeginTestRun() => BeginRun(true);

        private static void BeginRun(bool test)
        {
            var flow = GetOrCreate();
            if (flow.loading || previewScenePath != null) return;
            flow.testRun = test;
            flow.gameObject.name = test ? "Test Scene Flow" : "Battle Scene Flow";
            flow.preparation = null;
            flow.claimCards = null;
            flow.deployment = null;
            flow.nextBattleLevelNumber = 1;
            flow.highestClearedLevel = 0;
            flow.failedAttempts = 0;
            flow.livesExhausted = false;
            LastRewardGold = 0;
            LastStageClearGold = 0;
            LastTowerBonusGold = 0;
            LastTowersDestroyedCount = 0;
            flow.wallet = new CampaignWallet(flow.economySettings != null ? flow.economySettings.StartingGold : 35, unlimited: test);
            LastOutcome = BattleOutcome.Running;
            CompletedLevelNumber = 0;
            flow.QueueScene(test ? TestUpdateScene : BattleScene);
        }

        public static void ContinueToUpgrade()
        {
            var flow = GetOrCreate();
            if (!flow.livesExhausted) flow.QueueScene(flow.PreparationScenePath);
        }

        public static SquadPreparation PrepareSquad(SquadRoster roster)
        {
            var flow = GetOrCreate();
            if (flow.preparation == null)
            {
                flow.preparation = new SquadPreparation(roster);
                var baseCost = flow.economySettings != null ? flow.economySettings.BaseSummonCost : 30;
                var stepCost = flow.economySettings != null ? flow.economySettings.SummonCostIncreasePerPull : 15;
                flow.claimCards = new ClaimCardShop(flow.wallet, flow.preparation, baseCost, stepCost);
            }
            if (flow.preparation.Roster != roster)
                throw new System.InvalidOperationException("Preparation and battle must share the same roster.");

            flow.claimCards.SetStageProgress(flow.highestClearedLevel);
            return flow.preparation;
        }

        public static void StartPreparedBattle(SquadPreparation draft)
        {
            var flow = GetOrCreate();
            if (flow.loading || previewScenePath != null) return;
            if (draft == null || draft != flow.preparation) throw new System.InvalidOperationException("No current squad preparation.");
            if (flow.livesExhausted) return;
            flow.deployment = draft.CaptureDeployment();
            flow.QueueScene(flow.testRun ? TestLevelScene : SceneForLevel(flow.nextBattleLevelNumber));
        }

        public static void ReturnToStart()
        {
            var flow = GetOrCreate();
            flow.QueueScene(flow.testRun ? TestStartScene : StartScene);
        }

        private static BattleSceneFlow GetOrCreate()
        {
            if (current != null) return current;
            current = new GameObject("Battle Scene Flow").AddComponent<BattleSceneFlow>();
            current.testRun = IsTestScenePath(SceneManager.GetActiveScene().path);
            current.economySettings = Resources.Load<GameEconomySettings>("RebuildEconomy");
            current.wallet = new CampaignWallet(current.economySettings != null ? current.economySettings.StartingGold : 35, unlimited: current.testRun);
            DontDestroyOnLoad(current.gameObject);
            return current;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unbind();
            if (current == this) current = null;
        }

        private void QueueScene(string path)
        {
            if (loading) return;
            loading = true;
            StartCoroutine(ChangeScene(path));
        }

        private IEnumerator ChangeScene(string path)
        {
            yield return null;
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            loading = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsPreviewScene(scene)) return;
            loading = false;
            Unbind();
            if (testRun)
            {
                if (scene.path == TestUpdateScene) return;
                if (scene.path != TestLevelScene) { Destroy(gameObject); return; }
            }
            else
            {
                if (scene.path == EndScene || scene.path == UpgradeScene || scene.path == SettlementScene) return;
                if (!IsCampaignScene(scene.path)) { Destroy(gameObject); return; }
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                session = root.GetComponentInChildren<BattleSession>();
                if (session != null) break;
            }
            if (session == null) { Debug.LogError("Battle Scene Flow cannot find BattleSession.", this); return; }

            nextBattleLevelNumber = session.DisplayedLevelNumber;
            settled = false;
            session.RestoreFailedAttempts(failedAttempts);

            if (deployment != null && preparation != null && !session.UsesSceneRosterOnly)
            {
                if (session.InitialRoster != preparation.Roster)
                    throw new System.InvalidOperationException("Battle roster differs from preparation.");
                session.SetPreparedMembers(deployment);
            }
            session.SimulationCreated += Bind;
            Bind(session.Simulation);
        }

        private void Bind(BattleSimulation next)
        {
            if (simulation != null) simulation.BattleEnded -= OnBattleEnded;
            simulation = next;
            if (simulation == null) return;
            simulation.BattleEnded += OnBattleEnded;
            settled = false;
            if (simulation.Outcome != BattleOutcome.Running) Settle(simulation.Outcome);
        }

        private void OnBattleEnded(BattleEndedEvent result) => Settle(result.Outcome);

        private void Settle(BattleOutcome outcome)
        {
            if (settled || loading || outcome == BattleOutcome.Running) return;
            settled = true;
            LastOutcome = outcome;
            LastRewardGold = 0;
            LastStageClearGold = 0;
            LastTowerBonusGold = 0;
            LastTowersDestroyedCount = simulation != null ? simulation.TowersDestroyedCount : 0;

            if (testRun)
            {
                CompletedLevelNumber = 0;
                failedAttempts = 0;
                session.RestoreFailedAttempts(0);
                return;
            }
            CompletedLevelNumber = session.DisplayedLevelNumber;

            if (outcome == BattleOutcome.Victory)
            {
                var clearGold = economySettings != null
                    ? economySettings.GetStageClearGold(CompletedLevelNumber)
                    : CompletedLevelNumber * 45;

                if (wallet.TryRewardLevel(CompletedLevelNumber, clearGold))
                    LastStageClearGold = clearGold;

                var towerGold = simulation != null ? simulation.TowerBountyGoldEarned : 0;
                if (towerGold > 0)
                {
                    wallet.AddBonusGold(towerGold);
                    LastTowerBonusGold = towerGold;
                }

                LastRewardGold = LastStageClearGold + LastTowerBonusGold;

                highestClearedLevel = Mathf.Max(highestClearedLevel, CompletedLevelNumber);
                if (claimCards != null) claimCards.SetStageProgress(highestClearedLevel);

                failedAttempts = 0;
                var nextLevel = CompletedLevelNumber + 1;
                if (CompletedLevelNumber >= battleScenes.Length || !Application.CanStreamedLevelBeLoaded(SceneForLevel(nextLevel)))
                {
                    QueueScene(EndScene);
                    return;
                }
                nextBattleLevelNumber = nextLevel;
            }
            else
            {
                failedAttempts++;
                if (failedAttempts >= session.AttemptsPerLevel)
                {
                    livesExhausted = true;
                    session.RecoveryRequired = true;
                    return;
                }
                if (CompletedLevelNumber == 1)
                {
                    session.RecoveryRequired = true;
                    return;
                }
            }
            QueueScene(SettlementScene);
        }

        private void Unbind()
        {
            if (session != null) session.SimulationCreated -= Bind;
            if (simulation != null) simulation.BattleEnded -= OnBattleEnded;
            session = null;
            simulation = null;
        }
    }
}