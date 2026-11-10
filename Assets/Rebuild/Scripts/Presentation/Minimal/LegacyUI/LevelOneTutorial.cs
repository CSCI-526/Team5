using System.Collections;
using System.Collections.Generic;
using Team5.Rebuild.Analytics;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    [DisallowMultipleComponent, DefaultExecutionOrder(300)]
    public sealed class LevelOneTutorial : MonoBehaviour
    {
        private const int FirstFormationStep = 3;
        private const int FirstFormationResultStep = 4;
        private const int SpeedStep = 5;
        private const int SecondFormationStep = 6;

        private static bool hasPlayedTutorial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => hasPlayedTutorial = false;

        [SerializeField] private BattleSession session;

        private readonly TutorialCameraController cameraController = new TutorialCameraController();
        private readonly TutorialHighlightController highlightController = new TutorialHighlightController();
        private readonly TutorialBannerView bannerView = new TutorialBannerView();

        private BattleSimulation simulation;
        private LegacyBattleHud hud;
        private BattlePresentation presentation;
        private BattleUnitInspection inspection;
        private Coroutine routine;

        private bool ownsPause;
        private bool wasPaused;
        private bool nextPressed;
        private bool firstHit;
        private bool speedPromptActive;

        private int checkpointStep;
        private int resumeStep;
        private bool checkpointResult;
        private bool resumeResult;
        private string checkpointHeading;
        private string checkpointMessage;

        public int CurrentStep { get; private set; }
        public bool IsActive => routine != null;
        private int TotalSteps => simulation != null && simulation.Towers.Count > 1 ? 8 : 6;

        private void PrepareRewind()
        {
            resumeStep = checkpointStep;
            resumeResult = checkpointResult;
        }

        public void SuspendForDefeat() => StopTutorial();

        private void OnEnable()
        {
            if (BattleSceneFlow.IsPreviewScene(gameObject.scene)) return;
            if (session == null) session = GetComponent<BattleSession>();
            if (session == null) return;
            session.SimulationCreated += Bind;
            session.TutorialRewindRequested += PrepareRewind;
            Bind(session.Simulation);
        }

        private void Start()
        {
            if (!BattleSceneFlow.IsPreviewScene(gameObject.scene) && session != null)
                Bind(session.Simulation);
        }

        private void Bind(BattleSimulation value)
        {
            if (simulation == value && routine != null) return;
            StopTutorial();
            simulation = value;
            if (value == null || !isActiveAndEnabled) return;

            // Do not run the tutorial again if the player restarted the level or already played the tutorial,
            // unless they explicitly clicked "Back to Last Hint" (resumeStep > 0).
            var isRewind = resumeStep > 0;
            var isRetryAttempt = session.AttemptsRemaining < session.AttemptsPerLevel;
            if (!isRewind && (hasPlayedTutorial || isRetryAttempt)) return;

            routine = StartCoroutine(Run());
        }

        private void OnDamaged(DamageEvent value)
        {
            var captain = Captain();
            if (captain != null && value.UnitId == captain.Id) firstHit = true;
        }

        private UnitState Captain()
        {
            if (simulation == null) return null;
            foreach (var unit in simulation.Units)
                if (unit.Role == UnitRole.Captain && unit.IsAlive) return unit;
            return null;
        }

        private bool Running => session != null && session.Simulation == simulation && simulation != null &&
            simulation.Outcome == BattleOutcome.Running && Captain() != null;

        private IEnumerator Run()
        {
            var startStep = resumeStep;
            var replayResult = resumeResult;
            resumeStep = 0;
            resumeResult = false;

            yield return null;
            while (simulation != null && simulation.Outcome == BattleOutcome.Running && Captain() == null)
                yield return null;
            if (!Running) { routine = null; yield break; }

            foreach (var candidate in FindObjectsByType<LegacyBattleHud>(FindObjectsSortMode.None))
                if (candidate.Session == session) { hud = candidate; break; }
            foreach (var candidate in FindObjectsByType<BattlePresentation>(FindObjectsSortMode.None))
                if (candidate.Session == session) { presentation = candidate; inspection = candidate.GetComponent<BattleUnitInspection>(); break; }

            var mainCamera = Camera.main;
            if (hud == null || inspection == null || mainCamera == null) { routine = null; yield break; }

            hasPlayedTutorial = true;
            cameraController.CaptureSnapshot(mainCamera);
            bannerView.EnsureBuilt(transform, Continue, Skip);

            simulation.UnitDamaged += OnDamaged;
            firstHit = startStep >= FirstFormationStep;

            if (startStep == 0) yield return new WaitForSecondsRealtime(0.55f);
            if (!Running) { Finish(); yield break; }

            AcquirePause();
            if (startStep > 0) wasPaused = session.CheckpointPausedBeforeHint;

            if (startStep <= 1)
            {
                inspection.CloseImmediately();
                cameraController.FocusPoint(Captain().Position, 2f);
                GlowCaptain();
                Show(1, "YOU ARE THE ATTACKER!", "\nThe gold crown is your Captain. It marches automatically.", true);
                yield return WaitForNext();
                if (!Running) { Finish(); yield break; }
            }

            var firstTower = simulation.Towers.Count > 0 ? simulation.Towers[0] : null;
            if (startStep <= 2)
            {
                cameraController.RestoreImmediate();
                if (firstTower != null)
                    highlightController.GlowWorld(session.Towers[0].transform, keepCrown: false, blue: true);
                Show(2, "ENEMY TOWERS", "\nBlue strips mark fixed firing lanes.", true);
                yield return WaitForNext();
                if (!Running) { Finish(); yield break; }
            }

            if (startStep < FirstFormationStep)
            {
                Hide();
                cameraController.RestoreImmediate();
                ReleasePause();
                while (Running && !firstHit) yield return null;
                if (!Running) { Finish(); yield break; }
                AcquirePause();
            }

            if (startStep <= FirstFormationResultStep)
            {
                if (startStep == FirstFormationResultStep && replayResult)
                {
                    FocusCaptainAndTower(firstTower);
                    GlowCaptain();
                    Show(FirstFormationResultStep, checkpointHeading, checkpointMessage, true, true);
                    yield return WaitForNext();
                }
                else
                {
                    yield return GuideFormation(FirstFormationStep, firstTower, startStep == FirstFormationResultStep);
                }
            }
            if (!Running) { Finish(); yield break; }

            if (startStep <= SpeedStep)
            {
                yield return GuideSpeed();
                if (!Running) { Finish(); yield break; }
            }

            Hide();
            cameraController.RestoreImmediate();
            ReleasePause();

            var secondTower = simulation.Towers.Count > 1 ? simulation.Towers[1] : null;
            if (secondTower != null)
            {
                while (Running && secondTower.IsAlive && !Approaching(Captain(), secondTower))
                    yield return null;
                if (!Running) { Finish(); yield break; }

                if (secondTower.IsAlive)
                {
                    AcquirePause();
                    if (OutsideLane(Captain(), secondTower))
                    {
                        inspection.CloseImmediately();
                        cameraController.FocusPoint(
                            (Captain().Position + secondTower.Position) * 0.5f,
                            Vector3.Distance(Captain().Position, secondTower.Position) * 0.5f + 2f);
                        highlightController.GlowWorld(session.Towers[1].transform, keepCrown: false, blue: true);
                        Show(SecondFormationStep, "AVOID THE TOWER",
                            "\nThis tower fires upward. Your Captain is already outside its firing line.\nDifferent tower directions require different formation positions.",
                            true);
                        yield return WaitForNext();
                    }
                    else
                    {
                        yield return GuideFormation(SecondFormationStep, secondTower);
                    }
                    Hide();
                    cameraController.RestoreImmediate();
                    ReleasePause();
                }
            }

            var route = session.Route.CaptureTopCenters();
            var length = 0f;
            for (var i = 1; i < route.Length; i++)
                length += Vector3.Distance(route[i - 1], route[i]);

            while (Running && length - Captain().RouteProgress > 6f)
                yield return null;
            if (!Running) { Finish(); yield break; }

            AcquirePause();
            inspection.CloseImmediately();
            cameraController.FocusPoint(route[route.Length - 1], 3f);
            highlightController.GlowWorld(session.Route.Cells[session.Route.Cells.Count - 1].transform);
            Show(TotalSteps, "REACH THE GOAL TO WIN!", "\nGet your Captain to the end of the road to win.", true);
            yield return WaitForNext();

            if (Running) TutorialAnalytics.Complete();
            Finish();
        }

        private IEnumerator GuideFormation(int step, TowerState tower, bool reopenEditor = false)
        {
            if (!Running) yield break;
            inspection.CloseImmediately();
            cameraController.RestoreImmediate();
            highlightController.ClearWorldGlow();

            var isFirstTower = step == FirstFormationStep;
            Show(step, isFirstTower ? "EDIT YOUR FORMATION" : "AVOID THE TOWER",
                isFirstTower
                    ? "\nClick the highlighted squad crown to edit formation."
                    : "Click the highlighted squad crown.\nMove Captain to the <b>left or right column</b>, outside the vertical firing strip. Choose <b>APPLY & RESUME</b>.",
                false);
            highlightController.GlowButton(hud.SquadButton(0));
            if (reopenEditor) hud.SelectSquad();

            var moved = false;
            while (Running && !moved)
            {
                var originalSlot = Captain().SlotId;
                while (Running && (hud.FormationEditor == null || !hud.FormationEditor.gameObject.activeSelf))
                    yield return null;
                if (!Running) yield break;

                highlightController.ClearButtonGlow();
                bannerView.HideArrow();
                Show(step + 1, "EDIT YOUR FORMATION",
                    isFirstTower
                        ? "\nDrag your unit <b>down by one cell to avoid attack</b>."
                        : "Move Captain to the <b>left or right column</b>, outside the vertical firing strip. Choose <b>APPLY & RESUME</b>.",
                    false);

                while (Running && hud.FormationEditor != null && hud.FormationEditor.gameObject.activeSelf)
                    yield return null;
                if (!Running) yield break;

                yield return null;
                moved = Captain().SlotId != originalSlot && (tower == null || OutsideLane(Captain(), tower));
                if (!moved)
                {
                    Show(step, "EDIT YOUR FORMATION",
                        "The Captain is still in the firing lane, or the draft was canceled.\nOpen the squad again, move to a safe row / column, then <b>APPLY & RESUME</b>.",
                        false);
                    highlightController.GlowButton(hud.SquadButton(0));
                }
            }

            if (!Running) yield break;
            FocusCaptainAndTower(tower);
            GlowCaptain();
            Show(step + 1, "POSITIONING AVOIDS DAMAGE",
                "\nYour Captain moved to a different slot.\n The new position is outside this tower's firing line.",
                true, true);
            yield return WaitForNext();
        }

        private IEnumerator GuideSpeed()
        {
            if (!Running || hud.SpeedSlider == null) yield break;
            inspection.CloseImmediately();
            cameraController.RestoreImmediate();
            highlightController.ClearWorldGlow();

            if (session.PlaybackSpeed < 3)
            {
                Show(SpeedStep, "SPEED IT UP", "Drag the speed slider at the top right to <b>3x</b>.\nYou can change it any time.", false);
                highlightController.GlowImage(hud.SpeedSlider.targetGraphic as Image);
                speedPromptActive = true;
                hud.TutorialAllowsSpeed = true;
                while (Running && session.PlaybackSpeed < 3) yield return null;
                speedPromptActive = false;
                hud.TutorialAllowsSpeed = false;
                highlightController.ClearButtonGlow();
                bannerView.HideArrow();
                if (!Running) yield break;
            }

            Show(SpeedStep, "NICE!", "\nThe battle now runs 3x faster.\nSlow it down any time with the same slider.", true);
            yield return WaitForNext();
        }

        public static bool OutsideLane(UnitState unit, TowerState tower)
        {
            if (unit == null || tower == null) return false;
            var offset = unit.Position - tower.Position;
            if (tower.Definition.Area == AttackArea.Square)
                return !SquadCombatRules.Contains(offset, tower.Definition.Area, tower.Definition.Range, tower.Definition.AttackWidth);
            var direction = SquadCombatRules.Direction(tower.Definition.Area);
            return Mathf.Abs(Vector3.Dot(offset, new Vector3(-direction.z, 0f, direction.x))) > tower.Definition.AttackWidth * 0.5f;
        }

        private static bool Approaching(UnitState unit, TowerState tower)
        {
            var offset = unit.Position - tower.Position;
            var direction = SquadCombatRules.Direction(tower.Definition.Area);
            var forward = Vector3.Dot(offset, direction);
            var side = Mathf.Abs(Vector3.Dot(offset, new Vector3(-direction.z, 0f, direction.x)));
            return forward >= 0f && forward <= tower.Definition.Range + 3f && side <= 3f;
        }

        private IEnumerator WaitForNext()
        {
            while (Running && !nextPressed) yield return null;
            nextPressed = false;
        }

        public void Continue() => nextPressed = true;

        public void Skip()
        {
            if (routine != null) TutorialAnalytics.Skip(CurrentStep);
            StopTutorial();
        }

        [ContextMenu("Replay Level 1 Tutorial")]
        public void Replay()
        {
            if (!Application.isPlaying || session == null) return;
            hasPlayedTutorial = false;
            StopTutorial();
            simulation = null;
            session.StartBattle();
        }

        private void AcquirePause()
        {
            if (ownsPause || simulation == null) return;
            wasPaused = simulation.IsPaused;
            simulation.SetPaused(true);
            ownsPause = true;
            hud.TutorialOwnsPause = true;
            inspection.IsInputSuppressed = true;
        }

        private void ReleasePause()
        {
            if (ownsPause && session != null && session.Simulation == simulation && simulation.Outcome == BattleOutcome.Running)
                simulation.SetPaused(wasPaused);
            ownsPause = false;
            if (hud != null) hud.TutorialOwnsPause = false;
            if (inspection != null) inspection.IsInputSuppressed = false;
        }

        private void Finish()
        {
            Cleanup();
            routine = null;
        }

        private void StopTutorial()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Cleanup();
        }

        private void Cleanup()
        {
            if (simulation != null) simulation.UnitDamaged -= OnDamaged;
            if (inspection != null && ownsPause) inspection.CloseImmediately();
            cameraController.RestoreImmediate();
            cameraController.ResetSnapshot();
            ReleasePause();
            Hide();
            firstHit = false;
            nextPressed = false;
            CurrentStep = 0;
            speedPromptActive = false;
            if (hud != null) hud.TutorialAllowsSpeed = false;
        }

        private void FocusCaptainAndTower(TowerState tower)
        {
            var captain = Captain();
            if (captain == null) return;
            inspection.CloseImmediately();

            if (tower == null)
            {
                cameraController.FocusPoint(captain.Position, 2f);
                return;
            }

            var bounds = new Bounds(captain.Position, Vector3.one);
            if (presentation != null && presentation.TryGetView(captain.Id, out var view) && view.TryGetVisibleBounds(out var visible))
                bounds = visible;

            foreach (var sceneTower in session.Towers)
            {
                if (sceneTower.RuntimeState == tower)
                {
                    foreach (var renderer in sceneTower.GetComponentsInChildren<Renderer>())
                        bounds.Encapsulate(renderer.bounds);
                }
            }

            cameraController.FocusMidpointBounds((captain.Position + tower.Position) * 0.5f, bounds);
        }

        private void GlowCaptain()
        {
            var captain = Captain();
            if (captain != null && presentation != null && presentation.TryGetView(captain.Id, out var view))
                highlightController.GlowWorld(view.transform, keepCrown: true);
            else
                highlightController.ClearWorldGlow();
        }

        private void Show(int step, string heading, string message, bool modal, bool formationResult = false)
        {
            checkpointStep = step;
            checkpointHeading = heading;
            checkpointMessage = message;
            checkpointResult = formationResult;
            session.CaptureTutorialCheckpoint(wasPaused);
            TutorialAnalytics.Step(step, TotalSteps, heading);
            CurrentStep = step;
            nextPressed = false;
            highlightController.ClearButtonGlow();
            bannerView.Show(step, TotalSteps, heading, message, modal);
        }

        private void Hide()
        {
            highlightController.ClearWorldGlow();
            highlightController.ClearButtonGlow();
            bannerView.Hide();
        }

        private void LateUpdate()
        {
            cameraController.Tick(Time.unscaledDeltaTime);
            if (!bannerView.IsVisible) return;

            highlightController.TickPulse();
            var showSquadArrow = CurrentStep == FirstFormationStep && highlightController.GlowingButton != null;
            var showSpeedArrow = !showSquadArrow && speedPromptActive && hud != null && hud.SpeedSlider != null;
            bannerView.UpdateArrow(showSquadArrow, showSpeedArrow, highlightController.GlowingButton, hud != null ? hud.SpeedSlider : null);
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.SimulationCreated -= Bind;
                session.TutorialRewindRequested -= PrepareRewind;
            }
            StopTutorial();
            simulation = null;
        }
    }

    /// <summary>
    /// Handles camera snapshots, framing calculations, and smooth interpolation during tutorial steps.
    /// </summary>
    internal sealed class TutorialCameraController
    {
        private Camera camera;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float originalOrthoSize;
        private Vector3 targetPosition;
        private float targetOrthoSize;
        private bool cameraOwned;
        private bool hasSnapshot;

        public void CaptureSnapshot(Camera targetCamera)
        {
            camera = targetCamera;
            originalPosition = camera.transform.position;
            originalRotation = camera.transform.rotation;
            originalOrthoSize = camera.orthographicSize;
            hasSnapshot = true;
        }

        public void FocusPoint(Vector3 point, float radius)
        {
            if (camera == null) return;
            cameraOwned = true;
            var tan = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            var distance = Mathf.Max(6f, radius * 1.2f / tan);
            targetOrthoSize = radius * 1.2f;
            var verticalOffset = camera.orthographic ? targetOrthoSize * 0.3f : distance * tan * 0.3f;
            targetPosition = point - originalRotation * Vector3.forward * distance - originalRotation * Vector3.up * verticalOffset;
        }

        public void FocusMidpointBounds(Vector3 center, Bounds bounds)
        {
            if (camera == null) return;
            var extent = Vector3.zero;
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = Quaternion.Inverse(originalRotation) * (bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)) - center);
                        extent = Vector3.Max(extent, new Vector3(Mathf.Abs(corner.x), Mathf.Abs(corner.y), Mathf.Abs(corner.z)));
                    }

            targetOrthoSize = Mathf.Max(2f, extent.y / 0.6f, extent.x / (camera.aspect * 0.85f));
            var tan = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            var distance = Mathf.Max(6f, targetOrthoSize / tan + extent.z, camera.nearClipPlane + extent.z + 1f);
            var offset = camera.orthographic ? targetOrthoSize * 0.3f : distance * tan * 0.3f;
            targetPosition = center - originalRotation * Vector3.forward * distance - originalRotation * Vector3.up * offset;
            cameraOwned = true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!cameraOwned || camera == null) return;
            var alpha = 1f - Mathf.Exp(-12f * unscaledDeltaTime);
            camera.transform.position = Vector3.Lerp(camera.transform.position, targetPosition, alpha);
            if (camera.orthographic)
                camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, targetOrthoSize, alpha);
        }

        public void RestoreImmediate()
        {
            if (cameraOwned && hasSnapshot && camera != null)
            {
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
                camera.orthographicSize = originalOrthoSize;
            }
            cameraOwned = false;
        }

        public void ResetSnapshot()
        {
            hasSnapshot = false;
            cameraOwned = false;
            camera = null;
        }
    }

    /// <summary>
    /// Manages gold and blue pulsing highlights on 3D world renderers and UI buttons.
    /// </summary>
    internal sealed class TutorialHighlightController
    {
        private Renderer[] glowingRenderers;
        private MaterialPropertyBlock[] originalGlowProperties;
        private MaterialPropertyBlock glowProperties;
        private Image glowingButton;
        private Color originalButtonColor;
        private bool blueWorldGlow;

        public Image GlowingButton => glowingButton;

        private static Color GoldPulse => Color.Lerp(
            new Color(1f, 0.64f, 0.08f),
            new Color(1f, 0.94f, 0.48f),
            (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f);

        private Color WorldPulse => blueWorldGlow
            ? Color.Lerp(new Color(0.12f, 0.35f, 0.95f), new Color(0.45f, 0.82f, 1f), (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f)
            : GoldPulse;

        public void GlowWorld(Transform target, bool keepCrown = false, bool blue = false)
        {
            ClearWorldGlow();
            if (target == null) return;
            blueWorldGlow = blue;

            var renderers = new List<Renderer>();
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!keepCrown || renderer.name == "Body" || renderer.name == "Outline")
                    renderers.Add(renderer);
            }

            glowingRenderers = renderers.ToArray();
            originalGlowProperties = new MaterialPropertyBlock[glowingRenderers.Length];
            for (var i = 0; i < glowingRenderers.Length; i++)
            {
                originalGlowProperties[i] = new MaterialPropertyBlock();
                glowingRenderers[i].GetPropertyBlock(originalGlowProperties[i]);
            }
            ApplyWorldPulse(WorldPulse);
        }

        public void ClearWorldGlow()
        {
            if (glowingRenderers != null)
            {
                for (var i = 0; i < glowingRenderers.Length; i++)
                {
                    if (glowingRenderers[i] != null)
                        glowingRenderers[i].SetPropertyBlock(originalGlowProperties[i]);
                }
            }
            glowingRenderers = null;
            originalGlowProperties = null;
        }

        public void GlowButton(Button button) => GlowImage(button != null ? button.targetGraphic as Image : null);

        public void GlowImage(Image image)
        {
            ClearButtonGlow();
            glowingButton = image;
            if (glowingButton == null) return;
            originalButtonColor = glowingButton.color;
            glowingButton.color = GoldPulse;
        }

        public void ClearButtonGlow()
        {
            if (glowingButton != null) glowingButton.color = originalButtonColor;
            glowingButton = null;
        }

        public void TickPulse()
        {
            ApplyWorldPulse(WorldPulse);
            if (glowingButton != null) glowingButton.color = GoldPulse;
        }

        private void ApplyWorldPulse(Color tint)
        {
            if (glowingRenderers == null) return;
            if (glowProperties == null) glowProperties = new MaterialPropertyBlock();
            foreach (var renderer in glowingRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(glowProperties);
                glowProperties.SetColor("_BaseColor", tint);
                glowProperties.SetColor("_Color", tint);
                renderer.SetPropertyBlock(glowProperties);
            }
        }
    }

    /// <summary>
    /// Builds and updates the tutorial banner card, modal blocker, and animated pointer arrow.
    /// </summary>
    internal sealed class TutorialBannerView
    {
        private readonly Vector3[] buttonCorners = new Vector3[4];
        private GameObject ui;
        private GameObject blocker;
        private RectTransform root;
        private RectTransform banner;
        private RectTransform squadArrow;
        private TMP_Text title;
        private TMP_Text body;
        private Button nextButton;

        public bool IsVisible => ui != null && ui.activeSelf;

        public void EnsureBuilt(Transform parent, UnityEngine.Events.UnityAction onContinue, UnityEngine.Events.UnityAction onSkip)
        {
            if (ui != null) return;

            ui = new GameObject("Level 1 Tutorial", typeof(RectTransform));
            ui.transform.SetParent(parent, false);
            LegacyUiKit.CreateCanvas(ui, 900, new Vector2(1280f, 720f), 0.5f);
            root = (RectTransform)ui.transform;

            var shade = LegacyUiKit.CreateImage(root, "Modal Input Blocker", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.08f));
            LegacyUiKit.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            blocker = shade.gameObject;

            var panel = LegacyUiKit.CreateImage(root, "Tutorial Banner", Vector2.zero, new Vector2(880f, 175f), new Color(0.08f, 0.11f, 0.17f, 0.96f));
            panel.raycastTarget = true;
            banner = panel.rectTransform;

            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.85f, 0.4f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);

            title = LegacyUiKit.CreateText(banner, "Title", "", Vector2.zero, Vector2.zero, 30f, new Color(0.35f, 0.95f, 0.45f), FontStyles.Bold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 30f;

            body = LegacyUiKit.CreateText(banner, "Body", "", Vector2.zero, Vector2.zero, 23f, Color.white);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.alignment = TextAlignmentOptions.Center;
            body.enableAutoSizing = true;
            body.fontSizeMin = 18f;
            body.fontSizeMax = 23f;

            var skip = LegacyUiKit.CreateButton(banner, "Skip Tutorial", Vector2.zero, new Vector2(110f, 30f), Color.white, 0f);
            var skipRect = (RectTransform)skip.transform;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(0f, 0f);
            skipRect.pivot = new Vector2(0f, 0.5f);
            skipRect.anchoredPosition = new Vector2(24f, 22f);
            LegacyUiKit.CreateText(skip.transform, "Label", "SKIP", Vector2.zero, new Vector2(100f, 28f), 18f, new Color(0.196f, 0.196f, 0.196f));
            skip.onClick.AddListener(onSkip);

            nextButton = LegacyUiKit.CreateButton(banner, "Continue", Vector2.zero, new Vector2(144f, 30f), new Color(0.2f, 0.6f, 0.2f), 0f);
            var nextRect = (RectTransform)nextButton.transform;
            nextRect.anchorMin = nextRect.anchorMax = new Vector2(1f, 0f);
            nextRect.pivot = new Vector2(1f, 0.5f);
            nextRect.anchoredPosition = new Vector2(-24f, 22f);
            LegacyUiKit.CreateText(nextButton.transform, "Label", "CONTINUE", Vector2.zero, new Vector2(138f, 28f), 18f, Color.white);
            nextButton.onClick.AddListener(onContinue);

            LayoutBanner(false);

            squadArrow = LegacyUiKit.CreateRect(root, "Team1 Tutorial Arrow", Vector2.zero, new Vector2(84f, 62f));
            ArrowStroke("Shaft Border", new Vector2(-34f, 0f), new Vector2(42f, 0f), 12f, Color.black);
            ArrowStroke("Upper Border", new Vector2(-38f, 0f), new Vector2(-10f, 22f), 12f, Color.black);
            ArrowStroke("Lower Border", new Vector2(-38f, 0f), new Vector2(-10f, -22f), 12f, Color.black);
            ArrowStroke("Shaft", new Vector2(-34f, 0f), new Vector2(38f, 0f), 6f, Color.white);
            ArrowStroke("Upper", new Vector2(-37f, 0f), new Vector2(-10f, 22f), 6f, Color.white);
            ArrowStroke("Lower", new Vector2(-37f, 0f), new Vector2(-10f, -22f), 6f, Color.white);
            squadArrow.gameObject.SetActive(false);
        }

        public void Show(int step, int totalSteps, string heading, string message, bool modal)
        {
            ui.SetActive(true);
            banner.gameObject.SetActive(true);
            title.text = $"{step}/{totalSteps} - {heading}";
            body.text = message;
            LayoutBanner(!modal);
            blocker.SetActive(modal);
            nextButton.gameObject.SetActive(modal);
            HideArrow();
        }

        public void Hide()
        {
            HideArrow();
            if (ui != null) ui.SetActive(false);
        }

        public void HideArrow()
        {
            if (squadArrow != null) squadArrow.gameObject.SetActive(false);
        }

        public void UpdateArrow(bool showSquadArrow, bool showSpeedArrow, Image glowingButton, Slider speedSlider)
        {
            if (squadArrow == null) return;
            squadArrow.gameObject.SetActive(showSquadArrow || showSpeedArrow);

            if (showSpeedArrow && speedSlider != null)
            {
                ((RectTransform)speedSlider.transform).GetWorldCorners(buttonCorners);
                var speedCanvas = speedSlider.GetComponentInParent<Canvas>();
                var eventCamera = speedCanvas != null && speedCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? speedCanvas.worldCamera : null;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, (buttonCorners[0] + buttonCorners[1]) * 0.5f);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out var left)) return;
                var scale = Mathf.Lerp(0.8f, 1.15f, (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f);
                squadArrow.localScale = Vector3.one * scale;
                squadArrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
                squadArrow.SetAsLastSibling();
                squadArrow.anchoredPosition = new Vector2(left.x - 12f - 42f * scale, left.y);
            }
            else if (showSquadArrow && glowingButton != null)
            {
                squadArrow.localRotation = Quaternion.identity;
                glowingButton.rectTransform.GetWorldCorners(buttonCorners);
                var sourceCanvas = glowingButton.GetComponentInParent<Canvas>();
                var eventCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, (buttonCorners[2] + buttonCorners[3]) * 0.5f);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out var right)) return;
                var scale = Mathf.Lerp(0.8f, 1.15f, (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f);
                squadArrow.localScale = Vector3.one * scale;
                squadArrow.SetAsLastSibling();
                squadArrow.anchoredPosition = new Vector2(right.x + 12f + 42f * scale, right.y);
            }
        }

        private void LayoutBanner(bool interactive)
        {
            banner.anchorMin = banner.anchorMax = new Vector2(interactive ? 0.75f : 0.5f, 0f);
            banner.pivot = new Vector2(0.5f, 0f);
            banner.anchoredPosition = new Vector2(0f, 24f);
            banner.sizeDelta = interactive ? new Vector2(560f, 230f) : new Vector2(880f, 175f);
            LayoutBannerText(title.rectTransform, interactive ? 0.72f : 0.68f, 0.96f, 40f);
            LayoutBannerText(body.rectTransform, interactive ? 0.20f : 0.24f, interactive ? 0.72f : 0.68f, 50f);
        }

        private static void LayoutBannerText(RectTransform rect, float bottom, float top, float horizontalInset)
        {
            rect.anchorMin = new Vector2(0f, bottom);
            rect.anchorMax = new Vector2(1f, top);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-horizontalInset, 0f);
        }

        private void ArrowStroke(string name, Vector2 from, Vector2 to, float thickness, Color tint)
        {
            var delta = to - from;
            var image = LegacyUiKit.CreateImage(squadArrow, name, (from + to) * 0.5f, new Vector2(delta.magnitude, thickness), tint);
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}