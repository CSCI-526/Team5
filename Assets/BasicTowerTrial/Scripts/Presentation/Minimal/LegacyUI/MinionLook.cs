using System.Collections.Generic;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation.LegacyUI
{
    public static class MinionLook
    {
        private const float GuardSize = 0.6f;
        private const float GuardHeight = 0.42f;
        private const float PlateSize = 0.5f;
        private const float MarkLength = 0.36f;
        private const float MarkWidth = 0.07f;
        private const float Sheet = 0.02f;
        private static readonly Color outlineColor = new Color(0.2f, 0.06f, 0.28f);
        private static readonly Color bodyColor = new Color(0.58f, 0.2f, 0.75f);

        public static Color BodyColor => bodyColor;

        public static GameObject Build(Transform root, Mesh mesh, Material source, List<Material> owned)
        {
            if (root == null || mesh == null || source == null) return null;
            var look = new GameObject("Look");
            look.transform.SetParent(root, false);
            var guard = new GameObject("Guard");
            guard.transform.SetParent(look.transform, false);
            Part(guard.transform, "Body", mesh, Copy(source, outlineColor, owned), new Vector3(0f, GuardHeight / 2f, 0f), new Vector3(GuardSize, GuardHeight, GuardSize), 0f);
            Part(guard.transform, "Plate", mesh, Copy(source, bodyColor, owned), new Vector3(0f, GuardHeight + Sheet / 2f, 0f), new Vector3(PlateSize, Sheet, PlateSize), 0f);
            var mark = Copy(source, Color.white, owned);
            Part(guard.transform, "Mark A", mesh, mark, new Vector3(0f, GuardHeight + Sheet, 0f), new Vector3(MarkLength, Sheet, MarkWidth), 45f);
            Part(guard.transform, "Mark B", mesh, mark, new Vector3(0f, GuardHeight + Sheet, 0f), new Vector3(MarkLength, Sheet, MarkWidth), -45f);
            return look;
        }

        private static Material Copy(Material source, Color color, List<Material> owned)
        {
            var material = new Material(source) { name = source.name + " (Minion)" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            owned.Add(material);
            return material;
        }

        private static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, float yaw)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
