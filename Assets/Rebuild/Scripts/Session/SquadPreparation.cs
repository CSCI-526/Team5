using System;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;

namespace Team5.Rebuild.Session
{
    // Per-run draft, never writes to the shared roster or a Scene's configuration.
    public sealed class SquadPreparation
    {
        public const int WarehouseRows = 2, WarehouseColumns = 6;
        public const int WarehouseCapacity = WarehouseRows * WarehouseColumns;
        public const int MaxDeployed = 6;
        private readonly List<MemberSpawnDefinition> members;
        private readonly int[] memberIds = new int[9]; // 0 = empty; stable IDs are never slot indexes.
        public bool CanAddMember => members.Count < WarehouseCapacity && !members.Exists(member => member.MemberId == int.MaxValue);
        public SquadRoster Roster { get; }
        public IReadOnlyList<MemberSpawnDefinition> Members => members.AsReadOnly();
        public event Action Changed;
        public int DeployedCount
        {
            get { var count = 0; foreach (var id in memberIds) if (id != 0) count++; return count; }
        }
        public bool CanStart => DeployedCount > 0 && DeployedCount <= MaxDeployed;
        public SquadPreparation(SquadRoster roster) : this(roster != null ? roster.CaptureMembers() : throw new ArgumentNullException(nameof(roster)))
            => Roster = roster;
        public SquadPreparation(IReadOnlyList<MemberSpawnDefinition> availableMembers)
        {
            if (availableMembers == null) throw new ArgumentNullException(nameof(availableMembers));
            members = new List<MemberSpawnDefinition>(availableMembers);
            var ids = new HashSet<int>();
            foreach (var member in members)
                if (member == null || !ids.Add(member.MemberId)) throw new ArgumentException("Warehouse members require unique IDs.");
            if (members.Count > WarehouseCapacity) throw new ArgumentException("Warehouse supports up to twelve members.");
        }
        public bool TryAddRewardMember(UnitRole role)
        {
            if (role == UnitRole.Test || !Enum.IsDefined(typeof(UnitRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (!CanAddMember) return false;
            var id = 0;
            foreach (var member in members) id = Math.Max(id, member.MemberId);
            var reward = new MemberSpawnDefinition(checked(id + 1), 4, SquadCombatRules.Create(role));
            members.Add(reward);
            Changed?.Invoke();
            return true;
        }
        public int MemberAtSlot(int slot) => slot >= 0 && slot < 9 ? memberIds[slot] : 0;
        public UnitRole RoleAtSlot(int slot) => FindMember(MemberAtSlot(slot))?.Unit.Role ?? UnitRole.Test;
        public bool IsDeployed(int memberId) => memberId > 0 && Array.IndexOf(memberIds, memberId) >= 0;
        public MemberSpawnDefinition FindMember(int memberId)
        {
            foreach (var member in members) if (member.MemberId == memberId) return member;
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
                if (member != null) result.Add(new MemberSpawnDefinition(member.MemberId, slot, member.Unit, member.SpawnTime));
            }
            return result.AsReadOnly();
        }
    }
}
