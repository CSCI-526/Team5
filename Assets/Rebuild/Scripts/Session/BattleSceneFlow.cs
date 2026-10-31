using System.Collections;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Session
{
    // Real levels always bind settlement, including direct Editor Play. Prototype scenes stay standalone.
    public sealed class BattleSceneFlow : MonoBehaviour
    {
        public const string StartScene = "Assets/Rebuild/Scenes/StartGame.unity";
        public const string UpgradeScene = "Assets/Rebuild/Scenes/UpgradeScene.unity";
        public const string PrototypeBattleScene = "Assets/Rebuild/Scenes/SquadBattlePrototype.unity";
        public const string BattleScene = "Assets/Rebuild/Scenes/Level1GridBattle.unity";
        public const string LevelTwoScene = "Assets/Rebuild/Scenes/Level2GridBattle.unity";
        public const string LevelThreeScene = "Assets/Rebuild/Scenes/Level3GridBattle.unity";
        public const string LevelFourScene = "Assets/Rebuild/Scenes/Level4GridBattle.unity";
        private static readonly string[] battleScenes = { BattleScene, LevelTwoScene, LevelThreeScene, LevelFourScene };
        public static string SceneForLevel(int level)
        {
            if(level < 1 || level > battleScenes.Length) throw new System.ArgumentOutOfRangeException(nameof(level));
            return battleScenes[level-1];
        }
        private static bool IsCampaignScene(string path) => System.Array.IndexOf(battleScenes,path) >= 0;
        public const string SettlementScene = "Assets/Rebuild/Scenes/LevelSettlement.unity";
        public const string EndScene = "Assets/Rebuild/Scenes/EndGame.unity";
        private static string previewScenePath;
        public static string NextBattleScenePath => SceneForLevel(NextBattleLevelNumber);
        public static bool IsPreviewScene(Scene scene) => previewScenePath != null && scene.path == previewScenePath;
        public static bool TryBeginLevelPreview()
        {
            if (current == null || current.loading || current.livesExhausted || previewScenePath != null || SceneManager.GetActiveScene().path != UpgradeScene) return false;
            previewScenePath = NextBattleScenePath;
            return true;
        }
        public static void EndLevelPreview(string path) { if (previewScenePath == path) previewScenePath = null; }
        private static BattleSceneFlow current;
        private BattleSession session;
        private BattleSimulation simulation;
        private bool loading;
        private SquadPreparation preparation;
        private ClaimCardShop claimCards;
        public static ClaimCardShop CardShop => current != null ? current.claimCards : null;
        public static IReadOnlyList<ClaimCard> OpenClaimCards()
        {
            if (current == null || current.loading || current.livesExhausted || SceneManager.GetActiveScene().path != UpgradeScene || current.claimCards == null)
                return System.Array.Empty<ClaimCard>();
            return current.claimCards.BeginOffer();
        }
        public static bool TryClaimCard(ClaimCard card) => current != null && !current.loading && !current.livesExhausted &&
            SceneManager.GetActiveScene().path == UpgradeScene && current.claimCards != null && current.claimCards.TryPurchase(card);
        private IReadOnlyList<MemberSpawnDefinition> deployment;
        private int nextBattleLevelNumber = 1;
        private int failedAttempts;
        private bool settled;
        private bool livesExhausted;
        public static bool IsOutOfLives => current != null && current.livesExhausted;
        public static void RestartCurrentLevel()
        {
            if (current == null || current.loading || current.livesExhausted || current.session == null || !current.session.RecoveryRequired || current.failedAttempts >= current.session.AttemptsPerLevel) return;
            current.QueueScene(current.session.gameObject.scene.path);
        }
        public static bool RewindLevelOne() => current != null && !current.loading && current.session != null && current.session.RewindTutorialCheckpoint();
        private CampaignWallet wallet;
        private GameEconomySettings economySettings;
        private readonly List<UnitRole> pendingUnitRewards = new List<UnitRole>();
        public static int PendingUnitRewardCount => current != null ? current.pendingUnitRewards.Count : 0;
        public static int LastRewardGold { get; private set; }
        public static IReadOnlyList<UnitRole> LastRewardUnits { get; private set; } = System.Array.Empty<UnitRole>();
        private static void ClearLastReward()
        {
            LastRewardGold = 0;
            LastRewardUnits = System.Array.Empty<UnitRole>();
        }
        public static int GoldBalance => current != null && current.wallet != null ? current.wallet.Balance : 0;

        // Shop controllers use this only after validating the product and its destination.
        public static bool TrySpendGold(int price)
        {
            if (price <= 0) throw new System.ArgumentOutOfRangeException(nameof(price));
            return current != null && !current.loading && SceneManager.GetActiveScene().path == UpgradeScene
                && current.wallet.TrySpend(price);
        }
        public static BattleOutcome LastOutcome { get; private set; }
        public static int CompletedLevelNumber { get; private set; }
        public static bool IsRunActive => current != null;
        public static int NextBattleLevelNumber => current != null ? current.nextBattleLevelNumber : 1;
        public static int CurrentLevelLivesRemaining => System.Math.Max(0, BattleSession.DefaultLivesPerLevel - (current != null ? current.failedAttempts : 0));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= EnsureLevelFlow;
            current = null;
            previewScenePath = null;
            LastOutcome = BattleOutcome.Running;
            CompletedLevelNumber = 0;
            ClearLastReward();
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
            if (!IsCampaignScene(scene.path) || current != null) return;
            // This new listener was not present when sceneLoaded began dispatching.
            GetOrCreate().OnSceneLoaded(scene, mode);
        }

        public static void BeginRun()
        {
            var flow = GetOrCreate();
            if (flow.loading || previewScenePath != null) return;
            flow.preparation = null;
            flow.claimCards = null;
            flow.deployment = null;
            flow.nextBattleLevelNumber = 1;
            flow.failedAttempts = 0;
            flow.livesExhausted = false;
            flow.pendingUnitRewards.Clear();
            ClearLastReward();
            flow.wallet = new CampaignWallet(flow.economySettings != null ? flow.economySettings.StartingGold : 0);
            LastOutcome = BattleOutcome.Running;
            CompletedLevelNumber = 0;
            flow.QueueScene(BattleScene);
        }
        public static void ContinueToUpgrade()
        {
            var flow = GetOrCreate();
            if (!flow.livesExhausted) flow.QueueScene(UpgradeScene);
        }
        public static SquadPreparation PrepareSquad(SquadRoster roster)
        {
            var flow = GetOrCreate();
            if (flow.preparation == null)
            {
                flow.preparation = new SquadPreparation(roster);
                flow.claimCards = new ClaimCardShop(flow.wallet, flow.preparation);
            }
            if (flow.preparation.Roster != roster) throw new System.InvalidOperationException("Preparation and battle must share the same roster.");
            while (flow.pendingUnitRewards.Count > 0 && flow.preparation.TryAddRewardMember(flow.pendingUnitRewards[0]))
                flow.pendingUnitRewards.RemoveAt(0);
            return flow.preparation;
        }
        public static void StartPreparedBattle(SquadPreparation draft)
        {
            var flow = GetOrCreate();
            if (flow.loading || previewScenePath != null) return;
            if (draft == null || draft != flow.preparation) throw new System.InvalidOperationException("No current squad preparation.");
            if (flow.livesExhausted) return;
            flow.deployment = draft.CaptureDeployment();
            flow.QueueScene(SceneForLevel(flow.nextBattleLevelNumber));
        }
        public static void ReturnToStart() => GetOrCreate().QueueScene(StartScene);

        private static BattleSceneFlow GetOrCreate()
        {
            if (current != null) return current;
            current = new GameObject("Battle Scene Flow").AddComponent<BattleSceneFlow>();
            current.economySettings = Resources.Load<GameEconomySettings>("RebuildEconomy");
            current.wallet = new CampaignWallet(current.economySettings != null ? current.economySettings.StartingGold : 0);
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
            // Never unload a Scene from inside a simulation tick or button event dispatch.
            yield return null;
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            loading = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsPreviewScene(scene)) return;
            loading = false;
            Unbind();
            if (scene.path == EndScene || scene.path == UpgradeScene || scene.path == SettlementScene) return;
            if (!IsCampaignScene(scene.path)) { Destroy(gameObject); return; }
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
                if (session.InitialRoster != preparation.Roster) throw new System.InvalidOperationException("Battle roster differs from preparation.");
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
            CompletedLevelNumber = session.DisplayedLevelNumber;
            ClearLastReward();
            if (outcome == BattleOutcome.Victory)
            {
                var reward = economySettings != null ? economySettings.RewardForLevel(CompletedLevelNumber) : null;
                var units = reward != null ? reward.CaptureUnitRoles() : System.Array.Empty<UnitRole>();
                if (wallet.TryRewardLevel(CompletedLevelNumber, reward != null ? reward.gold : 0))
                {
                    pendingUnitRewards.AddRange(units);
                    LastRewardGold = reward != null ? reward.gold : 0;
                    LastRewardUnits = System.Array.AsReadOnly(units);
                }
                failedAttempts = 0;
                if (CompletedLevelNumber >= battleScenes.Length) { QueueScene(EndScene); return; }
                nextBattleLevelNumber = CompletedLevelNumber + 1;
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
