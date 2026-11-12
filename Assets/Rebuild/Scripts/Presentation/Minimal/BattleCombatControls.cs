using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Renders runtime projectiles and dead tower material states.
    [RequireComponent(typeof(BattleSession))]
    public sealed class BattleCombatControls : MonoBehaviour
    {
        private BattleSession session;
        private BattleSimulation bound;
        private readonly Dictionary<int, Transform> shots = new Dictionary<int, Transform>();
        private readonly Dictionary<Renderer, MaterialPropertyBlock> originalColors = new Dictionary<Renderer, MaterialPropertyBlock>();
        private readonly Dictionary<Renderer, bool> wallVisibility = new Dictionary<Renderer, bool>();
        private MaterialPropertyBlock color;
        private Material shotMaterial;
        public bool ShowDebugPanel { get; set; }

        private void OnEnable()
        {
            session = GetComponent<BattleSession>();
            color = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            var sim = session.Simulation;
            if (sim != bound)
            {
                ClearVisuals();
                bound = sim;
            }
            if (sim == null || sim.Objective != BattleObjective.ReachGoal) return;

            foreach (var shot in sim.Projectiles)
            {
                if (!shot.IsActive)
                {
                    if (shots.TryGetValue(shot.Id, out var old))
                    {
                        Destroy(old.gameObject);
                        shots.Remove(shot.Id);
                    }
                    continue;
                }
                if (!shots.TryGetValue(shot.Id, out var view))
                {
                    var model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    model.name = $"Projectile View {shot.Id}";
                    model.transform.SetParent(transform, false);
                    model.transform.localScale = Vector3.one * 0.18f;
                    Destroy(model.GetComponent<Collider>());
                    if (shotMaterial == null)
                        shotMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
                    var renderer = model.GetComponent<Renderer>();
                    renderer.sharedMaterial = shotMaterial;
                    color.SetColor("_BaseColor", shot.TargetKind == ProjectileTargetKind.Tower ? Color.yellow : Color.magenta);
                    renderer.SetPropertyBlock(color);
                    view = model.transform;
                    shots.Add(shot.Id, view);
                }
                view.position = shot.Position + Vector3.up * 0.5f;
            }

            for (var i = 0; i < session.Towers.Count; i++)
            {
                var tower = session.Towers[i];
                if (tower == null || tower.RuntimeState == null) continue;
                foreach (var renderer in tower.GetComponentsInChildren<Renderer>())
                {
                    if (tower.IsWall)
                    {
                        if (!wallVisibility.ContainsKey(renderer)) wallVisibility.Add(renderer, renderer.enabled);
                        renderer.enabled = tower.RuntimeState.IsAlive && wallVisibility[renderer];
                        continue;
                    }
                    if (!originalColors.ContainsKey(renderer))
                    {
                        var original = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(original);
                        originalColors.Add(renderer, original);
                    }
                    if (tower.RuntimeState.IsAlive) renderer.SetPropertyBlock(originalColors[renderer]);
                    else
                    {
                        color.SetColor("_BaseColor", Color.gray);
                        renderer.SetPropertyBlock(color);
                    }
                }
            }
        }

        private void ClearVisuals()
        {
            foreach (var view in shots.Values) if (view != null) Destroy(view.gameObject);
            shots.Clear();
            foreach (var pair in originalColors) if (pair.Key != null) pair.Key.SetPropertyBlock(pair.Value);
            originalColors.Clear();
            foreach (var pair in wallVisibility) if (pair.Key != null) pair.Key.enabled = pair.Value;
            wallVisibility.Clear();
        }

        private void OnDisable()
        {
            ClearVisuals();
            bound = null;
            if (shotMaterial != null) Destroy(shotMaterial);
        }
    }
}