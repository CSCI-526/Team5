using System.Collections;
using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Adapted from Team5 HowToPlay's six freeze-frame lessons. Observes simulation
    // state/events and uses the real formation editor; never writes HP, slots or damage.
    [DisallowMultipleComponent, DefaultExecutionOrder(300)]
    public sealed class LevelOneTutorial : MonoBehaviour
    {
        [SerializeField] private BattleSession session;
        private BattleSimulation simulation;
        private LegacyBattleHud hud;
        private BattleUnitInspection inspection;
        private Coroutine routine;
        private GameObject ui, blocker;
        private RectTransform root, banner;
        private TMP_Text title, body;
        private Button nextButton;
        private Camera camera;
        private Vector3 originalPosition, targetPosition;
        private Quaternion originalRotation;
        private float originalOrthoSize, targetOrthoSize;
        private bool cameraOwned, hasCameraSnapshot, ownsPause, wasPaused, next, firstHit;
        private BattlePresentation presentation;
        private Renderer[] glowingRenderers;
        private MaterialPropertyBlock[] originalGlowProperties;
        private MaterialPropertyBlock glowProperties;
        private Image glowingButton;
        private Color originalButtonColor;
        private bool blueWorldGlow;
        private RectTransform squadArrow;
        private readonly Vector3[] buttonCorners = new Vector3[4];
        public int CurrentStep { get; private set; }
        private int TotalSteps => simulation != null && simulation.Towers.Count > 1 ? 8 : 6;
        public bool IsActive => routine != null;
        private int checkpointStep, resumeStep;
        private bool checkpointResult, resumeResult;
        private string checkpointHeading, checkpointMessage;
        private void PrepareRewind() { resumeStep = checkpointStep; resumeResult = checkpointResult; }
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
        private void Start() { if (!BattleSceneFlow.IsPreviewScene(gameObject.scene) && session != null) Bind(session.Simulation); }
        private void Bind(BattleSimulation value)
        {
            if (simulation == value && routine != null) return;
            StopTutorial();
            simulation = value;
            if (value == null || !isActiveAndEnabled) return;
            routine = StartCoroutine(Run());
        }
        private void OnDamaged(DamageEvent value)
        {
            // The coroutine acquires pause on the next frame, outside the simulation tick.
            if (Captain() != null && value.UnitId == Captain().Id) firstHit = true;
        }
        private UnitState Captain()
        {
            if (simulation != null)
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
            resumeStep = 0; resumeResult = false;
            yield return null;
            while (simulation != null && simulation.Outcome == BattleOutcome.Running && Captain() == null) yield return null;
            if (!Running) { routine = null; yield break; }
            foreach (var candidate in FindObjectsByType<LegacyBattleHud>(FindObjectsSortMode.None))
                if (candidate.Session == session) { hud = candidate; break; }
            foreach (var candidate in FindObjectsByType<BattlePresentation>(FindObjectsSortMode.None))
                if (candidate.Session == session) { presentation = candidate; inspection = candidate.GetComponent<BattleUnitInspection>(); break; }
            camera = Camera.main;
            if (hud == null || inspection == null || camera == null) { routine = null; yield break; }
            originalPosition = camera.transform.position; originalRotation = camera.transform.rotation;
            originalOrthoSize = camera.orthographicSize; hasCameraSnapshot = true;
            BuildUi();
            simulation.UnitDamaged += OnDamaged;
            firstHit = startStep >= 3;
            if (startStep == 0) yield return new WaitForSecondsRealtime(0.55f);
            if (!Running) { Finish(); yield break; }
            AcquirePause();
            if (startStep > 0) wasPaused = session.CheckpointPausedBeforeHint;
            if (startStep <= 1)
            {
            Focus(Captain().Position, 2);
            GlowCaptain();
            Show(1, "YOU ARE THE ATTACKER!", "\nThe gold crown is your Captain. It marches automatically.", true);
            yield return WaitForNext();
            if (!Running) { Finish(); yield break; }
            }

            var firstTower = simulation.Towers.Count > 0 ? simulation.Towers[0] : null;
            if (startStep <= 2)
            {
            RestoreCamera();
            if (firstTower != null)
            {
                GlowWorld(session.Towers[0].transform, blue: true);
            }
            Show(2, "ENEMY TOWERS", "\nBlue strips mark fixed firing lanes.", true);
            yield return WaitForNext();
            }
            if (startStep <= 3)
            {
            Hide(); RestoreCamera(); ReleasePause();
            while (Running && !firstHit) yield return null;
            if (!Running) { Finish(); yield break; }
            AcquirePause();
            FocusCaptainAndTower(firstTower);
            GlowCaptain();
            Show(3, "LIVE STATS", $"\nThe Captain was hit. Click a unit during normal play to inspect its live stats.", true);
            yield return WaitForNext();
            if (!Running) { Finish(); yield break; }
            }
            if (startStep <= 5)
            {
                if (startStep == 5 && replayResult)
                {
                    FocusCaptainAndTower(firstTower); GlowCaptain();
                    Show(5, checkpointHeading, checkpointMessage, true, true);
                    yield return WaitForNext();
                }
                else yield return GuideFormation(4, firstTower, startStep == 5);
            }
            if (!Running) { Finish(); yield break; }
            Hide(); RestoreCamera(); ReleasePause();

            var secondTower = simulation.Towers.Count > 1 ? simulation.Towers[1] : null;
            if (secondTower != null)
            {
                while (Running && secondTower.IsAlive && !Approaching(Captain(), secondTower)) yield return null;
                if (!Running) { Finish(); yield break; }
                if (secondTower.IsAlive)
                {
                    AcquirePause();
                    if (OutsideLane(Captain(), secondTower))
                    {
                        Focus((Captain().Position + secondTower.Position) / 2, Vector3.Distance(Captain().Position, secondTower.Position) / 2 + 2);
                        GlowWorld(session.Towers[1].transform, blue: true);
                        Show(6, "AVOID THE TOWER", "\nThis tower fires upward. Your Captain is already outside its firing line.\nDifferent tower directions require different formation positions.", true);
                        yield return WaitForNext();
                    }
                    else yield return GuideFormation(6, secondTower);
                    Hide(); RestoreCamera(); ReleasePause();
                }
            }
            var route = session.Route.CaptureTopCenters();
            var length = 0f;
            for (var i = 1; i < route.Length; i++) length += Vector3.Distance(route[i - 1], route[i]);
            while (Running && length - Captain().RouteProgress > 6) yield return null;
            if (!Running) { Finish(); yield break; }
            AcquirePause();
            Focus(route[route.Length - 1], 3);
            GlowWorld(session.Route.Cells[session.Route.Cells.Count - 1].transform);
            Show(TotalSteps, "REACH THE GOAL TO WIN!", "\nGet your Captain to the end of the road to win.", true);
            yield return WaitForNext();
            Finish();
        }

        private IEnumerator GuideFormation(int step, TowerState tower, bool reopenEditor = false)
        {
            if (!Running) yield break;
            inspection.CloseImmediately(); RestoreCamera();
            ClearWorldGlow();
            Show(step, step == 4 ? "EDIT YOUR FORMATION" : "AVOID THE TOWER",
                step == 4 ? "\nClick the highlighted squad crown to edit formation."
                    : "Click the highlighted squad crown.\nMove Captain to the <b>left or right column</b>, outside the vertical firing strip. Choose <b>APPLY & RESUME</b>.", false);
            GlowButton(hud.SquadButton(0));
            if (reopenEditor) hud.SelectSquad();
            var moved = false;
            while (Running && !moved)
            {
                var originalSlot = Captain().SlotId;
                while (Running && (hud.FormationEditor == null || !hud.FormationEditor.gameObject.activeSelf)) yield return null;
                if (!Running) yield break;
                ClearButtonGlow();
                Show(step + 1, "EDIT YOUR FORMATION",
                    step == 4 ? "\nDrag your unit <b>down by one cell to avoid attack</b>."
                        : "Move Captain to the <b>left or right column</b>, outside the vertical firing strip. Choose <b>APPLY & RESUME</b>.", false);
                while (Running && hud.FormationEditor != null && hud.FormationEditor.gameObject.activeSelf) yield return null;
                if (!Running) yield break;
                // Let inspection release its view; the editor restores the tutorial's pause.
                yield return null;
                moved = Captain().SlotId != originalSlot && (tower == null || OutsideLane(Captain(), tower));
                if (!moved)
                {
                    Show(step, "EDIT YOUR FORMATION", "The Captain is still in the firing lane, or the draft was canceled.\nOpen the squad again, move to a safe row / column, then <b>APPLY & RESUME</b>.", false);
                    GlowButton(hud.SquadButton(0));
                }
            }
            if (!Running) yield break;
            FocusCaptainAndTower(tower);
            GlowCaptain();
            Show(step + 1, "POSITIONING AVOIDS DAMAGE", "\nYour Captain moved to a different slot.\n The new position is outside this tower's firing line.", true, true);
            yield return WaitForNext();
        }
        public static bool OutsideLane(UnitState unit, TowerState tower)
        {
            if (unit == null || tower == null) return false;
            var offset = unit.Position - tower.Position;
            if (tower.Definition.Area == AttackArea.Square)
                return !SquadCombatRules.Contains(offset, tower.Definition.Area, tower.Definition.Range, tower.Definition.AttackWidth);
            var direction = SquadCombatRules.Direction(tower.Definition.Area);
            return Mathf.Abs(Vector3.Dot(offset, new Vector3(-direction.z, 0, direction.x))) > tower.Definition.AttackWidth / 2;
        }
        private static bool Approaching(UnitState unit, TowerState tower)
        {
            var offset = unit.Position - tower.Position;
            var direction = SquadCombatRules.Direction(tower.Definition.Area);
            var forward = Vector3.Dot(offset, direction);
            var side = Mathf.Abs(Vector3.Dot(offset, new Vector3(-direction.z, 0, direction.x)));
            return forward >= 0 && forward <= tower.Definition.Range + 3 && side <= 3;
        }
        private IEnumerator WaitForNext()
        {
            while (Running && !next) yield return null;
            next = false;
        }
        public void Continue() => next = true;
        public void Skip() => StopTutorial();
        [ContextMenu("Replay Level 1 Tutorial")]
        public void Replay()
        {
            if (!Application.isPlaying || session == null) return;
            StopTutorial();
            simulation = null;
            session.StartBattle();
        }
        private void AcquirePause()
        {
            if (ownsPause || simulation == null) return;
            wasPaused = simulation.IsPaused; simulation.SetPaused(true); ownsPause = true;
            hud.TutorialOwnsPause = true; inspection.IsInputSuppressed = true;
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
            Cleanup(); routine = null;
        }
        private void StopTutorial()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null; Cleanup();
        }
        private void Cleanup()
        {
            if (simulation != null) simulation.UnitDamaged -= OnDamaged;
            if (inspection != null && ownsPause) inspection.CloseImmediately();
            RestoreCamera(); ReleasePause(); Hide();
            firstHit = false; next = false; CurrentStep = 0; hasCameraSnapshot = false;
        }
        private void FocusCaptainAndTower(TowerState tower)
        {
            var captain = Captain();
            if (captain == null) return;
            if (tower == null) { Focus(captain.Position, 2); return; }
            inspection.CloseImmediately();
            var center = (captain.Position + tower.Position) / 2;
            var bounds = new Bounds(captain.Position, Vector3.one);
            if (presentation != null && presentation.TryGetView(captain.Id, out var view) && view.TryGetVisibleBounds(out var visible)) bounds = visible;
            foreach (var sceneTower in session.Towers)
                if (sceneTower.RuntimeState == tower)
                    foreach (var renderer in sceneTower.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            var extent = Vector3.zero;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = Quaternion.Inverse(originalRotation) * (bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)) - center);
                extent = Vector3.Max(extent, new Vector3(Mathf.Abs(corner.x), Mathf.Abs(corner.y), Mathf.Abs(corner.z)));
            }
            // Frame both complete models around their midpoint, above the tutorial banner.
            targetOrthoSize = Mathf.Max(2, extent.y / 0.6f, extent.x / (camera.aspect * 0.85f));
            var tan = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2);
            var distance = Mathf.Max(6, targetOrthoSize / tan + extent.z, camera.nearClipPlane + extent.z + 1);
            var offset = camera.orthographic ? targetOrthoSize * 0.3f : distance * tan * 0.3f;
            targetPosition = center - originalRotation * Vector3.forward * distance - originalRotation * Vector3.up * offset;
            cameraOwned = true;
        }
        private void Focus(Vector3 point, float radius)
        {
            inspection.CloseImmediately();
            cameraOwned = true;
            var tan = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2);
            var distance = Mathf.Max(6, radius * 1.2f / tan);
            targetOrthoSize = radius * 1.2f;
            var verticalOffset = camera.orthographic ? targetOrthoSize * 0.3f : distance * tan * 0.3f;
            targetPosition = point - originalRotation * Vector3.forward * distance - originalRotation * Vector3.up * verticalOffset;
        }
        private void RestoreCamera()
        {
            if (cameraOwned && hasCameraSnapshot && camera != null)
            { camera.transform.SetPositionAndRotation(originalPosition, originalRotation); camera.orthographicSize = originalOrthoSize; }
            cameraOwned = false;
        }
        private void GlowCaptain()
        {
            var captain = Captain();
            if (captain != null && presentation != null && presentation.TryGetView(captain.Id, out var view))
                GlowWorld(view.transform, true);
            else ClearWorldGlow();
        }
        private static Color GoldPulse => Color.Lerp(new Color(1, 0.64f, 0.08f), new Color(1, 0.94f, 0.48f),
            (Mathf.Sin(Time.unscaledTime * 4) + 1) / 2);
        private Color WorldPulse => blueWorldGlow ? Color.Lerp(new Color(0.12f, 0.35f, 0.95f), new Color(0.45f, 0.82f, 1f),
            (Mathf.Sin(Time.unscaledTime * 4) + 1) / 2) : GoldPulse;
        private void GlowWorld(Transform target, bool keepCrown = false, bool blue = false)
        {
            ClearWorldGlow();
            if (target == null) return;
            blueWorldGlow = blue;
            var renderers = new List<Renderer>();
            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
                if (!keepCrown || renderer.name == "Body" || renderer.name == "Outline") renderers.Add(renderer);
            glowingRenderers = renderers.ToArray();
            originalGlowProperties = new MaterialPropertyBlock[glowingRenderers.Length];
            for (var i = 0; i < glowingRenderers.Length; i++)
            {
                originalGlowProperties[i] = new MaterialPropertyBlock();
                glowingRenderers[i].GetPropertyBlock(originalGlowProperties[i]);
            }
            PulseWorld(WorldPulse);
        }
        private void PulseWorld(Color gold)
        {
            if (glowingRenderers == null) return;
            if (glowProperties == null) glowProperties = new MaterialPropertyBlock();
            foreach (var renderer in glowingRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(glowProperties);
                glowProperties.SetColor("_BaseColor", gold); glowProperties.SetColor("_Color", gold);
                renderer.SetPropertyBlock(glowProperties);
            }
        }
        private void ClearWorldGlow()
        {
            if (glowingRenderers != null)
                for (var i = 0; i < glowingRenderers.Length; i++)
                    if (glowingRenderers[i] != null) glowingRenderers[i].SetPropertyBlock(originalGlowProperties[i]);
            glowingRenderers = null; originalGlowProperties = null;
        }
        private void GlowButton(Button button)
        {
            ClearButtonGlow();
            glowingButton = button != null ? button.targetGraphic as Image : null;
            if (glowingButton == null) return;
            originalButtonColor = glowingButton.color; glowingButton.color = GoldPulse;
        }
        private void ClearButtonGlow()
        {
            if (glowingButton != null) glowingButton.color = originalButtonColor;
            glowingButton = null;
            if (squadArrow != null) squadArrow.gameObject.SetActive(false);
        }
        private void Show(int step, string heading, string message, bool modal, bool formationResult = false)
        {
            checkpointStep = step; checkpointHeading = heading; checkpointMessage = message; checkpointResult = formationResult;
            session.CaptureTutorialCheckpoint(wasPaused);
            CurrentStep = step; next = false; ui.SetActive(true); banner.gameObject.SetActive(true);
            title.text = $"{step}/{TotalSteps} - {heading}"; body.text = message;
            LayoutBanner(!modal);
            blocker.SetActive(modal); nextButton.gameObject.SetActive(modal);
            ClearButtonGlow();
        }
        private void Hide()
        {
            ClearWorldGlow(); ClearButtonGlow();
            if (ui != null) ui.SetActive(false);
        }
        private void LateUpdate()
        {
            if (cameraOwned && camera != null)
            {
                var alpha = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
                camera.transform.position = Vector3.Lerp(camera.transform.position, targetPosition, alpha);
                if (camera.orthographic) camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, targetOrthoSize, alpha);
            }
            if (ui == null || !ui.activeSelf) return;
            var gold = GoldPulse;
            PulseWorld(WorldPulse);
            if (glowingButton != null) glowingButton.color = gold;
            var showArrow = CurrentStep == 4 && glowingButton != null;
            squadArrow.gameObject.SetActive(showArrow);
            if (showArrow)
            {
                glowingButton.rectTransform.GetWorldCorners(buttonCorners);
                var sourceCanvas = glowingButton.GetComponentInParent<Canvas>();
                var eventCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
                var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, (buttonCorners[2] + buttonCorners[3]) / 2);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, null, out var right)) return;
                var scale = Mathf.Lerp(0.8f, 1.15f, (Mathf.Sin(Time.unscaledTime * 4) + 1) / 2);
                squadArrow.localScale = Vector3.one * scale;
                squadArrow.SetAsLastSibling();
                // Keep the left-pointing tip next to the icon while the body grows and shrinks.
                squadArrow.anchoredPosition = new Vector2(right.x + 12 + 42 * scale, right.y);
            }
        }
        private void BuildUi()
        {
            if (ui != null) return;
            ui = new GameObject("Level 1 Tutorial", typeof(RectTransform)); ui.transform.SetParent(transform, false);
            LegacyUiKit.CreateCanvas(ui, 900, new Vector2(1280, 720), 0.5f); root = (RectTransform)ui.transform;
            var shade = LegacyUiKit.CreateImage(root, "Modal Input Blocker", Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.08f));
            LegacyUiKit.Stretch(shade.rectTransform); shade.raycastTarget = true; blocker = shade.gameObject;
            // Match the original HowToPlay.BuildFreezeFrameUI card and TMP typography.
            // Both projects use LiberationSans SDF through TMP_Settings.defaultFontAsset.
            var panel = LegacyUiKit.CreateImage(root, "Tutorial Banner", Vector2.zero, new Vector2(880, 175), new Color(0.08f, 0.11f, 0.17f, 0.96f));
            panel.raycastTarget = true; banner = panel.rectTransform;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.85f, 0.4f, 1);
            outline.effectDistance = new Vector2(3, -3);
            title = LegacyUiKit.CreateText(banner, "Title", "", Vector2.zero, Vector2.zero, 30, new Color(0.35f, 0.95f, 0.45f), FontStyles.Bold);
            title.enableAutoSizing = true; title.fontSizeMin = 26; title.fontSizeMax = 30;
            body = LegacyUiKit.CreateText(banner, "Body", "", Vector2.zero, Vector2.zero, 23, Color.white);
            body.textWrappingMode = TextWrappingModes.Normal; body.alignment = TextAlignmentOptions.Center;
            // Keep every existing message, including its line breaks, visible in the legacy card.
            body.enableAutoSizing = true; body.fontSizeMin = 18; body.fontSizeMax = 23;
            var skip = LegacyUiKit.CreateButton(banner, "Skip Tutorial", Vector2.zero, new Vector2(110, 30), Color.white, 0);
            var skipRect = (RectTransform)skip.transform;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(0, 0); skipRect.pivot = new Vector2(0, 0.5f);
            skipRect.anchoredPosition = new Vector2(24, 22);
            LegacyUiKit.CreateText(skip.transform, "Label", "SKIP", Vector2.zero, new Vector2(100, 28), 18, new Color(0.196f, 0.196f, 0.196f));
            skip.onClick.AddListener(Skip);
            nextButton = LegacyUiKit.CreateButton(banner, "Continue", Vector2.zero, new Vector2(144, 30), new Color(0.2f, 0.6f, 0.2f), 0);
            var nextRect = (RectTransform)nextButton.transform;
            nextRect.anchorMin = nextRect.anchorMax = new Vector2(1, 0); nextRect.pivot = new Vector2(1, 0.5f);
            nextRect.anchoredPosition = new Vector2(-24, 22);
            LegacyUiKit.CreateText(nextButton.transform, "Label", "CONTINUE", Vector2.zero, new Vector2(138, 28), 18, Color.white);
            nextButton.onClick.AddListener(Continue);
            LayoutBanner(false);
            squadArrow = LegacyUiKit.CreateRect(root, "Team1 Tutorial Arrow", Vector2.zero, new Vector2(84, 62));
            // Native Image strokes use the same rendering path as the existing working HUD.
            ArrowStroke("Shaft Border", new Vector2(-34, 0), new Vector2(42, 0), 12, Color.black);
            ArrowStroke("Upper Border", new Vector2(-38, 0), new Vector2(-10, 22), 12, Color.black);
            ArrowStroke("Lower Border", new Vector2(-38, 0), new Vector2(-10, -22), 12, Color.black);
            ArrowStroke("Shaft", new Vector2(-34, 0), new Vector2(38, 0), 6, Color.white);
            ArrowStroke("Upper", new Vector2(-37, 0), new Vector2(-10, 22), 6, Color.white);
            ArrowStroke("Lower", new Vector2(-37, 0), new Vector2(-10, -22), 6, Color.white);
            squadArrow.gameObject.SetActive(false);
        }
        private void LayoutBanner(bool interactive)
        {
            // The full-width legacy card would cover the embedded formation editor's Apply button.
            // Use the same visual style on a narrower right-hand card during interactive lessons.
            banner.anchorMin = banner.anchorMax = new Vector2(interactive ? 0.75f : 0.5f, 0);
            banner.pivot = new Vector2(0.5f, 0);
            banner.anchoredPosition = new Vector2(0, 24);
            banner.sizeDelta = interactive ? new Vector2(560, 230) : new Vector2(880, 175);
            LayoutBannerText(title.rectTransform, interactive ? 0.72f : 0.68f, 0.96f, 40);
            LayoutBannerText(body.rectTransform, interactive ? 0.20f : 0.24f, interactive ? 0.72f : 0.68f, 50);
        }
        private static void LayoutBannerText(RectTransform rect, float bottom, float top, float horizontalInset)
        {
            rect.anchorMin = new Vector2(0, bottom); rect.anchorMax = new Vector2(1, top);
            rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-horizontalInset, 0);
        }
        private void ArrowStroke(string name, Vector2 from, Vector2 to, float thickness, Color tint)
        {
            var delta = to - from;
            var image = LegacyUiKit.CreateImage(squadArrow, name, (from + to) / 2, new Vector2(delta.magnitude, thickness), tint);
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
        private void OnDisable()
        {
            if (session != null) { session.SimulationCreated -= Bind; session.TutorialRewindRequested -= PrepareRewind; }
            StopTutorial(); simulation = null;
        }
    }
}
