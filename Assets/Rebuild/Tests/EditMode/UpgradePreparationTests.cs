using System;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class UpgradePreparationTests
    {
        [Test]
        public void SummonPurchasesChosenUnitAndPreservesOfferUntilPurchase()
        {
            var initial = new[] { new MemberSpawnDefinition(10, 4, SquadCombatRules.Create(UnitRole.Captain)) };
            var preparation = new SquadPreparation(initial);
            var wallet = new CampaignWallet(100);
            var shop = new ClaimCardShop(wallet, preparation, 3, 1);
            var offer = shop.BeginOffer().ToArray();
            Assert.That(offer.Length, Is.EqualTo(3));
            Assert.That(offer.All(unit => unit.Role != UnitRole.Captain && unit.Role != UnitRole.Test), Is.True);
            Assert.That(offer.All(unit => unit.Level >= 1 && unit.Level <= 2), Is.True);
            Assert.That(shop.BeginOffer().ToArray(), Is.EqualTo(offer));
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(shop.TrySummon(-1), Is.False);
            Assert.That(shop.TrySummon(3), Is.False);
            Assert.That(shop.TrySummon(2), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(97));
            Assert.That(preparation.Members[1].Unit, Is.SameAs(offer[2]));
            Assert.That(preparation.Members[1].MemberId, Is.EqualTo(11));
            Assert.That(preparation.DeployedCount, Is.Zero);
            Assert.That(initial.Length, Is.EqualTo(1));
            Assert.That(shop.NextPrice, Is.EqualTo(4));
            Assert.That(shop.TrySummon(0), Is.False, "A purchase consumes its offer.");
            shop.BeginOffer();
            Assert.That(shop.TrySummon(0), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(93));
        }

        [Test]
        public void UnlimitedSummonsCanExceedOldQuotaButRespectWarehouseCapacity()
        {
            var preparation = new SquadPreparation(new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)) });
            var wallet = new CampaignWallet(unlimited: true);
            var shop = new ClaimCardShop(wallet, preparation, 3, 1);
            for (var i = 1; i < SquadPreparation.WarehouseCapacity; i++)
            {
                Assert.That(shop.BeginOffer().Count, Is.EqualTo(3));
                Assert.That(shop.TrySummon(0), Is.True);
            }
            Assert.That(shop.SummonsCompleted, Is.EqualTo(11));
            Assert.That(wallet.Balance, Is.EqualTo(int.MaxValue));
            Assert.That(preparation.Members.Count, Is.EqualTo(12));
            Assert.That(shop.BeginOffer(), Is.Empty);
            Assert.That(shop.TrySummon(0), Is.False);
            Assert.That(preparation.DeployedCount, Is.Zero);
        }

        [Test]
        public void SummonPoolAdvancesWithStageProgressAndRejectsUnaffordablePurchases()
        {
            var preparation = new SquadPreparation(new[] { new MemberSpawnDefinition(1, 4, SquadCombatRules.Create(UnitRole.Captain)) });
            var shop = new ClaimCardShop(new CampaignWallet(2), preparation, 3, 1);
            Assert.That(shop.BeginOffer(), Is.Empty);
            Assert.That(shop.TrySummon(0), Is.False);
            Assert.That(preparation.Members.Count, Is.EqualTo(1));
            var funded = new ClaimCardShop(new CampaignWallet(100), preparation, 3, 1);
            funded.SetStageProgress(2);
            Assert.That(funded.GetRarityWeights(), Is.EqualTo((50, 35, 15, 0)));
            Assert.That(funded.BeginOffer().All(unit => unit.Level >= 2 && unit.Level <= 3), Is.True);
            funded.SetStageProgress(3);
            Assert.That(funded.GetRarityWeights(), Is.EqualTo((30, 40, 25, 5)));
            funded.SetStageProgress(4);
            funded.SetStageProgress(1);
            Assert.That(funded.HighestStageCleared, Is.EqualTo(4));
            Assert.That(funded.GetRarityWeights(), Is.EqualTo((15, 35, 35, 15)));
            Assert.That(funded.BeginOffer().All(unit => unit.Level >= 4 && unit.Level <= 5), Is.True);
        }

        [Test]
        public void UnlimitedWalletNeverDeductsAndDoesNotChangeNormalWalletRules()
        {
            var unlimited = new CampaignWallet(unlimited: true);
            var normal = new CampaignWallet(10);
            for (var i = 0; i < 100; i++) Assert.That(unlimited.TrySpend(int.MaxValue), Is.True);
            Assert.That(unlimited.IsUnlimited, Is.True);
            Assert.That(unlimited.Balance, Is.EqualTo(int.MaxValue));
            Assert.That(unlimited.TryRewardLevel(1, int.MaxValue), Is.True);
            Assert.That(unlimited.TryRewardLevel(1, int.MaxValue), Is.False);
            Assert.That(unlimited.Balance, Is.EqualTo(int.MaxValue));
            Assert.That(normal.IsUnlimited, Is.False);
            Assert.That(normal.TrySpend(10), Is.True);
            Assert.That(normal.TrySpend(1), Is.False);
            Assert.That(normal.Balance, Is.Zero);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => unlimited.TrySpend(0));
        }

        [Test]
        public void StageGoldRewardsUseAuthoredValuesAndNewFallbackWithoutGrantingUnits()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameEconomySettings>("Assets/Rebuild/Resources/RebuildEconomy.asset");
            Assert.That(settings.GetStageClearGold(1), Is.EqualTo(10));
            Assert.That(settings.GetStageClearGold(2), Is.EqualTo(100));
            Assert.That(settings.GetStageClearGold(3), Is.EqualTo(150));
            Assert.That(settings.GetStageClearGold(4), Is.EqualTo(200));
        }

        [Test]
        public void RewardMemberHasNewIdentityAndDoesNotMutateInventoryOrDeployment()
        {
            var original = new[] { new MemberSpawnDefinition(110, 4, SquadCombatRules.Create(UnitRole.Captain)) };
            var preparation = new SquadPreparation(original);
            preparation.TryAssign(110, 4);
            Assert.That(preparation.TryAddRewardMember(UnitRole.Tank), Is.True);
            Assert.That(original.Length, Is.EqualTo(1));
            Assert.That(preparation.Members.Select(member => member.MemberId), Is.EqualTo(new[] { 110, 111 }));
            Assert.That(preparation.MemberAtSlot(4), Is.EqualTo(110));
            Assert.That(preparation.IsDeployed(111), Is.False);
            Assert.That(preparation.TryAssign(111, 7), Is.True);
            Assert.That(preparation.CaptureDeployment().Single(member => member.MemberId == 111).Unit.Role, Is.EqualTo(UnitRole.Tank));
            for (var i = preparation.Members.Count; i < SquadPreparation.WarehouseCapacity; i++) preparation.TryAddRewardMember(UnitRole.Attack);
            Assert.That(preparation.TryAddRewardMember(UnitRole.Heal), Is.False);
            Assert.That(preparation.Members.Count, Is.EqualTo(12));
            Assert.Throws<ArgumentOutOfRangeException>(() => preparation.TryAddRewardMember(UnitRole.Test));
        }
        [Test]
        public void WalletRewardsEachLevelOnceAndPurchasesNeverOverdraw()
        {
            var wallet = new CampaignWallet();
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(wallet.TryRewardLevel(1, 100), Is.True);
            Assert.That(wallet.TryRewardLevel(1, 100), Is.False);
            Assert.That(wallet.TrySpend(40), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(60));
            Assert.That(wallet.TrySpend(61), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(60));
            Assert.That(wallet.TryRewardLevel(2, 100), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(160));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TryRewardLevel(0, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TryRewardLevel(3, -1));
            Assert.That(new CampaignWallet().Balance, Is.Zero);
        }

        [Test]
        public void RewardOverflowDoesNotConsumeLevelReward()
        {
            var wallet = new CampaignWallet(int.MaxValue);
            Assert.Throws<OverflowException>(() => wallet.TryRewardLevel(1, 100));
            Assert.That(wallet.TrySpend(100), Is.True);
            Assert.That(wallet.TryRewardLevel(1, 100), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(int.MaxValue));
        }
        [TearDown] public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        [Test]
        public void UpgradeHasNoMissingDependenciesAndSharesPrototypeRoster()
        {
            var scene = EditorSceneManager.OpenScene(BattleSceneFlow.UpgradeScene);
            var upgrade = UnityEngine.Object.FindFirstObjectByType<LegacyUpgradeScene>();
            Assert.That(upgrade.Roster, Is.Not.Null);
            Assert.That(upgrade.StartWaveButton, Is.Not.Null);
            Assert.That(upgrade.transform.Find("GoldTXT"), Is.Null);
            foreach (var root in scene.GetRootGameObjects())
                Assert.That(root.GetComponentsInChildren<Component>(true).All(component => component != null), Is.True);
            var roster = upgrade.Roster;
            EditorSceneManager.OpenScene(BattleSceneFlow.PrototypeBattleScene);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<BattleSession>().InitialRoster, Is.SameAs(roster));
        }

        [Test]
        public void SelectedWarehouseMembersKeepIdentityAndStatsInBattle()
        {
            EditorSceneManager.OpenScene(BattleSceneFlow.PrototypeBattleScene);
            var session = UnityEngine.Object.FindFirstObjectByType<BattleSession>();
            var before = session.InitialRoster.CaptureMembers();
            var fixture = Enumerable.Range(1, 8).Select(id => new MemberSpawnDefinition(id, id - 1,
                SquadCombatRules.Create(UnitRole.Attack, UnitRarity.Epic, 3), .5f)).ToArray();
            var preparation = new SquadPreparation(fixture);
            Assert.That(preparation.DeployedCount, Is.Zero);
            Assert.That(preparation.CanStart, Is.False);
            var selected = preparation.Members.First(member => member.SlotId == 4);
            Assert.That(preparation.TryAssign(selected.MemberId, 8), Is.True);
            session.SetPreparedMembers(preparation.CaptureDeployment());
            var after = session.CaptureDefinition();
            var result = after.Members.Single();
            Assert.That((result.MemberId, result.SlotId), Is.EqualTo((selected.MemberId, 8)));
            Assert.That(result.Unit, Is.SameAs(selected.Unit));
            Assert.That(result.SpawnTime, Is.EqualTo(selected.SpawnTime));
            Assert.That(session.InitialRoster.CaptureMembers().Select(member => (member.MemberId, member.SlotId)),
                Is.EqualTo(before.Select(member => (member.MemberId, member.SlotId))));
            Assert.Throws<ArgumentException>(() => session.SetPreparedMembers(fixture.Take(7).ToArray()));
        }

        [Test]
        public void SixMemberCapAllowsMovesReplacementAndReturnWithoutDuplicates()
        {
            var fixture = Enumerable.Range(1, 8).Select(id => new MemberSpawnDefinition(id, id - 1,
                SquadCombatRules.Create(UnitRole.Attack))).ToArray();
            var preparation = new SquadPreparation(fixture);
            var available = preparation.Members;
            var changes = 0; preparation.Changed += () => changes++;
            for (var slot = 0; slot < 6; slot++) Assert.That(preparation.TryAssign(available[slot].MemberId, slot), Is.True);
            Assert.That(preparation.TryAssign(available[6].MemberId, 8), Is.False);
            Assert.That((preparation.DeployedCount, changes), Is.EqualTo((6, 6)));
            Assert.That(preparation.TryAssign(available[0].MemberId, 8), Is.True);
            Assert.That(preparation.MemberAtSlot(0), Is.Zero);
            Assert.That(preparation.TryAssign(available[6].MemberId, 8), Is.True);
            Assert.That(preparation.IsDeployed(available[0].MemberId), Is.False);
            Assert.That(preparation.DeployedCount, Is.EqualTo(6));
            Assert.That(preparation.TryRemove(8), Is.True);
            Assert.That(preparation.DeployedCount, Is.EqualTo(5));
            Assert.That(preparation.TryAssign(available[7].MemberId, 7), Is.True);
            Assert.That(preparation.CaptureDeployment().Select(member => member.MemberId).Distinct().Count(), Is.EqualTo(6));
        }
    }
}
