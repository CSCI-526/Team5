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

        [Header("References")]
        [SerializeField, Tooltip("The board whose path the soldiers walk.")]
        private BoardView board;

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

        private bool waveStarted;
        private bool transitionStarted;
        private int livingUnits = 0;
        private bool spawningComplete = false;
        private int spawnedCount = 0;

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
            Time.timeScale = timeScale;
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

            // Begin statistics once; repeated requests must not clear an active run.
            if (waveStarted) return;
            waveStarted = true;
            if (offenseData != null) offenseData.RunStats.Begin();
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
                StartCoroutine(TransitionToUpgradeScene());
            }
        }

        private void SpawnSoldier()
        {
            // Spawn inactive so combat registration occurs cleanly upon SetActive
            var soldierObject = new GameObject($"OffenseSoldier {spawnedCount++}");
            soldierObject.SetActive(false);
            soldierObject.transform.SetParent(transform, false);

            var soldier = soldierObject.AddComponent<OffenseSoldier>();
            soldier.Initialize(board.Path, offenseData, soldierAttackRange);

            soldierObject.SetActive(true);
            livingUnits++;
        }

        public void ReportSoldierDeath()
        {
            livingUnits--;

            if (spawningComplete && livingUnits <= 0)
            {
                StartCoroutine(TransitionToUpgradeScene());
            }
        }

        private IEnumerator TransitionToUpgradeScene()
        {
            // Multiple death notifications must not freeze or transition the same run twice.
            if (transitionStarted) yield break;
            transitionStarted = true;
            yield return new WaitForSeconds(1.2f);
            // Preserve any success already recorded at the path endpoint.
            if (offenseData != null && !offenseData.RunStats.Complete(false)) yield break;
            SceneManager.LoadScene(upgradeSceneName);
        }
    }
}