using Team5.Rebuild.Simulation;
using Team5.Rebuild.Presentation.LegacyUI;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace Team5.Rebuild.Presentation
{
    public sealed class UnitView : MonoBehaviour
    {
        public const float LookScale = 2f;
        private const int HealRingSegments = 48;
        private const float HealNeighborRadius = 1.45f; // Covers all 8 neighboring slots in the 3x3 squad
        private const float LevelBadgeWidth = 32f;
        private const float LevelBadgeToUnitWidth = 0.6f;

        private UnitState state;
        private Vector3 previousPosition;
        private Vector3 currentPosition;
        private double sampleTime;
        private float sampleInterval;
        private MaterialPropertyBlock colors;
        private RectTransform levelBadge;
        private RectTransform levelBadgeLayer;

        public void CreateLevelBadge(RectTransform layer)
        {
            if (levelBadge != null) Destroy(levelBadge.gameObject);
            levelBadgeLayer = layer;
            var background = LegacyUiKit.CreateImage(layer, "Unit Level " + UnitId, Vector2.zero,
                new Vector2(LevelBadgeWidth, 17), new Color(0.035f, 0.045f, 0.065f, 0.94f), 3);
            levelBadge = background.rectTransform;
            levelBadge.pivot = Vector2.one;
            var label = LegacyUiKit.CreateText(levelBadge, "Level", $"Lv.{state.Definition.Level}",
                Vector2.zero, Vector2.zero, 11, Color.white, FontStyles.Bold);
            LegacyUiKit.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(2, 1);
            label.rectTransform.offsetMax = new Vector2(-2, -1);
            label.enableAutoSizing = true;
            label.fontSizeMin = 8;
            label.fontSizeMax = 11;
            levelBadge.gameObject.SetActive(false);
        }

        private void UpdateLevelBadge()
        {
            if (levelBadge == null || levelBadgeLayer == null) return;
            var camera = Camera.main;
            if (state == null || !state.IsAlive || camera == null || !TryGetVisibleBounds(out var bounds))
            { levelBadge.gameObject.SetActive(false); return; }
            levelBadge.gameObject.SetActive(true);
            var right = float.MinValue;
            var left = float.MaxValue;
            var top = float.MinValue;
            for (var z = -1; z <= 1; z += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var x = -1; x <= 1; x += 2)
            {
                var point = camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                if (point.z <= 0) { levelBadge.gameObject.SetActive(false); return; }
                right = Mathf.Max(right, point.x);
                left = Mathf.Min(left, point.x);
                top = Mathf.Max(top, point.y);
            }
            var center = camera.WorldToViewportPoint(bounds.center);
            if (center.x < 0 || center.x > 1 || center.y < 0 || center.y > 1)
            { levelBadge.gameObject.SetActive(false); return; }
            var canvasScale = levelBadgeLayer.GetComponentInParent<Canvas>().scaleFactor;
            // Match the projected model width rather than a fixed screen/font size.
            // Scale the whole badge so text, padding, background and offsets stay proportional.
            var badgeScale = (right - left) * LevelBadgeToUnitWidth / (LevelBadgeWidth * canvasScale);
            levelBadge.localScale = Vector3.one * badgeScale;
            var pixelScale = badgeScale * canvasScale;
            // Sit just inside the upper-right corner, leaving the health bar above it clear.
            var screen = new Vector2(right + 3 * pixelScale, top - 2 * pixelScale);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(levelBadgeLayer, screen, null, out var local))
                levelBadge.anchoredPosition = local;
        }

        // Animated healing range components
        private LineRenderer healPerimeterRing;
        private LineRenderer healWaveRing;
        private Material healPerimeterMat;
        private Material healWaveMat;
        private float healWavePhase;
        private float healBurstBoost;

        public int UnitId => state?.Id ?? 0;
        public UnitState State => state;

        public bool TryGetVisibleBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is LineRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        public Vector3 PreviousPosition => previousPosition;
        public Vector3 CurrentPosition => currentPosition;

        public void Bind(UnitState unit)
        {
            state = unit;
            if (colors == null) colors = new MaterialPropertyBlock();
            previousPosition = currentPosition = unit.Position;
            sampleTime = Time.timeAsDouble;
            sampleInterval = Time.fixedDeltaTime;
            transform.position = currentPosition;
            ApplyAppearance(unit.Role);
            ConfigureHealingRangeAnimation(unit.Role == UnitRole.Heal);
        }

        public void TriggerHealBurst()
        {
            healWavePhase = 0f;
            healBurstBoost = 1f;
        }

        private void ConfigureHealingRangeAnimation(bool isHealer)
        {
            if (!isHealer)
            {
                if (healPerimeterRing != null) healPerimeterRing.gameObject.SetActive(false);
                if (healWaveRing != null) healWaveRing.gameObject.SetActive(false);
                return;
            }

            if (healPerimeterRing == null)
            {
                healPerimeterMat = CreateRingMaterial(new Color(0.25f, 0.95f, 0.50f, 0.45f));
                healWaveMat = CreateRingMaterial(new Color(0.35f, 1.00f, 0.60f, 0.75f));

                healPerimeterRing = CreateRing("Heal Range Perimeter", healPerimeterMat, 0.035f);
                healWaveRing = CreateRing("Heal Range Ripple", healWaveMat, 0.055f);
                SetCirclePositions(healPerimeterRing, HealNeighborRadius);
            }

            healPerimeterRing.gameObject.SetActive(true);
            healWaveRing.gameObject.SetActive(true);
        }

        private LineRenderer CreateRing(string ringName, Material mat, float width)
        {
            var go = new GameObject(ringName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = HealRingSegments;
            lr.widthMultiplier = width;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static void SetCirclePositions(LineRenderer lr, float radius)
        {
            var step = 2f * Mathf.PI / HealRingSegments;
            for (var i = 0; i < HealRingSegments; i++)
            {
                var angle = i * step;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private static Material CreateRingMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { renderQueue = 3000 };
            mat.SetColor("_BaseColor", color);
            mat.color = color;
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return mat;
        }

        private void UpdateHealingRangeAnimation()
        {
            if (state == null || !state.IsAlive || state.Role != UnitRole.Heal || healWaveRing == null) return;

            healBurstBoost = Mathf.MoveTowards(healBurstBoost, 0f, Time.deltaTime * 1.5f);
            healWavePhase = (healWavePhase + Time.deltaTime / SquadCombatRules.HealInterval) % 1f;

            var radius = Mathf.Lerp(0.25f, HealNeighborRadius, healWavePhase);
            SetCirclePositions(healWaveRing, radius);

            var waveAlpha = Mathf.Lerp(0.75f + 0.25f * healBurstBoost, 0.0f, healWavePhase * healWavePhase);
            var waveColor = new Color(0.35f, 1.00f, 0.60f, waveAlpha);
            healWaveMat.SetColor("_BaseColor", waveColor);
            healWaveMat.color = waveColor;

            var borderPulse = 0.30f + 0.15f * Mathf.Sin(Time.time * 3.5f) + 0.25f * healBurstBoost;
            var borderColor = new Color(0.25f, 0.95f, 0.50f, Mathf.Clamp01(borderPulse));
            healPerimeterMat.SetColor("_BaseColor", borderColor);
            healPerimeterMat.color = borderColor;
        }

        public void ApplyAppearance(UnitRole role)
        {
            if (colors == null) colors = new MaterialPropertyBlock();
            var look = LegacyUnitLooks.For(role);
            var body = role == UnitRole.Test ? new Color(1, 0.85f, 0.2f) : look.Body;
            SetPart("Outline", look.Outline, true, new Vector3(look.OuterSize * LookScale, 0.26f, look.OuterSize * LookScale));
            SetPart("Body", body, true, new Vector3(look.InnerSize * LookScale, 0.008f, look.InnerSize * LookScale));
            var showMark = role != UnitRole.Test;
            SetPart("Mark Diamond", look.MarkColor, showMark && look.Mark == SoldierMark.Diamond,
                new Vector3(SoldierLook.DiamondSize * LookScale, 0.004f, SoldierLook.DiamondSize * LookScale));
            SetPart("Mark Plate", look.MarkColor, showMark && look.Mark == SoldierMark.Plate,
                new Vector3(SoldierLook.PlateSize * LookScale, 0.004f, SoldierLook.PlateSize * LookScale));
            SetPart("Mark Across", look.MarkColor, showMark && look.Mark == SoldierMark.Cross,
                new Vector3(SoldierLook.CrossLength * LookScale, 0.004f, SoldierLook.CrossWidth * LookScale));
            SetPart("Mark Upright", look.MarkColor, showMark && look.Mark == SoldierMark.Cross,
                new Vector3(SoldierLook.CrossWidth * LookScale, 0.004f, SoldierLook.CrossLength * LookScale));
            for (var i = 0; i < SoldierLook.CrownSegments.Length; i += 2)
            {
                var partName = "Mark Crown " + i;
                var part = transform.Find(partName);
                if (part == null && look.Mark == SoldierMark.Crown)
                {
                    var template = transform.Find("Mark Plate");
                    if (template == null) continue;
                    part = Instantiate(template, transform);
                    part.name = partName;
                }
                if (part == null) continue;
                var start = SoldierLook.CrownSegments[i];
                var end = SoldierLook.CrownSegments[i + 1];
                var delta = end - start;
                var center = (start + end) * (LookScale / 2);
                part.localPosition = new Vector3(center.x, 0.268f, center.y);
                part.localRotation = Quaternion.Euler(0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 0);
                SetPart(partName, look.MarkColor, showMark && look.Mark == SoldierMark.Crown,
                    new Vector3(delta.magnitude * LookScale, 0.004f, SoldierLook.CrownStroke * LookScale));
            }
        }

        private void SetPart(string partName, Color tint, bool visible, Vector3 scale)
        {
            var part = transform.Find(partName);
            if (part == null) return;
            part.gameObject.SetActive(visible);
            part.localScale = scale;
            colors.Clear();
            colors.SetColor("_BaseColor", tint);
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.SetPropertyBlock(colors);
        }

        public void SnapToState()
        {
            if (state == null) return;
            previousPosition = currentPosition = state.Position;
            transform.position = currentPosition;
            sampleTime = Time.timeAsDouble;
        }

        public void CaptureState()
        {
            previousPosition = currentPosition;
            currentPosition = state.Position;
            sampleTime = Time.timeAsDouble;
            sampleInterval = Time.fixedDeltaTime;
        }

        public void Present(float alpha)
        {
            transform.position = Vector3.Lerp(previousPosition, currentPosition, Mathf.Clamp01(alpha));
        }

        private void LateUpdate()
        {
            Present(sampleInterval > 0 ? (float)((Time.timeAsDouble - sampleTime) / sampleInterval) : 1);
            UpdateHealingRangeAnimation();
            UpdateLevelBadge();
        }

        private void OnDisable()
        {
            if (levelBadge != null) levelBadge.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (levelBadge != null) Destroy(levelBadge.gameObject);
            if (healPerimeterMat != null) Destroy(healPerimeterMat);
            if (healWaveMat != null) Destroy(healWaveMat);
        }
    }
}
