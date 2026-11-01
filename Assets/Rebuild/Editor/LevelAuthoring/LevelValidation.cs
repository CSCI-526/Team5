using System;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor.LevelAuthoring
{
    public readonly struct LevelValidationIssue
    {
        public string Message { get; }
        public Object Context { get; }
        public bool IsError { get; }
        public LevelValidationIssue(string message, Object context = null, bool isError = true)
        { Message = message; Context = context; IsError = isError; }
    }

    public static class LevelValidation
    {
        public static T[] Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        // Reads the scene and temporary lookup indices. Does not save or repair anything.
        public static List<LevelValidationIssue> Validate(Scene scene)
        {
            var issues = new List<LevelValidationIssue>();
            if (!scene.IsValid() || !scene.isLoaded)
            { issues.Add(new LevelValidationIssue("Open a level scene first.")); return issues; }
            var sessions = Components<BattleSession>(scene);
            if (sessions.Length != 1)
            { issues.Add(new LevelValidationIssue("The scene must contain exactly one BattleSession.")); return issues; }
            var session = sessions[0];
            if (!session.isActiveAndEnabled) issues.Add(new LevelValidationIssue("BattleSession must be active and enabled.", session));
            try
            {
                issues.AddRange(session.ValidateConfiguration().Select(message => new LevelValidationIssue(message, session)));
                if (!issues.Any(issue => issue.IsError))
                    _ = new BattleSimulation(session.CaptureDefinition()); // Constructor also checks full formation clearance.
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { issues.Add(new LevelValidationIssue(error.Message, session)); }
            if (session.Grid != null)
            {
                var owned = new HashSet<BattleCell>(session.Grid.SceneCells);
                foreach (var cell in Components<BattleCell>(scene))
                    if (!owned.Contains(cell)) issues.Add(new LevelValidationIssue("BattleCell is outside the session's GameplayGrid.", cell));
            }
            var towers = new HashSet<BattleTower>(session.Towers);
            foreach (var tower in Components<BattleTower>(scene))
                if (!towers.Contains(tower)) issues.Add(new LevelValidationIssue("Tower is not registered in BattleSession → Towers.", tower));
            var cameras = Components<Camera>(scene).Where(camera => camera.isActiveAndEnabled && camera.CompareTag("MainCamera")).ToArray();
            if (cameras.Length != 1)
                issues.Add(new LevelValidationIssue("Expected one enabled MainCamera for unit inspection.", null, false));
            var presentations = Components<BattlePresentation>(scene);
            if (presentations.Length == 0)
                issues.Add(new LevelValidationIssue("No BattlePresentation: simulation can run, but units will not be displayed.", session, false));
            foreach (var view in presentations)
                if (view.Session != session || view.UnitPrefab == null)
                    issues.Add(new LevelValidationIssue("Presentation needs this session and a UnitView prefab.", view));
            foreach (var hud in Components<LegacyBattleHud>(scene))
                if (hud.Session != session) issues.Add(new LevelValidationIssue("HUD is bound to another session.", hud));
            return issues;
        }
    }
}
