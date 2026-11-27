using NUnit.Framework;
using Team5.Rebuild.Presentation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class StageOverviewCameraTests
    {
        [TestCase(1.333333f)] [TestCase(1.6f)] [TestCase(1.777778f)] [TestCase(2.333333f)]
        public void AllBattlefieldCornersStayInsideReservedViewport(float aspect)
        {
            var go = new GameObject("Composition test camera");
            try
            {
                var camera = go.AddComponent<Camera>(); camera.aspect = aspect;
                var bounds = new Bounds(new Vector3(18f, 1f, 12f), new Vector3(33f, 3f, 24f));
                StageOverviewCamera.Fit(camera, bounds, aspect);
                Assert.That(camera.orthographic, Is.True);
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x,y,z));
                    var p = camera.WorldToViewportPoint(corner);
                    Assert.That(p.x, Is.InRange(.059f, .941f));
                    Assert.That(p.y, Is.InRange(.099f, .901f));
                    Assert.That(p.z, Is.GreaterThan(0f));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
