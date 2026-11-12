using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    [CustomEditor(typeof(BattleSession))]
    public sealed class BattleSessionInspector : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var session = (BattleSession)target;
            if (GUILayout.Button("Validate Scene"))
            {
                var errors = session.ValidateConfiguration();
                if (errors.Count == 0) Debug.Log("Battle Scene is valid.", session);
                else Debug.LogError(string.Join("\n", errors), session);
            }
            if (Application.isPlaying && GUILayout.Button("Start / Restart Battle")) session.StartBattle();
            var simulation = session.Simulation;
            if (simulation == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{simulation.Outcome} | {simulation.ElapsedTime:F2}s | Lives {simulation.LivesRemaining}");
            EditorGUILayout.LabelField($"Spawned {simulation.SpawnedCount}  Alive {simulation.AliveCount}  Dead {simulation.KilledCount}  Goal {simulation.ReachedGoalCount}");
            foreach (var tower in simulation.Towers)
                EditorGUILayout.LabelField($"Tower {tower.Id}: HP {tower.Hp:F1}, cooldown {tower.CooldownRemaining:F2}, target {tower.TargetId?.ToString() ?? "-"}, {tower.Position}");
            foreach (var unit in simulation.Units)
                EditorGUILayout.LabelField($"#{unit.MemberId} {unit.Role} {unit.LifeState} | HP {unit.Hp:F1}/{unit.MaxHp:F1} | {unit.Position} | Squad {unit.SquadId} Slot {unit.SlotId} Cell {unit.OccupiedCell}");
        }
    }

    [CustomEditor(typeof(BattleTower))]
    public sealed class BattleTowerInspector : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            var tower = (BattleTower)target;
            if (tower.IsWall)
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHp"), new GUIContent("Wall HP"));
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.HelpBox("3x1 road wall. HP only; no attacks or bounty. Rotate around Y in 90-degree steps to choose its orientation.", MessageType.Info);
                using (new EditorGUI.DisabledScope(Application.isPlaying || EditorUtility.IsPersistent(tower)))
                    if (GUILayout.Button("Snap Wall To Road Surface")) SnapWallToRoad(tower);
            }
            else DrawDefaultInspector();
            var state = tower.RuntimeState;
            if (state != null) EditorGUILayout.LabelField($"Cooldown {state.CooldownRemaining:F2} | Target {state.TargetId?.ToString() ?? "-"}");
        }

        private static void SnapWallToRoad(BattleTower wall)
        {
            BattleCell nearest = null;
            var distance = float.PositiveInfinity;
            var position = wall.transform.position;
            foreach (var root in wall.gameObject.scene.GetRootGameObjects())
                foreach (var cell in root.GetComponentsInChildren<BattleCell>())
                {
                    if (cell.Kind != CellKind.Road || Mathf.Abs(cell.transform.position.x - position.x) > .5f ||
                        Mathf.Abs(cell.transform.position.z - position.z) > .5f) continue;
                    var gap = Mathf.Abs(cell.TopCenter.y - position.y);
                    if (gap >= distance) continue;
                    nearest = cell;
                    distance = gap;
                }
            if (nearest == null)
            {
                Debug.LogWarning("No Road cell under this wall. Move the wall over the road first.", wall);
                return;
            }
            Undo.RecordObject(wall.transform, "Snap Wall To Road Surface");
            wall.transform.position = nearest.TopCenter;
            PrefabUtility.RecordPrefabInstancePropertyModifications(wall.transform);
        }
    }

    public static class BattleDebugLabels
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawLabels(BattleDebugGizmos view, GizmoType type)
        {
            if (!view.enabled || view.Session == null) return;
            var session = view.Session;
            if (session.Route != null)
            {
                for (var i = 0; i < session.Route.Cells.Count; i++)
                {
                    var cell = session.Route.Cells[i];
                    if (cell != null) Handles.Label(cell.TopCenter + Vector3.up * 0.1f, $"{i}: {cell.Kind}");
                }
            }
            foreach (var tower in session.Towers)
                if (tower != null) Handles.Label(tower.transform.position + Vector3.up, $"{tower.name} R={tower.Range:F1} CD={tower.CurrentCooldown:F1}");
            if (session.Simulation == null) return;
            foreach (var unit in session.Simulation.Units)
                if (unit.IsAlive) Handles.Label(unit.Position + Vector3.up * 0.6f, $"#{unit.Id} HP {unit.Hp:F0}");
        }
    }
}
