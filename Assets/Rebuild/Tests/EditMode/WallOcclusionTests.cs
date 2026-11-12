using NUnit.Framework;
using Team5.Rebuild.Presentation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class WallOcclusionTests
    {
        private static Vector3[] Rect(float left, float bottom, float right, float top, float depth) =>
            new[] { new Vector3(left, bottom, depth), new Vector3(right, bottom, depth),
                new Vector3(right, top, depth), new Vector3(left, top, depth) };

        [Test]
        public void TinyPartialOverlapMakesWallTransparent() =>
            Assert.That(WallOcclusionGeometry.Occludes(Rect(0, 0, 10, 10, 3), Rect(9.999f, 4, 12, 6, 5)), Is.True);
        [Test]
        public void CompleteOcclusionMakesWallTransparent() =>
            Assert.That(WallOcclusionGeometry.Occludes(Rect(0, 0, 10, 10, 3), Rect(2, 2, 4, 4, 5)), Is.True);
        [Test]
        public void UnitInFrontDoesNotFadeWall() =>
            Assert.That(WallOcclusionGeometry.Occludes(Rect(0, 0, 10, 10, 7), Rect(2, 2, 4, 4, 5)), Is.False);
        [Test]
        public void NoOverlapRestoresOpaqueWall() =>
            Assert.That(WallOcclusionGeometry.Occludes(Rect(0, 0, 10, 10, 3), Rect(11, 2, 14, 4, 5)), Is.False);
        [Test]
        public void DiagonalSilhouettesAreUsedInsteadOfTheirBoundingRectangles()
        {
            var diamond = new[] { new Vector3(0, 5, 3), new Vector3(5, 10, 3),
                new Vector3(10, 5, 3), new Vector3(5, 0, 3) };
            Assert.That(WallOcclusionGeometry.Occludes(diamond, Rect(0, 0, 1, 1, 5)), Is.False);
        }
        [Test]
        public void DuplicatedProjectedCornersAreSupported()
        {
            var wall = Rect(0, 0, 10, 10, 3);
            var doubled = new Vector3[8];
            wall.CopyTo(doubled, 0); wall.CopyTo(doubled, 4);
            Assert.That(WallOcclusionGeometry.Occludes(doubled, Rect(9.99f, 4, 12, 6, 5)), Is.True);
        }
        [Test]
        public void GeometryBehindCameraDoesNotFadeWall() =>
            Assert.That(WallOcclusionGeometry.Occludes(Rect(0, 0, 10, 10, -3), Rect(2, 2, 4, 4, 5)), Is.False);
    }
}
