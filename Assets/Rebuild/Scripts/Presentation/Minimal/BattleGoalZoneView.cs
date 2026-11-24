using System.Collections.Generic;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation
{
    // Runtime-only objective markers. Never modify scene assets or create colliders.
    [DisallowMultipleComponent]
    public sealed class BattleGoalZoneView : MonoBehaviour
    {
        private const int Segments = 64;
        private static readonly Color StartColor = new Color(.30f, .67f, 1f);
        private static readonly Color ExitColor = new Color(.13f, .88f, .65f);
        private static readonly Color FillColor = new Color(1f, .82f, .20f);
        private BattleSession session;
        private GameObject world, overlay;
        private RectTransform root;
        private TMP_Text startText, exitText;
        private LineRenderer startRing, exitRing, fillRing, portalFrame, portalLight, entryArrow;
        private Mesh captureMesh;
        private MeshRenderer captureSurface;
        private readonly Vector3[] sectorVertices = new Vector3[Segments + 2];
        private float previousProgress = -1f, previousRingProgress = -1f;
        private double startDepartedAt = -1d;
        private const float StartFadeSeconds = .8f;
        private CanvasGroup startLabelFade;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Renderer> hiddenBeacons = new List<Renderer>();
        private BattleSimulation last;

        private bool ShowTutorialLabels => session != null && session.DisplayedLevelNumber == 1;

        public void Bind(BattleSession value)
        {
            session = value;
            if (world != null) return;
            // Resources keeps this transparent shader available in WebGL player builds.
            var shader = Resources.Load<Shader>("BattleObjectiveUnlit");
            if (shader == null) shader = Shader.Find("Team5/Rebuild/BattleObjectiveUnlit");
            if (shader == null) { Debug.LogWarning("Battle objective shader missing; retaining existing beacons.", this); return; }
            world = new GameObject("Battle objective zones");
            world.transform.SetParent(transform, false);
            startRing = CreateRing("Start boundary", shader, StartColor, .075f);
            exitRing = CreateRing("Exit boundary", shader, ExitColor, .10f);
            fillRing = CreateRing("Exit capture progress", shader, FillColor, .19f);
            fillRing.sharedMaterial.renderQueue = 3003;
            portalFrame = CreateRing("Exit gateway frame", shader, new Color(.035f, .18f, .16f), .23f);
            portalLight = CreateRing("Exit gateway light", shader, ExitColor, .10f);
            entryArrow = CreateRing("Exit direction chevron", shader, ExitColor, .11f);
            portalFrame.alignment = portalLight.alignment = LineAlignment.View;
            CreateCaptureSurface(shader);
            overlay = new GameObject("Battle objective labels", typeof(RectTransform));
            overlay.transform.SetParent(transform, false);
            LegacyUiKit.CreateCanvas(overlay, 11, new Vector2(1280, 720), .5f);
            // No objective label can intercept ground clicks.
            var raycaster = overlay.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            root = (RectTransform)overlay.transform;
            startText = Label("Start", "START", StartColor);
            startLabelFade = startText.transform.parent.gameObject.AddComponent<CanvasGroup>();
            startLabelFade.blocksRaycasts = false;
            startLabelFade.interactable = false;
            exitText = Label("Exit", "EXIT  •  STAY INSIDE 2s", ExitColor);
            SetVisible(false);
        }

        private TMP_Text Label(string name, string value, Color color)
        {
            var plate = LegacyUiKit.CreateImage(root, name + " plate", Vector2.zero,
                new Vector2(250, 37), new Color(.04f, .10f, .15f, .92f), 8);
            var label = LegacyUiKit.CreateText(plate.transform, name, value, Vector2.zero,
                new Vector2(244, 35), 18f, color, FontStyles.Bold);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12;
            label.fontSizeMax = 18;
            return label;
        }

        private LineRenderer CreateRing(string name, Shader shader, Color color, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(world.transform, false);
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.transform.rotation = Quaternion.Euler(90, 0, 0);
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = MakeMaterial(shader, color, 3000);
            return line;
        }

        private Material MakeMaterial(Shader shader, Color color, int queue)
        {
            var mat = new Material(shader) { renderQueue = queue };
            mat.SetColor("_Color", color);
            materials.Add(mat);
            return mat;
        }

        private static void Tint(Renderer renderer, Color color) => renderer.sharedMaterial.SetColor("_Color", color);

        private void CreateCaptureSurface(Shader shader)
        {
            var surface = new GameObject("Exit capture ground fill", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(world.transform, false);
            captureMesh = new Mesh { name = "Exit capture sector" };
            captureMesh.MarkDynamic();
            var triangles = new int[Segments * 3];
            for (var i = 0; i < Segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            captureMesh.vertices = sectorVertices;
            captureMesh.triangles = triangles;
            surface.GetComponent<MeshFilter>().sharedMesh = captureMesh;
            captureSurface = surface.GetComponent<MeshRenderer>();
            captureSurface.sharedMaterial = MakeMaterial(shader, new Color(1f, .82f, .20f, .20f), 2980);
            captureSurface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            captureSurface.receiveShadows = false;
            captureSurface.enabled = false;
        }

        private void DrawCaptureSurface(Vector3 goal, float radius, float progress)
        {
            captureSurface.enabled = progress > 0f;
            if (!captureSurface.enabled || previousProgress == progress) return;
            previousProgress = progress;
            sectorVertices[0] = captureSurface.transform.InverseTransformPoint(goal + Vector3.up * .035f);
            for (var i = 0; i <= Segments; i++)
            {
                var angle = Mathf.PI * .5f - Mathf.PI * 2f * progress * i / Segments;
                var point = goal + new Vector3(Mathf.Cos(angle) * radius, .035f, Mathf.Sin(angle) * radius);
                sectorVertices[i + 1] = captureSurface.transform.InverseTransformPoint(point);
            }
            captureMesh.vertices = sectorVertices;
            captureMesh.RecalculateBounds();
        }

        private void DrawGateway(Vector3 goal, float radius)
        {
            // Face the incoming route. Keep the arch behind the actual capture center.
            var cells = session.Route.Cells;
            var forward = goal - cells[cells.Count - 2].TopCenter;
            forward.y = 0f;
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var basePoint = goal + forward * (radius * .62f) + Vector3.up * .06f;
            var halfWidth = radius * .46f;
            var height = Mathf.Clamp(radius * .63f, .8f, 1.4f);
            var points = new Vector3[19];
            points[0] = basePoint - right * halfWidth;
            for (var i = 0; i <= 16; i++)
            {
                var angle = Mathf.PI * (1f - i / 16f);
                points[i + 1] = basePoint + right * (Mathf.Cos(angle) * halfWidth) +
                    Vector3.up * (height * (.60f + .40f * Mathf.Sin(angle)));
            }
            points[18] = basePoint + right * halfWidth;
            portalFrame.positionCount = portalLight.positionCount = points.Length;
            portalFrame.SetPositions(points);
            portalLight.SetPositions(points);
            portalFrame.sharedMaterial.renderQueue = 3001;
            portalLight.sharedMaterial.renderQueue = 3002;
            // A simple forward chevron, not another ambiguous circle.
            var arrowCenter = goal - forward * (radius * .35f) + Vector3.up * .09f;
            entryArrow.positionCount = 3;
            entryArrow.SetPosition(0, arrowCenter - right * radius * .22f - forward * radius * .12f);
            entryArrow.SetPosition(1, arrowCenter + forward * radius * .12f);
            entryArrow.SetPosition(2, arrowCenter + right * radius * .22f - forward * radius * .12f);
        }

        private float StartVisibility(BattleSimulation sim, Vector3 start)
        {
            if (startDepartedAt < 0d && sim.PendingSpawnCount == 0 && sim.AliveCount > 0)
            {
                var radius = sim.StartZoneRadius;
                var anyInside = false;
                foreach (var unit in sim.Units)
                {
                    if (!unit.IsAlive) continue;
                    var delta = unit.Position - start;
                    delta.y = 0f;
                    if (delta.sqrMagnitude <= radius * radius) { anyInside = true; break; }
                }
                if (!anyInside) startDepartedAt = sim.ElapsedTime;
            }
            return startDepartedAt < 0d ? 1f : 1f - Mathf.SmoothStep(0f, 1f,
                (float)((sim.ElapsedTime - startDepartedAt) / StartFadeSeconds));
        }

        private static void Draw(LineRenderer line, Vector3 center, float radius, float fraction, float height)
        {
            line.enabled = fraction > 0;
            if (!line.enabled) return;
            var count = Mathf.Max(1, Mathf.CeilToInt(Segments * fraction));
            line.positionCount = count + 1;
            for (var i = 0; i <= count; i++)
            {
                var angle = Mathf.PI * .5f - Mathf.PI * 2 * fraction * i / count;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
            }
        }

        private void HideOldBeacons()
        {
            if (hiddenBeacons.Count != 0 || session == null) return;
            foreach (var sceneRoot in session.gameObject.scene.GetRootGameObjects())
                foreach (var child in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != "START Beacon" && child.name != "GOAL Beacon") continue;
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                        if (renderer.enabled) { renderer.enabled = false; hiddenBeacons.Add(renderer); }
                }
        }

        private void RestoreBeacons()
        {
            foreach (var renderer in hiddenBeacons) if (renderer != null) renderer.enabled = true;
            hiddenBeacons.Clear();
        }

        private void LateUpdate()
        {
            if (world == null) return;
            var sim = session != null ? session.Simulation : null;
            if (sim == null || !sim.ContinuousMovementEnabled || sim.Objective != BattleObjective.ReachGoal ||
                sim.Outcome != BattleOutcome.Running || session.Route == null || session.Route.Cells.Count < 2)
            { SetVisible(false); return; }
            SetVisible(true);
            HideOldBeacons();
            var start = session.Route.Cells[0].TopCenter;
            var goal = sim.GoalPosition;
            if (last != sim)
            {
                last = sim;
                startDepartedAt = -1d;
                previousProgress = previousRingProgress = -1f;
                Draw(startRing, start, sim.StartZoneRadius, 1f, .045f);
                Draw(exitRing, goal, sim.GoalZoneRadius, 1f, .055f);
                DrawGateway(goal, sim.GoalZoneRadius);
            }
            var progress = sim.GoalHoldProgress;
            if (previousRingProgress != progress)
            {
                Draw(fillRing, goal, sim.GoalZoneRadius, progress, .075f);
                previousRingProgress = progress;
            }
            DrawCaptureSurface(goal, sim.GoalZoneRadius * .96f, progress);
            // Animation follows simulated time: no misleading progress while paused.
            var pulse = .5f + .5f * Mathf.Sin((float)sim.ElapsedTime * Mathf.PI * 3f);
            var capturing = sim.IsSquadReadyToCapture;
            var accent = capturing ? Color.Lerp(FillColor, Color.white, pulse * .30f) : ExitColor;
            Tint(portalLight, accent);
            Tint(entryArrow, accent);
            Tint(exitRing, capturing ? Color.Lerp(ExitColor, FillColor, .30f + .25f * pulse) : ExitColor);
            portalLight.widthMultiplier = capturing ? .10f + .045f * pulse : .10f;
            fillRing.widthMultiplier = .19f + .065f * pulse;
            Tint(fillRing, accent);
            Tint(captureSurface, new Color(1f, .82f, .20f, .15f + .12f * pulse));
            var visibility = StartVisibility(sim, start);
            startRing.enabled = visibility > .001f;
            Tint(startRing, new Color(StartColor.r, StartColor.g, StartColor.b, visibility));
            startLabelFade.alpha = visibility;
            var camera = Camera.main;
            if (ShowTutorialLabels)
            {
                exitText.text = sim.IsSquadReadyToCapture
                    ? (sim.IsPaused ? "EXIT  •  PAUSED" : $"EXIT  •  {Mathf.CeilToInt(progress * 100)}%")
                    : "EXIT  •  STAY INSIDE 2s";
                if (visibility > .001f) Place(startText, camera, start);
                else startText.transform.parent.gameObject.SetActive(false);
                Place(exitText, camera, goal);
            }
            else
            {
                if (startText != null && startText.transform.parent != null)
                    startText.transform.parent.gameObject.SetActive(false);
                if (exitText != null && exitText.transform.parent != null)
                    exitText.transform.parent.gameObject.SetActive(false);
            }
        }

        private void Place(TMP_Text label, Camera camera, Vector3 position)
        {
            var plate = (RectTransform)label.transform.parent;
            if (camera == null) { plate.gameObject.SetActive(false); return; }
            var screen = camera.WorldToScreenPoint(position + Vector3.up * .65f);
            if (screen.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root,
                new Vector2(screen.x, screen.y), null, out var local))
            { plate.gameObject.SetActive(false); return; }
            local += new Vector2(0, 35);
            plate.gameObject.SetActive(root.rect.Contains(local));
            plate.anchoredPosition = local;
        }

        private void SetVisible(bool visible)
        {
            if (world != null && world.activeSelf != visible) world.SetActive(visible);
            if (overlay != null && overlay.activeSelf != visible) overlay.SetActive(visible);
        }
        private void OnDisable() { SetVisible(false); RestoreBeacons(); }
        private void OnDestroy()
        {
            RestoreBeacons();
            if (world != null) Destroy(world);
            if (overlay != null) Destroy(overlay);
            if (captureMesh != null) Destroy(captureMesh);
            foreach (var mat in materials) if (mat != null) Destroy(mat);
        }
    }
}
