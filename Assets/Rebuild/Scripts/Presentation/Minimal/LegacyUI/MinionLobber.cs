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
        private const float SplatSize = MinionRules.BombRadius * 2.3f;
        private const float GroundLift = 0.03f;
        private const float MarkSize = 0.95f;
        private const float MarkScale = 0.82f;
        private const float MarkFade = 0.4f;
        private const int TrailDots = 5;
        private const float TrailSpacing = 0.035f;

        private readonly MinionPost post;
        private readonly float damage;
        private readonly Transform ring;
        private readonly Transform fill;
        private readonly Transform bomb;
        private readonly Transform shadow;
        private readonly Transform splat;
        private readonly Transform mark;
        private readonly Transform embers;
        private readonly Transform[] trail;
        private readonly Material ringMaterial;
        private readonly Material fillMaterial;
        private readonly Material splatMaterial;
        private readonly Material embersMaterial;
        private readonly Color splatColor;
        private readonly List<Vector3> targets = new List<Vector3>();
        private readonly Dictionary<int, Vector3> previousPositions = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector3> predictedPositions = new Dictionary<int, Vector3>();
        private Camera view;
        private double lastTime = -1d;
        private double landAt;
        private float cooldown;
        private float splatLeft;
        private bool engaged;
        private bool marking;
        private Vector3 center;
        private Vector3 launch;

        public MinionLobber(Transform parent, MinionPost post, float damage, MinionMaterials materials, MinionElement element = MinionElement.None)
        {
            this.post = post;
            this.damage = damage;
            Element = element;
            var root = new GameObject("Lobber Effects").transform;
            root.SetParent(parent, false);
            splatColor = element == MinionElement.Fire ? Tint(MinionPalette.Scorch, 0.9f) : element == MinionElement.Ice ? Tint(Color.white, 0.85f) : Tint(MinionPalette.Glass, 0.8f);
            ringMaterial = materials.Glow(MinionPalette.Danger, materials.Ring);
            fillMaterial = materials.Glow(Tint(MinionPalette.FillOf(element), 0.28f), materials.Disc);
            splatMaterial = materials.Glow(splatColor, element == MinionElement.Fire ? materials.Scorch : element == MinionElement.Ice ? materials.Frost : materials.Splat);
            ring = MinionMaterials.Show(root, "Warning Ring", materials.Flat, ringMaterial).transform;
            fill = MinionMaterials.Show(root, "Warning Fill", materials.Flat, fillMaterial).transform;
            shadow = MinionMaterials.Show(root, "Bomb Shadow", materials.Flat, materials.Glow(MinionPalette.Shadow, materials.Disc)).transform;
            splat = MinionMaterials.Show(root, "Paint Splat", materials.Flat, splatMaterial).transform;
            var bombRenderer = MinionMaterials.Show(root, "Paint Bomb", materials.Ball, materials.Toon(MinionPalette.GlowOf(element), 0.9f));
            bombRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            bomb = bombRenderer.transform;
            bomb.localScale = Vector3.one * BombSize;
            shadow.localScale = Vector3.one * ShadowSize;
            if (element != MinionElement.None)
            {
                var markMaterial = element == MinionElement.Fire
                    ? materials.Glow(Tint(Color.white, 0.9f), materials.FlameMark)
                    : materials.Glow(Tint(MinionPalette.IceGlyph, 0.9f), materials.SnowMark);
                mark = MinionMaterials.Show(root, "Element Mark", materials.Flat, markMaterial).transform;
                var trailMaterial = materials.Glow(Tint(MinionPalette.TrailOf(element), 0.8f), materials.Disc);
                trail = new Transform[TrailDots];
                for (var i = 0; i < trail.Length; i++) trail[i] = MinionMaterials.Show(root, "Bomb Trail", materials.Card, trailMaterial).transform;
            }
            if (element == MinionElement.Fire)
            {
                embersMaterial = materials.Glow(MinionPalette.ScorchEmber, materials.ScorchEmbers);
                embers = MinionMaterials.Show(root, "Scorch Embers", materials.Flat, embersMaterial).transform;
            }
            HideMark();
            HideSplat();
        }

        public MinionElement Element { get; }

        public bool MarkedThisFrame { get; private set; }

        public void Reset()
        {
            lastTime = -1d;
            previousPositions.Clear(); predictedPositions.Clear();
            cooldown = 0f;
            splatLeft = 0f;
            engaged = false;
            marking = false;
            MarkedThisFrame = false;
            HideMark();
            HideSplat();
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
            // Sample movement up to launch; aim remains locked throughout the flight.
            var origin = post.Position;
            predictedPositions.Clear();
            foreach (var unit in simulation.Units)
            {
                if (!unit.IsAlive) continue;
                if (simulated > .0001f && previousPositions.TryGetValue(unit.Id, out var previous))
                {
                    predictedPositions[unit.Id] = MinionBombRules.PredictTarget(unit.Position, previous, simulated,
                        origin, MinionRules.BombReach);
                }
                previousPositions[unit.Id] = unit.Position;
            }
            var wasMarking = marking;
            if (splatLeft > 0f)
            {
                splatLeft = Mathf.Max(0f, splatLeft - simulated);
                splat.gameObject.SetActive(splatLeft > 0f);
                MinionMaterials.SetColor(splatMaterial, Element == MinionElement.None
                    ? Tint(MinionPalette.Glass, 0.8f * splatLeft / SplatSeconds)
                    : Tint(splatColor, splatColor.a * Mathf.Clamp01(splatLeft / MarkFade)));
                if (embers != null)
                {
                    embers.gameObject.SetActive(splatLeft > 0f);
                    MinionMaterials.SetColor(embersMaterial, Tint(MinionPalette.ScorchEmber, (0.6f + 0.4f * Mathf.Sin((float)now * 8f)) * Mathf.Clamp01(splatLeft / MarkFade)));
                }
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
            foreach (var unit in simulation.Units)
                if (unit.IsAlive && MinionBombRules.FlatDistanceSquared(unit.Position, origin) <= MinionRules.BombReach * MinionRules.BombReach)
                    targets.Add(predictedPositions.TryGetValue(unit.Id, out var predicted) ? predicted : unit.Position);
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
            if (mark != null)
            {
                mark.gameObject.SetActive(true);
                mark.position = center + Vector3.up * (GroundLift * 1.5f);
                mark.localScale = Vector3.one * MarkSize;
            }
            var ground = Vector3.Lerp(launch, center, progress);
            ground.y = center.y;
            bomb.position = Arc(progress);
            shadow.position = ground + Vector3.up * (GroundLift * 1.5f);
            if (trail != null) Trail(progress);
            if (now < landAt || simulation.IsPaused) return;
            Land(simulation);
            marking = false;
            HideMark();
            var splatSize = SplatSize * (Element == MinionElement.None ? 1f : MarkScale);
            var turn = Quaternion.Euler(0f, (float)(now * 97d % 360d), 0f);
            splat.position = center + Vector3.up * (GroundLift * 2f);
            splat.localScale = Vector3.one * splatSize;
            splat.localRotation = turn;
            splatLeft = SplatSeconds;
            splat.gameObject.SetActive(true);
            if (embers == null) return;
            embers.position = center + Vector3.up * (GroundLift * 2.5f);
            embers.localScale = Vector3.one * splatSize;
            embers.localRotation = turn;
            embers.gameObject.SetActive(true);
        }

        private void Land(BattleSimulation simulation)
        {
            var source = post.Tower.RuntimeState.Id;
            var impact = MinionRules.ImpactDamage(Element, damage);
            if (Element == MinionElement.None)
            {
                simulation.TryApplyEnvironmentalDamage(center, MinionRules.BombRadius, impact, source);
                return;
            }
            simulation.TryApplyEnvironmentalDamage(center, MinionRules.BombRadius, impact, source,
                MinionRules.FreezeTime(Element), MinionRules.BurnTickCount(Element), MinionRules.BurnTickDamage(Element, damage));
        }

        private Vector3 Arc(float progress) => Vector3.Lerp(launch, center, progress) + Vector3.up * (4f * ArcHeight * progress * (1f - progress));

        private void Trail(float progress)
        {
            if (view == null) view = Camera.main;
            var facing = view != null ? view.transform.rotation : Quaternion.identity;
            for (var i = 0; i < trail.Length; i++)
            {
                var behind = progress - (i + 1) * TrailSpacing;
                var dot = trail[i];
                dot.gameObject.SetActive(behind > 0f);
                if (behind <= 0f) continue;
                var fade = 1f - (i + 1f) / (trail.Length + 1f);
                dot.SetPositionAndRotation(Arc(behind), facing);
                dot.localScale = Vector3.one * (0.12f * fade + 0.04f);
            }
        }

        private void HideMark()
        {
            ring.gameObject.SetActive(false);
            fill.gameObject.SetActive(false);
            bomb.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
            if (mark != null) mark.gameObject.SetActive(false);
            if (trail == null) return;
            foreach (var dot in trail) dot.gameObject.SetActive(false);
        }

        private void HideSplat()
        {
            splat.gameObject.SetActive(false);
            if (embers != null) embers.gameObject.SetActive(false);
        }

        private static Color Tint(Color color, float alpha) => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}
