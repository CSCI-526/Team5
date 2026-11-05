using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class NextMapProjection
    {
        private readonly Vector3 right;
        private readonly Vector3 up;
        private readonly Vector3 forward;
        private readonly float centerX;
        private readonly float centerY;
        private readonly float nearDepth;
        private readonly float depthRange;

        private NextMapProjection(Quaternion rotation, Vector2 panelSize, float pixelsPerUnit, float centerX, float centerY, float nearDepth, float depthRange)
        {
            Rotation = rotation;
            PanelSize = panelSize;
            PixelsPerUnit = pixelsPerUnit;
            right = rotation * Vector3.right;
            up = rotation * Vector3.up;
            forward = rotation * Vector3.forward;
            this.centerX = centerX;
            this.centerY = centerY;
            this.nearDepth = nearDepth;
            this.depthRange = depthRange;
        }

        public Quaternion Rotation { get; }
        public Vector2 PanelSize { get; }
        public float PixelsPerUnit { get; }
        public float GroundSquash => new Vector2(up.x, up.z).magnitude;

        public static NextMapProjection Fit(Quaternion rotation, IEnumerable<Vector3> points, Vector2 panelSize, float side, float top, float bottom)
        {
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var point in points)
            {
                var x = Vector3.Dot(point, right);
                var y = Vector3.Dot(point, up);
                var z = Vector3.Dot(point, forward);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
            }
            if (minX > maxX) { minX = maxX = minY = maxY = minZ = maxZ = 0f; }
            var scale = Mathf.Min((panelSize.x - 2f * side) / Mathf.Max(maxX - minX, 0.5f),
                (panelSize.y - top - bottom) / Mathf.Max(maxY - minY, 0.5f));
            var centerX = (minX + maxX) / 2f;
            var centerY = (minY + maxY) / 2f + (top - bottom) / (2f * scale);
            return new NextMapProjection(rotation, panelSize, scale, centerX, centerY, minZ, maxZ - minZ);
        }

        public Vector2 ToPanel(Vector3 world) =>
            new Vector2((Vector3.Dot(world, right) - centerX) * PixelsPerUnit, (Vector3.Dot(world, up) - centerY) * PixelsPerUnit);

        public void Apply(Camera camera)
        {
            camera.orthographic = true;
            camera.orthographicSize = PanelSize.y / 2f / PixelsPerUnit;
            camera.aspect = PanelSize.x / PanelSize.y;
            camera.transform.SetPositionAndRotation(right * centerX + up * centerY + forward * (nearDepth - 10f), Rotation);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = depthRange + 20f;
        }
    }
}
