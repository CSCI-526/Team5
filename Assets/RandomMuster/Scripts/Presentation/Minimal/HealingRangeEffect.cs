using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed class HealingRangeEffect
    {
        private const int RingSegments = 48;
        private readonly LineRenderer perimeter;
        private readonly LineRenderer ripple;
        private readonly Material perimeterMaterial;
        private readonly Material rippleMaterial;
        private readonly Color perimeterTint;
        private readonly Color rippleTint;
        private float perimeterRadius = -1f;

        public HealingRangeEffect(Transform parent, Color perimeterTint, Color rippleTint)
        {
            this.perimeterTint = perimeterTint;
            this.rippleTint = rippleTint;
            perimeter = CreateRing(parent, "Heal Range Perimeter", .035f);
            ripple = CreateRing(parent, "Heal Range Ripple", .055f);
            perimeterMaterial = perimeter.sharedMaterial;
            rippleMaterial = ripple.sharedMaterial;
        }

        public void SetVisible(bool visible)
        {
            if (perimeter != null) perimeter.gameObject.SetActive(visible);
            if (ripple != null) ripple.gameObject.SetActive(visible);
        }

        public void SetScale(float scale)
        {
            perimeter.transform.localScale = Vector3.one * scale;
            ripple.transform.localScale = Vector3.one * scale;
        }

        public void Draw(float radius, float phase, float boost)
        {
            if (!Mathf.Approximately(perimeterRadius, radius))
            {
                SetCircle(perimeter, radius);
                perimeterRadius = radius;
            }
            SetCircle(ripple, Mathf.Lerp(.25f, radius, phase));
            SetTint(rippleMaterial, rippleTint, Mathf.Lerp(.75f + .25f * boost, 0f, phase * phase));
            SetTint(perimeterMaterial, perimeterTint, Mathf.Clamp01(.30f + .15f * Mathf.Sin(Time.time * 3.5f) + .25f * boost));
        }

        public void DestroyMaterials()
        {
            Object.Destroy(perimeterMaterial);
            Object.Destroy(rippleMaterial);
        }

        private static LineRenderer CreateRing(Transform parent, string name, float width)
        {
            var ring = new GameObject(name).AddComponent<LineRenderer>();
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = Vector3.up * .045f;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.widthMultiplier = width;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { renderQueue = 3000 };
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            ring.sharedMaterial = material;
            return ring;
        }

        private static void SetCircle(LineRenderer ring, float radius)
        {
            var step = 2f * Mathf.PI / RingSegments;
            for (var i = 0; i < RingSegments; i++)
            {
                var angle = i * step;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private static void SetTint(Material material, Color tint, float alpha)
        {
            tint.a = alpha;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            material.color = tint;
        }
    }
}
