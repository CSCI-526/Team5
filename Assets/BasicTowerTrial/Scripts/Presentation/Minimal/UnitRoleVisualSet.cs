using System;
using System.Collections.Generic;
using Team5.BasicTowerTrial.Simulation;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation
{
    [Serializable]
    public sealed class RoleVisualEntry
    {
        public UnitRole role;
        public UnitView prefab;
    }

    [CreateAssetMenu(menuName = "Team5/Unit role visuals")]
    public sealed class UnitRoleVisualSet : ScriptableObject
    {
        [SerializeField] private List<RoleVisualEntry> entries = new List<RoleVisualEntry>();
        public IReadOnlyList<RoleVisualEntry> Entries => entries;
        public bool TryGet(UnitRole role, out UnitView prefab)
        {
            foreach (var entry in entries)
                if (entry != null && entry.role == role && entry.prefab != null)
                { prefab = entry.prefab; return true; }
            prefab = null; return false;
        }
    }
}
