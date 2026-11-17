using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using Team5.Rebuild.Presentation.LegacyUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Team5.Rebuild.Presentation
{
    [DefaultExecutionOrder(400)]
    public sealed class SquadRouteInput : MonoBehaviour
    {
        private BattlePresentation owner;
        private BattleSimulation drawingSimulation;
        private BattleUnitInspection inspection;
        private bool drawing, dragging, resumePaused, followingRoute;
        private float previousTimeScale;
        private GameObject overlay;
        private TMP_Text hint;
        private LineRenderer line;
        private Material lineMaterial;
        private readonly List<Vector3> path = new List<Vector3>();
        private readonly Dictionary<Renderer, MaterialPropertyBlock> colors = new Dictionary<Renderer, MaterialPropertyBlock>();
        private readonly Dictionary<Renderer, MaterialPropertyBlock> dimColors = new Dictionary<Renderer, MaterialPropertyBlock>();
        private readonly List<(CanvasGroup group, float alpha, bool interactable, bool blocks, bool added)> groups = new List<(CanvasGroup, float, bool, bool, bool)>();
        public void Bind(BattlePresentation presentation) => owner = presentation;
        public static SquadRouteInput ForSession(BattleSession session)
        {
            foreach (var control in Object.FindObjectsByType<SquadRouteInput>(FindObjectsSortMode.None))
                if (control.owner != null && control.owner.Session == session) return control;
            return null;
        }
        private UnitState Leader()
        {
            if (owner != null && owner.Session.Simulation != null)
                foreach (var unit in owner.Session.Simulation.Units) if (unit.IsAlive) return unit;
            return null;
        }
        public void BeginDrawing()
        {
            if (drawing || owner == null || !owner.Session.ManualSquadMovement || Leader() == null) return;
            inspection = owner.GetComponent<BattleUnitInspection>();
            if (inspection != null) inspection.CloseImmediately();
            drawingSimulation = owner.Session.Simulation;
            resumePaused = drawingSimulation.IsPaused;
            drawingSimulation.SetPaused(true);
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            if (inspection != null) inspection.IsInputSuppressed = true;
            drawing = true; dragging = false; followingRoute = false; path.Clear();
            var anchor = Leader().AnchorPosition;
            var protectedCells = new HashSet<Vector3Int>();
            for (var slot = 0; slot < 9; slot++)
                foreach (var cell in FormationClearance.SweptCells(anchor + SquadCombatRules.Offset(slot), anchor + SquadCombatRules.Offset(slot)))
                    protectedCells.Add(cell);
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.GetComponentInParent<UnitView>() != null || renderer == line) continue;
                var cell = renderer.GetComponentInParent<BattleCell>();
                if (cell != null && protectedCells.Contains(cell.Occupancy)) continue;
                var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original); colors[renderer] = original;
                var dim = new MaterialPropertyBlock(); renderer.GetPropertyBlock(dim);
                var tint = original.HasColor("_BaseColor") ? original.GetColor("_BaseColor") :
                    renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor") ? renderer.sharedMaterial.GetColor("_BaseColor") : Color.white;
                tint.r *= .18f; tint.g *= .18f; tint.b *= .18f;
                dim.SetColor("_BaseColor", tint); dim.SetColor("_Color", tint); dimColors[renderer] = dim;
            }
            ApplyDimming();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                var group = canvas.GetComponent<CanvasGroup>(); var added = group == null;
                if (added) group = canvas.gameObject.AddComponent<CanvasGroup>();
                groups.Add((group, group.alpha, group.interactable, group.blocksRaycasts, added));
                group.alpha = .18f; group.interactable = false; group.blocksRaycasts = false;
            }
            overlay = new GameObject("Squad Route Planning", typeof(RectTransform));
            LegacyUiKit.CreateCanvas(overlay, 2000, new Vector2(1280, 720), .5f);
            var blocker = LegacyUiKit.CreateImage(overlay.transform, "Input Blocker", Vector2.zero, Vector2.zero, Color.clear);
            LegacyUiKit.Stretch(blocker.rectTransform); blocker.raycastTarget = true;
            hint = LegacyUiKit.CreateText(overlay.transform, "Instructions", "Drag in 3-cell steps. Turn at right angles. Release to move.  |  Esc: cancel", new Vector2(0, 310), new Vector2(1200, 50), 21, LegacyUiKit.HighlightColor, FontStyles.Bold);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (line == null)
            {
                line = new GameObject("Squad Planned Route").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false); line.useWorldSpace = true; line.widthMultiplier = .1f;
                lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                lineMaterial.SetColor("_BaseColor", new Color(.15f, 1, .8f)); line.sharedMaterial = lineMaterial;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            line.positionCount = 0; line.enabled = true;
        }

        // Tower and range presentation refresh their colors every frame. Apply the mask afterwards.
        private void LateUpdate()
        {
            if (drawing) ApplyDimming();
            else if (followingRoute) RefreshRemainingRoute();
        }
        private void RefreshRemainingRoute()
        {
            var leader = Leader();
            if (line == null || owner.Session.Simulation != drawingSimulation || leader == null || leader.NextWaypoint >= path.Count)
            {
                followingRoute = false;
                if (line != null) { line.positionCount = 0; line.enabled = false; }
                return;
            }
            // Use waypoint progress rather than nearest-point matching, so loops and crossings stay correct.
            var next = Mathf.Max(1, leader.NextWaypoint);
            line.positionCount = path.Count - next + 1;
            line.SetPosition(0, leader.AnchorPosition + Vector3.up * .08f);
            for (var i = next; i < path.Count; i++)
                line.SetPosition(i - next + 1, path[i] + Vector3.up * .08f);
        }
        private void ApplyDimming()
        {
            foreach (var entry in dimColors) if (entry.Key != null) entry.Key.SetPropertyBlock(entry.Value);
        }
        private bool PointerOnGround(out Vector3 point)
        {
            point = default;
            var camera = Camera.main;
            var unit = Leader();
            if (camera == null || unit == null || Mouse.current == null) return false;
            var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var plane = new Plane(Vector3.up, unit.AnchorPosition);
            if (!plane.Raycast(ray, out var distance)) return false;
            point = ray.GetPoint(distance); return true;
        }
        private bool CanRouteAnchor(Vector3 point, Vector3 previous)
        {
            var sim = owner.Session.Simulation;
            if (!sim.CanPlaceSquadAt(point)) return false;
            var key = SmallCellOccupancy.PositionToCell(point);
            if (!owner.Session.Grid.TryGetCell(key, out var middle)) return false;
            var leavingSpawn = false;
            if (middle.Kind == CellKind.Spawn)
            {
                leavingSpawn = owner.Session.Grid.TryGetCell(SmallCellOccupancy.PositionToCell(previous), out var prior) && prior.Kind == CellKind.Spawn;
            }
            if (middle.Kind != CellKind.Ground && middle.Kind != CellKind.Road && middle.Kind != CellKind.Goal && !leavingSpawn) return false;
            foreach (var member in sim.Units)
                if (member.IsAlive)
                {
                    var cell = SmallCellOccupancy.PositionToCell(point + member.FormationOffset);
                    if (!owner.Session.Grid.TryGetCell(cell, out var support) ||
                        support.Kind == CellKind.Obstacle || support.Kind == CellKind.TowerPlatform) return false;
                    if (support.Kind == CellKind.Spawn &&
                        (!owner.Session.Grid.TryGetCell(SmallCellOccupancy.PositionToCell(previous + member.FormationOffset), out var priorSupport) ||
                         priorSupport.Kind != CellKind.Spawn)) return false;
                }
            return true;
        }
        private bool CanRouteSegment(Vector3 from, Vector3 to)
        {
            // Every intervening Small Cell must fit the squad, even though drawing advances three at a time.
            var direction = (to - from) / BattleSimulation.ManualRouteStep;
            for (var cell = 1; cell <= BattleSimulation.ManualRouteStep; cell++)
                if (!CanRouteAnchor(from + direction * cell, from + direction * (cell - 1))) return false;
            return true;
        }
        private static float SnapDistance(float distance) => Mathf.Sign(distance) *
            Mathf.Floor(Mathf.Abs(distance) / BattleSimulation.ManualRouteStep) * BattleSimulation.ManualRouteStep;
        private void Update()
        {
            if (!drawing) return;
            if (owner == null || owner.Session.Simulation != drawingSimulation || Leader() == null) { Cancel(); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var released = dragging && mouse.leftButton.wasReleasedThisFrame;
            if (!PointerOnGround(out var cursor)) { if (released) CompleteDrawing(); return; }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                var start = Leader().AnchorPosition;
                var near = Mathf.Abs(cursor.x - start.x) <= 1.6f && Mathf.Abs(cursor.z - start.z) <= 1.6f;
                var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
                foreach (var unit in drawingSimulation.Units)
                    if (unit.IsAlive && owner.TryGetView(unit.Id, out var view) && view.TryGetVisibleBounds(out var bounds) && bounds.IntersectRay(ray)) near = true;
                if (!near) return;
                dragging = true; path.Clear(); path.Add(start);
            }
            if (dragging && (mouse.leftButton.isPressed || released))
            {
                // Snap relative to the actual squad position, including when replanning while moving.
                var target = path[0] + new Vector3(SnapDistance(cursor.x - path[0].x), 0, SnapDistance(cursor.z - path[0].z));
                for (var i = 0; i < 200; i++)
                {
                    var previous = path[path.Count - 1]; var delta = target - previous;
                    if (delta.sqrMagnitude < .001f) break;
                    var next = previous + BattleSimulation.ManualRouteStep *
                        (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z) ? Vector3.right * Mathf.Sign(delta.x) : Vector3.forward * Mathf.Sign(delta.z));
                    if (path.Count > 1 && Vector3.Distance(next, path[path.Count - 2]) < .01f) path.RemoveAt(path.Count - 1);
                    else
                    {
                        if (!CanRouteSegment(previous, next)) { hint.text = "Blocked: the whole 3-cell step must be clear. Draw around obstacles."; break; }
                        path.Add(next);
                    }
                }
                line.positionCount = path.Count;
                for (var i = 0; i < path.Count; i++) line.SetPosition(i, path[i] + Vector3.up * .08f);
            }
            if (released) CompleteDrawing();
        }

        private void CompleteDrawing()
        {
            var valid = drawingSimulation.TrySetManualRoute(path);
            EndDrawing();
            drawingSimulation.SetPaused(valid ? false : resumePaused);
            followingRoute = valid;
            if (valid) RefreshRemainingRoute();
            if (!valid && line != null) line.enabled = false;
        }
        private void EndDrawing()
        {
            drawing = false; dragging = false;
            Time.timeScale = previousTimeScale;
            foreach (var entry in colors) if (entry.Key != null) entry.Key.SetPropertyBlock(entry.Value);
            colors.Clear();
            dimColors.Clear();
            foreach (var entry in groups)
                if (entry.group != null)
                {
                    entry.group.alpha = entry.alpha; entry.group.interactable = entry.interactable; entry.group.blocksRaycasts = entry.blocks;
                    if (entry.added) Destroy(entry.group);
                }
            groups.Clear();
            if (overlay != null) { overlay.SetActive(false); Destroy(overlay); overlay = null; }
            if (inspection != null) inspection.IsInputSuppressed = false;
        }
        public void Cancel()
        {
            followingRoute = false;
            if (line != null) line.enabled = false;
            if (!drawing) return;
            EndDrawing();
            if (drawingSimulation != null && drawingSimulation.Outcome == BattleOutcome.Running) drawingSimulation.SetPaused(resumePaused);
        }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (line != null) Destroy(line.gameObject);
            if (lineMaterial != null) Destroy(lineMaterial);
        }
    }
}
