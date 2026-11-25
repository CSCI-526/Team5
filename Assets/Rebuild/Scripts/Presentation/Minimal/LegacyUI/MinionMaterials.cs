using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionMaterials
    {
        private const string ToonShader = "TW/ToonOutline";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private const int TextureSize = 128;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int EmissionId = Shader.PropertyToID("_Emission");

        private readonly List<Object> owned = new List<Object>();
        private readonly Dictionary<Color, Material> toons = new Dictionary<Color, Material>();
        private Material toonSource;
        private bool searched;
        private Mesh box;
        private Mesh ball;
        private Mesh tube;
        private Mesh flat;
        private Texture2D ring;
        private Texture2D disc;
        private Texture2D splat;
        private Mesh cone;
        private Mesh gem;
        private Mesh hex;
        private Mesh card;
        private Texture2D frost;
        private Texture2D scorch;
        private Texture2D scorchEmbers;
        private Texture2D flameMark;
        private Texture2D snowMark;
        private Texture2D flame;
        private Texture2D facets;

        public Mesh Box => box != null ? box : (box = Own(MinionMeshes.Box()));
        public Mesh Ball => ball != null ? ball : (ball = Own(MinionMeshes.Ball()));
        public Mesh Tube => tube != null ? tube : (tube = Own(MinionMeshes.Tube()));
        public Mesh Flat => flat != null ? flat : (flat = Own(MinionMeshes.Flat()));
        public Mesh Cone => cone != null ? cone : (cone = Own(MinionMeshes.Cone()));
        public Mesh Gem => gem != null ? gem : (gem = Own(MinionMeshes.Gem()));
        public Mesh Hex => hex != null ? hex : (hex = Own(MinionMeshes.HexPrism()));
        public Mesh Card => card != null ? card : (card = Own(MinionMeshes.Card()));
        public Texture2D Ring => ring != null ? ring : (ring = Own(CreateTexture("Minion Ring", RingAlpha)));
        public Texture2D Disc => disc != null ? disc : (disc = Own(CreateTexture("Minion Disc", DiscAlpha)));
        public Texture2D Splat => splat != null ? splat : (splat = Own(CreateTexture("Minion Splat", SplatAlpha)));
        public Texture2D Frost => frost != null ? frost : (frost = Own(MinionGlyphs.Paint("Minion Frost", TextureSize, MinionGlyphs.FrostMark)));
        public Texture2D Scorch => scorch != null ? scorch : (scorch = Own(MinionGlyphs.Mask("Minion Scorch", TextureSize, MinionGlyphs.ScorchBlot)));
        public Texture2D ScorchEmbers => scorchEmbers != null ? scorchEmbers : (scorchEmbers = Own(MinionGlyphs.Mask("Minion Scorch Embers", TextureSize, MinionGlyphs.ScorchEmbers)));
        public Texture2D FlameMark => flameMark != null ? flameMark : (flameMark = Own(MinionGlyphs.Paint("Minion Flame Mark", TextureSize, MinionGlyphs.FlameMark)));
        public Texture2D SnowMark => snowMark != null ? snowMark : (snowMark = Own(MinionGlyphs.Mask("Minion Snow Mark", TextureSize, MinionGlyphs.SnowGlyph)));
        public Texture2D Flame => flame != null ? flame : (flame = Own(MinionGlyphs.Paint("Minion Flame", TextureSize, MinionGlyphs.FlameCard)));
        public Texture2D Facets => facets != null ? facets : (facets = Own(CreateFacets()));

        public T Own<T>(T item) where T : Object
        {
            if (item != null) owned.Add(item);
            return item;
        }

        public Material Toon(Color color, float emission)
        {
            var key = new Color(color.r, color.g, color.b, emission);
            if (toons.TryGetValue(key, out var cached)) return cached;
            var source = FindToonSource();
            Material material;
            if (source != null) material = new Material(source);
            else
            {
                var shader = Shader.Find(ToonShader);
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find(UnlitShader);
                material = new Material(shader);
            }
            material.name = "Minion Toon (Runtime)";
            SetColor(material, new Color(color.r, color.g, color.b, 1f));
            if (material.HasProperty(OutlineColorId)) material.SetColor(OutlineColorId, MinionPalette.Outline);
            if (material.HasProperty(OutlineWidthId)) material.SetFloat(OutlineWidthId, 1.3f);
            if (material.HasProperty(EmissionId)) material.SetFloat(EmissionId, emission);
            toons[key] = Own(material);
            return material;
        }

        public Material Glow(Color color, Texture texture)
        {
            var shader = Shader.Find(UnlitShader);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "Minion Glow (Runtime)", renderQueue = 3000 };
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (texture != null)
            {
                if (material.HasProperty(BaseMapId)) material.SetTexture(BaseMapId, texture);
                if (material.HasProperty(MainTexId)) material.SetTexture(MainTexId, texture);
            }
            SetColor(material, color);
            return Own(material);
        }

        public static void SetColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        public static MeshRenderer Show(Transform parent, string name, Mesh mesh, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        public void Dispose()
        {
            foreach (var item in owned)
                if (item != null) Object.Destroy(item);
            owned.Clear();
            toons.Clear();
            box = null;
            ball = null;
            tube = null;
            flat = null;
            ring = null;
            disc = null;
            splat = null;
            cone = null;
            gem = null;
            hex = null;
            card = null;
            frost = null;
            scorch = null;
            scorchEmbers = null;
            flameMark = null;
            snowMark = null;
            flame = null;
            facets = null;
        }

        private static Texture2D CreateFacets()
        {
            var texture = new Texture2D(4, 1, TextureFormat.RGBA32, false)
            {
                name = "Minion Ice Facets",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.SetPixels32(new[]
            {
                (Color32)new Color(1f, 1f, 1f, 0.55f), (Color32)new Color(1f, 1f, 1f, 0.42f),
                (Color32)new Color(0.82f, 0.84f, 0.88f, 0.36f), (Color32)new Color(0.66f, 0.7f, 0.78f, 0.3f)
            });
            texture.Apply(false, true);
            return texture;
        }

        private Material FindToonSource()
        {
            if (searched) return toonSource;
            searched = true;
            var catalogs = CharacterSetCatalogs.Instance;
            if (catalogs == null || !catalogs.TryGet(CharacterSet.PaintSquad, out var entry)) return null;
            if (!entry.roleVisuals.TryGet(UnitRole.Attack, out var prefab) || prefab == null) return null;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.shader != null && material.shader.name == ToonShader)
                    {
                        toonSource = material;
                        return toonSource;
                    }
            return null;
        }

        private static float RingAlpha(float x, float y)
        {
            var distance = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((0.97f - distance) * 30f) * Mathf.Clamp01((distance - 0.8f) * 30f);
        }

        private static float DiscAlpha(float x, float y) => Mathf.Clamp01((0.97f - Mathf.Sqrt(x * x + y * y)) * 30f);

        private static float SplatAlpha(float x, float y)
        {
            var distance = Mathf.Sqrt(x * x + y * y);
            var angle = Mathf.Atan2(y, x);
            var edge = 0.66f + 0.1f * Mathf.Sin(angle * 5f) + 0.06f * Mathf.Sin(angle * 9f + 1.3f);
            var alpha = Mathf.Clamp01((edge - distance) * 18f);
            for (var drop = 0; drop < 5; drop++)
            {
                var dropAngle = drop * Mathf.PI * 0.4f + 0.5f;
                var dx = x - Mathf.Cos(dropAngle) * 0.86f;
                var dy = y - Mathf.Sin(dropAngle) * 0.86f;
                alpha = Mathf.Max(alpha, Mathf.Clamp01((0.08f - Mathf.Sqrt(dx * dx + dy * dy)) * 30f));
            }
            return alpha;
        }

        private static Texture2D CreateTexture(string name, System.Func<float, float, float> alphaAt)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[TextureSize * TextureSize];
            var half = TextureSize / 2f;
            for (var y = 0; y < TextureSize; y++)
                for (var x = 0; x < TextureSize; x++)
                {
                    var alpha = Mathf.Clamp01(alphaAt((x + 0.5f - half) / half, (y + 0.5f - half) / half));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
