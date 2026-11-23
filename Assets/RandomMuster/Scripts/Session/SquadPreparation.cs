using System;
using System.Collections.Generic;
using Team5.BasicTowerTrial.Simulation;

namespace Team5.BasicTowerTrial.Session
{
    public sealed class SquadPreparation
    {
        public const int WarehouseRows = 2, WarehouseColumns = 6;
        public const int WarehouseCapacity = WarehouseRows * WarehouseColumns;
        public const int MaxDeployed = 6;

        private readonly List<MemberSpawnDefinition> members;
        private readonly HashSet<UnitRole> ownedRoles = new HashSet<UnitRole>();
        private int lastLegendaryLevelAwardStage;
        public bool HasOwnedRole(UnitRole role) => ownedRoles.Contains(role);
        public bool FirstSummonPromptCompleted { get; private set; }
        public void CompleteFirstSummonPrompt() => FirstSummonPromptCompleted = true;
        private readonly int[] memberIds = new int[9];

        public bool CanAddMember => members.Count < WarehouseCapacity && !members.Exists(m => m.MemberId == int.MaxValue);
        public SquadRoster Roster { get; }
        public IReadOnlyList<MemberSpawnDefinition> Members => members.AsReadOnly();
        public event Action Changed;
        private readonly List<int> deploymentOrder = new List<int>();

        public int DeployedCount
        {
            get
            {
                var count = 0;
                foreach (var id in memberIds) if (id != 0) count++;
                return count;
            }
        }

        public bool CanStart => DeployedCount > 0 && DeployedCount <= MaxDeployed;

        public SquadPreparation(SquadRoster roster)
            : this(roster != null ? roster.CaptureMembers() : throw new ArgumentNullException(nameof(roster)))
            => Roster = roster;

        public SquadPreparation(IReadOnlyList<MemberSpawnDefinition> availableMembers)
        {
            if (availableMembers == null) throw new ArgumentNullException(nameof(availableMembers));
            members = new List<MemberSpawnDefinition>(availableMembers);
            var ids = new HashSet<int>();
            foreach (var member in members)
                if (member == null || !ids.Add(member.MemberId))
                    throw new ArgumentException("Warehouse members require unique IDs.");
            if (members.Count > WarehouseCapacity)
                throw new ArgumentException("Warehouse supports up to twelve members.");
            foreach (var member in members)
            {
                if (member.Unit.Rarity == UnitRarity.Legendary && ownedRoles.Contains(member.Unit.Role))
                    throw new ArgumentException("Only one copy of each legendary character may be owned.");
                ownedRoles.Add(member.Unit.Role);
            }
        }

        // Campaign owners always have the unique Captain; test fixtures can still use empty/non-Captain squads.
        public void EnsureStartingCaptain()
        {
            if (HasOwnedRole(UnitRole.Captain)) return;
            if (!CanAddMember) throw new InvalidOperationException("Reserve a warehouse space for the starting Captain.");
            var id = 0;
            foreach (var member in members) id = Math.Max(id, member.MemberId);
            members.Insert(0, new MemberSpawnDefinition(checked(id + 1), 4, SquadCombatRules.Create(UnitRole.Captain)));
            ownedRoles.Add(UnitRole.Captain);
            Changed?.Invoke();
        }

        // Legendary characters grow automatically when campaign progress advances, without duplicates or fusion.
        public void AdvanceLegendaryLevels(int highestStageCleared)
        {
            var newStage = Math.Max(0, highestStageCleared);
            var levelsEarned = Math.Max(0, newStage - lastLegendaryLevelAwardStage);
            if (levelsEarned == 0) return;
            lastLegendaryLevelAwardStage = newStage;
            var changed = false;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member.Unit.Rarity != UnitRarity.Legendary) continue;
                members[i] = new MemberSpawnDefinition(member.MemberId, member.SlotId,
                    SquadCombatRules.Create(member.Unit.Role, UnitRarity.Legendary, checked(member.Unit.Level + levelsEarned)), member.SpawnTime);
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        public bool TryAddSummonedUnit(UnitDefinition unit)
        {
            if (unit == null || unit.Role == UnitRole.Test) throw new ArgumentException(nameof(unit));
            if (!CanAddMember) return false;
            if (unit.Rarity == UnitRarity.Legendary && HasOwnedRole(unit.Role)) return false;
            var id = 0;
            foreach (var member in members) id = Math.Max(id, member.MemberId);
            members.Add(new MemberSpawnDefinition(checked(id + 1), 4, unit));
            ownedRoles.Add(unit.Role);
            Changed?.Invoke();
            return true;
        }

        public bool TryAddRewardMember(UnitRole role) =>
            TryAddSummonedUnit(SquadCombatRules.Create(role));

        // Allows dismissing older/weaker units to free up storage space (protects Captain and last unit)
        public bool TryDismissMember(int memberId)
        {
            if (members.Count <= 1) return false;
            var member = FindMember(memberId);
            if (member == null || member.Unit.Rarity == UnitRarity.Legendary) return false;

            var slot = Array.IndexOf(memberIds, memberId);
            if (slot >= 0) memberIds[slot] = 0;

            deploymentOrder.Remove(memberId);
            members.Remove(member);
            Changed?.Invoke();
            return true;
        }

        public int MemberAtSlot(int slot) => slot >= 0 && slot < 9 ? memberIds[slot] : 0;
        public UnitRole RoleAtSlot(int slot) => FindMember(MemberAtSlot(slot))?.Unit.Role ?? UnitRole.Test;
        public bool IsDeployed(int memberId) => memberId > 0 && Array.IndexOf(memberIds, memberId) >= 0;

        public MemberSpawnDefinition FindMember(int memberId)
        {
            foreach (var member in members)
                if (member.MemberId == memberId) return member;
            return null;
        }

        public bool TryAssign(int memberId, int slot)
        {
            if (slot < 0 || slot >= 9 || FindMember(memberId) == null)
                return false;

            var previous = Array.IndexOf(memberIds, memberId);

            if (previous == slot)
                return true;

            if (previous < 0 && memberIds[slot] == 0 &&
                DeployedCount >= MaxDeployed)
                return false;

            int displaced = memberIds[slot];

            // A member entering the formation goes to the end
            // of the deployment order.
            if (previous < 0)
            {
                if (displaced != 0)
                    deploymentOrder.Remove(displaced);

                deploymentOrder.Add(memberId);
            }

            if (previous >= 0)
                memberIds[previous] = displaced;

            memberIds[slot] = memberId;

            Changed?.Invoke();
            return true;
        }

        public bool TryMove(int from, int to)
        {
            if (from < 0 || from >= 9 || to < 0 || to >= 9 || memberIds[from] == 0) return false;
            return TryAssign(memberIds[from], to);
        }

        public bool TryRemove(int slot)
        {
            if (slot < 0 || slot >= 9 || memberIds[slot] == 0)
                return false;

            deploymentOrder.Remove(memberIds[slot]);
            memberIds[slot] = 0;

            Changed?.Invoke();
            return true;
        }

        public IReadOnlyList<MemberSpawnDefinition> CaptureDeployment()
        {
            if (!CanStart)
                throw new InvalidOperationException(
                    "Deploy between one and six members before starting.");

            var result = new List<MemberSpawnDefinition>();

            // Preserve the order members were added to the formation.
            foreach (int memberId in deploymentOrder)
            {
                int slot = Array.IndexOf(memberIds, memberId);

                if (slot < 0)
                    continue;

                var member = FindMember(memberId);

                if (member == null)
                    continue;

                result.Add(new MemberSpawnDefinition(
                    member.MemberId,
                    slot,
                    member.Unit,
                    member.SpawnTime
                ));
            }

            return result.AsReadOnly();
        }
    }
}
