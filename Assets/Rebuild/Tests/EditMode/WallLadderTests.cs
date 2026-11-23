using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class WallLadderTests
    {
        private const float Top=1.39f;
        private static BattleSimulation Create(bool ladder=true)
        {
            var cells=new List<Vector3Int>();for(var x=-5;x<=7;x++)for(var z=-4;z<=5;z++)cells.Add(new Vector3Int(x,0,z));
            var top=Enumerable.Range(-1,3).Select(x=>new Vector3Int(x,2,0)).ToArray();cells.AddRange(top);
            var wall=new TowerDefinition(1,new Vector3(0,.25f,0),0,0,1,TargetRule.FurthestAlongRoute,1,100,isWall:true);
            var members=Enumerable.Range(0,4).Select(i=>new MemberSpawnDefinition(11+i,i,SquadCombatRules.Create(UnitRole.Attack)));
            var link=new LadderDefinition(1,new Vector3(0,.25f,-.85f),new Vector3(0,Top,-.85f),new Vector3(0,Top,0));
            var definition=new BattleDefinition(new[]{new Vector3(-3,.25f,-2),new Vector3(-3,.25f,-3)},new[]{wall},members,cells,
                new[]{new WallSurfaceDefinition(1,Top,top)},ladder?new[]{link}:new LadderDefinition[0]);
            var sim=new BattleSimulation(definition);sim.EnableContinuousMovement(.4f,.12f,true);sim.Step(.02f);return sim;
        }
        [Test]public void LadderConnectsGroundToWallAndBackWithoutTeleportingOrChangingSlots()
        {
            var sim=Create();var slots=sim.Units.Select(u=>u.SlotId).ToArray();
            Assert.That(sim.RequestMovement(new MovementRequest(new Vector3(0,Top,0))),Is.True);
            var climbed=false;
            for(var i=0;i<5000&&sim.IsSquadMoving;i++)
            {
                var previous=sim.Units.Select(u=>u.Position).ToArray();sim.Step(.02f);
                for(var j=0;j<sim.Units.Count;j++)
                {
                    var p=sim.Units[j].Position;
                    Assert.That(Vector3.Distance(previous[j],p),Is.LessThanOrEqualTo(sim.Units[j].Speed*.0201f));
                    if(p.y>.26f&&p.y<Top-.01f){climbed=true;Assert.That(p.x,Is.EqualTo(0).Within(.001f));Assert.That(p.z,Is.EqualTo(-.85f).Within(.001f));}
                }
            }
            Assert.That(climbed,Is.True);Assert.That(sim.IsSquadMoving,Is.False);
            Assert.That(sim.Units.All(u=>Mathf.Abs(u.Position.y-Top)<.001f),Is.True);
            Assert.That(sim.Units.Select(u=>u.SlotId),Is.EqualTo(slots));
            Assert.That(sim.RequestMovement(new MovementRequest(new Vector3(.5f,Top,0))),Is.True);Finish(sim);
            Assert.That(sim.RequestMovement(new MovementRequest(new Vector3(-3,.25f,-2))),Is.True);Finish(sim);
            Assert.That(sim.Units.All(u=>Mathf.Abs(u.Position.y-.25f)<.001f),Is.True);
        }
        [Test]public void NoLadderCannotEnterUpperFloorAndPauseStopsClimb()
        {
            var plain=Create(false);Assert.That(plain.RequestMovement(new MovementRequest(new Vector3(0,Top,0))),Is.False);
            var sim=Create();Assert.That(sim.RequestMovement(new MovementRequest(new Vector3(0,Top,0))),Is.True);
            for(var i=0;i<500&&!sim.Units.Any(u=>u.Position.y>.3f);i++)sim.Step(.02f);
            var before=sim.Units.Select(u=>u.Position).ToArray();sim.SetPaused(true);sim.Step(1);
            Assert.That(sim.Units.Select(u=>u.Position),Is.EqualTo(before));
        }
        private static void Finish(BattleSimulation sim){for(var i=0;i<5000&&sim.IsSquadMoving;i++)sim.Step(.02f);Assert.That(sim.IsSquadMoving,Is.False);}
    }
}
