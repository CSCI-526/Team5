using System.Collections.Generic;
using System.Linq;
using Team5.BasicTowerTrial.Presentation.LegacyUI;
using Team5.BasicTowerTrial.Session;
using Team5.BasicTowerTrial.Simulation;
using Team5.BasicTowerTrial.World;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed class TacticalAssault : MonoBehaviour
    {
        public const string ScenePath = "Assets/Rebuild/Scenes/2.unity";
        private static readonly Color SquadColor = new Color(.32f, .9f, .94f);
        private static readonly Color RamColor = new Color(1f, .72f, .31f);
        private enum DeploymentSlot { Squad, Ram }
        private readonly List<DeploymentSlot> actionOrder = new List<DeploymentSlot>();
        private readonly Dictionary<DeploymentSlot, Button> slotButtons = new Dictionary<DeploymentSlot, Button>();
        private readonly Dictionary<DeploymentSlot, TMP_Text> orderBadges = new Dictionary<DeploymentSlot, TMP_Text>();
        private DeploymentSlot? selectedSlot;
        private GameObject deploymentBar;
        private bool EditRam => selectedSlot == DeploymentSlot.Ram;
        private bool RamFirst => actionOrder.Count > 0 && actionOrder[0] == DeploymentSlot.Ram;

        private readonly List<Order> orders = new List<Order>();
        private readonly List<Vector3> ramRoute = new List<Vector3>();
        private readonly List<Vector3> routeDraft = new List<Vector3>();
        private readonly List<RaycastResult> hudHits = new List<RaycastResult>();
        private BattleSession session;
        private BattleSimulation simulation;
        private SiegeRamFleet fleet;
        private LegacyBattleHud hud;
        private Camera battleCamera, deploymentCamera;
        private BattleCell[] cells;
        private Vector3 origin, exit;
        private float minX, maxZ, mapScale;
        private Vector2 mapOffset;
        private bool planning = true, ramLaunched, travelling, finished, dragging, draftValid;
        private int nextOrder;
        private double retrySince = -1, movementStarted;
        private string status = "Select a unit below to assign its action order.";
        private GUIStyle label;

        private sealed class Order
        {
            public Vector3 Target;
            public IReadOnlyList<Vector3> Preview;
        }

        private void Start()
        {
            var roots = gameObject.scene.GetRootGameObjects();
            session = roots.SelectMany(root => root.GetComponentsInChildren<BattleSession>()).Single();
            fleet = roots.SelectMany(root => root.GetComponentsInChildren<SiegeRamFleet>()).FirstOrDefault();
            battleCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).FirstOrDefault(camera => camera.CompareTag("MainCamera"));
            if (session.Simulation == null) session.StartBattle();
            simulation = session.Simulation;
            if (simulation == null || !simulation.ContinuousMovementEnabled)
            {
                status = "Battle configuration could not start.";
                return;
            }

            simulation.SetPaused(true);
            hud = roots.SelectMany(root => root.GetComponentsInChildren<LegacyBattleHud>())
                .FirstOrDefault(view => view.Session == session);
            if (hud != null) hud.TutorialOwnsPause = true;
            if (fleet != null) fleet.HideVehicleHud = true;
            origin = session.Route.Cells[0].TopCenter;
            var goal = session.Grid.SceneCells.Where(cell => cell.Kind == CellKind.Goal)
                .OrderBy(cell => (cell.TopCenter - session.Route.Cells.Last().TopCenter).sqrMagnitude).FirstOrDefault();
            exit = goal != null ? goal.TopCenter : session.Route.Cells.Last().TopCenter;
            cells = session.Grid.SceneCells.GroupBy(cell => new Vector2Int(cell.Occupancy.x, cell.Occupancy.z))
                .Select(column => column.OrderByDescending(cell => cell.Occupancy.y).First()).ToArray();
            minX = cells.Min(cell => cell.TopCenter.x) - .5f;
            maxZ = cells.Max(cell => cell.TopCenter.z) + .5f;
            var width = cells.Max(cell => cell.TopCenter.x) + .5f - minX;
            var height = maxZ - cells.Min(cell => cell.TopCenter.z) + .5f;
            mapScale = Mathf.Min(1040 / width, 475 / height);
            mapOffset = new Vector2(120 + (1040 - width * mapScale) / 2, 95 + (475 - height * mapScale) / 2);
            var view = new GameObject("2D Deployment Camera");
            view.transform.SetParent(transform, false);
            // Match the center of the UI's reference frame so a tile and its overlay share a projection.
            var cameraCenter = new Vector3(minX + (640 - mapOffset.x) / mapScale,
                cells.Max(cell => cell.TopCenter.y) + 40, maxZ - (360 - mapOffset.y) / mapScale);
            view.transform.SetPositionAndRotation(cameraCenter, Quaternion.Euler(90, 0, 0));
            deploymentCamera = view.AddComponent<Camera>();
            deploymentCamera.orthographic = true;
            deploymentCamera.clearFlags = CameraClearFlags.SolidColor;
            deploymentCamera.backgroundColor = new Color(.055f, .085f, .115f);
            if (battleCamera != null)
            {
                deploymentCamera.cullingMask = battleCamera.cullingMask;
                battleCamera.enabled = false;
            }
            FitDeploymentCamera();
            if (fleet != null) ramRoute.Add(fleet.SpawnPosition);
            SuppressBattleInput(roots);
            BuildDeploymentBar();
        }

        private void SuppressBattleInput(IEnumerable<GameObject> roots)
        {
            foreach (var root in roots)
            foreach (var inspection in root.GetComponentsInChildren<BattleUnitInspection>())
                inspection.IsInputSuppressed = true;
        }

        private void LateUpdate()
        {
            if (planning && simulation != null) FitDeploymentCamera();
        }

        private void BuildDeploymentBar()
        {
            deploymentBar = new GameObject("Deployment slots", typeof(RectTransform));
            deploymentBar.transform.SetParent(transform, false);
            LegacyUiKit.CreateCanvas(deploymentBar, 20, new Vector2(1280, 720), .5f);
            CreateSlot(DeploymentSlot.Squad, fleet == null ? 0 : -63, "SQUAD");
            if (fleet != null) CreateSlot(DeploymentSlot.Ram, 63, "SIEGE RAM");
            RefreshSlots();
        }

        private void CreateSlot(DeploymentSlot slot, float x, string name)
        {
            var button = LegacyUiKit.CreateButton(deploymentBar.transform, name, new Vector2(x, -239),
                new Vector2(112, 74), LegacyUiKit.PanelColor, 6);
            if (slot == DeploymentSlot.Squad)
            {
                var member = simulation.PlannedMembers.FirstOrDefault(unit => unit.Unit.Role == UnitRole.Captain)
                    ?? simulation.PlannedMembers.FirstOrDefault();
                LegacyUnitIconUI.Create(button.transform, new Vector2(0, 11), 104)
                    .SetType(member?.Unit.Role ?? UnitRole.Attack);
            }
            else
            {
                var image = LegacyUiKit.CreateImage(button.transform, "Ram icon", new Vector2(0, 10),
                    new Vector2(56, 43), Color.white);
                image.sprite = fleet.PlanningIcon;
                image.preserveAspect = true;
                image.rectTransform.localScale = new Vector3(-1, 1, 1);
            }

            LegacyUiKit.CreateText(button.transform, "Label", name, new Vector2(0, -24),
                new Vector2(100, 20), 13, Color.white);
            var badge = LegacyUiKit.CreateCircle(button.transform, "Action order", new Vector2(46, 31), 25, SquadColor);
            var number = LegacyUiKit.CreateText(badge.transform, "Number", "", Vector2.zero,
                new Vector2(25, 25), 16, new Color(.06f, .10f, .14f), FontStyles.Bold);
            slotButtons.Add(slot, button);
            orderBadges.Add(slot, number);
            button.onClick.AddListener(() => SelectSlot(slot));
        }

        private void SelectSlot(DeploymentSlot slot)
        {
            if (dragging) return;
            if (selectedSlot.HasValue && selectedSlot != slot && !actionOrder.Contains(selectedSlot.Value))
            {
                status = "Save the selected route first, or press Esc to cancel it.";
                return;
            }
            selectedSlot = slot;
            status = EditRam ? "Drag from R to draw the ram route." : "Drag from S to draw the squad route.";
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            foreach (var item in slotButtons)
            {
                var slot = item.Key;
                var selected = selectedSlot == slot;
                var index = actionOrder.IndexOf(slot);
                item.Value.targetGraphic.color = selected ? LegacyUiKit.NodeBlueColor : LegacyUiKit.PanelColor;
                var badge = orderBadges[slot];
                badge.transform.parent.gameObject.SetActive(index >= 0 || selected);
                badge.text = (index >= 0 ? index + 1 : actionOrder.Count + 1).ToString();
            }
        }

        private void CommitSlot(DeploymentSlot slot)
        {
            if (!actionOrder.Contains(slot)) actionOrder.Add(slot);
            RefreshSlots();
        }

        private static float UiScale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);

        private void FitDeploymentCamera()
        {
            if (deploymentCamera != null && UiScale > 0)
                deploymentCamera.orthographicSize = Screen.height / (2 * UiScale * mapScale);
        }

        private void Update()
        {
            if (planning || simulation == null || finished || simulation.IsPaused) return;
            if (simulation.Outcome != BattleOutcome.Running)
            {
                finished = true;
                status = simulation.Outcome == BattleOutcome.Victory ? "Assault complete" : "Squad defeated";
                return;
            }
            var elapsed = simulation.ElapsedTime;
            if (elapsed > 150)
            {
                finished = true;
                simulation.SetPaused(true);
                status = "Assault timed out · restart to revise the plan";
                return;
            }
            if (simulation.InitialDeploymentHeld)
            {
                if (!ramLaunched)
                    ramLaunched = fleet.TryLaunchPlannedRoute(ramRoute);
                if (ramLaunched && !fleet.SpawnOccupied())
                {
                    simulation.SetInitialDeploymentHeld(false);
                    status = "Ram cleared the launch area · deploying squad";
                }
                return;
            }
            if (simulation.PendingSpawnCount > 0) return;
            if (!RamFirst && !ramLaunched && ramRoute.Count > 1 && (travelling || nextOrder > 0))
                ramLaunched = fleet.TryLaunchPlannedRoute(ramRoute);
            if (travelling)
            {
                if (simulation.IsSquadMoving)
                {
                    if (elapsed - movementStarted > 95)
                    {
                        finished = true;
                        simulation.SetPaused(true);
                        status = "Squad could not reach its waypoint · restart to revise the plan";
                    }
                    return;
                }
                travelling = false;
                nextOrder++;
            }
            if (nextOrder >= orders.Count)
            {
                status = orders.Last().Target == exit ? "Assault complete · squad reached the exit" : "Orders complete · squad holding position";
                finished = orders.Last().Target == exit &&
                    (ramRoute.Count <= 1 || ramLaunched && fleet.Vehicles.All(vehicle => !vehicle.IsMoving));
                if (finished) simulation.SetPaused(true);
                return;
            }
            if (simulation.RequestMovement(new MovementRequest(orders[nextOrder].Target, source: MovementCommandSource.AI)))
            {
                travelling = true;
                movementStarted = elapsed;
                retrySince = -1;
                status = $"Moving to waypoint {nextOrder + 1} / {orders.Count}";
                return;
            }
            if (retrySince < 0) retrySince = elapsed;
            status = $"Waypoint {nextOrder + 1} blocked · waiting for a clear route";
            if (elapsed - retrySince > 12)
            {
                finished = true;
                simulation.SetPaused(true);
                status = "Route blocked · restart to revise the plan";
            }
        }

        private void BeginAssault()
        {
            if (!planning || dragging || simulation == null || orders.Count == 0 ||
                (selectedSlot.HasValue && !actionOrder.Contains(selectedSlot.Value))) return;
            planning = false;
            if (hud != null) hud.TutorialOwnsPause = false;
            if (fleet != null) fleet.HideVehicleHud = ramRoute.Count > 1;
            if (deploymentBar != null) deploymentBar.SetActive(false);
            if (RamFirst) simulation.SetInitialDeploymentHeld(true);
            if (deploymentCamera != null) deploymentCamera.enabled = false;
            if (battleCamera != null) battleCamera.enabled = true;
            status = RamFirst ? "Deploying siege ram" : "Deploying squad";
            simulation.SetPaused(false);
        }

        private bool SetSquadRoute(IReadOnlyList<Vector3> points)
        {
            var replacement = new List<Order>();
            for (var i = 1; i < points.Count; i++)
            {
                var target = points[i];
                var preview = simulation.PreviewSquadPath(points[i - 1], target);
                if (preview == null) return false;
                replacement.Add(new Order { Target = target, Preview = preview });
            }
            if (replacement.Count == 0) return false;
            orders.Clear();
            orders.AddRange(replacement);
            CommitSlot(DeploymentSlot.Squad);
            return true;
        }

        private void HandleRouteDrag()
        {
            var current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && selectedSlot.HasValue)
            {
                dragging = false;
                selectedSlot = null;
                RefreshSlots();
                status = "Select a unit below to edit its route.";
                current.Use();
                return;
            }
            if (!selectedSlot.HasValue) return;
            if (current.type == EventType.MouseDown && current.button == 1 && !PointerOverBattleHud())
            {
                if (dragging) dragging = false;
                else if (EditRam && ramRoute.Count > 1) ramRoute.RemoveAt(ramRoute.Count - 1);
                else if (!EditRam && orders.Count > 0) orders.RemoveAt(orders.Count - 1);
                if (EditRam ? ramRoute.Count <= 1 : orders.Count == 0)
                    actionOrder.Remove(selectedSlot.Value);
                RefreshSlots();
                status = EditRam ? $"Ram segments: {ramRoute.Count - 1}" : $"Squad waypoints: {orders.Count}";
                current.Use();
                return;
            }
            var start = EditRam ? ramRoute[0] : origin;
            var position = current.mousePosition;
            var cursor = new Vector3(minX + (position.x - mapOffset.x) / mapScale, start.y,
                maxZ - (position.y - mapOffset.y) / mapScale);
            if (current.type == EventType.MouseDown && current.button == 0 && !PointerOverBattleHud() &&
                (cursor - start).sqrMagnitude <= 1.5f * 1.5f)
            {
                routeDraft.Clear();
                routeDraft.Add(start);
                dragging = true;
                draftValid = true;
                current.Use();
                return;
            }
            if (!dragging || current.button != 0 ||
                (current.type != EventType.MouseDrag && current.type != EventType.MouseUp)) return;

            System.Func<Vector3, bool> passable = EditRam ? fleet.IsRoutePassable : simulation.CanStandOnSquadRoute;
            draftValid = SiegeRamMotion.ExtendRouteDraft(routeDraft, cursor, passable);
            if (current.type == EventType.MouseUp)
            {
                dragging = false;
                if (!draftValid || routeDraft.Count < 2 || (EditRam && !fleet.CanUsePlannedRoute(routeDraft)))
                    status = "Draw a clear route to the next 3 x 3 center.";
                else if (EditRam)
                {
                    ramRoute.Clear();
                    ramRoute.AddRange(routeDraft);
                    CommitSlot(DeploymentSlot.Ram);
                    status = $"Ram route ready · {ramRoute.Count - 1} segments";
                }
                else if (SetSquadRoute(routeDraft))
                    status = $"Squad route ready · {orders.Count} segments";
                else status = "No squad route through those points.";
            }
            else status = draftValid ? "Release to save the route." : "Blocked · move the cursor back or draw around the obstacle.";
            current.Use();
        }

        private Vector2 Project(Vector3 point) => mapOffset + new Vector2(point.x - minX, maxZ - point.z) * mapScale;

        private void OnGUI()
        {
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            var previous = GUI.matrix;
            var scale = UiScale;
            if (scale <= 0) return;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2),
                Quaternion.identity, Vector3.one * scale);
            try
            {
                if (planning && cells != null) DrawMap();
                Fill(new Rect(24, 640, 780, 52), new Color(.07f, .1f, .14f, .92f));
                var ramStatus = !planning && ramRoute.Count > 1 && !ramLaunched ? "    |    Ram queued until spawn clears" : "";
                GUI.Label(new Rect(38, 650, 755, 34), status + ramStatus, label);
                var previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && (!planning || (!dragging && orders.Count > 0 && (!selectedSlot.HasValue || actionOrder.Contains(selectedSlot.Value))));
                var clicked = GUI.Button(new Rect(1030, 642, 220, 50),
                    planning ? "Begin 3D assault" : "Restart deployment");
                GUI.enabled = previousEnabled;
                if (clicked)
                {
                    if (planning) BeginAssault();
                    else Reload();
                }
            }
            finally { GUI.matrix = previous; }
        }

        private void DrawMap()
        {
            HandleRouteDrag();
            for (var i = 0; i < orders.Count && (!dragging || EditRam); i++)
            {
                var order = orders[i];
                DrawRoute(order.Preview, SquadColor);
                Marker(order.Target, (i + 1).ToString(), SquadColor);
            }
            if (!dragging || !EditRam) DrawRoute(ramRoute, RamColor);
            if (dragging) DrawRoute(routeDraft, draftValid ? (EditRam ? RamColor : SquadColor) : Color.red);
            var showRam = fleet != null && (EditRam || ramRoute.Count > 1);
            var sharesOrigin = showRam && (ramRoute[0] - origin).sqrMagnitude < .001f;
            Marker(origin, sharesOrigin ? "S / R" : "S", sharesOrigin && EditRam ? RamColor : SquadColor);
            Marker(exit, "Exit", new Color(.35f, .92f, .55f));
            if (showRam && !sharesOrigin)
                Marker(ramRoute[0], "R", RamColor);
        }

        private bool PointerOverBattleHud()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            var position = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            position.y = Screen.height - position.y;
            hudHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, hudHits);
            return hudHits.Any(hit => hit.gameObject.GetComponentInParent<Selectable>() != null);
        }

        private void DrawRoute(IReadOnlyList<Vector3> route, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            const float halfWidth = 2;
            var viewMatrix = GUI.matrix;
            try
            {
                for (var i = 1; i < route.Count; i++)
                {
                    var start = Project(route[i - 1]);
                    var direction = Project(route[i]) - start;
                    if (direction.sqrMagnitude < .001f) continue;
                    var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    var segmentMatrix = Matrix4x4.TRS(new Vector3(start.x, start.y, 0),
                        Quaternion.Euler(0, 0, angle), Vector3.one);
                    // Build the segment in map coordinates before applying Game view scaling.
                    GUI.matrix = viewMatrix * segmentMatrix;
                    Fill(new Rect(-halfWidth, -halfWidth, direction.magnitude + 2 * halfWidth, 2 * halfWidth), color);
                }
            }
            finally { GUI.matrix = viewMatrix; }
        }

        private void Marker(Vector3 point, string value, Color color)
        {
            var screen = Project(point);
            Fill(new Rect(screen.x - 10, screen.y - 10, 20, 20), color);
            GUI.Label(new Rect(screen.x + 11, screen.y - 12, 80, 24), value);
        }

        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Reload()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(ScenePath);
#endif
        }
    }
}
