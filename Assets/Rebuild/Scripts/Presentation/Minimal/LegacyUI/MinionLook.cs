using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class MinionLook
    {
        private const float GlassGlow = 0.85f;

        public static Color MarkerColor => MinionPalette.Gunmetal;

        public static MinionRig Build(Transform root, MinionKind kind, Vector3 facing, MinionMaterials materials)
        {
            if (root == null || materials == null) return null;
            var look = new GameObject("Look");
            look.transform.SetParent(root, false);
            var flat = new Vector3(facing.x, 0f, facing.z);
            look.transform.localRotation = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
            var bot = Child(look.transform, "Bot", Vector3.zero);
            var gear = Child(bot, "Gear", Vector3.zero);
            var body = new Dictionary<Color, List<CombineInstance>>();
            var kit = new Dictionary<Color, List<CombineInstance>>();
            var cube = materials.Box;
            var ball = materials.Ball;
            var tube = materials.Tube;
            var level = Quaternion.identity;
            Add(body, MinionPalette.Gunmetal, 0f, cube, new Vector3(-0.09f, 0.08f, 0f), level, new Vector3(0.13f, 0.16f, 0.15f));
            Add(body, MinionPalette.Gunmetal, 0f, cube, new Vector3(0.09f, 0.08f, 0f), level, new Vector3(0.13f, 0.16f, 0.15f));
            Add(body, MinionPalette.Foundation, 0f, cube, new Vector3(0f, 0.29f, 0f), level, new Vector3(0.34f, 0.26f, 0.26f));
            Add(body, MinionPalette.Brass, 0f, cube, new Vector3(0f, 0.22f, 0f), level, new Vector3(0.36f, 0.045f, 0.28f));
            Add(body, MinionPalette.Gunmetal, 0f, cube, new Vector3(-0.21f, 0.3f, 0.02f), level, new Vector3(0.08f, 0.2f, 0.1f));
            Add(body, MinionPalette.Gunmetal, 0f, cube, new Vector3(0.21f, 0.3f, 0.02f), level, new Vector3(0.08f, 0.2f, 0.1f));
            Add(body, MinionPalette.Head, 0f, cube, new Vector3(0f, 0.6f, 0f), level, new Vector3(0.44f, 0.34f, 0.36f));
            Add(body, MinionPalette.BlueStone, 0f, cube, new Vector3(0f, 0.6f, 0.185f), level, new Vector3(0.36f, 0.22f, 0.02f));
            Add(body, MinionPalette.Brass, 0f, ball, new Vector3(-0.235f, 0.6f, 0f), level, Vector3.one * 0.09f);
            Add(body, MinionPalette.Brass, 0f, ball, new Vector3(0.235f, 0.6f, 0f), level, Vector3.one * 0.09f);
            Add(body, MinionPalette.Trim, 0f, tube, new Vector3(0f, 0.83f, 0f), level, new Vector3(0.03f, 0.12f, 0.03f));
            Add(body, MinionPalette.Glass, GlassGlow, ball, new Vector3(0f, 0.9f, 0f), level, Vector3.one * 0.065f);
            Transform muzzle;
            if (kind == MinionKind.Bearer)
            {
                Add(body, MinionPalette.Glass, GlassGlow, cube, new Vector3(0f, 0.61f, 0.198f), level, new Vector3(0.28f, 0.08f, 0.02f));
                Add(kit, MinionPalette.Brass, 0f, cube, new Vector3(0.03f, 0.33f, 0.235f), level, new Vector3(0.42f, 0.44f, 0.03f));
                Add(kit, MinionPalette.BlueStone, 0f, cube, new Vector3(0.03f, 0.33f, 0.255f), level, new Vector3(0.37f, 0.39f, 0.03f));
                Add(kit, MinionPalette.Trim, 0f, cube, new Vector3(0.03f, 0.34f, 0.275f), level, new Vector3(0.08f, 0.13f, 0.02f));
                muzzle = Child(gear, "Muzzle", new Vector3(0.03f, 0.42f, 0.3f));
            }
            else
            {
                var tilt = Quaternion.Euler(-35f, 0f, 0f);
                var tubeCenter = new Vector3(0.06f, 0.6f, -0.17f);
                var tubeTop = tubeCenter + tilt * Vector3.up * 0.15f;
                Add(body, MinionPalette.Glass, GlassGlow, ball, new Vector3(0f, 0.6f, 0.2f), level, Vector3.one * 0.11f);
                Add(kit, MinionPalette.Gunmetal, 0f, tube, tubeCenter, tilt, new Vector3(0.14f, 0.3f, 0.14f));
                Add(kit, MinionPalette.Brass, 0f, tube, tubeTop, tilt, new Vector3(0.17f, 0.04f, 0.17f));
                Add(kit, MinionPalette.Glass, GlassGlow, ball, new Vector3(-0.17f, 0.3f, 0.17f), level, Vector3.one * 0.16f);
                muzzle = Child(gear, "Muzzle", tubeTop);
            }
            Group(bot, body, materials);
            Group(gear, kit, materials);
            return new MinionRig(look, bot, gear, muzzle);
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }

        private static void Add(Dictionary<Color, List<CombineInstance>> groups, Color color, float glow, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var key = new Color(color.r, color.g, color.b, glow);
            if (!groups.TryGetValue(key, out var parts)) groups[key] = parts = new List<CombineInstance>();
            parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
        }

        private static void Group(Transform parent, Dictionary<Color, List<CombineInstance>> groups, MinionMaterials materials)
        {
            foreach (var pair in groups)
            {
                var mesh = new Mesh { name = "Minion Part" };
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                materials.Own(mesh);
                var part = new GameObject("Part");
                part.transform.SetParent(parent, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = materials.Toon(pair.Key, pair.Key.a);
            }
        }
    }
}
