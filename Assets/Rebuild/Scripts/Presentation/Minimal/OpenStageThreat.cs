using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Simple enemy sentry / pressure-pad threat on open Tactile-Wars-like maps.
    // Does not need tower platforms and can be avoided through click-to-move navigation.
    [DisallowMultipleComponent]
    public sealed class OpenStageThreat : MonoBehaviour, ITacticalTrapSite
    {
        public enum ThreatKind { Sentry, GroundTrap, Freeze, Meteor, Boulder }
        [SerializeField] private ThreatKind kind;
        [SerializeField, Min(0.1f)] private float radius = 1.6f;
        [SerializeField, Min(.01f)] private float damage = .7f;
        [SerializeField, Min(.1f)] private float hitInterval = 2.0f;
        [SerializeField] private Vector3 rollDirection = Vector3.forward;
        private BattleSession session;
        private TacticalTrapVisual tacticalVisual;
        public bool IsTactical => kind == ThreatKind.Freeze || kind == ThreatKind.Meteor || kind == ThreatKind.Boulder;
        public Vector3 RollDirection => rollDirection;
        public bool TryCaptureTrap(out TacticalTrapDefinition trap)
        {
            trap = null;
            if (!IsTactical || !isActiveAndEnabled) return false;
            var center = transform.position + Vector3.up * (.25f - .035f);
            var id = -10001 - Mathf.RoundToInt(center.x) * 100 - Mathf.RoundToInt(center.z);
            trap = new TacticalTrapDefinition(id, (TacticalTrapKind)((int)kind - 2), center, radius, damage, hitInterval, rollDirection);
            return true;
        }
        private BattleSimulation simulation;
        private double nextHitTime;
        private double lastHitTime = double.NegativeInfinity;
        private float groundY;
        private Vector3 home;
        private LineRenderer warningRing;
        private Material warningMaterial;
        public ThreatKind Kind => kind;
        public float Radius => radius;

        public void Configure(ThreatKind threatKind, float hitRadius, float hitDamage, float interval, Vector3 direction = default)
        {
            kind = threatKind;
            radius = hitRadius;
            damage = hitDamage;
            hitInterval = interval;
            rollDirection = direction.sqrMagnitude > .01f ? direction.normalized : Vector3.forward;
            if (warningRing != null) UpdateRingGeometry();
        }

        private void Awake()
        {
            home = transform.position;
            groundY = home.y - (kind == ThreatKind.Sentry ? .50f : .035f);
            foreach (var current in FindObjectsByType<BattleSession>(FindObjectsSortMode.None))
                if (current.gameObject.scene == gameObject.scene) { session = current; break; }
            if (session != null && session.Route != null && session.Route.Cells.Count > 0 && session.Route.Cells[0] != null)
                groundY = session.Route.Cells[0].TopCenter.y;
            if (IsTactical)
            {
                if (TryCaptureTrap(out var definition)) tacticalVisual = new TacticalTrapVisual(transform, definition);
                return;
            }
            CreateWarningRing();
        }

        private void Update()
        {
            var current = session != null ? session.Simulation : null;
            if (IsTactical)
            {
                if (tacticalVisual != null) tacticalVisual.Refresh(current);
                return;
            }
            if (simulation != current)
            {
                simulation = current;
                nextHitTime = 0;
                lastHitTime = double.NegativeInfinity;
            }
            if (simulation == null || simulation.Outcome != BattleOutcome.Running ||
                !float.IsFinite(radius) || radius <= 0 || !float.IsFinite(damage) || damage <= 0 ||
                !float.IsFinite(hitInterval) || hitInterval <= 0)
            {
                if (warningRing != null) warningRing.enabled = false;
                return;
            }

            var now = simulation.ElapsedTime;
            var near = false;
            var inRange = false;
            foreach (var unit in simulation.Units)
            {
                if (!unit.IsAlive) continue;
                var dx = unit.Position.x - home.x;
                var dz = unit.Position.z - home.z;
                var distance = dx * dx + dz * dz;
                if (distance <= (radius + 2.5f) * (radius + 2.5f)) near = true;
                if (distance <= radius * radius) inRange = true;
            }
            if (!simulation.IsPaused && inRange && now >= nextHitTime &&
                simulation.TryApplyEnvironmentalDamage(home, radius, damage))
            {
                lastHitTime = now;
                nextHitTime = now + hitInterval;
            }
            if (kind == ThreatKind.Sentry)
                transform.position = home + Vector3.up * (Mathf.Sin((float)now * 2.4f) * .11f);
            if (warningRing == null || warningMaterial == null) return;
            var flash = Mathf.Clamp01(1f - (float)(now - lastHitTime) / .35f);
            warningRing.enabled = simulation.Outcome == BattleOutcome.Running && (near || flash > 0f);
            if (!warningRing.enabled) return;
            // A thin outline is the exact danger boundary. A bright pulse indicates an
            // actual hit; a dim outline during cooldown is not a second damage zone.
            var ready = now >= nextHitTime;
            var alpha = Mathf.Lerp(ready ? .78f : .30f, 1f, flash);
            warningRing.widthMultiplier = Mathf.Lerp(.055f, .12f, flash);
            var tone = kind == ThreatKind.Sentry ? new Color(1f, .24f, .27f, alpha) : new Color(1f, .6f, .13f, alpha);
            if (warningMaterial.HasProperty("_BaseColor")) warningMaterial.SetColor("_BaseColor", tone);
            if (warningMaterial.HasProperty("_Color")) warningMaterial.SetColor("_Color", tone);
        }

        private void CreateWarningRing()
        {
            var ring = new GameObject("Threat radius (visible telegraph)");
            ring.transform.SetParent(transform, false);
            warningRing = ring.AddComponent<LineRenderer>();
            warningRing.loop = true;
            warningRing.useWorldSpace = true;
            warningRing.positionCount = 48;
            warningRing.widthMultiplier = .055f;
            warningRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            warningRing.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            warningMaterial = new Material(shader);
            if (warningMaterial.HasProperty("_Surface")) warningMaterial.SetFloat("_Surface", 1f);
            if (warningMaterial.HasProperty("_SrcBlend")) warningMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (warningMaterial.HasProperty("_DstBlend")) warningMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (warningMaterial.HasProperty("_ZWrite")) warningMaterial.SetFloat("_ZWrite", 0f);
            warningMaterial.SetOverrideTag("RenderType", "Transparent");
            warningMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            warningMaterial.renderQueue = 3000;
            warningRing.sharedMaterial = warningMaterial;
            UpdateRingGeometry();
            warningRing.enabled = false;
        }

        private void UpdateRingGeometry()
        {
            for (var i = 0; i < 48; i++)
            {
                var a = i * Mathf.PI * 2f / 48f;
                warningRing.SetPosition(i, new Vector3(home.x + Mathf.Cos(a) * radius, groundY + .035f, home.z + Mathf.Sin(a) * radius));
            }
        }

        private void OnDisable() { if (warningRing != null) warningRing.enabled = false; if (tacticalVisual != null) tacticalVisual.SetVisible(false); }

        private void OnDestroy()
        {
            if (warningMaterial != null) Destroy(warningMaterial);
            if (tacticalVisual != null) tacticalVisual.Dispose();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == ThreatKind.Sentry ? Color.red : new Color(1, .55f, 0);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
