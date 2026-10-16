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
        [SerializeField, Min(0f), Tooltip("Seconds between soldier spawns.")]
        private float interval = 0.6f;

        [SerializeField, Tooltip("Name of the upgrade scene to load once all units die.")]
        private string upgradeSceneName = "UpgradeScene";

        [SerializeField, Range(0.1f, 5f), Tooltip("Game speed.")]
        private float timeScale = 1f;

        [SerializeField] private Slider speedSlider;

        private int livingUnits = 0;
        private bool spawningComplete = false;
        private int spawnedCount = 0;
        private bool transitionQueued = false;

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
            }

            SpawnWave();
        }

        private void Update()
        {
            Time.timeScale = IsTutorialPaused ? 0f : timeScale;
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

            StartCoroutine(SpawnSoldiersRoutine());
        }

        private IEnumerator SpawnSoldiersRoutine()
        {
            int count = offenseData != null ? offenseData.TotalSpawnCount : 1;
            spawningComplete = false;

            for (int i = 0; i < count; i++)
            {
                SpawnSoldier();
                yield return new WaitForSeconds(interval);
            }

            spawningComplete = true;

            if (livingUnits <= 0)
            {
                QueueUpgradeTransition();
            }
        }

        private void SpawnSoldier()
        {
            // Spawn inactive so combat registration occurs cleanly upon SetActive
            var soldierObject = new GameObject($"OffenseSoldier {spawnedCount++}");
            soldierObject.SetActive(false);
            soldierObject.transform.SetParent(transform, false);

            var soldier = soldierObject.AddComponent<OffenseSoldier>();
            soldier.Initialize(board.Path,offenseData,soldierAttackRange,board.LowerPath,forkCommandSlot);

            soldierObject.SetActive(true);
            livingUnits++;
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