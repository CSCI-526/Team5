using System;
using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    [Serializable]
    public sealed class RolePortrait
    {
        public UnitRole role;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class CharacterSetEntry
    {
        public CharacterSet set;
        public string displayName;
        public string tagline;
        public UnitRoleVisualSet roleVisuals;
        [Tooltip("Captain preview shown on the Choose your squad card.")]
        public Sprite captainPreview;
        [Tooltip("Role portraits for unit icons. Empty keeps the legacy block icons.")]
        public List<RolePortrait> portraits = new List<RolePortrait>();

        public bool TryGetPortrait(UnitRole role, out Sprite sprite)
        {
            if (role == UnitRole.Test) role = UnitRole.Attack;
            foreach (var portrait in portraits)
                if (portrait != null && portrait.role == role && portrait.sprite != null)
                { sprite = portrait.sprite; return true; }
            sprite = null;
            return false;
        }
    }

    // Resources/CharacterSetCatalogs.asset: one entry per selectable squad look. Built by
    // Team5/Rebuild/Paint Squad/Build character set catalogs.
    public sealed class CharacterSetCatalogs : ScriptableObject
    {
        public const string ResourceName = "CharacterSetCatalogs";
        [SerializeField] private List<CharacterSetEntry> entries = new List<CharacterSetEntry>();
        public IReadOnlyList<CharacterSetEntry> Entries => entries;

        private static CharacterSetCatalogs loaded;
        private static bool attempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { loaded = null; attempted = false; }

        public static CharacterSetCatalogs Instance
        {
            get
            {
                if (loaded == null && !attempted)
                {
                    attempted = true;
                    loaded = Resources.Load<CharacterSetCatalogs>(ResourceName);
                }
                return loaded;
            }
        }

        public bool TryGet(CharacterSet set, out CharacterSetEntry entry)
        {
            foreach (var candidate in entries)
                if (candidate != null && candidate.set == set && candidate.roleVisuals != null)
                { entry = candidate; return true; }
            entry = null;
            return false;
        }

        public bool Contains(UnitRoleVisualSet visuals)
        {
            if (visuals == null) return false;
            foreach (var candidate in entries)
                if (candidate != null && candidate.roleVisuals == visuals) return true;
            return false;
        }

        public static bool TryGetSelected(out CharacterSetEntry entry)
        {
            var catalogs = Instance;
            if (catalogs != null && catalogs.TryGet(CharacterSetSelection.Current, out entry)) return true;
            entry = null;
            return false;
        }

        // The role catalog a battle scene should use. Only scenes authored with one of the selectable
        // catalogs are switched; debug scenes with no catalog or another catalog keep what they serialize.
        public static UnitRoleVisualSet Resolve(UnitRoleVisualSet sceneVisuals)
        {
            var catalogs = Instance;
            if (catalogs == null || !catalogs.Contains(sceneVisuals)) return sceneVisuals;
            return catalogs.TryGet(CharacterSetSelection.Current, out var entry) ? entry.roleVisuals : sceneVisuals;
        }

        public static bool TryGetSelectedPortrait(UnitRole role, out Sprite sprite)
        {
            if (TryGetSelected(out var entry)) return entry.TryGetPortrait(role, out sprite);
            sprite = null;
            return false;
        }
    }
}
