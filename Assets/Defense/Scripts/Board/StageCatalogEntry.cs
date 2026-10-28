using System;
using UnityEngine;

namespace ReverseTD.Defense
{
    [Serializable]
    public struct StageCatalogEntry
    {
        [SerializeField, Tooltip("The scene's name, as SceneManager.LoadScene takes it.")]
        private string sceneName;

        [SerializeField, Tooltip("The stage that the scene's board builds.")]
        private StageLayout layout;

        public string SceneName => sceneName;
        public StageLayout Layout => layout;
    }
}
