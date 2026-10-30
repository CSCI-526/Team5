using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Optional observer only. Removing this entire component cannot change combat.
    public sealed class BattleDebugGizmos : MonoBehaviour
    {
        [SerializeField] private BattleSession session;
        [SerializeField] private bool showRanges = true;
        public BattleSession Session => session;

        private void OnDrawGizmos()
        {
            if (!enabled || session == null || session.Grid == null) return;
            foreach (var cell in session.Grid.SceneCells)
            {
                Gizmos.color = CellColor(cell.Kind);
                Gizmos.DrawCube(cell.transform.position, BattleCell.Size * 0.96f);
                Gizmos.color = new Color(0, 0, 0, 0.6f);
                Gizmos.DrawWireCube(cell.transform.position, BattleCell.Size);
            }
            if (session.Route != null)
            {
                var cells = session.Route.Cells;
                Gizmos.color = Color.cyan;
                for (var i = 1; i < cells.Count; i++)
                    if (cells[i - 1] != null && cells[i] != null)
                        Gizmos.DrawLine(cells[i - 1].TopCenter + Vector3.up * 0.04f, cells[i].TopCenter + Vector3.up * 0.04f);
            }
            foreach (var tower in session.Towers)
            {
                if (tower == null) continue;
                Gizmos.color = new Color(0.3f, 0.6f, 1);
                Gizmos.DrawCube(tower.transform.position + Vector3.up * 0.4f, new Vector3(0.4f, 0.8f, 0.4f));
                Gizmos.DrawWireCube(tower.transform.position + Vector3.up * 0.03f,
                    new Vector3(tower.FootprintSize, 0.06f, tower.FootprintSize));
                if (showRanges)
                {
                    Gizmos.color = new Color(0.3f, 0.6f, 1, 0.3f);
                    var definition = tower.RuntimeState?.Definition ?? tower.CaptureDefinition(0);
                    var bounds = SquadCombatRules.AreaBounds(tower.RuntimeState?.Position ?? tower.transform.position,
                        definition.Area, definition.Range, definition.AttackWidth);
                    Gizmos.DrawWireCube(bounds.center + Vector3.up * 0.035f, bounds.size);
                }
            }
            var simulation = session.Simulation;
            if (simulation == null) return;
            foreach (var unit in simulation.Units)
            {
                if (!unit.IsAlive) continue;
                Gizmos.color = Color.Lerp(Color.red, Color.green, unit.Hp / unit.MaxHp);
                Gizmos.DrawSphere(unit.Position + Vector3.up * 0.22f, 0.19f);
                Gizmos.DrawLine(unit.Position, unit.Position + Vector3.up * 0.55f);
            }
        }

        private static Color CellColor(CellKind kind)
        {
            switch (kind)
            {
                case CellKind.Road: return new Color(0.65f, 0.54f, 0.35f);
                case CellKind.Spawn: return new Color(0.9f, 0.25f, 0.25f);
                case CellKind.Goal: return new Color(0.2f, 0.75f, 0.9f);
                case CellKind.TowerPlatform: return new Color(0.35f, 0.4f, 0.65f);
                case CellKind.Obstacle: return new Color(0.35f, 0.32f, 0.3f);
                default: return new Color(0.38f, 0.53f, 0.39f);
            }
        }
    }
}
