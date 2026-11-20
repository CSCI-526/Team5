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
            Show(1, "CLICK TO MOVE", "Click the road ahead.", true);
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
            Show(2, "DODGE ENEMIES", "Steer around towers and traps.", true);
            yield return WaitForNextOrSeconds(3.5f);
            if (!Running) { Finish(); yield break; }

            var panel = FindFirstObjectByType<LegendarySkillPanel>();
            var button = panel != null ? panel.ButtonForRole(UnitRole.Captain) : null;
            if (button != null)
            {
                uiTarget = (RectTransform)button.transform;
                Show(3, "CAPTAIN: CALL BACKUP", "Tap the gold skill for reinforcements; full squads strike towers.", false);
                var used = false;
                activeSkillListener = (role, member, position) =>
                { if (role == UnitRole.Captain) used = true; };
                simulation.LegendarySkillUsed += activeSkillListener;
                var skillTimer = 0f;
                while (Running && !used && !next && skillTimer < 6f)
                {
                    skillTimer += Time.unscaledDeltaTime;
                    yield return null;
                }
                simulation.LegendarySkillUsed -= activeSkillListener;
                activeSkillListener = null;
                if (!Running) { Finish(); yield break; }
            }
            next = false;

            worldTarget = cells[cells.Count - 1].TopCenter;
            Show(4, "REACH THE GOAL", "Click the goal to finish. Six deployment cards; roles field different unit counts.", true);
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
            nextButton = LegacyUiKit.CreateButton(canvasRoot, "Next Hint", new Vector2(300, -121),
                new Vector2(118, 38), LegacyUiKit.Green, 8f);
            var rect = (RectTransform)nextButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            LegacyUiKit.CreateText(rect, "Next Label", "NEXT / SKIP", Vector2.zero,
                new Vector2(112, 30), 14, Color.white, FontStyles.Bold);
            nextButton.onClick.AddListener(Continue);
            arrow = LegacyUiKit.CreateRect(canvasRoot, "Animated Tutorial Pointer", Vector2.zero, new Vector2(76, 76));
            var shape = LegacyUiKit.CreateImage(arrow, "White Arrow", Vector2.zero,
                new Vector2(68, 68), Color.white);
            shape.sprite = NextMapShapes.Triangle;
            shape.raycastTarget = false;
            arrow.localRotation = Quaternion.Euler(0, 0, -90);
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

        private void LateUpdate()
        {
            if (canvasRoot == null || !canvasRoot.gameObject.activeSelf || arrow == null) return;
            var camera = Camera.main;
            if (camera == null) return;
            var point = pointToWorld ? camera.WorldToScreenPoint(worldTarget) :
                uiTarget != null ? RectTransformUtility.WorldToScreenPoint(null, uiTarget.position) : Vector3.zero;
            if (point.z < 0 && pointToWorld) { arrow.gameObject.SetActive(false); return; }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot,
                new Vector2(point.x, point.y), null, out var local)) return;
            arrow.gameObject.SetActive(true);
            arrow.anchoredPosition = local + new Vector2(-60, 40 + Mathf.Sin(Time.unscaledTime * 6f) * 10f);
            arrow.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * .08f);
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
