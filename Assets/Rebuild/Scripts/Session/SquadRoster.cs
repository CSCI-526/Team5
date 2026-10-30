using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Session
{
    [CreateAssetMenu(menuName = "Team5/Rebuild/Squad Roster")]
    public sealed class SquadRoster : ScriptableObject
    {
        [SerializeField] private List<SquadMemberConfiguration> members = new List<SquadMemberConfiguration>();
        public IReadOnlyList<SquadMemberConfiguration> Members => members.AsReadOnly();
        public IReadOnlyList<MemberSpawnDefinition> CaptureMembers()
        {
            var result = new List<MemberSpawnDefinition>();
            foreach (var member in members)
                if (member != null && member.enabled)
                    result.Add(new MemberSpawnDefinition(member.memberId, member.slotId, SquadCombatRules.Create(member.role), member.spawnTime));
            return result.AsReadOnly();
        }
    }
}
