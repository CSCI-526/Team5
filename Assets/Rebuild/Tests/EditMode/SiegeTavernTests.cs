using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class SiegeTavernTests
    {
        [Test] public void PurchaseRefreshAndFreezeRespectGoldAndKeepOffers()
        {
            var run=new SiegeTavernRun();run.Offers[0]=2;
            Assert.IsTrue(run.Buy(0));Assert.AreEqual(5,run.Gold);Assert.AreEqual(4,run.Followers.Count);
            Assert.IsFalse(run.Buy(0));Assert.AreEqual(5,run.Gold);
            Assert.IsTrue(run.Roll());Assert.AreEqual(4,run.Gold);
            var offers=run.Offers.ToArray();run.Frozen=true;run.Resolve(true);
            CollectionAssert.AreEqual(offers,run.Offers);Assert.AreEqual(2,run.Round);
            run.Gold=0;Assert.IsFalse(run.Roll());Assert.IsFalse(run.Buy(1));
        }
        [Test] public void FormationSwapsAndProtectsLastDeployedFollower()
        {
            var run=new SiegeTavernRun();var a=run.Followers[0];var b=run.Followers[1];
            Assert.IsTrue(run.Place(a.Id,b.Slot));Assert.AreEqual(0,b.Slot);Assert.AreEqual(1,a.Slot);
            Assert.IsTrue(run.Bench(a.Id));Assert.IsTrue(run.Bench(b.Id));
            var last=run.Followers.Single(f=>f.Slot>=0);
            Assert.IsFalse(run.Bench(last.Id));Assert.IsFalse(run.Sell(last.Id));
            Assert.AreEqual(1,run.Deployment().Count);
        }
        [Test] public void EnemyStartsEmptyBuildsUpgradesAndStopsWhenPaused()
        {
            var tower=new TowerDefinition(10,new Vector3(5,0,5),3,.55f,1.2f,TargetRule.FurthestAlongRoute);
            var battle=new BattleSimulation(new BattleDefinition(new[]{Vector3.zero,Vector3.right*100},new[]{tower},new UnitDefinition(100,1),1,1,1));
            var builder=new SiegeEnemyBuilder(battle,new[]{10});var state=battle.Towers[0];
            Assert.IsTrue(state.UnderConstruction);Assert.IsFalse(state.IsAlive);
            builder.Tick(3.9f);Assert.AreEqual(0,builder.BuiltCount);
            builder.Tick(.1f);Assert.IsTrue(state.IsAlive);Assert.AreEqual(1,builder.Levels[0]);
            var damage=state.Definition.Damage;builder.Tick(5);Assert.AreEqual(2,builder.Levels[0]);
            Assert.Greater(state.Definition.Damage,damage);Assert.AreEqual(28,state.Hp,.001f);
            battle.SetPaused(true);var gold=builder.Gold;builder.Tick(100);
            Assert.AreEqual(gold,builder.Gold);Assert.AreEqual(2,builder.Levels[0]);
            battle.SetPaused(false);builder.Tick(7);Assert.AreEqual(3,builder.Levels[0]);
            builder.Tick(100);Assert.AreEqual(3,builder.Levels[0]);
        }
        [Test] public void NormalBattlesStillStartWithFullyBuiltTowers()
        {
            var tower=new TowerDefinition(10,Vector3.forward*3,3,1,1,TargetRule.Nearest);
            var battle=new BattleSimulation(new BattleDefinition(new[]{Vector3.zero,Vector3.right*10},new[]{tower},new UnitDefinition(10,1),1,1,1));
            Assert.IsTrue(battle.Towers[0].IsAlive);Assert.IsFalse(battle.Towers[0].UnderConstruction);
        }
        [Test] public void UpgradePreservesDamageAndDestroyedTowerCannotBeRebuilt()
        {
            var tower=new TowerDefinition(10,new Vector3(2,0,2),0,.1f,1,TargetRule.Nearest,maxHp:18);
            var member=new MemberSpawnDefinition(1,4,new UnitDefinition(100,.1f,UnitRole.Attack,damage:2,range:10));
            var battle=new BattleSimulation(new BattleDefinition(new[]{Vector3.zero,Vector3.right*50},new[]{tower},new[]{member}));
            var builder=new SiegeEnemyBuilder(battle,new[]{10});builder.Tick(4);
            for(var i=0;i<100&&battle.Towers[0].Hp>=18;i++)battle.Step(.02f);
            var state=battle.Towers[0];Assert.Less(state.Hp,18);Assert.Greater(state.Hp,0);
            var fraction=state.Hp/state.Definition.MaxHp;
            builder.Tick(5);Assert.AreEqual(2,builder.Levels[0]);
            Assert.AreEqual(fraction,state.Hp/state.Definition.MaxHp,.0001f);
            for(var i=0;i<3000&&state.IsAlive;i++)battle.Step(.02f);
            Assert.IsFalse(state.IsAlive);Assert.IsFalse(battle.CompleteOrUpgradeTower(10,3));
            builder.Tick(100);Assert.AreEqual(2,builder.Levels[0]);
        }
        [Test] public void DeploymentEffectsAndLossProgressionAreIsolated()
        {
            var run=new SiegeTavernRun();var guard=run.Deployment().Single(m=>m.MemberId==run.Followers[0].Id);
            Assert.AreEqual(9,guard.Unit.MaxHp); // captain health aura
            Assert.Greater(guard.Unit.Defense,0); // neighboring tank
            run.Resolve(false);Assert.AreEqual(2,run.Lives);Assert.AreEqual(1,run.Round);
            run.Resolve(false);run.Resolve(false);Assert.IsTrue(run.Finished);
        }
        [Test] public void FullFormationCanSwapInABenchedRecruitWithoutExceedingCapacity()
        {
            var run=new SiegeTavernRun();run.Gold=15;
            for(var i=0;i<4;i++){run.Offers[0]=0;Assert.IsTrue(run.Buy(0));}
            Assert.AreEqual(6,run.Deployment().Count);
            var bench=run.Followers.Single(f=>f.Slot<0);var occupant=run.Followers.Single(f=>f.Slot==0);
            Assert.IsFalse(run.Place(bench.Id,8));Assert.IsTrue(run.Place(bench.Id,0));
            Assert.AreEqual(-1,occupant.Slot);Assert.AreEqual(6,run.Deployment().Count);
            Assert.AreEqual(6,run.Deployment().Select(m=>m.SlotId).Distinct().Count());
        }
    }
}
