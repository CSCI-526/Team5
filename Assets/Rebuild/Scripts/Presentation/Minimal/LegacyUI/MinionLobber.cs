using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionLobber
    {
        private const float ArcHeight = 2.2f;
        private const float BombSize = 0.36f;
        private const float ShadowSize = 0.34f;
        private const float SplatSeconds = 1.1f;
        private const float SplatSize = 2.3f;
        private const float GroundLift = 0.03f;

        private readonly MinionPost post;
        private readonly float damage;
        private readonly Transform ring;
        private readonly Transform fill;
        private readonly Transform bomb;
        private readonly Transform shadow;
        private readonly Transform splat;
        private readonly Material ringMaterial;
        private readonly Material fillMaterial;
        private readonly Material splatMaterial;
        private readonly List<Vector3> targets = new List<Vector3>();
        private double lastTime = -1d;
        private double landAt;
        private float cooldown;
        private float splatLeft;
        private bool engaged;
        private bool marking;
        private Vector3 center;
        private Vector3 launch;

        public MinionLobber(Transform parent, MinionPost post, float damage, MinionMaterials materials)
        {
            this.post = post;
            this.damage = damage;
            var root = new GameObject("Lobber Effects").transform;
            root.SetParent(parent, false);
            ringMaterial = materials.Glow(MinionPalette.Danger, materials.Ring);
            fillMaterial = materials.Glow(Tint(MinionPalette.Danger, 0.28f), materials.Disc);
            splatMaterial = materials.Glow(Tint(MinionPalette.Glass, 0.8f), materials.Splat);
            ring = MinionMaterials.Show(root, "Warning Ring", materials.Flat, ringMaterial).transform;
            fill = MinionMaterials.Show(root, "Warning Fill", materials.Flat, fillMaterial).transform;
            shadow = MinionMaterials.Show(root, "Bomb Shadow", materials.Flat, materials.Glow(MinionPalette.Shadow, materials.Disc)).transform;
            splat = MinionMaterials.Show(root, "Paint Splat", materials.Flat, splatMaterial).transform;
            var bombRenderer = MinionMaterials.Show(root, "Paint Bomb", materials.Ball, materials.Toon(MinionPalette.Glass, 0.9f));
            bombRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            bomb = bombRenderer.transform;
            bomb.localScale = Vector3.one * BombSize;
            shadow.localScale = Vector3.one * ShadowSize;
            HideMark();
            splat.gameObject.SetActive(false);
        }

        public bool MarkedThisFrame { get; private set; }

        public void Reset()
        {
            lastTime = -1d;
            cooldown = 0f;
            splatLeft = 0f;
            engaged = false;
            marking = false;
            MarkedThisFrame = false;
            HideMark();
            splat.gameObject.SetActive(false);
        }

        public void Tick(BattleSimulation simulation, float time, float deltaTime)
        {
            MarkedThisFrame = false;
            if (simulation == null || simulation.Outcome != BattleOutcome.Running || !post.IsAlive)
            {
                Reset();
                return;
            }
            var now = simulation.ElapsedTime;
            var simulated = lastTime < 0d ? 0f : Mathf.Max(0f, (float)(now - lastTime));
            lastTime = now;
            if (simulation.IsPaused) return;
            var wasMarking = marking;
            if (splatLeft > 0f)
            {
                splatLeft = Mathf.Max(0f, splatLeft - simulated);
                splat.gameObject.SetActive(splatLeft > 0f);
                MinionMaterials.SetColor(splatMaterial, Tint(MinionPalette.Glass, 0.8f * splatLeft / SplatSeconds));
            }
            if (marking) Fly(simulation, now, (float)now, true);
            if (simulation.Outcome != BattleOutcome.Running) { Reset(); return; }
            if (post.IsFrozen) return;
            // BombInterval is launch-to-launch; flight time is part of it.
            if (wasMarking)
            {
                cooldown = Mathf.Max(0f, cooldown - simulated);
                return;
            }
            targets.Clear();
            var origin = post.Position;
            foreach (var unit in simulation.Units)
                if (unit.IsAlive && MinionBombRules.FlatDistanceSquared(unit.Position, origin) <= MinionRules.BombReach * MinionRules.BombReach)
                    targets.Add(unit.Position);
            if (targets.Count == 0)
            {
                engaged = false;
                cooldown = Mathf.Max(0f, cooldown - simulated);
                return;
            }
            if (!engaged)
            {
                engaged = true;
                cooldown = Mathf.Max(cooldown, MinionRules.BombFirstDelay);
            }
            cooldown -= simulated;
            if (cooldown > 0f) return;
            if (MinionBombRules.PickTarget(targets, origin, MinionRules.BombReach, MinionRules.BombRadius, out var target) <= 0) return;
            // PickTarget returns a ground position from living squad members.
            // Using the mortar's height buries the warning on raised terrain.
            center = target;
            post.Face(center);
            launch = post.MuzzlePosition;
            landAt = now + MinionRules.BombWarning;
            cooldown = MinionRules.BombInterval;
            marking = true;
            MarkedThisFrame = true;
            post.Kick();
            Fly(simulation, now, (float)now, true);
        }

        private void Fly(BattleSimulation simulation, double now, float time, bool running)
        {
            if (!running)
            {
                marking = false;
                HideMark();
                return;
            }
            var progress = Mathf.Clamp01(1f - (float)((landAt - now) / MinionRules.BombWarning));
            ring.gameObject.SetActive(true);
            fill.gameObject.SetActive(true);
            bomb.gameObject.SetActive(true);
            shadow.gameObject.SetActive(true);
            var size = MinionRules.BombRadius * 2.06f;
            ring.position = center + Vector3.up * GroundLift;
            ring.localScale = Vector3.one * size;
            fill.position = center + Vector3.up * (GroundLift * 0.5f);
            fill.localScale = Vector3.one * (size * Mathf.Max(0.05f, progress));
            MinionMaterials.SetColor(ringMaterial, Tint(MinionPalette.Danger, 0.75f + Mathf.Sin(time * 14f) * 0.25f));
            var ground = Vector3.Lerp(launch, center, progress);
            ground.y = center.y;
            bomb.position = Vector3.Lerp(launch, center, progress) + Vector3.up * (4f * ArcHeight * progress * (1f - progress));
            shadow.position = ground + Vector3.up * (GroundLift * 1.5f);
            if (now < landAt || simulation.IsPaused) return;
            simulation.TryApplyEnvironmentalDamage(center, MinionRules.BombRadius, damage, post.Tower.RuntimeState.Id);
            marking = false;
            HideMark();
            splat.position = center + Vector3.up * (GroundLift * 2f);
            splat.localScale = Vector3.one * SplatSize;
            splat.localRotation = Quaternion.Euler(0f, (float)(now * 97d % 360d), 0f);
            splatLeft = SplatSeconds;
            splat.gameObject.SetActive(true);
        }

        private void HideMark()
        {
            ring.gameObject.SetActive(false);
            fill.gameObject.SetActive(false);
            bomb.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
        }

        private static Color Tint(Color color, float alpha) => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}
