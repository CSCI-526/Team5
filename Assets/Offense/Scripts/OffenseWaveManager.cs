using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ReverseTD.Defense;
using UnityEngine.UI;

namespace ReverseTD.Offense
{
    public class OffenseWaveManager : MonoBehaviour
    {

        public static OffenseWaveManager Instance { get; private set; }

        public static bool IsTutorialPaused { get; set; } = false;

        [Header("References")]
        [SerializeField, Tooltip("The board whose path the soldiers walk.")]
        private BoardView board;

        [SerializeField]
        private ForkCommandSlot forkCommandSlot;

        [SerializeField, Tooltip("Persistent upgrade and currency data.")]
        private OffenseData offenseData;

        [SerializeField, Tooltip("Attack range for spawned soldiers.")]
        private float soldierAttackRange = 2f;

        [Header("Wave Settings")]
        [SerializeField, Tooltip("Name of the upgrade scene to load once all units die.")]
        private string upgradeSceneName = "UpgradeScene";

        [SerializeField, Range(0.1f, 5f), Tooltip("Game speed.")]
        private float timeScale = 1f;

        [SerializeField] private Slider speedSlider;

        private readonly OffenseSoldier[] soldiers = new OffenseSoldier[SquadRules.CellCount];
        private int livingUnits = 0;
        private bool spawningComplete = false;
        private int spawnedCount = 0;
        private bool transitionQueued = false;
        private float healTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            IsTutorialPaused = false;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (offenseData != null)
            {
                timeScale = offenseData.gameSpeed;

                if (speedSlider != null)
                {
                    if (timeScale == 1f)
                        speedSlider.SetValueWithoutNotify(0);
                    else if (timeScale == 2f)
                        speedSlider.SetValueWithoutNotify(1);
                    else
                        speedSlider.SetValueWithoutNotify(2);
                }

                if (forkCommandSlot != null && offenseData.lastRouteCommand != RouteCommand.None)
                {
                    forkCommandSlot.SetCommand(offenseData.lastRouteCommand);
                }
            }

            SpawnWave();
        }

        private void Update()
        {
            Time.timeScale = IsTutorialPaused ? 0f : timeScale;

            if (forkCommandSlot != null && offenseData != null)
            {
                offenseData.lastRouteCommand = forkCommandSlot.CurrentCommand;
            }

            TickHealing();
        }

        private void TickHealing()
        {
            healTimer += Time.deltaTime;
            if (healTimer < SquadRules.HealInterval) return;
            healTimer -= SquadRules.HealInterval;

            float amount = SquadRules.HealRate(offenseData != null ? offenseData.upgradeLevels : null) * SquadRules.HealInterval;
            float reachSqr = SquadRules.HealReach * SquadRules.HealReach;

            for (int cell = 0; cell < soldiers.Length; cell++)
            {
                OffenseSoldier healer = soldiers[cell];
                if (healer == null || !healer.IsAlive || healer.Type != UnitType.Heal) continue;

                int row = cell / SquadRules.Columns;
                int column = cell % SquadRules.Columns;
                for (int r = row - 1; r <= row + 1; r++)
                {
                    for (int c = column - 1; c <= column + 1; c++)
                    {
                        if (r < 0 || r >= SquadRules.Rows || c < 0 || c >= SquadRules.Columns || (r == row && c == column)) continue;

                        OffenseSoldier neighbor = soldiers[r * SquadRules.Columns + c];
                        if (neighbor == null || !neighbor.IsAlive) continue;
                        if ((neighbor.Position - healer.Position).sqrMagnitude > reachSqr) continue;

                        neighbor.Heal(amount);
                    }
                }
            }
        }

        public void SetSpeedFromSlider(float value)
        {
            int index = Mathf.RoundToInt(value);

            if (index == 0)
                timeScale = 1f;
            else if (index == 1)
                timeScale = 2f;
            else
                timeScale = 4f;

            if (offenseData != null)
                offenseData.gameSpeed = timeScale;
        }

        private void OnDisable()
        {
            IsTutorialPaused = false;
            Time.timeScale = 1f;
        }

        [ContextMenu("Spawn Wave")]
        public void SpawnWave()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Spawn Wave only works in Play mode.", this);
                return;
            }

            if (board == null)
            {
                Debug.LogError("OffenseWaveManager has no board assigned.", this);
                return;
            }

            SpawnSquad();
        }

        private void SpawnSquad()
        {
            if (offenseData != null)
            {
                offenseData.EnsureSquad();
            }

            var upperLane = new LanePath(board.Path.Waypoints, SquadRules.CornerRadius);
            StagePath lowerPath = board.LowerPath;
            LanePath lowerLane = lowerPath != null ? new LanePath(lowerPath.Waypoints, SquadRules.CornerRadius) : null;
            float forkDistance = lowerLane != null ? upperLane.DivergeDistance(lowerLane) : float.MaxValue;
            spawningComplete = false;

            for (int cell = 0; cell < SquadRules.CellCount; cell++)
            {
                UnitType type = offenseData != null ? offenseData.squad[cell]
                    : (cell == SquadRules.CenterCell ? UnitType.Attack : UnitType.None);
                if (type == UnitType.None) continue;

                int row = cell / SquadRules.Columns;
                int column = cell % SquadRules.Columns;
                float startDistance = (SquadRules.Rows - 1 - row) * SquadRules.Spacing;
                float laneOffset = (1 - column) * SquadRules.Spacing;
                soldiers[cell] = SpawnSoldier(type, column, startDistance, laneOffset, upperLane, lowerLane, forkDistance);
            }

            spawningComplete = true;

            if (livingUnits <= 0)
            {
                QueueUpgradeTransition();
            }
        }

        private OffenseSoldier SpawnSoldier(UnitType type, int column, float startDistance, float laneOffset, LanePath upperLane, LanePath lowerLane, float forkDistance)
        {
            // Spawn inactive so combat registration occurs cleanly upon SetActive
            var soldierObject = new GameObject($"OffenseSoldier {spawnedCount++}");
            soldierObject.SetActive(false);
            soldierObject.transform.SetParent(transform, false);

            var soldier = soldierObject.AddComponent<OffenseSoldier>();
            soldier.Initialize(offenseData, type, column, startDistance, laneOffset, soldierAttackRange, upperLane, lowerLane, forkDistance, forkCommandSlot);

            soldierObject.SetActive(true);
            livingUnits++;
            return soldier;
        }

        public void ReportSoldierDeath()
        {
            if (transitionQueued)
            {
                return;
            }

            livingUnits--;

            if (spawningComplete && livingUnits <= 0)
            {
                QueueUpgradeTransition();
            }
        }

        private void QueueUpgradeTransition()
        {
            if (transitionQueued)
            {
                return;
            }

            transitionQueued = true;
            StartCoroutine(TransitionToUpgradeScene());
        }

        public void ReportGoalReached()
        {
            if (transitionQueued)
            {
                return;
            }

            if (offenseData == null)
            {
                Debug.LogError("OffenseData is missing.");
                return;
            }

            string currentScene = SceneManager.GetActiveScene().name;

            Debug.Log("Current Scene: " + currentScene);
            Debug.Log("Current Level: " + offenseData.currentLevel);

            if (currentScene == "DefenseSandbox")
            {
                Debug.Log("LEVEL 1 BRANCH EXECUTED");

                transitionQueued = true;

                offenseData.ResetLevelStats();
                offenseData.currentLevel = 2;

                SceneManager.LoadScene("Level2Intro");
            }
            else if (currentScene == "DefenseLevel2")
            {
                Debug.Log("LEVEL 2 BRANCH EXECUTED");

                // Level 2 completed
                transitionQueued = true;
                SceneManager.LoadScene("EndGame");
            }
        }

        private IEnumerator TransitionToUpgradeScene()
        {
            yield return new WaitForSeconds(1.2f);
            SceneManager.LoadScene(upgradeSceneName);
        }
    }
}