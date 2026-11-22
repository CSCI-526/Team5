using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionSites
    {
        private readonly Dictionary<Vector2Int, Vector3> tops = new Dictionary<Vector2Int, Vector3>();
        private readonly HashSet<Vector2Int> hosts = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> walkable = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> cluttered = new HashSet<Vector2Int>();

        public int HostCount => hosts.Count;

        public void AddStandable(Vector2Int cell, Vector3 top) => tops[cell] = top;

        public void AddHost(Vector2Int cell, Vector3 top)
        {
            tops[cell] = top;
            hosts.Add(cell);
        }

        public void AddWalkable(Vector2Int cell) => walkable.Add(cell);

        public void AddClutter(Vector2Int cell) => cluttered.Add(cell);

        public bool CanStand(Vector2Int cell) => tops.ContainsKey(cell);

        public bool CanHost(Vector2Int cell) => hosts.Contains(cell);

        public bool IsWalkable(Vector2Int cell) => walkable.Contains(cell);

        public bool IsClean(Vector2Int cell) => !cluttered.Contains(cell);

        public Vector3 Top(Vector2Int cell) => tops.TryGetValue(cell, out var top) ? top : new Vector3(cell.x, 0f, cell.y);
    }
}
