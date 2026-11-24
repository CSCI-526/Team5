using System.Collections;
using Team5.Rebuild.Analytics;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Four short, non-modal hints; the player's movement is never paused or blocked.
    // World/UI arrows track their targets while the camera follows the squad.
    [DisallowMultipleComponent]
    public sealed class ClickMoveTutorial : MonoBehaviour
    {
        private static bool played;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlayed() => played = false;

        [SerializeField] private BattleSession session;
        private BattleSimulation simulation;
        private Coroutine routine;
        private RectTransform canvasRoot, arrow;
        private readonly Vector3[] buttonCorners = new Vector3[4];
        private TMP_Text title, description;
        private Button nextButton;
        private bool next;
        private System.Action<UnitRole, int, Vector3> activeSkillListener;
        private Vector3 worldTarget;
        private RectTransform uiTarget;
        private bool pointToWorld;

        public bool IsActive => routine != null;
        public int CurrentStep { get; private set; }

        private void OnEnable()
        {
            if (session == null) session = GetComponent<BattleSession>();
            if (session == null || BattleSceneFlow.IsPreviewScene(gameObject.scene)) return;
            session.SimulationCreated += Bind;
            Bind(session.Simulation);
        }

        private void Start() { if (session != null) Bind(session.Simulation); }

        private void Bind(BattleSimulation value)
        {
            StopTutorial();
            simulation = value;
            if (value != null && value.ContinuousMovementEnabled && isActiveAndEnabled &&
                !played && session.DisplayedLevelNumber == 1)
                routine = StartCoroutine(Guide());
        }

        private bool Running => simulation != null && session != null && session.Simulation == simulation &&
            simulation.Outcome == BattleOutcome.Running;

        private IEnumerator Guide()
        {
            while (Running && (simulation.PendingSpawnCount > 0 || simulation.AliveCount == 0)) yield return null;
            if (!Running) { routine = null; yield break; }
            played = true;
            Build();
            var cells = session.Route.Cells;
            worldTarget = cells[Mathf.Min(cells.Count - 1, 5)].TopCenter;
            Show(1, "CLICK TO MOVE", "Click the road ahead. Press Space or click Pause to pause/resume.", true);
            var moved = false;
            while (Running && !moved && !next)
            {
                moved = simulation.IsSquadMoving || Vector3.Distance(simulation.SquadDestination, cells[0].TopCenter) > 1.1f;
                yield return null;
            }
            if (!Running || next) { Finish(); yield break; }
            next = false;

            worldTarget = session.Towers.Count > 0 ? session.Towers[0].transform.position :
                cells[Mathf.Min(cells.Count - 1, 8)].TopCenter;
            Show(2, "DODGE ENEMIES", "Steer around towers and traps. Skills use the numbered keys shown on their icons.", true);
            yield return WaitForNextOrSeconds(3.5f);
            if (!Running) { Finish(); yield break; }

            // Skills are built in the HUD Update, not necessarily before the tutorial.
            Button button = null;
            var findUntil = Time.unscaledTime + 2f;
            while (Running && button == null)
            {
                button = FindCaptainButton();
                if (button != null || !HasCaptain() || Time.unscaledTime >= findUntil) break;
                yield return null;
            }
            if (button != null)
            {
                uiTarget = (RectTransform)button.transform;
                var panel = button.GetComponentInParent<LegendarySkillPanel>();
                var key = panel != null ? panel.HotkeyForRole(UnitRole.Captain) : "";
                var instruction = key.Length > 0 ? $"Press {key} or click the Captain skill to call reinforcements." :
                    "Click the Captain skill to call reinforcements.";
                Show(3, "CAPTAIN: CALL BACKUP", instruction + " Full squads strike towers.", false);
                var used = false;
                activeSkillListener = (role, member, position) =>
                { if (role == UnitRole.Captain) used = true; };
                simulation.LegendarySkillUsed += activeSkillListener;
                while (Running && !used && !next && HasCaptain()) yield return null;
                simulation.LegendarySkillUsed -= activeSkillListener;
                activeSkillListener = null;
                if (!Running) { Finish(); yield break; }
            }
            next = false;

            worldTarget = cells[cells.Count - 1].TopCenter;
            Show(4, "REACH THE GOAL", "Keep the squad inside EXIT for 2 seconds. You can keep moving inside.", true);
            yield return WaitForNextOrSeconds(3f);
            if (Running) TutorialAnalytics.Complete();
            Finish();
        }

        private IEnumerator WaitForNextOrSeconds(float seconds)
        {
            var elapsed = 0f;
            while (Running && !next && elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            next = false;
        }

        private void Build()
        {
            if (canvasRoot != null) { canvasRoot.gameObject.SetActive(true); return; }
            var root = new GameObject("Click-to-Move Tutorial", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            LegacyUiKit.CreateCanvas(root, 920, new Vector2(1280, 720), .5f);
            canvasRoot = (RectTransform)root.transform;
            var banner = LegacyUiKit.CreateImage(canvasRoot, "Hint Banner", new Vector2(0, -53),
                new Vector2(660, 90), new Color(.07f, .12f, .19f, .92f));
            banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(.5f, 1);
            banner.raycastTarget = false;
            title = LegacyUiKit.CreateText(banner.transform, "Heading", "", new Vector2(0, 18),
                new Vector2(600, 38), 24, LegacyUiKit.Gold, FontStyles.Bold);
            title.raycastTarget = false;
            description = LegacyUiKit.CreateText(banner.transform, "Help", "", new Vector2(0, -19),
                new Vector2(620, 34), 16, Color.white);
            description.raycastTarget = false;
            description.textWrappingMode = TextWrappingModes.Normal;
            description.enableAutoSizing = true;
            description.fontSizeMin = 12f;
            description.fontSizeMax = 16f;
            nextButton = LegacyUiKit.CreateButton(canvasRoot, "Next Hint", new Vector2(300, -121),
                new Vector2(118, 38), LegacyUiKit.Green, 8f);
            var rect = (RectTransform)nextButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            LegacyUiKit.CreateText(rect, "Next Label", "NEXT / SKIP", Vector2.zero,
                new Vector2(112, 30), 14, Color.white, FontStyles.Bold);
            nextButton.onClick.AddListener(Continue);
            arrow = LegacyUiKit.CreateRect(canvasRoot, "Animated Tutorial Pointer", Vector2.zero, new Vector2(76, 76));
            // Triangle sprite points RIGHT by default. Add a shaft and dark outline.
            LegacyUiKit.CreateImage(arrow, "Shaft outline", new Vector2(-18, 0), new Vector2(44, 22), Color.black, 4);
            var outline = LegacyUiKit.CreateImage(arrow, "Head outline", new Vector2(12, 0), new Vector2(52, 52), Color.black);
            outline.sprite = NextMapShapes.Triangle;
            LegacyUiKit.CreateImage(arrow, "Shaft", new Vector2(-18, 0), new Vector2(40, 14), LegacyUiKit.Gold, 3);
            var shape = LegacyUiKit.CreateImage(arrow, "Head", new Vector2(12, 0), new Vector2(42, 42), LegacyUiKit.Gold);
            shape.sprite = NextMapShapes.Triangle;
            arrow.SetAsLastSibling();
        }

        private void Show(int step, string heading, string line, bool world)
        {
            CurrentStep = step; next = false; pointToWorld = world;
            if (world) uiTarget = null;
            title.text = step + "/4  " + heading;
            description.text = line;
            if (canvasRoot != null) canvasRoot.gameObject.SetActive(true);
            TutorialAnalytics.Step(step, 4, heading);
        }

        private bool HasCaptain()
        {
            if (simulation == null) return false;
            foreach (var unit in simulation.Units)
                if (unit.IsAlive && unit.Role == UnitRole.Captain) return true;
            return false;
        }

        private Button FindCaptainButton()
        {
            foreach (var panel in FindObjectsByType<LegendarySkillPanel>(FindObjectsSortMode.None))
            {
                if (panel.gameObject.scene != gameObject.scene) continue;
                var button = panel.ButtonForRole(UnitRole.Captain);
                if (button != null && button.gameObject.activeInHierarchy) return button;
            }
            return null;
        }

        private void LateUpdate()
        {
            if (canvasRoot == null || !canvasRoot.gameObject.activeSelf || arrow == null) return;
            Vector2 screen;
            Vector2 direction;
            if (pointToWorld)
            {
                var camera = Camera.main;
                if (camera == null) { arrow.gameObject.SetActive(false); return; }
                var p = camera.WorldToScreenPoint(worldTarget);
                if (p.z <= 0) { arrow.gameObject.SetActive(false); return; }
                screen = new Vector2(p.x, p.y);
                direction = Vector2.down;
            }
            else
            {
                if (uiTarget == null || !uiTarget.gameObject.activeInHierarchy)
                {
                    var button = FindCaptainButton();
                    if (button == null) { arrow.gameObject.SetActive(false); return; }
                    uiTarget = (RectTransform)button.transform;
                }
                var canvas = uiTarget.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled) { arrow.gameObject.SetActive(false); return; }
                var rootCanvas = canvas.rootCanvas;
                var targetCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
                // Point at the RIGHT EDGE of the left-side HUD button, not its top-left pivot.
                uiTarget.GetWorldCorners(buttonCorners);
                screen = RectTransformUtility.WorldToScreenPoint(targetCamera, (buttonCorners[2] + buttonCorners[3]) * .5f);
                direction = Vector2.left;
                var tutorialCanvas = canvasRoot.GetComponent<Canvas>();
                if (tutorialCanvas != null) tutorialCanvas.sortingOrder = Mathf.Max(920, rootCanvas.sortingOrder + 1);
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screen, null, out var local))
            { arrow.gameObject.SetActive(false); return; }
            var bounds = canvasRoot.rect;
            if (!bounds.Contains(local)) { arrow.gameObject.SetActive(false); return; }
            var offset = 58f + Mathf.Sin(Time.unscaledTime * 6f) * 8f;
            var pos = local - direction * offset;
            // Keep the pointer on screen; reverse the approach if the default side does not fit.
            if (!bounds.Contains(pos)) { direction = -direction; pos = local - direction * offset; }
            arrow.gameObject.SetActive(true);
            arrow.anchoredPosition = pos;
            arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        public void Continue() => next = true;
        public void Skip()
        {
            if (routine != null) TutorialAnalytics.Skip(CurrentStep);
            StopTutorial();
        }
        public void Replay()
        {
            if (!Application.isPlaying || session == null) return;
            played = false; StopTutorial(); session.StartBattle();
        }
        public void SuspendForDefeat() => StopTutorial();
        private void Finish() { Hide(); routine = null; CurrentStep = 0; }
        private void Hide() { if (canvasRoot != null) canvasRoot.gameObject.SetActive(false); }
        private void StopTutorial()
        {
            if (routine != null) StopCoroutine(routine);
            if (simulation != null && activeSkillListener != null)
                simulation.LegendarySkillUsed -= activeSkillListener;
            activeSkillListener = null;
            routine = null; Hide(); CurrentStep = 0;
        }
        private void OnDisable()
        {
            if (session != null) session.SimulationCreated -= Bind;
            StopTutorial();
        }
    }
}
