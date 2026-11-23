using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    public sealed class BasicTowerTrial : MonoBehaviour
    {
        public const string ScenePath = "Assets/Rebuild/Scenes/BasicTowerTrial.unity";
        private static readonly Color[] Accent =
        {
            Color.clear, new Color(.32f,.70f,.39f), new Color(.60f,.38f,.92f),
            new Color(.94f,.54f,.24f), new Color(.29f,.68f,.90f)
        };
        private static readonly Color Stone = new Color(.30f, .34f, .39f);
        private readonly Dictionary<Color, Material> palette = new Dictionary<Color, Material>();
        private readonly Dictionary<BattleSimulation.DefenseShot, Transform> shots = new Dictionary<BattleSimulation.DefenseShot, Transform>();
        private readonly Dictionary<int, Transform> guards = new Dictionary<int, Transform>();
        private readonly List<Impact> impacts = new List<Impact>();
        private readonly Dictionary<int, (Transform Model, Vector3 BaseScale)> models =
            new Dictionary<int, (Transform Model, Vector3 BaseScale)>();
        private readonly Dictionary<int, float> firingAt = new Dictionary<int, float>();
        private BattleSimulation simulation;
        private Shader shader;

        private struct Impact
        {
            public Transform View;
            public float Start, Radius;
        }

        internal void Bind(BattleSession session, BattleSimulation battle)
        {
            if (simulation != null)
            {
                simulation.BasicDefenseImpact -= ShowImpact;
                simulation.TowerAttacked -= OnTowerAttacked;
            }
            foreach (var view in shots.Values) Destroy(view.gameObject);
            foreach (var view in guards.Values) Destroy(view.gameObject);
            foreach (var impact in impacts) Destroy(impact.View.gameObject);
            shots.Clear();
            guards.Clear();
            impacts.Clear();
            firingAt.Clear();
            foreach (var (model, baseScale) in models.Values)
                if (model != null) model.localScale = baseScale;
            models.Clear();
            simulation = battle;
            if (battle == null) return;
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            foreach (var tower in session.Towers)
            {
                if (tower.BasicDefense == BasicDefenseKind.None || tower.RuntimeState == null) continue;
                var model = tower.transform.Find("Model");
                if (model != null) models.Add(tower.RuntimeState.Id, (model, model.localScale));
            }
            battle.BasicDefenseImpact += ShowImpact;
            battle.TowerAttacked += OnTowerAttacked;
        }

        private void OnTowerAttacked(TowerAttackEvent shot) => firingAt[shot.TowerId] = (float)simulation.ElapsedTime;

        private Transform Shape(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            var collider = part.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var node = part.transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            node.localScale = scale;
            if (!palette.TryGetValue(color, out var material))
            {
                material = new Material(shader) { color = color };
                palette.Add(color, material);
            }
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return node;
        }

        private void ShowImpact(BasicDefenseKind kind, Vector3 point, float radius)
        {
            var view = Shape(transform, PrimitiveType.Cylinder, "Defense impact", Vector3.zero,
                new Vector3(.08f, .025f, .08f), Accent[(int)kind]);
            view.position = point + Vector3.up * .045f;
            impacts.Add(new Impact { View = view, Start = (float)simulation.ElapsedTime, Radius = radius });
        }

        private void LateUpdate()
        {
            if (simulation == null) return;
            var time = (float)simulation.ElapsedTime;
            foreach (var tower in simulation.Towers)
            {
                if (!models.TryGetValue(tower.Id, out var viewState)) continue;
                var (model, baseScale) = viewState;
                if (model == null) continue;
                model.gameObject.SetActive(tower.IsAlive);
                var pulse = firingAt.TryGetValue(tower.Id, out var fired)
                    ? Mathf.Clamp01(1 - (time - fired) * 7) : 0;
                model.localScale = Vector3.Scale(baseScale, new Vector3(1, 1 + pulse * .045f, 1));
            }
            foreach (var shot in simulation.DefenseShots)
            {
                if (!shots.TryGetValue(shot, out var view))
                {
                    var scale = shot.Kind == BasicDefenseKind.Artillery ? .32f : .12f;
                    view = Shape(transform, PrimitiveType.Sphere, shot.Kind + " shot", Vector3.zero,
                        Vector3.one * scale, Accent[(int)shot.Kind]);
                    view.position = shot.Position;
                    shots.Add(shot, view);
                }
                if (shot.Kind == BasicDefenseKind.Archer && (shot.Position - view.position).sqrMagnitude > .000001f)
                {
                    view.rotation = Quaternion.LookRotation(shot.Position - view.position);
                    view.localScale = new Vector3(.07f, .07f, .4f);
                }
                view.position = shot.Position;
            }
            foreach (var expired in shots.Keys.Where(shot => !simulation.DefenseShots.Contains(shot)).ToArray())
            {
                Destroy(shots[expired].gameObject);
                shots.Remove(expired);
            }
            foreach (var guard in simulation.BarracksGuards)
            {
                if (!guards.TryGetValue(guard.Id, out var view))
                {
                    view = Shape(transform, PrimitiveType.Capsule, "Barracks guard " + guard.Id, Vector3.zero,
                        new Vector3(.42f, .44f, .42f), Accent[(int)BasicDefenseKind.Barracks]);
                    Shape(view, PrimitiveType.Cube, "Shield", new Vector3(0, 0, -.52f), new Vector3(.65f, .67f, .18f), Stone);
                    guards.Add(guard.Id, view);
                }
                view.gameObject.SetActive(guard.Visible);
                view.position = guard.Position + Vector3.up * .44f;
            }
            for (var i = impacts.Count - 1; i >= 0; i--)
            {
                var effect = impacts[i];
                var progress = (time - effect.Start) / .23f;
                if (progress >= 1 || simulation.Outcome != BattleOutcome.Running)
                {
                    Destroy(effect.View.gameObject);
                    impacts.RemoveAt(i);
                }
                else effect.View.localScale = new Vector3(2 * effect.Radius * Mathf.Sqrt(Mathf.Max(.03f, progress)), .016f,
                    2 * effect.Radius * Mathf.Sqrt(Mathf.Max(.03f, progress)));
            }
        }

        private void OnDestroy()
        {
            if (simulation != null)
            {
                simulation.BasicDefenseImpact -= ShowImpact;
                simulation.TowerAttacked -= OnTowerAttacked;
            }
            foreach (var material in palette.Values) Destroy(material);
        }
    }
}
