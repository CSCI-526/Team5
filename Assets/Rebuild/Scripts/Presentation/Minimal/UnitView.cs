using Team5.Rebuild.Simulation;
using Team5.Rebuild.Presentation.LegacyUI;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // The root is the foot position; the authored square model uses the formation UI's look.
    // This observer can only read UnitState; it never advances or changes the simulation.
    public sealed class UnitView : MonoBehaviour
    {
        public const float LookScale = 2f; // Largest Tank remains 0.88 wide inside one Small Cell.
        private UnitState state;
        private Vector3 previousPosition;
        private Vector3 currentPosition;
        private double sampleTime;
        private float sampleInterval;
        private MaterialPropertyBlock colors;
        public int UnitId => state?.Id ?? 0;
        public UnitState State => state;
        public bool TryGetVisibleBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
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
            // One simulation tick of visual latency; no extrapolation or speed integration.
            // Use the last sample time, so a paused/ended simulation never oscillates with FixedUpdate.
            Present(sampleInterval > 0 ? (float)((Time.timeAsDouble - sampleTime) / sampleInterval) : 1);
        }
    }
}
