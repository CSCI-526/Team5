using Team5.BasicTowerTrial.Simulation;
using UnityEngine;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed class MusterZombieCatalog : ScriptableObject
    {
        [System.Serializable]
        private struct Entry
        {
            public UnitRole role;
            public GameObject model;
            public Sprite portrait;
            [TextArea] public string specialEffect;
            [TextArea] public string upgradedSpecialEffect;
        }

        [SerializeField] private Entry[] entries;
        private static MusterZombieCatalog loaded;

        internal static bool TryGet(UnitRole role, out GameObject model, out Sprite portrait)
        {
            var entry = Find(role);
            model = entry?.model;
            portrait = entry?.portrait;
            return entry.HasValue;
        }

        internal static string SpecialEffect(UnitRole role, bool upgraded)
        {
            var entry = Find(role);
            var upgradedEffect = entry?.upgradedSpecialEffect;
            return upgraded && !string.IsNullOrEmpty(upgradedEffect) ? upgradedEffect : entry?.specialEffect ?? string.Empty;
        }

        private static Entry? Find(UnitRole role)
        {
            if (!MusterZombieRules.IsZombie(role)) return null;
            if (loaded == null) loaded = Resources.Load<MusterZombieCatalog>("MusterZombieCatalog");
            if (loaded == null) return null;
            foreach (var entry in loaded.entries)
                if (entry.role == role) return entry;
            return null;
        }
    }
}
