using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Simple enemy sentry / pressure-pad threat on open Tactile-Wars-like maps.
    // Does not need tower platforms and can be avoided through click-to-move navigation.
    [DisallowMultipleComponent]
    public sealed class OpenStageThreat : MonoBehaviour
    {
        public enum ThreatKind { Sentry, GroundTrap }
        [SerializeField] private ThreatKind kind;
        [SerializeField, Min(0.1f)] private float radius = 1.6f;
        [SerializeField, Min(.01f)] private float damage = .7f;
        [SerializeField, Min(.1f)] private float hitInterval = 2.0f;
        private BattleSession session;
        private BattleSimulation simulation;
        private double nextHitTime;
        private Vector3 home;
        private LineRenderer warningRing;
        private Material warningMaterial;
        public ThreatKind Kind => kind;
        public float Radius => radius;

        public void Configure(ThreatKind threatKind, float hitRadius, float hitDamage, float interval)
        {
            kind = threatKind;
            radius = hitRadius;
            damage = hitDamage;
            hitInterval = interval;
        }

        private void Awake()
        {
            home = transform.position;
            CreateWarningRing();
            foreach (var current in FindObjectsByType<BattleSession>(FindObjectsSortMode.None))
                if (current.gameObject.scene == gameObject.scene) { session = current; break; }
        }

        private void Update()
        {
            // Localized visual motion tells players where the danger is without route arrows.
            if (kind == ThreatKind.Sentry)
                transform.position = home + Vector3.up * (Mathf.Sin(Time.unscaledTime * 2.4f) * .11f);
            if (warningRing != null && warningMaterial != null)
            {
                warningRing.transform.position = new Vector3(home.x, .33f, home.z);
                var pulse = .46f + .20f * Mathf.Sin(Time.unscaledTime * (kind == ThreatKind.Sentry ? 3.3f : 2f));
                var tone = kind == ThreatKind.Sentry ? new Color(1f, .24f, .27f, pulse) : new Color(1f, .6f, .13f, pulse);
                if (warningMaterial.HasProperty("_BaseColor")) warningMaterial.SetColor("_BaseColor", tone);
                if (warningMaterial.HasProperty("_Color")) warningMaterial.SetColor("_Color", tone);
            }
            if (session == null || session.Simulation == null) return;
            if (simulation != session.Simulation)
            {
                simulation = session.Simulation;
                nextHitTime = 0;
            }
            if (simulation.IsPaused || simulation.Outcome != BattleOutcome.Running ||
                simulation.ElapsedTime < nextHitTime) return;
            var hit = simulation.TryApplyEnvironmentalDamage(home, radius, damage);
            if (hit) nextHitTime = simulation.ElapsedTime + hitInterval;
        }

        private void CreateWarningRing()
        {
            var ring = new GameObject("Threat radius (visible telegraph)");
            ring.transform.SetParent(transform, false);
            ring.transform.position = new Vector3(home.x, .33f, home.z);
            warningRing = ring.AddComponent<LineRenderer>();
            warningRing.loop = true;
            warningRing.useWorldSpace = false;
            warningRing.positionCount = 48;
            warningRing.widthMultiplier = .055f;
            warningRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            warningRing.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            warningMaterial = new Material(shader);
            if (warningMaterial.HasProperty("_Surface")) warningMaterial.SetFloat("_Surface", 1f);
            warningRing.sharedMaterial = warningMaterial;
            for (var i = 0; i < 48; i++)
            {
                var a = i * Mathf.PI * 2f / 48f;
                warningRing.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            }
        }

        private void OnDestroy()
        {
            if (warningMaterial != null) Destroy(warningMaterial);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == ThreatKind.Sentry ? Color.red : new Color(1, .55f, 0);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
