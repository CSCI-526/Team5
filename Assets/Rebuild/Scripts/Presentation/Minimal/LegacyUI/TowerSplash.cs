using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class TowerSplash
    {
        private const int DropCount = 5;
        private const int RippleDrops = 3;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly MinionMaterials materials;
        private readonly MeshRenderer disc;
        private readonly MeshRenderer ripple;
        private readonly MeshRenderer[] drops = new MeshRenderer[DropCount];
        private readonly Vector3[] velocities = new Vector3[DropCount];
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private Vector3 origin;
        private float floor;
        private Color paint;
        private bool shielded;
        private float age = -1f;

        public TowerSplash(Transform parent, MinionMaterials materials, Material discMaterial, Material ringMaterial)
        {
            this.materials = materials;
            disc = MinionMaterials.Show(parent, "Splash Flash", materials.Flat, discMaterial);
            ripple = MinionMaterials.Show(parent, "Shield Ripple", materials.Flat, ringMaterial);
            var start = materials.Toon(TowerFeedbackRules.NeutralPaint, 0.35f);
            for (var i = 0; i < DropCount; i++) drops[i] = MinionMaterials.Show(parent, "Paint Drop", materials.Ball, start);
            Hide();
        }

        public void PlayPaint(Vector3 point, Vector3 direction, float ground, Color color, System.Random random)
        {
            Begin(point, ground, color, false);
            var side = Vector3.Cross(Vector3.up, direction);
            var material = materials.Toon(color, 0.35f);
            for (var i = 0; i < DropCount; i++)
            {
                velocities[i] = direction * (1f + Next(random) * 0.8f) + side * ((Next(random) - 0.5f) * 2f) + Vector3.up * (1.7f + Next(random) * 1.2f);
                drops[i].sharedMaterial = material;
            }
        }

        public void PlayRipple(Vector3 point, Vector3 direction, float ground, Color glass)
        {
            Begin(point, ground, glass, true);
            var side = Vector3.Cross(Vector3.up, direction);
            var material = materials.Toon(glass, 0.6f);
            for (var i = 0; i < DropCount; i++)
            {
                velocities[i] = direction * 1.4f + side * ((i - 1) * 0.9f) + Vector3.up * (1.6f + i * 0.3f);
                drops[i].sharedMaterial = material;
            }
        }

        public void Hide()
        {
            age = -1f;
            if (disc != null) disc.gameObject.SetActive(false);
            if (ripple != null) ripple.gameObject.SetActive(false);
            foreach (var drop in drops)
                if (drop != null) drop.gameObject.SetActive(false);
        }

        public void Tick(float deltaTime, Quaternion facing)
        {
            if (age < 0f) return;
            age += deltaTime;
            if (age >= TowerFeedbackRules.SplashSeconds)
            {
                Hide();
                return;
            }
            if (shielded) Billboard(ripple, age / TowerFeedbackRules.RippleSeconds, 0.16f, 0.84f, paint, 1f, facing);
            else Billboard(disc, age / TowerFeedbackRules.SplashFlashSeconds, 0.36f, 0.7f, Color.Lerp(paint, Color.white, 0.2f), 0.95f, facing);
            var count = shielded ? RippleDrops : DropCount;
            var shrink = Mathf.Clamp01((TowerFeedbackRules.SplashSeconds - age) / 0.15f);
            for (var i = 0; i < DropCount; i++)
            {
                var drop = drops[i];
                var show = i < count;
                drop.gameObject.SetActive(show);
                if (!show) continue;
                var place = drop.transform;
                place.position = TowerFeedbackRules.Ballistic(origin, velocities[i], age, floor);
                place.localScale = Vector3.one * (TowerFeedbackRules.DropletSize * (shielded ? 0.75f : 1f) * shrink);
            }
        }

        private void Begin(Vector3 point, float ground, Color color, bool shield)
        {
            origin = point;
            floor = ground + 0.03f;
            paint = color;
            shielded = shield;
            age = 0f;
            disc.gameObject.SetActive(false);
            ripple.gameObject.SetActive(false);
        }

        private void Billboard(MeshRenderer renderer, float progress, float from, float to, Color color, float alpha, Quaternion facing)
        {
            var visible = progress < 1f;
            renderer.gameObject.SetActive(visible);
            if (!visible) return;
            var place = renderer.transform;
            place.SetPositionAndRotation(origin, facing);
            place.localScale = Vector3.one * Mathf.Lerp(from, to, TowerFeedbackRules.EaseOut(progress));
            block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, alpha * (1f - progress)));
            renderer.SetPropertyBlock(block);
        }

        private static float Next(System.Random random) => (float)random.NextDouble();
    }
}
