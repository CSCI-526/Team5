using NUnit.Framework;
using Team5.Rebuild.Presentation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class MeadowRoadFieldTests
    {
        [Test]
        public void FiveWideRoadHasContinuousInteriorGradientAndSymmetricShoulders()
        {
            const int n=12,w=120,h=108;
            var mask=new bool[w*h];
            for(var z=24;z<84;z++)for(var x=12;x<108;x++)mask[z*w+x]=true;
            var f=TacticalRoadView.SmoothDistance(mask,w,h,n);
            int middle=60;
            Assert.That(f[54*w+middle],Is.GreaterThan(1.8f));
            Assert.That(f[23*w+middle],Is.LessThan(.05f));
            Assert.That(f[36*w+middle],Is.GreaterThan(.8f));
            for(var z=25;z<53;z++)
            {
                Assert.That(f[(z+1)*w+middle],Is.GreaterThan(f[z*w+middle]));
                Assert.That(f[z*w+middle],Is.EqualTo(f[(107-z)*w+middle]).Within(.002f));
            }
        }
        [Test]
        public void CornersRoundWithoutLosingTheRouteCore()
        {
            const int n=12,w=120,h=120;var mask=new bool[w*h];
            for(var z=24;z<84;z++)for(var x=24;x<96;x++)mask[z*w+x]=true;
            var f=TacticalRoadView.SmoothDistance(mask,w,h,n);
            Assert.That(f[25*w+25],Is.LessThan(f[25*w+60]));
            Assert.That(f[54*w+60],Is.GreaterThan(1.8f));
            Assert.That(f[12*w+12],Is.LessThan(-.7f));
        }
        [Test]
        public void VisualSpineFilletsCornerWithoutChangingEndpoints()
        {
            var points=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(2,0,0),new Vector3(3,0,0),new Vector3(3,0,1),new Vector3(3,0,2),new Vector3(3,0,3)};
            var curve=TacticalRoadView.CurvedSpine(points);
            Assert.That(curve[0],Is.EqualTo(Vector2.zero));
            Assert.That(curve[curve.Length-1],Is.EqualTo(new Vector2(3,3)));
            Assert.That(System.Array.Exists(curve,p=>p.x>1.6f && p.x<3 && p.y>0 && p.y<1.4f),Is.True);
            Assert.That(System.Array.Exists(curve,p=>p==new Vector2(3,0)),Is.False);
        }
        [TestCase(1.333f)] [TestCase(1.778f)] [TestCase(2.333f)]
        public void DollyRetainsAtLeastTwentyFiveWorldUnitsHorizontallyWhenMapAllows(float aspect)
        {
            var size=StageOverviewCamera.LocalSize(20f,aspect);
            Assert.That(size*aspect*2,Is.GreaterThanOrEqualTo(24.99f));
            Assert.That(size,Is.LessThan(20f));
            Assert.That(StageOverviewCamera.LocalSize(5f,aspect),Is.EqualTo(5f));
        }
    }
}
