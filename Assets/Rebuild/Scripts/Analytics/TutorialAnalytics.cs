using UnityEngine;

namespace Team5.Rebuild.Analytics
{
    // LevelOneTutorial re-shows the same banner often; only step changes are reported.
    public static class TutorialAnalytics
    {
        private static int step;
        private static float shownAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset() => step = 0;

        public static void Step(int value, int total, string heading)
        {
            if (!GameAnalytics.IsActive || value == step) return;
            var now = Time.realtimeSinceStartup;
            GameAnalytics.TrackBattle("tutorial_step", ("step", value), ("total", total), ("heading", heading),
                ("prev_step", step), ("seconds_on_prev_step", step > 0 ? now - shownAt : 0));
            step = value;
            shownAt = now;
        }

        public static void Skip(int at) =>
            GameAnalytics.TrackBattle("tutorial_skip", ("step", at), ("seconds_on_step", step > 0 ? Time.realtimeSinceStartup - shownAt : 0));

        public static void Complete() =>
            GameAnalytics.TrackBattle("tutorial_complete", ("step", step), ("seconds_on_step", step > 0 ? Time.realtimeSinceStartup - shownAt : 0));
    }
}
