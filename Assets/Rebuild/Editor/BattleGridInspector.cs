using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    [CustomEditor(typeof(BattleGrid))]
    public sealed class BattleGridInspector : UnityEditor.Editor
    {
        private Vector3Int macro;
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var grid = (BattleGrid)target;
            macro = EditorGUILayout.Vector3IntField("Macro (X, layer, Z)", macro);
            var errors = new System.Collections.Generic.List<string>();
            grid.RebuildIndex(errors);
            var session = Object.FindObjectsByType<BattleSession>(FindObjectsSortMode.None).FirstOrDefault(value => value.Grid == grid);
            var cells = grid.GetMacroCells(macro);
            var occupancy = session?.Simulation == null ? null : grid.GetMacroOccupancy(macro, session.Simulation);
            for (var i = 0; i < cells.Length; i++)
            {
                EditorGUILayout.ObjectField($"Small ({i % 3},{i / 3})", cells[i], typeof(BattleCell), true);
                if (occupancy != null)
                    EditorGUILayout.LabelField($"Towers: {string.Join(",", occupancy[i].TowerIds)}  Units: {string.Join(",", occupancy[i].UnitIds)}");
            }
            foreach (var error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
