using System;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Editor.LevelAuthoring
{
    [Serializable]
    public sealed class MapTowerStamp
    {
        public Vector2Int cell;
        public GameObject prefab;
        public AttackArea area;
        public bool isAoe;
        public int goldBounty = 25;
        public string configuration;
    }

    public sealed class MapDraft : ScriptableObject
    {
        public int width = 24, length = 18, originX, originZ, layer, levelNumber = 1;
        public int[] tiles = Array.Empty<int>();
        public List<Vector2Int> route = new List<Vector2Int>();
        public List<MapTowerStamp> towers = new List<MapTowerStamp>();
        public bool Inside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < length;
        public int Tile(Vector2Int p) => Inside(p) ? tiles[p.y * width + p.x] : -1;
        public Vector3 Position(Vector2Int p) => new Vector3(originX + p.x, layer * .5f, originZ + p.y);

        public void ResetGrid(int x, int z)
        {
            if (x < 3 || z < 3 || x > 96 || z > 96) throw new ArgumentException("Width and length must be between 3 and 96.");
            width = x; length = z; originX = originZ = layer = 0;
            tiles = Enumerable.Repeat(-1, x * z).ToArray(); route.Clear(); towers.Clear();
        }

        public void Paint(Vector2Int p, int kind, int size)
        {
            if (!Inside(p)) return;
            var x0 = size == 3 ? Mathf.FloorToInt((p.x + originX) / 3f) * 3 - originX : p.x;
            var z0 = size == 3 ? Mathf.FloorToInt((p.y + originZ) / 3f) * 3 - originZ : p.y;
            if (x0 < 0 || z0 < 0 || x0 + size > width || z0 + size > length)
                throw new ArgumentException("The whole brush must fit inside the grid.");
            for (var z = z0; z < z0 + size; z++)
                for (var x = x0; x < x0 + size; x++) tiles[z * width + x] = kind;
        }

        public void AppendRoute(Vector2Int point)
        {
            if (!Inside(point)) throw new ArgumentException("Route point is outside the grid.");
            var addition = new List<Vector2Int>();
            if (route.Count == 0) addition.Add(point);
            else
            {
                var previous = route[route.Count - 1];
                if (previous == point) return;
                if (previous.x != point.x && previous.y != point.y)
                    throw new ArgumentException("Choose an orthogonal corner first; diagonal routes are not allowed.");
                var step = new Vector2Int(Math.Sign(point.x - previous.x), Math.Sign(point.y - previous.y));
                for (var p = previous + step; ; p += step) { addition.Add(p); if (p == point) break; }
            }
            foreach (var p in addition)
            {
                if (route.Contains(p)) throw new ArgumentException("Route cannot cross or repeat itself.");
                var tile = Tile(p);
                if (tile != (int)CellKind.Road && tile != (int)CellKind.Spawn && tile != (int)CellKind.Goal)
                    throw new ArgumentException($"Route ({p.x}, {p.y}) needs Road, Spawn or Goal.");
            }
            route.AddRange(addition);
        }

        public void PlaceTower(Vector2Int point, GameObject prefab, AttackArea area, bool isAoe = false, int goldBounty = 25)
        {
            var tower = prefab != null ? prefab.GetComponent<BattleTower>() : null;
            if (tower == null) throw new ArgumentException("Choose a tower prefab with BattleTower on its root.");
            var radius = tower.FootprintSize / 2;
            if (radius == 1 && (((point.x + originX) % 3 + 3) % 3 != 1 || ((point.y + originZ) % 3 + 3) % 3 != 1))
                throw new ArgumentException("A 3x3 tower must be placed at a macro center (3n+1, 3n+1).");
            for (var z = -radius; z <= radius; z++)
                for (var x = -radius; x <= radius; x++)
                {
                    var p = point + new Vector2Int(x, z);
                    if (Tile(p) != (int)CellKind.TowerPlatform) throw new ArgumentException("The whole tower footprint needs TowerPlatform tiles.");
                    if (towers.Any(t => Mathf.Abs(t.cell.x - p.x) <= t.prefab.GetComponent<BattleTower>().FootprintSize / 2 &&
                                        Mathf.Abs(t.cell.y - p.y) <= t.prefab.GetComponent<BattleTower>().FootprintSize / 2))
                        throw new ArgumentException("Tower footprints overlap.");
                }
            towers.Add(new MapTowerStamp
            {
                cell = point,
                prefab = prefab,
                area = area,
                isAoe = isAoe,
                goldBounty = goldBounty > 0 ? goldBounty : (isAoe ? 40 : 25)
            });
        }
    }
}