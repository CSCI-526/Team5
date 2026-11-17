using NUnit.Framework;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class SiegeRamMotionTests
    {
        [Test]public void FirstLegAlignsToAdjacentBlockCenterThenKeepsThreeCellSegments()
        {
            var start=new Vector3(1.8f,.25f,2.1f);
            var first=SiegeRamMotion.NextBlockCenter(start,Vector3.right);
            Assert.That(first,Is.EqualTo(new Vector3(4,.25f,1)));
            var ram=new SiegeRamMotion(1,start,1);
            Assert.That(ram.TrySetRoute(new[]{start,first,first+Vector3.forward*3},p=>true,true),Is.True);
            ram.Advance(20,p=>true);Assert.That(ram.Position,Is.EqualTo(first+Vector3.forward*3));
            ram=new SiegeRamMotion(2,start,1);
            Assert.That(ram.TrySetRoute(new[]{start,start+Vector3.right*3},p=>true,true),Is.False,"The first point must be a block center.");
            Assert.That(ram.TrySetRoute(new[]{start,first,first+Vector3.forward},p=>true,true),Is.False,"Only the first leg permits a non-three-cell distance.");
            Assert.That(ram.TrySetRoute(new[]{start,first,first+new Vector3(3,0,3)},p=>true,true),Is.False);
            Assert.That(ram.TrySetRoute(new[]{start,first},p=>p.x<2.5f,true),Is.False,"Alignment must not skip obstacles.");
            Assert.That(SiegeRamMotion.NextBlockCenter(new Vector3(-1,0,-1),Vector3.left),Is.EqualTo(new Vector3(-5,0,-2)));
            var nearEdge=new Vector3(2.4f,0,1);
            Assert.That(new SiegeRamMotion(3,nearEdge,1).TrySetRoute(new[]{nearEdge,new Vector3(4,0,1)},p=>true,true),Is.True);
        }
        [Test]public void ContactDamagesOnlyWoodenGateAndDeathReleasesRamRoute()
        {
            var gate=new TowerDefinition(1,new Vector3(3,0,0),0,0,1,TargetRule.Nearest,maxHp:20,isWall:true,wallAlongZ:true,isWoodenGate:true);
            var stone=new TowerDefinition(2,new Vector3(3,0,5),0,0,1,TargetRule.Nearest,maxHp:20,isWall:true,wallAlongZ:true);
            var sim=new BattleSimulation(new BattleDefinition(new[]{Vector3.zero,Vector3.right*12},new[]{gate,stone},new UnitDefinition(100,1),1,1,1));
            var damageEvents=0;var deaths=0;sim.TowerDamaged+=e=>damageEvents++;sim.TowerDied+=id=>deaths++;
            Assert.That(sim.TryRamWoodenGate(-1,Vector3.zero,.42f,10),Is.False,"No remote damage.");
            Assert.That(sim.TryRamWoodenGate(-1,stone.Position,.42f,10),Is.False,"Stone is immune to ram damage.");
            sim.SetPaused(true);Assert.That(sim.TryRamWoodenGate(-1,gate.Position,.42f,10),Is.False);sim.SetPaused(false);
            var ram=new SiegeRamMotion(1,Vector3.zero,1);
            Assert.That(ram.TrySetRoute(new[]{Vector3.zero,Vector3.right*6},p=>true),Is.True);
            bool Passable(Vector3 p)=>!sim.Towers[0].IsAlive||Mathf.Abs(p.x-3)>.92f;
            ram.Advance(6,Passable,p=>Assert.That(sim.TryRamWoodenGate(-1,p,.42f,10),Is.True));
            Assert.That(sim.Towers[0].Hp,Is.EqualTo(10));Assert.That(ram.Position.x,Is.LessThan(2.08f));
            var stopped=ram.Position;ram.Advance(0,Passable,p=>Assert.Fail("Pause must not attack."));Assert.That(ram.Position,Is.EqualTo(stopped));
            ram.Advance(1,Passable,p=>sim.TryRamWoodenGate(-1,p,.42f,10));
            Assert.That(sim.Towers[0].IsAlive,Is.False);Assert.That(deaths,Is.EqualTo(1));Assert.That(damageEvents,Is.EqualTo(2));
            Assert.That(sim.GetCellOccupancy(new Vector3Int(3,0,0)).TowerIds,Is.Empty);
            ram.Advance(10,Passable);Assert.That(ram.Position,Is.EqualTo(Vector3.right*6));
            Assert.That(sim.Towers[1].Hp,Is.EqualTo(20));Assert.That(sim.TowerBountyGoldEarned,Is.Zero);
        }
        [Test]public void OrthogonalRouteMovesInOrderWithoutAffectingTheSquad()
        {
            var ram=new SiegeRamMotion(1,Vector3.zero,2);
            Assert.That(ram.TrySetRoute(new[]{Vector3.zero,Vector3.right*3,new Vector3(3,0,3)},p=>true),Is.True);
            ram.Advance(1.5f,p=>true);Assert.That(Vector3.Distance(ram.Position,Vector3.right*3),Is.LessThan(.0001f));
            ram.Advance(1.5f,p=>true);Assert.That(Vector3.Distance(ram.Position,new Vector3(3,0,3)),Is.LessThan(.0001f));Assert.That(ram.IsMoving,Is.False);
        }
        [Test]public void InvalidPathsAreRejectedAndNewObstaclesStopMotion()
        {
            var ram=new SiegeRamMotion(1,Vector3.zero,1);
            Assert.That(ram.TrySetRoute(new[]{Vector3.zero,new Vector3(3,0,3)},p=>true),Is.False);
            Assert.That(ram.TrySetRoute(new[]{Vector3.zero,Vector3.right},p=>true),Is.False);
            Assert.That(ram.TrySetRoute(new[]{Vector3.zero,Vector3.right*3},p=>p.x<1||p.x>2),Is.False);
            Assert.That(ram.TrySetRoute(new[]{Vector3.zero,Vector3.right*3},p=>true),Is.True);
            ram.Advance(10,p=>p.x<1);Assert.That(ram.Position.x,Is.LessThan(1));
            var stopped=ram.Position;ram.Advance(0,p=>true);Assert.That(ram.Position,Is.EqualTo(stopped));
            ram.Advance(10,p=>true);Assert.That(ram.Position,Is.EqualTo(Vector3.right*3));
        }
    }
}
