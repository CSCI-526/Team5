using System;
using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class NextMapData
    {
        private NextMapData(NextMapRoute route, IReadOnlyList<NextMapTower> towers, Bounds cells, bool hasCells, Vector3 spawn, Vector3 goal)
        {
            Route = route;
            Towers = towers;
            Cells = cells;
            HasCells = hasCells;
            Spawn = spawn;
            Goal = goal;
        }

        public NextMapRoute Route { get; }
        public IReadOnlyList<NextMapTower> Towers { get; }
        public Bounds Cells { get; }
        public bool HasCells { get; }
        public Vector3 Spawn { get; }
        public Vector3 Goal { get; }
        public bool HasMap => HasCells && Route.Count >= 2;

        public static NextMapData Capture(Scene scene)
        {
            BattleSession session = null;
            SceneRoute sceneRoute = null;
            var cells = new List<BattleCell>();
            var sceneTowers = new List<BattleTower>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (session == null) session = root.GetComponentInChildren<BattleSession>(true);
                if (sceneRoute == null) sceneRoute = root.GetComponentInChildren<SceneRoute>(true);
                cells.AddRange(root.GetComponentsInChildren<BattleCell>(true));
                sceneTowers.AddRange(root.GetComponentsInChildren<BattleTower>(true));
            }
            if (session != null && session.Route != null) sceneRoute = session.Route;

            var hasCells = false;
            var cellBounds = new Bounds();
            Vector3 spawnSum = Vector3.zero, goalSum = Vector3.zero;
            int spawnCount = 0, goalCount = 0;
            foreach (var cell in cells)
            {
                if (cell == null || !cell.gameObject.activeInHierarchy) continue;
                if (RendererBounds(cell, out var bounds))
                {
                    if (hasCells) cellBounds.Encapsulate(bounds);
                    else { cellBounds = bounds; hasCells = true; }
                }
                if (cell.Kind == CellKind.Spawn) { spawnSum += cell.TopCenter; spawnCount++; }
                else if (cell.Kind == CellKind.Goal) { goalSum += cell.TopCenter; goalCount++; }
            }

            var route = new NextMapRoute(CaptureRoute(sceneRoute));
            var towers = new List<NextMapTower>();
            var source = session != null ? session.Towers : sceneTowers;
            foreach (var tower in source)
            {
                if (tower == null || !tower.gameObject.activeInHierarchy || !tower.HasValidConfiguration) continue;
                var hasModel = RendererBounds(tower, out var model);
                towers.Add(new NextMapTower(tower.CaptureDefinition(towers.Count + 1), model, hasModel));
            }

            var start = route.Count > 0 ? route[0] : Vector3.zero;
            var end = route.Count > 0 ? route[route.Count - 1] : Vector3.zero;
            return new NextMapData(route, towers, cellBounds, hasCells,
                spawnCount > 0 ? spawnSum / spawnCount : start,
                goalCount > 0 ? goalSum / goalCount : end);
        }

        private static Vector3[] CaptureRoute(SceneRoute route)
        {
            if (route == null) return Array.Empty<Vector3>();
            try { return route.CaptureTopCenters(); }
            catch (Exception) { return Array.Empty<Vector3>(); }
        }

        private static bool RendererBounds(Component owner, out Bounds bounds)
        {
            bounds = new Bounds();
            var found = false;
            foreach (var renderer in owner.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            return found;
        }
    }
}
