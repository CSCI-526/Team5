using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionShieldView
    {
        private const int BeadsPerBearer = 7;
        private const float BeadSize = 0.075f;
        private const float BeadSpeed = 0.55f;
        private const float BeadArc = 0.28f;
        private static readonly Vector3 DomeScale = new Vector3(1.75f, 2.7f, 1.75f);
        private const float RingSize = 2f;
        private const float BaseAlpha = 0.16f;
        private const float FlashAlpha = 0.5f;
        private const float FlashSeconds = 0.25f;
        private const float PopSeconds = 0.45f;

        private readonly int towerId;
        private readonly Transform root;
        private readonly Transform dome;
        private readonly Transform ring;
        private readonly Material domeMaterial;
        private readonly Material ringMaterial;
        private readonly List<MinionPost> bearers;
        private readonly List<Transform> beads = new List<Transform>();
        private bool wasShielded;
        private float flash;
        private float pop;

        public MinionShieldView(Transform parent, Vector3 towerBase, int towerId, List<MinionPost> bearers, MinionMaterials materials)
        {
            this.towerId = towerId;
            this.bearers = bearers;
            root = new GameObject("Shield " + towerId).transform;
            root.SetParent(parent, false);
            root.position = towerBase;
            domeMaterial = materials.Glow(Tint(MinionPalette.Glass, BaseAlpha), null);
            ringMaterial = materials.Glow(Tint(MinionPalette.Glass, 0.8f), materials.Ring);
            dome = MinionMaterials.Show(root, "Dome", materials.Ball, domeMaterial).transform;
            dome.localScale = DomeScale;
            ring = MinionMaterials.Show(root, "Ring", materials.Flat, ringMaterial).transform;
            ring.localPosition = Vector3.up * 0.02f;
            ring.localScale = Vector3.one * RingSize;
            var beadMaterial = materials.Glow(Tint(MinionPalette.Glass, 0.9f), null);
            for (var i = 0; i < bearers.Count * BeadsPerBearer; i++)
            {
                var bead = MinionMaterials.Show(parent, "Bead", materials.Ball, beadMaterial).transform;
                bead.localScale = Vector3.one * BeadSize;
                beads.Add(bead);
            }
            SetVisible(false);
        }

        public int TowerId => towerId;

        public void Flash() => flash = FlashSeconds;

        public void Reset()
        {
            wasShielded = false;
            flash = 0f;
            pop = 0f;
            SetVisible(false);
        }

        public void Tick(BattleSimulation simulation, float time, float deltaTime)
        {
            var towerAlive = simulation != null && towerId >= 1 && towerId <= simulation.Towers.Count && simulation.Towers[towerId - 1].IsAlive;
            if (!towerAlive || simulation.Outcome != BattleOutcome.Running)
            {
                Reset();
                return;
            }
            var shielded = simulation.IsTowerShielded(towerId);
            if (wasShielded && !shielded && towerAlive) pop = PopSeconds;
            wasShielded = shielded;
            flash = Mathf.Max(0f, flash - deltaTime);
            pop = Mathf.Max(0f, pop - deltaTime);
            var showDome = shielded || pop > 0f;
            dome.gameObject.SetActive(showDome);
            ring.gameObject.SetActive(showDome);
            if (shielded)
            {
                var alpha = BaseAlpha + flash / FlashSeconds * (FlashAlpha - BaseAlpha) + Mathf.Sin(time * 3f) * 0.025f;
                dome.localScale = DomeScale;
                ring.localScale = Vector3.one * RingSize;
                MinionMaterials.SetColor(domeMaterial, Tint(MinionPalette.Glass, alpha));
                MinionMaterials.SetColor(ringMaterial, Tint(MinionPalette.Glass, 0.65f + flash / FlashSeconds * 0.35f));
            }
            else if (pop > 0f)
            {
                var progress = 1f - pop / PopSeconds;
                dome.localScale = DomeScale * (1f + progress * 0.35f);
                ring.localScale = Vector3.one * (RingSize * (1f + progress * 0.6f));
                MinionMaterials.SetColor(domeMaterial, Tint(MinionPalette.Glass, (1f - progress) * FlashAlpha));
                MinionMaterials.SetColor(ringMaterial, Tint(MinionPalette.Glass, 1f - progress));
            }
            var target = root.position + Vector3.up * 0.9f;
            for (var b = 0; b < bearers.Count; b++)
            {
                var bearer = bearers[b];
                var linked = shielded && bearer.IsAlive && !bearer.IsFrozen;
                var start = bearer.VisualPosition + Vector3.up * 0.45f;
                for (var i = 0; i < BeadsPerBearer; i++)
                {
                    var bead = beads[b * BeadsPerBearer + i];
                    bead.gameObject.SetActive(linked);
                    if (!linked) continue;
                    var along = Mathf.Repeat((float)i / BeadsPerBearer + time * BeadSpeed, 1f);
                    bead.position = Vector3.Lerp(start, target, along) + Vector3.up * (Mathf.Sin(along * Mathf.PI) * BeadArc);
                }
            }
        }

        private void SetVisible(bool visible)
        {
            dome.gameObject.SetActive(visible);
            ring.gameObject.SetActive(visible);
            foreach (var bead in beads) bead.gameObject.SetActive(visible);
        }

        private static Color Tint(Color color, float alpha) => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }
}
