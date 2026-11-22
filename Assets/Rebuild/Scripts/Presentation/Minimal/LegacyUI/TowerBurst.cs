using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class TowerBurst
    {
        private const int DropCount = 14;
        private const int DebrisCount = 8;
        private const int PuffCount = 6;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color[] MinionParts =
        {
            MinionPalette.Gunmetal, MinionPalette.Foundation, MinionPalette.BlueStone, MinionPalette.Brass,
            MinionPalette.Head, MinionPalette.Glass, MinionPalette.BlueStone
        };

        private readonly MinionMaterials materials;
        private readonly MeshRenderer ring;
        private readonly MeshRenderer splat;
        private readonly MeshRenderer flash;
        private readonly MeshRenderer[] drops = new MeshRenderer[DropCount];
        private readonly MeshRenderer[] debris = new MeshRenderer[DebrisCount];
        private readonly MeshRenderer[] puffs = new MeshRenderer[PuffCount];
        private readonly Vector3[] dropVelocities = new Vector3[DropCount];
        private readonly Vector3[] debrisStarts = new Vector3[DebrisCount];
        private readonly Vector3[] debrisVelocities = new Vector3[DebrisCount];
        private readonly Vector3[] debrisSpins = new Vector3[DebrisCount];
        private readonly float[] debrisSizes = new float[DebrisCount];
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private Vector3 center;
        private Vector3 origin;
        private Color paint;
        private float floor;
        private float age = -1f;
        private float spin;
        private bool minion;
        private int dropCount;
        private int debrisCount;
        private int puffCount;

        public TowerBurst(Transform parent, MinionMaterials materials, Material ringMaterial, Material splatMaterial, Material discMaterial)
        {
            this.materials = materials;
            ring = MinionMaterials.Show(parent, "Burst Ring", materials.Flat, ringMaterial);
            splat = MinionMaterials.Show(parent, "Burst Splat", materials.Flat, splatMaterial);
            flash = MinionMaterials.Show(parent, "Burst Flash", materials.Flat, discMaterial);
            var start = materials.Toon(TowerFeedbackRules.NeutralPaint, 0.35f);
            for (var i = 0; i < DropCount; i++) drops[i] = MinionMaterials.Show(parent, "Burst Drop", materials.Ball, start);
            for (var i = 0; i < DebrisCount; i++) debris[i] = MinionMaterials.Show(parent, "Burst Debris", materials.Box, start);
            for (var i = 0; i < PuffCount; i++) puffs[i] = MinionMaterials.Show(parent, "Burst Dust", materials.Flat, discMaterial);
            Hide();
        }

        public void PlayTower(Vector3 towerBase, Color color, Material[] debrisMaterials, System.Random random)
        {
            Begin(towerBase, towerBase + Vector3.up * 0.9f, color, false, random);
            dropCount = DropCount;
            puffCount = PuffCount;
            debrisCount = debrisMaterials != null && debrisMaterials.Length > 0 ? DebrisCount : 0;
            var drop = materials.Toon(color, 0.35f);
            for (var i = 0; i < DropCount; i++)
            {
                var angle = Next(random) * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                dropVelocities[i] = outward * (1.5f + Next(random) * 2.2f) + Vector3.up * (2f + Next(random) * 2.6f);
                drops[i].sharedMaterial = drop;
            }
            for (var i = 0; i < debrisCount; i++)
            {
                var angle = Next(random) * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                debrisStarts[i] = towerBase + outward * 0.2f + Vector3.up * (0.7f + Next(random) * 0.7f);
                debrisVelocities[i] = outward * (1.2f + Next(random) * 1.6f) + Vector3.up * (2.2f + Next(random) * 2.2f);
                debrisSpins[i] = new Vector3(Next(random) - 0.5f, Next(random) - 0.5f, Next(random) - 0.5f) * 900f;
                debrisSizes[i] = 0.09f + Next(random) * 0.08f;
                debris[i].sharedMaterial = debrisMaterials[i % debrisMaterials.Length];
            }
        }

        public void PlayMinion(Vector3 ground, System.Random random)
        {
            Begin(ground, ground + Vector3.up * TowerFeedbackRules.MinionPopHeight, MinionPalette.Glass, true, random);
            dropCount = 6;
            puffCount = 5;
            debrisCount = MinionParts.Length;
            var drop = materials.Toon(MinionPalette.Glass, 0.6f);
            for (var i = 0; i < dropCount; i++)
            {
                var angle = i / (float)dropCount * Mathf.PI * 2f;
                dropVelocities[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.6f + Vector3.up * 2.2f;
                drops[i].sharedMaterial = drop;
            }
            for (var i = 0; i < debrisCount; i++)
            {
                var angle = Next(random) * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                debrisStarts[i] = ground + Vector3.up * (0.5f + Next(random) * 0.4f);
                debrisVelocities[i] = outward * (1f + Next(random) * 1.3f) + Vector3.up * (2f + Next(random) * 1.6f);
                debrisSpins[i] = new Vector3(Next(random) - 0.5f, Next(random) - 0.5f, Next(random) - 0.5f) * 1000f;
                debrisSizes[i] = 0.08f + Next(random) * 0.06f;
                debris[i].sharedMaterial = materials.Toon(MinionParts[i], 0.2f);
            }
        }

        public void Hide()
        {
            age = -1f;
            Show(ring, false);
            Show(splat, false);
            Show(flash, false);
            foreach (var item in drops) Show(item, false);
            foreach (var item in debris) Show(item, false);
            foreach (var item in puffs) Show(item, false);
        }

        public void Tick(float deltaTime, Quaternion facing)
        {
            if (age < 0f) return;
            age += deltaTime;
            if (age >= TowerFeedbackRules.BurstSeconds)
            {
                Hide();
                return;
            }
            TickGround();
            TickFlash(facing);
            TickPuffs(facing);
            var dropLife = minion ? 0.6f : 0.8f;
            for (var i = 0; i < DropCount; i++)
            {
                var show = i < dropCount && age < dropLife;
                Show(drops[i], show);
                if (!show) continue;
                var place = drops[i].transform;
                place.position = TowerFeedbackRules.Ballistic(origin, dropVelocities[i], age, floor);
                place.localScale = Vector3.one * (TowerFeedbackRules.DropletSize * 1.2f * Mathf.Clamp01((dropLife - age) / 0.2f));
            }
            var debrisLife = minion ? 0.9f : TowerFeedbackRules.BurstSeconds;
            var debrisFade = minion ? 0.3f : 0.5f;
            for (var i = 0; i < DebrisCount; i++)
            {
                var show = i < debrisCount && age < debrisLife;
                Show(debris[i], show);
                if (!show) continue;
                var size = debrisSizes[i] * Mathf.Clamp01((debrisLife - age) / debrisFade);
                var place = debris[i].transform;
                place.SetPositionAndRotation(TowerFeedbackRules.Ballistic(debrisStarts[i], debrisVelocities[i], age, floor + debrisSizes[i] * 0.5f),
                    Quaternion.Euler(debrisSpins[i] * Mathf.Min(age, 0.7f)));
                place.localScale = Vector3.one * size;
            }
        }

        private void Begin(Vector3 ground, Vector3 start, Color color, bool small, System.Random random)
        {
            center = ground;
            origin = start;
            paint = color;
            minion = small;
            floor = ground.y + 0.03f;
            spin = Next(random) * 360f;
            age = 0f;
        }

        private void TickGround()
        {
            var ringSeconds = minion ? 0.4f : 0.5f;
            var ringProgress = age / ringSeconds;
            Show(ring, ringProgress < 1f);
            if (ringProgress < 1f)
            {
                var place = ring.transform;
                place.SetPositionAndRotation(center + Vector3.up * 0.016f, Quaternion.identity);
                place.localScale = Vector3.one * Mathf.Lerp(minion ? 0.45f : 0.7f, minion ? 2f : 3.5f, TowerFeedbackRules.EaseOut(ringProgress));
                Tint(ring, Color.white, 0.85f * (1f - ringProgress));
            }
            var splatSeconds = minion ? 1.1f : 1.8f;
            var showSplat = age < splatSeconds;
            Show(splat, showSplat);
            if (!showSplat) return;
            var spot = splat.transform;
            spot.SetPositionAndRotation(center + Vector3.up * 0.012f, Quaternion.Euler(0f, spin, 0f));
            spot.localScale = Vector3.one * (minion ? 1.5f : 2.3f);
            var alpha = minion ? 0.8f * (1f - age / splatSeconds) : 0.85f * (1f - Mathf.Clamp01((age - 0.6f) / 1.2f));
            Tint(splat, paint, alpha);
        }

        private void TickFlash(Quaternion facing)
        {
            var show = minion && age < 0.12f;
            Show(flash, show);
            if (!show) return;
            var place = flash.transform;
            place.SetPositionAndRotation(origin, facing);
            place.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, age / 0.12f);
            Tint(flash, Color.white, 0.9f * (1f - age / 0.12f));
        }

        private void TickPuffs(Quaternion facing)
        {
            var life = minion ? 0.5f : 0.6f;
            for (var i = 0; i < PuffCount; i++)
            {
                var show = i < puffCount && age < life;
                Show(puffs[i], show);
                if (!show) continue;
                var angle = i / (float)puffCount * Mathf.PI * 2f + spin * Mathf.Deg2Rad;
                var reach = (minion ? 0.3f : 0.45f) + age * (minion ? 0.6f : 1f);
                var place = puffs[i].transform;
                place.SetPositionAndRotation(center + new Vector3(Mathf.Cos(angle) * reach, 0.2f + age * 0.35f, Mathf.Sin(angle) * reach), facing);
                place.localScale = Vector3.one * (0.24f + age * 1f);
                Tint(puffs[i], TowerFeedbackRules.Dust, 0.6f * (1f - age / life));
            }
        }

        private void Tint(MeshRenderer renderer, Color color, float alpha)
        {
            block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha)));
            renderer.SetPropertyBlock(block);
        }

        private static void Show(MeshRenderer renderer, bool visible)
        {
            if (renderer != null && renderer.gameObject.activeSelf != visible) renderer.gameObject.SetActive(visible);
        }

        private static float Next(System.Random random) => (float)random.NextDouble();
    }
}
