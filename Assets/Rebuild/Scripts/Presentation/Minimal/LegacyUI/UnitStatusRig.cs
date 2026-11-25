using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class UnitStatusRig
    {
        private const float DefaultWidth = 0.66f;
        private const float DefaultHeight = 0.95f;
        private const float ShellWidth = 1.18f;
        private const float ShellHeight = 1.02f;
        private const float FrostSize = 0.95f;
        private const float GrowSeconds = 0.1f;
        private const float FadeInSeconds = 0.12f;
        private const float EmberSize = 0.07f;
        private const int EmberCount = 5;
        private static readonly Vector3[] FlameSpots = { new Vector3(0.2f, 0.34f, 0.38f), new Vector3(-0.22f, 0.5f, 0.38f), new Vector3(0.04f, 0.95f, 0.3f) };
        private static readonly Vector2[] FlameSizes = { new Vector2(0.3f, 0.46f), new Vector2(0.26f, 0.4f), new Vector2(0.26f, 0.4f) };

        private readonly GameObject root;
        private readonly SortingGroup front;
        private readonly SortingGroup behind;
        private readonly Transform shell;
        private readonly Transform frost;
        private readonly Transform[] flames = new Transform[3];
        private readonly Transform[] embers = new Transform[EmberCount];
        private readonly List<Animator> animators = new List<Animator>();
        private UnitView view;
        private bool measured;
        private bool burning;
        private float width = DefaultWidth;
        private float height = DefaultHeight;
        private float frozenShownAt;
        private float burnShownAt;
        private float yaw;

        public UnitStatusRig(Transform parent, MinionMaterials materials, Material shellMaterial, Material frostMaterial, Material flameMaterial, Material emberMaterial, bool sorted)
        {
            root = new GameObject("Unit Status Rig");
            root.transform.SetParent(parent, false);
            var frontRoot = new GameObject("Front").transform;
            frontRoot.SetParent(root.transform, false);
            var behindRoot = new GameObject("Behind").transform;
            behindRoot.SetParent(root.transform, false);
            if (sorted)
            {
                front = frontRoot.gameObject.AddComponent<SortingGroup>();
                behind = behindRoot.gameObject.AddComponent<SortingGroup>();
            }
            shell = MinionMaterials.Show(frontRoot, "Ice Shell", materials.Hex, shellMaterial).transform;
            frost = MinionMaterials.Show(behindRoot, "Frost", materials.Flat, frostMaterial).transform;
            for (var i = 0; i < flames.Length; i++) flames[i] = MinionMaterials.Show(frontRoot, "Flame", materials.Card, flameMaterial).transform;
            for (var i = 0; i < embers.Length; i++) embers[i] = MinionMaterials.Show(frontRoot, "Ember", materials.Card, emberMaterial).transform;
            Hide();
            root.SetActive(false);
        }

        public int UnitId { get; private set; }
        public bool Frozen { get; private set; }
        public double FrozenSince { get; private set; }
        public Vector3 ShellCenter { get; private set; }
        public float Floor { get; private set; }

        public void Begin(int unitId)
        {
            UnitId = unitId;
            view = null;
            animators.Clear();
            measured = false;
            Frozen = false;
            burning = false;
            yaw = unitId * 37 % 60;
            root.SetActive(true);
        }

        public bool Show(UnitView unitView, Vector3 foot, bool frozen, bool burn, double now, float time, Quaternion facing, Vector3 toward, Vector3 side)
        {
            if (unitView != view)
            {
                view = unitView;
                animators.Clear();
                if (view != null) view.GetComponentsInChildren(true, animators);
                measured = false;
            }
            if (!measured) Measure(foot);
            var thawed = Frozen && !frozen;
            if (frozen && !Frozen)
            {
                FrozenSince = now;
                frozenShownAt = Time.unscaledTime;
            }
            if (burn && !burning) burnShownAt = Time.unscaledTime;
            Frozen = frozen;
            burning = burn;
            Floor = foot.y;
            ShellCenter = foot + Vector3.up * (height * ShellHeight * 0.5f);
            PlaceIce(foot);
            PlaceFire(foot, time, facing, toward, side);
            if (front != null)
            {
                var order = WorldZSceneSorting.Order(foot.z);
                front.sortingOrder = order + 1;
                behind.sortingOrder = order - 1;
            }
            return thawed;
        }

        public void HoldPose()
        {
            foreach (var animator in animators)
                if (animator != null && animator.isActiveAndEnabled) animator.speed = 0f;
        }

        public void Release()
        {
            Hide();
            view = null;
            animators.Clear();
            Frozen = false;
            burning = false;
            root.SetActive(false);
        }

        private void Measure(Vector3 foot)
        {
            measured = true;
            width = DefaultWidth;
            height = DefaultHeight;
            if (view == null || !view.TryGetVisibleBounds(out var bounds)) return;
            height = Mathf.Clamp(bounds.max.y - foot.y, 0.5f, 1.6f);
            width = Mathf.Clamp(Mathf.Min(bounds.size.x, height * 0.7f), 0.4f, 1.1f);
        }

        private void PlaceIce(Vector3 foot)
        {
            shell.gameObject.SetActive(Frozen);
            frost.gameObject.SetActive(Frozen);
            if (!Frozen) return;
            var grow = Mathf.Clamp01((Time.unscaledTime - frozenShownAt) / GrowSeconds);
            var across = width * ShellWidth * grow;
            var tall = height * ShellHeight * grow;
            shell.SetPositionAndRotation(foot + Vector3.up * (tall * 0.5f), Quaternion.Euler(0f, yaw, 0f));
            shell.localScale = new Vector3(across, tall, across);
            frost.SetPositionAndRotation(foot + Vector3.up * 0.03f, Quaternion.Euler(0f, yaw * 3f, 0f));
            frost.localScale = Vector3.one * (FrostSize * Mathf.Max(0.3f, grow));
        }

        private void PlaceFire(Vector3 foot, float time, Quaternion facing, Vector3 toward, Vector3 side)
        {
            foreach (var flame in flames) flame.gameObject.SetActive(burning);
            foreach (var ember in embers) ember.gameObject.SetActive(burning);
            if (!burning) return;
            var fade = Mathf.Clamp01((Time.unscaledTime - burnShownAt) / FadeInSeconds);
            var lift = height / 1.1f;
            for (var i = 0; i < flames.Length; i++)
            {
                var spot = FlameSpots[i];
                var size = FlameSizes[i];
                var center = foot + side * spot.x + Vector3.up * (spot.y * lift + size.y * 0.3f) + toward * spot.z;
                flames[i].SetPositionAndRotation(center, facing * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 7f + i * 1.3f) * 8f));
                flames[i].localScale = new Vector3(size.x * (0.85f + 0.15f * Mathf.Sin(time * 9f + i * 1.9f)), size.y * (0.9f + 0.1f * Mathf.Sin(time * 13f + i * 2.7f)), 1f) * fade;
            }
            for (var i = 0; i < embers.Length; i++)
            {
                var phase = Mathf.Repeat(time * 0.9f + (float)i / embers.Length + UnitId * 0.13f, 1f);
                var center = foot + side * (Mathf.Sin(i * 2.1f + UnitId) * 0.22f) + Vector3.up * (0.2f + phase * 1.1f) + toward * (0.15f + Mathf.Cos(i * 1.3f) * 0.1f);
                embers[i].SetPositionAndRotation(center, facing);
                embers[i].localScale = Vector3.one * (EmberSize * (1f - phase) * fade);
            }
        }

        private void Hide()
        {
            shell.gameObject.SetActive(false);
            frost.gameObject.SetActive(false);
            foreach (var flame in flames) flame.gameObject.SetActive(false);
            foreach (var ember in embers) ember.gameObject.SetActive(false);
        }
    }
}
