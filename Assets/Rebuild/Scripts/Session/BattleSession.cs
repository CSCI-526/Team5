using System;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Session
{
    [Serializable]
    public sealed class SquadMemberConfiguration
    {
        public bool enabled = true;
        [Min(1)] public int memberId = 1;
        [Range(0, 8)] public int slotId = 4;
        public UnitRole role = UnitRole.Attack;
        [Min(0)] public float spawnTime;
    }

    [DisallowMultipleComponent]
    public sealed class BattleSession : MonoBehaviour
    {
        [SerializeField] private BattleGrid grid;
        [SerializeField] private SceneRoute route;
        [SerializeField] private List<BattleTower> towers = new List<BattleTower>();
        [SerializeField, Min(1)] private int unitCount = 12;
        [SerializeField, Min(0.01f)] private float unitHp = 65;
        [SerializeField, Min(0.01f)] private float unitSpeed = 1.4f;
        [SerializeField, Min(0.01f)] private float spawnInterval = 0.65f;
        [SerializeField, Min(1)] private int goalLives = 3;
        [SerializeField] private int formationSize = 1;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool logEvents = true;
        [SerializeField] private bool squadCombat;
        [SerializeField] private List<SquadMemberConfiguration> members = new List<SquadMemberConfiguration>();
        [SerializeField] private SquadRoster initialRoster;
        [SerializeField] private bool useSceneRosterOnly;
        public bool UsesSceneRosterOnly => useSceneRosterOnly;
        private IReadOnlyList<MemberSpawnDefinition> preparedMembers;
        private IEnumerable<SquadMemberConfiguration> ConfiguredMembers => initialRoster != null ? initialRoster.Members : members;
        public SquadRoster InitialRoster => initialRoster;

        public void SetPreparedMembers(IReadOnlyList<MemberSpawnDefinition> deployment)
        {
            if (useSceneRosterOnly) throw new InvalidOperationException("This battle uses its fixed scene roster.");
            if (Simulation != null || advancing) throw new InvalidOperationException("Set deployment before starting the battle.");
            if (deployment == null || deployment.Count < 1 || deployment.Count > SquadPreparation.MaxDeployed ||
                deployment.Any(member => member == null || member.Unit.Role == UnitRole.Test) ||
                deployment.Select(member => member.MemberId).Distinct().Count() != deployment.Count ||
                deployment.Select(member => member.SlotId).Distinct().Count() != deployment.Count)
                throw new ArgumentException("Deployment requires 1..6 unique members and unique slots.");
            preparedMembers = Array.AsReadOnly(deployment.ToArray());
        }

        private IReadOnlyList<MemberSpawnDefinition> CaptureSquadMembers()
        {
            if (preparedMembers != null) return preparedMembers;
            // Direct Editor play uses the first six enabled configured members for debugging.
            var result = new List<MemberSpawnDefinition>();
            foreach (var member in ConfiguredMembers)
            {
                if (member == null) throw new ArgumentException("Missing member configuration.");
                if (!member.enabled) continue;
                result.Add(new MemberSpawnDefinition(member.memberId, member.slotId, SquadCombatRules.Create(member.role), member.spawnTime));
                if (result.Count == SquadPreparation.MaxDeployed) break;
            }
            return result.AsReadOnly();
        }
        [SerializeField, Min(1)] private int displayedLevelNumber = 1;
        public const int DefaultLivesPerLevel = 3;
        [SerializeField, Min(1)] private int attemptsPerLevel = DefaultLivesPerLevel;
        private int failedAttempts;
        internal void RestoreFailedAttempts(int count) => failedAttempts = Math.Max(0, count);
        private readonly List<BattleTower> runtimeTowers = new List<BattleTower>();
        private bool advancing;
        public BattleGrid Grid => grid;
        public SceneRoute Route => route;
        // Unity leaves a null list entry when an authored tower is deleted from the Scene.
        // All consumers use the same surviving order so simulation IDs and views stay aligned.
        public IReadOnlyList<BattleTower> Towers => towers.Where(tower => tower != null).ToArray();
        public BattleSimulation Simulation { get; private set; }
        public bool SquadCombat => squadCombat;
        public int DisplayedLevelNumber => displayedLevelNumber;
        public int AttemptsPerLevel => attemptsPerLevel;
        public int AttemptsRemaining => Math.Max(0, attemptsPerLevel - failedAttempts);
        public int PlaybackSpeed { get; private set; } = 1;
        public const float BaseTimeMultiplier = 0.8f;
        [SerializeField, Min(0.01f)] private float levelTimeMultiplier = 1;
        public float LevelTimeMultiplier => levelTimeMultiplier;
        public const float InspectionTimeMultiplier = 0.1f;
        public bool IsUnitInspectionActive { get; private set; }
        public float EffectivePlaybackSpeed => BaseTimeMultiplier * levelTimeMultiplier * (IsUnitInspectionActive ? InspectionTimeMultiplier : PlaybackSpeed);
        public void SetUnitInspectionActive(bool active) => IsUnitInspectionActive = active;
        // Presentation can subscribe before the first tick, and resubscribe after a restart.
        public event Action<BattleSimulation> SimulationCreated;
        public event Action TutorialRewindRequested;
        private BattleSimulation.Checkpoint tutorialCheckpoint;
        private int checkpointSpeed;
        private bool checkpointPaused;
        public bool RecoveryRequired { get; internal set; }
        public bool HasTutorialCheckpoint => tutorialCheckpoint != null;
        public bool CheckpointPausedBeforeHint => checkpointPaused;

        public void CaptureTutorialCheckpoint(bool pausedBeforeHint)
        {
            if (advancing || displayedLevelNumber != 1 || Simulation == null) return;
            tutorialCheckpoint = Simulation.CaptureCheckpoint();
            checkpointSpeed = PlaybackSpeed;
            checkpointPaused = pausedBeforeHint;
        }

        internal bool RewindTutorialCheckpoint()
        {
            if (advancing || !RecoveryRequired || AttemptsRemaining == 0 || tutorialCheckpoint == null) return false;
            var replayingHint = TutorialRewindRequested != null;
            TutorialRewindRequested?.Invoke();
            var restored = tutorialCheckpoint.Restore();
            restored.SetPaused(replayingHint || checkpointPaused);
            PlaybackSpeed = checkpointSpeed;
            RecoveryRequired = false;
            IsUnitInspectionActive = false;
            BindSimulation(restored);
            return true;
        }

        private void Start()
        {
            if (BattleSceneFlow.IsPreviewScene(gameObject.scene)) return;
            if (autoStart) StartBattle();
        }

        private void FixedUpdate()
        {
            if (Simulation != null && Simulation.Outcome == BattleOutcome.Running) AdvancePlaybackFrame(Time.fixedDeltaTime);
        }

        public void SetPlaybackSpeed(int multiplier)
        {
            if (multiplier < 1 || multiplier > 3) throw new ArgumentOutOfRangeException(nameof(multiplier));
            PlaybackSpeed = multiplier;
        }

        // Inspection overrides the slider: one slower tick per frame, without global Time.timeScale.
        public void AdvancePlaybackFrame(float fixedDeltaTime)
        {
            if (!float.IsFinite(fixedDeltaTime) || fixedDeltaTime <= 0) throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            var ticks = IsUnitInspectionActive ? 1 : PlaybackSpeed;
            var step = fixedDeltaTime * BaseTimeMultiplier * levelTimeMultiplier * (IsUnitInspectionActive ? InspectionTimeMultiplier : 1f);
            for (var i = 0; i < ticks && Simulation != null && Simulation.Outcome == BattleOutcome.Running && !Simulation.IsPaused; i++)
                Advance(step);
        }

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (!float.IsFinite(levelTimeMultiplier) || levelTimeMultiplier <= 0) errors.Add("Level time multiplier must be finite and positive.");
            if (grid == null || route == null) { errors.Add("BattleSession requires a BattleGrid and SceneRoute."); return errors; }
            if (grid.gameObject.scene != gameObject.scene || route.gameObject.scene != gameObject.scene)
                errors.Add("Grid and route must belong to the session's Scene.");
            grid.RebuildIndex(errors);
            route.CollectErrors(grid, errors);
            if ((formationSize != 1 && formationSize != 3) || route.RequiredWidth != formationSize)
                errors.Add("Formation size must be 1 or 3 and match the route's required width.");
            if (formationSize == 3 && unitCount > 9)
                errors.Add("This prototype supports one squad of up to nine independent members.");
            var seen = new HashSet<BattleTower>();
            var towerFootprints = new HashSet<Vector3Int>();
            foreach (var tower in towers)
            {
                if (tower == null) continue;
                if (!seen.Add(tower)) errors.Add($"Duplicate tower reference: {tower.name}.");
                if (tower.gameObject.scene != gameObject.scene || !tower.isActiveAndEnabled)
                    errors.Add($"{tower.name}: tower must be active in this Scene.");
                if (!tower.HasValidConfiguration) errors.Add($"{tower.name}: invalid tower stats or position.");
                tower.CollectFootprintErrors(grid, towerFootprints, errors);
                // Support is determined from this same world position; there are no tower slots.
                var position = tower.transform.position;
                var supportKey = new Vector3Int(Mathf.RoundToInt(position.x),
                    Mathf.RoundToInt((position.y - BattleCell.LayerHeight / 2) / BattleCell.LayerHeight), Mathf.RoundToInt(position.z));
                if (!grid.TryGetCell(supportKey, out var support) || support.Kind != CellKind.TowerPlatform ||
                    !grid.HasClearTop(support) || Mathf.Abs(position.y - support.TopCenter.y) > 0.001f ||
                    Mathf.Abs(position.x - support.TopCenter.x) > 0.5f || Mathf.Abs(position.z - support.TopCenter.z) > 0.5f)
                    errors.Add($"{tower.name}: tower Transform must stand on a clear TowerPlatform top surface.");
            }
            if (unitCount <= 0 || goalLives <= 0 || !float.IsFinite(unitHp) || unitHp <= 0 ||
                !float.IsFinite(unitSpeed) || unitSpeed <= 0 || !float.IsFinite(spawnInterval) || spawnInterval <= 0)
                errors.Add("Unit count, HP, speed, spawn interval and goal lives must be positive and finite.");
            if (squadCombat)
            {
                if (formationSize != 3) errors.Add("Squad combat requires the 3x3 formation.");
                var ids = new HashSet<int>();
                var slots = new HashSet<int>();
                try
                {
                    var captured = CaptureSquadMembers();
                    foreach (var member in captured)
                        if (!ids.Add(member.MemberId) || !slots.Add(member.SlotId) || member.Unit.Role == UnitRole.Test)
                            errors.Add("Members require unique IDs, unique slots and a combat role.");
                    if (captured.Count < 1 || captured.Count > SquadPreparation.MaxDeployed)
                        errors.Add("Squad combat requires 1..6 members.");
                }
                catch (ArgumentException error) { errors.Add(error.Message); }
            }
            return errors;
        }

        public BattleDefinition CaptureDefinition()
        {
            var errors = ValidateConfiguration();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            var towerDefinitions = new List<TowerDefinition>();
            var sceneTowers = Towers;
            for (var i = 0; i < sceneTowers.Count; i++) towerDefinitions.Add(sceneTowers[i].CaptureDefinition(i + 1));
            if (squadCombat)
            {
                var spawns = CaptureSquadMembers();
                var passable = new List<Vector3Int>();
                foreach (var cell in grid.SceneCells)
                    if (cell.IsRouteSurface && grid.HasClearTop(cell)) passable.Add(cell.Occupancy);
                return new BattleDefinition(route.CaptureTopCenters(), towerDefinitions, spawns, passable);
            }
            return new BattleDefinition(route.CaptureTopCenters(), towerDefinitions,
                new UnitDefinition(unitHp, unitSpeed), unitCount, spawnInterval, goalLives, formationSize);
        }

        [ContextMenu("Start / Restart Battle")]
        public void StartBattle()
        {
            if (BattleSceneFlow.IsPreviewScene(gameObject.scene)) return;
            if (!Application.isPlaying) { Debug.LogWarning("Start Battle requires Play Mode.", this); return; }
            if (advancing) throw new InvalidOperationException("Cannot restart a battle from a tick event.");
            IsUnitInspectionActive = false;
            var errors = ValidateConfiguration();
            if (errors.Count > 0)
            {
                ClearRuntimeBindings();
                Simulation = null;
                Debug.LogError("Battle configuration invalid:\n" + string.Join("\n", errors), this);
                return;
            }
            var next = new BattleSimulation(CaptureDefinition());
            tutorialCheckpoint = null;
            RecoveryRequired = false;
            BindSimulation(next);
        }

        private void BindSimulation(BattleSimulation next)
        {
            ClearRuntimeBindings();
            Simulation = next;
            next.BattleEnded += result =>
            {
                if (ReferenceEquals(Simulation, next) && result.Outcome == BattleOutcome.Defeat) failedAttempts++;
            };
            runtimeTowers.AddRange(Towers);
            for (var i = 0; i < runtimeTowers.Count; i++) runtimeTowers[i].BindRuntimeState(Simulation.Towers[i]);
            if (logEvents)
            {
                Simulation.UnitSpawned += value => Debug.Log($"UnitSpawned #{value.UnitId} at {value.Position}", this);
                Simulation.UnitDamaged += value => Debug.Log($"UnitDamaged #{value.UnitId}: -{value.Amount}, HP {value.RemainingHp}", this);
                Simulation.UnitDied += value => Debug.Log($"UnitDied #{value.UnitId}", this);
                Simulation.UnitReachedGoal += value => Debug.Log($"UnitReachedGoal #{value.UnitId}", this);
                Simulation.BattleEnded += value => Debug.Log($"BattleEnded {value.Outcome}: killed {value.Killed}, reached {value.ReachedGoal}", this);
                Simulation.TowerDamaged += value => Debug.Log($"TowerDamaged #{value.TowerId}: -{value.Amount}, HP {value.RemainingHp}", this);
                Simulation.UnitHealed += value => Debug.Log($"UnitHealed #{value.MemberId}: +{value.Amount}, HP {value.RemainingHp}", this);
            }
            SimulationCreated?.Invoke(Simulation);
        }

        public void Advance(float deltaTime)
        {
            if (Simulation == null) throw new InvalidOperationException("Start the battle first.");
            if (advancing) throw new InvalidOperationException("Cannot advance recursively from a tick event.");
            advancing = true;
            try
            {
                // No logical tower clone: runtime positions always come from the original Scene entities.
                for (var i = 0; i < runtimeTowers.Count; i++)
                    if (runtimeTowers[i] != null) Simulation.SetTowerPosition(i + 1, runtimeTowers[i].transform.position);
                Simulation.Step(deltaTime);
            }
            finally { advancing = false; }
        }

        [ContextMenu("Validate Scene Configuration")]
        private void LogValidation()
        {
            var errors = ValidateConfiguration();
            if (errors.Count == 0) Debug.Log("Battle scene configuration is valid.", this);
            else Debug.LogError(string.Join("\n", errors), this);
        }

        private void ClearRuntimeBindings()
        {
            foreach (var tower in runtimeTowers) if (tower != null) tower.BindRuntimeState(null);
            runtimeTowers.Clear();
        }
        private void OnDestroy() => ClearRuntimeBindings();
    }
}
