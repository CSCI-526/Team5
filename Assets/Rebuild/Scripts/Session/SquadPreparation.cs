using System;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;

namespace Team5.Rebuild.Session
{
    public sealed class SquadPreparation
    {
        public const int WarehouseRows = 2, WarehouseColumns = 6;
        public const int WarehouseCapacity = WarehouseRows * WarehouseColumns;
        public const int MaxDeployed = 6;

        private readonly List<MemberSpawnDefinition> members;
        private readonly HashSet<UnitRole> ownedRoles = new HashSet<UnitRole>();
        public bool HasOwnedRole(UnitRole role) => ownedRoles.Contains(role);
        public bool FirstSummonPromptCompleted { get; private set; }
        public void CompleteFirstSummonPrompt() => FirstSummonPromptCompleted = true;
        private readonly int[] memberIds = new int[9];

        public bool CanAddMember => members.Count < WarehouseCapacity && !members.Exists(m => m.MemberId == int.MaxValue);
        public SquadRoster Roster { get; }
        public IReadOnlyList<MemberSpawnDefinition> Members => members.AsReadOnly();
        public event Action Changed;

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
            foreach (var member in members) ownedRoles.Add(member.Unit.Role);
        }

        public bool TryAddSummonedUnit(UnitDefinition unit)
        {
            if (unit == null || unit.Role == UnitRole.Test) throw new ArgumentException(nameof(unit));
            if (!CanAddMember) return false;
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
            if (member == null || member.Unit.Role == UnitRole.Captain) return false;

            var slot = Array.IndexOf(memberIds, memberId);
            if (slot >= 0) memberIds[slot] = 0;

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
            if (slot < 0 || slot >= 9 || FindMember(memberId) == null) return false;
            var previous = Array.IndexOf(memberIds, memberId);
            if (previous == slot) return true;
            if (previous < 0 && memberIds[slot] == 0 && DeployedCount >= MaxDeployed) return false;
            if (previous >= 0) memberIds[previous] = memberIds[slot];
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
            if (slot < 0 || slot >= 9 || memberIds[slot] == 0) return false;
            memberIds[slot] = 0;
            Changed?.Invoke();
            return true;
        }

        public IReadOnlyList<MemberSpawnDefinition> CaptureDeployment()
        {
            if (!CanStart) throw new InvalidOperationException("Deploy between one and six members before starting.");
            var result = new List<MemberSpawnDefinition>();
            for (var slot = 0; slot < 9; slot++)
            {
                var member = FindMember(memberIds[slot]);
                if (member != null)
                    result.Add(new MemberSpawnDefinition(member.MemberId, slot, member.Unit, member.SpawnTime));
            }
            return result.AsReadOnly();
        }
    }
}
