using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;

namespace Team5.Rebuild.Tests
{
    public sealed class TavernRecruitmentTests
    {
        [Test] public void ShopUsesExistingUnitsAndChargesOnlySuccessfulTransactions()
        {
            var state=new TavernRecruitment(17);state.Offers[0]=UnitRole.Attack;
            Assert.AreEqual(3,state.Gold);Assert.AreEqual(3,state.Offers.Count);
            Assert.IsTrue(state.Buy(0));Assert.AreEqual(0,state.Gold);Assert.AreEqual(1,state.BenchCount);
            Assert.IsFalse(state.Buy(0));Assert.IsFalse(state.Roll());Assert.AreEqual(0,state.Gold);
            var follower=state.Followers.Last();var expected=SquadCombatRules.Create(UnitRole.Attack);
            Assert.AreEqual(expected.MaxHp,state.Stats(follower).MaxHp);
            Assert.AreEqual(expected.Damage,state.Stats(follower).Damage);
        }
        [Test] public void TriplesPreserveDeploymentDoubleStatsAndAwardFreeDiscovery()
        {
            var state=new TavernRecruitment(1);state.Offers[0]=UnitRole.Attack;state.Buy(0);
            state.Resolve(true);state.Offers[0]=UnitRole.Attack;Assert.IsTrue(state.Buy(0));
            var golden=state.Followers.Single(f=>f.Role==UnitRole.Attack);
            Assert.IsTrue(golden.Golden);Assert.AreEqual(7,golden.Slot);
            Assert.AreEqual(SquadCombatRules.Create(UnitRole.Attack).MaxHp*2,state.Stats(golden).MaxHp);
            Assert.AreEqual(3,state.Discover.Count);Assert.AreEqual(3,state.Discover.Distinct().Count());
            Assert.IsFalse(state.Ready);Assert.IsFalse(state.Roll());
            var gold=state.Gold;Assert.IsTrue(state.ChooseDiscovery(0));Assert.AreEqual(gold,state.Gold);Assert.IsTrue(state.Ready);
        }
        [Test] public void FreezeKeepsUnsoldOffersAndIncomeResetsEachTurn()
        {
            var state=new TavernRecruitment(1);state.Offers[0]=UnitRole.Attack;state.Buy(0);state.ToggleFreeze();
            var preserved=state.Offers[1];state.Resolve(true);
            Assert.AreEqual(preserved,state.Offers[1]);Assert.IsTrue(state.Offers[0].HasValue);
            Assert.IsFalse(state.Frozen);Assert.AreEqual(4,state.Gold);
            state.Resolve(true);Assert.AreEqual(5,state.Gold);
            Assert.IsTrue(state.Upgrade());Assert.AreEqual(2,state.Tier);
            Assert.IsTrue(state.Offers.All(r=>!r.HasValue||TavernRecruitment.RoleTier(r.Value)<=state.Tier));
        }
        [Test] public void BenchToBoardSwapsAndUniqueCaptainCannotBeSold()
        {
            var state=new TavernRecruitment(1);state.Offers[0]=UnitRole.Tank;state.Buy(0);
            var tank=state.Followers.Single(f=>f.Role==UnitRole.Tank);
            var attack=state.Followers.Single(f=>f.Role==UnitRole.Attack);
            Assert.IsTrue(state.Place(tank.Id,7));Assert.AreEqual(-1,attack.Slot);
            Assert.IsTrue(state.Bench(tank.Id));Assert.IsFalse(state.Bench(state.Followers.Single(f=>f.Role==UnitRole.Captain).Id));
            Assert.IsFalse(state.Sell(state.Followers.Single(f=>f.Role==UnitRole.Captain).Id));
            Assert.AreEqual(state.DeployedCount,state.Deployment().Select(m=>m.SlotId).Distinct().Count());
        }
        [Test] public void OrdinaryFullBenchRejectsPurchaseWithoutSpendingGold()
        {
            var state=new TavernRecruitment(1);
            for(var i=0;i<7;i++)state.Followers.Add(new TavernRecruitment.Follower{Id=9000+i,Role=UnitRole.Tank,Golden=true});
            state.Offers[0]=UnitRole.Heal;Assert.IsFalse(state.Buy(0));Assert.AreEqual(3,state.Gold);Assert.AreEqual(UnitRole.Heal,state.Offers[0]);
        }
        [Test] public void LossesEndRunAndActionsStopAfterFinish()
        {
            var state=new TavernRecruitment(1);state.Resolve(false);state.Resolve(false);state.Resolve(false);
            Assert.IsTrue(state.Finished);Assert.IsFalse(state.Ready);Assert.IsFalse(state.Buy(0));Assert.IsFalse(state.Roll());
            var turn=state.Turn;state.Resolve(true);Assert.AreEqual(turn,state.Turn);
        }
    }
}
