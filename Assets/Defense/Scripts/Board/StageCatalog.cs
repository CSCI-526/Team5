using System.Collections.Generic;
using UnityEngine;

namespace ReverseTD.Defense
{
    public class StageCatalog : ScriptableObject
    {
        public const string ResourceName = "StageCatalog";

        [SerializeField, Tooltip("Each enabled Build Settings scene whose board builds a stage. Written by Tools > RK > Build Stage Catalog, which also runs on its own.")]
        private StageCatalogEntry[] entries = new StageCatalogEntry[0];

        public IReadOnlyList<StageCatalogEntry> Entries => entries;

        public static StageLayout Find(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return null;
            }

            var catalog = Resources.Load<StageCatalog>(ResourceName);
            if (catalog == null)
            {
                return null;
            }

            for (int i = 0; i < catalog.entries.Length; i++)
            {
                if (catalog.entries[i].SceneName == sceneName)
                {
                    return catalog.entries[i].Layout;
                }
            }
            return null;
        }
    }
}
