using Team5.Rebuild.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class PlaybackSpeedMemory
    {
        private static int chosen = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            chosen = 1;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            BattleSceneFlow.RunStarted -= OnRunStarted;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            BattleSceneFlow.RunStarted -= OnRunStarted;
            BattleSceneFlow.RunStarted += OnRunStarted;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyToFirstScene() => Apply(SceneManager.GetActiveScene());

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

        private static void OnRunStarted() => chosen = 1;

        private static void Apply(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || BattleSceneFlow.IsPreviewScene(scene)) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var hud in root.GetComponentsInChildren<LegacyBattleHud>(true))
                {
                    var session = hud.Session;
                    var slider = hud.SpeedSlider;
                    if (session == null || slider == null) continue;
                    session.SetPlaybackSpeed(chosen);
                    slider.SetValueWithoutNotify(chosen - 1);
                    slider.onValueChanged.RemoveListener(Remember);
                    slider.onValueChanged.AddListener(Remember);
                }
        }

        private static void Remember(float value) => chosen = Mathf.Clamp(Mathf.RoundToInt(value) + 1, 1, 3);
    }
}
