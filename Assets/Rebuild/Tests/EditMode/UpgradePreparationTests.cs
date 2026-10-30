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
        public void LevelRewardConfigurationSupportsIndependentGoldAndUnitLists()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameEconomySettings>("Assets/Rebuild/Resources/RebuildEconomy.asset");
            var first = settings.RewardForLevel(1);
            Assert.That(first.gold, Is.EqualTo(10));
            Assert.That(first.CaptureUnitRoles(), Is.EqualTo(new[] { UnitRole.Tank }));
            Assert.That(settings.RewardForLevel(2).gold, Is.EqualTo(100));
            Assert.That(settings.RewardForLevel(2).CaptureUnitRoles(), Is.Empty);
            Assert.That(settings.RewardForLevel(99), Is.Null);
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
            var preparation = new SquadPreparation(session.InitialRoster);
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
            Assert.Throws<ArgumentException>(() => session.SetPreparedMembers(before.Take(7).ToArray()));
        }

        [Test]
        public void SixMemberCapAllowsMovesReplacementAndReturnWithoutDuplicates()
        {
            EditorSceneManager.OpenScene(BattleSceneFlow.PrototypeBattleScene);
            var preparation = new SquadPreparation(UnityEngine.Object.FindFirstObjectByType<BattleSession>().InitialRoster);
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
