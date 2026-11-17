using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    [RequireComponent(typeof(BattleSession))]
    public sealed class TowerSpecialEffects : MonoBehaviour
    {
        private BattleSimulation bound;
        private Material material;
        private readonly Dictionary<int, LineRenderer> lasers = new Dictionary<int, LineRenderer>();
        private readonly Dictionary<int, LineRenderer> frost = new Dictionary<int, LineRenderer>();
        private readonly List<(LineRenderer line, float born)> blasts = new List<(LineRenderer, float)>();
        private void LateUpdate()
        {
            var sim = GetComponent<BattleSession>().Simulation;
            if (bound != sim)
            {
                Clear(); bound = sim;
                if (bound != null) bound.BlastImpact += OnBlast;
            }
            if (sim == null) return;
            foreach (var tower in sim.Towers)
            {
                if (tower.Definition.Kind != TowerKind.Laser) continue;
                if (!lasers.TryGetValue(tower.Id, out var line)) lasers[tower.Id] = line = Line("Laser warning / beam", 2);
                line.enabled = tower.IsAlive && (tower.LaserChargeRemaining > 0 || tower.LaserFlashRemaining > 0);
                var firing = tower.LaserFlashRemaining > 0;
                line.startWidth = line.endWidth = firing ? .35f : .045f;
                line.startColor = line.endColor = firing ? new Color(1f, .05f, .12f) : new Color(.65f, .08f, .1f);
                var origin = tower.Position + Vector3.up * .5f;
                line.SetPosition(0, origin);
                line.SetPosition(1, origin + SquadCombatRules.Direction(tower.Definition.Area) * tower.Definition.Range);
                var coreTransform = line.transform.Find("White beam core");
                var core = coreTransform != null ? coreTransform.GetComponent<LineRenderer>() : Line("White beam core", 2);
                if (coreTransform == null) core.transform.SetParent(line.transform, false);
                core.enabled = line.enabled && firing;
                core.startWidth = core.endWidth = .11f;
                core.startColor = core.endColor = Color.white;
                core.SetPosition(0, line.GetPosition(0) + Vector3.up * .01f);
                core.SetPosition(1, line.GetPosition(1) + Vector3.up * .01f);
            }
            foreach (var unit in sim.Units)
            {
                if (unit.SlowRemaining <= 0 && !frost.ContainsKey(unit.Id)) continue;
                if (!frost.TryGetValue(unit.Id, out var ring)) frost[unit.Id] = ring = Line("Frost slow ring", 33);
                ring.enabled = unit.IsAlive && unit.SlowRemaining > 0;
                ring.startColor = ring.endColor = new Color(.18f, .78f, 1f);
                Circle(ring, unit.Position + Vector3.up * .06f, .45f);
            }
            for (var i = blasts.Count - 1; i >= 0; i--)
            {
                var (line, born) = blasts[i];
                var age = Time.time - born;
                if (age > .55f) { Destroy(line.gameObject); blasts.RemoveAt(i); continue; }
                var alpha = 1f - age / .55f;
                line.startColor = new Color(1, .8f, .08f, alpha);
                line.endColor = new Color(1, .12f, .01f, alpha);
                line.startWidth = line.endWidth = .1f * alpha;
                FireRing(line, line.transform.position, Mathf.Lerp(.2f, 1f, age / .55f), age);
            }
        }
        private void OnBlast(Vector3 position)
        {
            var line = Line("Blast impact - small fire ring", 33);
            line.transform.position = position + Vector3.up * .1f;
            blasts.Add((line, Time.time));
        }
        private LineRenderer Line(string name, int count)
        {
            if (material == null) material = new Material(Resources.Load<Material>("TowerEffect"));
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = material; line.useWorldSpace = true;
            line.positionCount = count; line.startWidth = line.endWidth = .06f;
            return line;
        }
        private static void FireRing(LineRenderer line, Vector3 center, float radius, float age)
        {
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2 / (line.positionCount - 1);
                var flame = Mathf.Max(0, Mathf.Sin(angle * 8 + age * 30)) * .12f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, flame, Mathf.Sin(angle) * radius));
            }
        }
        private static void Circle(LineRenderer line, Vector3 center, float radius)
        {
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2 / (line.positionCount - 1);
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
            }
        }
        private void Clear()
        {
            if (bound != null) bound.BlastImpact -= OnBlast;
            foreach (var l in lasers.Values) if (l != null) Destroy(l.gameObject);
            foreach (var l in frost.Values) if (l != null) Destroy(l.gameObject);
            foreach (var v in blasts) if (v.line != null) Destroy(v.line.gameObject);
            lasers.Clear(); frost.Clear(); blasts.Clear();
        }
        private void OnDisable() { Clear(); bound = null; }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
