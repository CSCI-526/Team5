using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionSiteScan
    {
        public const string DressingRootName = "Paint Squad Dressing";

        public static MinionSites Scan(BattleSession session)
        {
            var sites = new MinionSites();
            if (session == null || session.Grid == null) return sites;
            var grid = session.Grid;
            grid.RebuildIndex(new List<string>());
            var blocked = new HashSet<Vector2Int>();
            foreach (var tower in session.Towers)
            {
                if (tower == null) continue;
                var center = SmallCellOccupancy.PositionToCell(tower.transform.position);
                var footprint = tower.Footprint;
                for (var z = -footprint.y / 2; z <= footprint.y / 2; z++)
                    for (var x = -footprint.x / 2; x <= footprint.x / 2; x++)
                        blocked.Add(new Vector2Int(center.x + x, center.z + z));
            }
            var threats = new List<OpenStageThreat>();
            foreach (var root in session.gameObject.scene.GetRootGameObjects())
            {
                threats.AddRange(root.GetComponentsInChildren<OpenStageThreat>(true));
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == DressingRootName) AddClutter(sites, child);
            }
            foreach (var cell in grid.SceneCells)
            {
                if (cell == null || !cell.gameObject.activeInHierarchy || !grid.HasClearTop(cell)) continue;
                var occupancy = cell.Occupancy;
                var key = new Vector2Int(occupancy.x, occupancy.z);
                if (session.IsMovementSurface(cell)) sites.AddWalkable(key);
                if (cell.Kind != CellKind.Road && cell.Kind != CellKind.Ground) continue;
                // Keep the entire three-cell route clear for a nine-member formation.
                // Also excludes road cells from guard hopping destinations.
                if (session.UsesTacticalRoad && cell.Kind == CellKind.Road) continue;
                if (blocked.Contains(key) || NearThreat(cell.TopCenter, threats, 0f, MinionRules.ThreatBody)) continue;
                if (NearThreat(cell.TopCenter, threats, 1f, MinionRules.ThreatMargin)) sites.AddStandable(key, cell.TopCenter);
                else sites.AddHost(key, cell.TopCenter);
            }
            return sites;
        }

        private static void AddClutter(MinionSites sites, Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                var bounds = renderer.bounds;
                for (var x = Mathf.RoundToInt(bounds.min.x); x <= Mathf.RoundToInt(bounds.max.x); x++)
                    for (var z = Mathf.RoundToInt(bounds.min.z); z <= Mathf.RoundToInt(bounds.max.z); z++)
                        sites.AddClutter(new Vector2Int(x, z));
            }
        }

        private static bool NearThreat(Vector3 position, List<OpenStageThreat> threats, float radiusShare, float margin)
        {
            foreach (var threat in threats)
            {
                if (threat == null) continue;
                var limit = threat.Radius * radiusShare + margin;
                if (MinionBombRules.FlatDistanceSquared(position, threat.transform.position) < limit * limit) return true;
            }
            return false;
        }
    }
}
