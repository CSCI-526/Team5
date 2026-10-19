using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ReverseTD.Defense;
using UnityEngine.UI;
using ReverseTD.UpgradeUI;
using TMPro;

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
        public bool IsSquadEditing { get; private set; }
        public bool AllowTutorialSquadEditing { get; set; }
        public bool IsDefeated { get; private set; }
        public bool IsCombatPaused => IsTutorialPaused || IsSquadEditing || IsDefeated;
        public LevelOneTutorialRewind TutorialRewind { get; private set; }

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
            DisableForkMechanicUI();
            EnsureEditFormationButton();

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

            if (gameObject.scene.name == "DefenseSandbox")
            {
                TutorialRewind = gameObject.AddComponent<LevelOneTutorialRewind>();
                TutorialRewind.Initialize(this, offenseData);
            }
            SpawnWave();
        }

        private void DisableForkMechanicUI()
        {
            if (forkCommandSlot != null)
            {
                forkCommandSlot.gameObject.SetActive(false);
                forkCommandSlot = null;
            }

            ForkCommandSlot[] slots = FindObjectsByType<ForkCommandSlot>(FindObjectsSortMode.None);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].gameObject.SetActive(false);
            }

            DraggableCommand[] commands = FindObjectsByType<DraggableCommand>(FindObjectsSortMode.None);
            for (int i = 0; i < commands.Length; i++)
            {
                if (commands[i] == null) continue;
                Transform parent = commands[i].transform.parent;
                if (parent != null && parent.name == "CommandBar")
                {
                    parent.gameObject.SetActive(false);
                }
                commands[i].gameObject.SetActive(false);
            }
        }

        private void EnsureEditFormationButton()
        {
            Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;
                for (int j = 0; j < btn.onClick.GetPersistentEventCount(); j++)
                {
                    if (btn.onClick.GetPersistentTarget(j) == this &&
                        btn.onClick.GetPersistentMethodName(j) == nameof(OpenSquadEditor))
                    {
                        return;
                    }
                }
            }

            Canvas targetCanvas = speedSlider != null ? speedSlider.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
            if (targetCanvas == null) return;

            var btnObj = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.layer = targetCanvas.gameObject.layer;
            var rect = (RectTransform)btnObj.transform;
            rect.SetParent(targetCanvas.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(192.6f, 20f);
            rect.sizeDelta = new Vector2(80f, 25f);

            var image = btnObj.GetComponent<Image>();
            image.color = Color.white;

            var button = btnObj.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OpenSquadEditor);

            var textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObj.layer = targetCanvas.gameObject.layer;
            var textRect = (RectTransform)textObj.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;

            var label = textObj.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                label.font = TMP_Settings.defaultFontAsset;
            }
            label.text = "Edit Formation";
            label.fontSize = 10f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private void Update()
        {
            Time.timeScale = IsCombatPaused ? 0f : timeScale;

            if (forkCommandSlot != null && offenseData != null)
            {
                offenseData.lastRouteCommand = forkCommandSlot.CurrentCommand;
            }

            if (!IsCombatPaused)
            {
                TickHealing();
            }
        }

        // Bind the player's custom button to this method in the Inspector.
        public void OpenSquadEditor()
        {
            if (IsSquadEditing || (IsTutorialPaused && !AllowTutorialSquadEditing) || transitionQueued || livingUnits <= 0 || offenseData == null)
            {
                return;
            }

            offenseData.EnsureSquad();
            IsSquadEditing = true;
            Time.timeScale = 0f;
            BattleSquadEditor.Open(this, offenseData);
        }

        public bool IsSoldierAliveAt(int cell)
        {
            return cell >= 0 && cell < soldiers.Length && soldiers[cell] != null && soldiers[cell].IsAlive;
        }

        public OffenseSoldier GetSoldierAt(int cell)
        {
            return IsSoldierAliveAt(cell) ? soldiers[cell] : null;
        }

        public int GetSoldierCell(OffenseSoldier soldier)
        {
            return soldier != null ? System.Array.IndexOf(soldiers, soldier) : -1;
        }

        public bool ApplySquadFormation(int[] sourceCells)
        {
            if (!IsSquadEditing || transitionQueued || offenseData == null || sourceCells == null || sourceCells.Length != soldiers.Length)
            {
                return false;
            }

            // Track individual units, including identical types, so HP and deaths are preserved.
            var seen = new bool[soldiers.Length];
            for (int cell = 0; cell < sourceCells.Length; cell++)
            {
                int source = sourceCells[cell];
                if (source < 0 || source >= soldiers.Length || seen[source]) return false;
                seen[source] = true;
            }

            var previousSoldiers = (OffenseSoldier[])soldiers.Clone();
            var previousSquad = (UnitType[])offenseData.squad.Clone();
            for (int cell = 0; cell < sourceCells.Length; cell++)
            {
                int source = sourceCells[cell];
                offenseData.squad[cell] = previousSquad[source];
                soldiers[cell] = previousSoldiers[source];
                if (IsSoldierAliveAt(cell))
                {
                    soldiers[cell].SetFormationCell(cell);
                }
            }
            return true;
        }

        public void EndSquadEdit()
        {
            // Scene teardown may already have reset the manager and global time scale.
            if (!IsSquadEditing) return;
            IsSquadEditing = false;
            Time.timeScale = IsTutorialPaused ? 0f : timeScale;
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
            AllowTutorialSquadEditing = false;
            IsSquadEditing = false;
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

                int column = cell % SquadRules.Columns;
                Vector2 formationOffset = SquadRules.CellOffset(cell, SquadRules.Spacing);
                soldiers[cell] = SpawnSoldier(type, column, formationOffset, upperLane, lowerLane, forkDistance);
            }

            spawningComplete = true;

            if (livingUnits <= 0)
            {
                QueueUpgradeTransition();
            }
        }

        private OffenseSoldier SpawnSoldier(UnitType type, int column, Vector2 formationOffset, LanePath upperLane, LanePath lowerLane, float forkDistance)
        {
            // Spawn inactive so combat registration occurs cleanly upon SetActive
            var soldierObject = new GameObject($"OffenseSoldier {spawnedCount++}");
            soldierObject.SetActive(false);
            soldierObject.transform.SetParent(transform, false);

            var soldier = soldierObject.AddComponent<OffenseSoldier>();
            soldier.Initialize(offenseData, type, column, formationOffset, soldierAttackRange, upperLane, lowerLane, forkDistance, forkCommandSlot);

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
            if (TutorialRewind != null)
            {
                IsDefeated = true;
                Time.timeScale = 0f;
                TutorialRewind.ShowDefeat();
                return;
            }
            StartCoroutine(TransitionToUpgradeScene());
        }

        internal System.Action CaptureTutorialState()
        {
            var savedSoldiers = (OffenseSoldier[])soldiers.Clone();
            int savedLiving = livingUnits;
            float savedHealTimer = healTimer;
            float savedSpeed = timeScale;
            return () =>
            {
                System.Array.Copy(savedSoldiers, soldiers, soldiers.Length);
                livingUnits = savedLiving;
                healTimer = savedHealTimer;
                timeScale = savedSpeed;
                transitionQueued = false;
                IsDefeated = false;
                IsSquadEditing = false;
                AllowTutorialSquadEditing = false;
                if (speedSlider != null) speedSlider.SetValueWithoutNotify(savedSpeed == 1f ? 0 : savedSpeed == 2f ? 1 : 2);
                if (forkCommandSlot != null && offenseData != null) forkCommandSlot.SetCommand(offenseData.lastRouteCommand);
            };
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
