using System;
using Team5.Rebuild.Presentation;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Editor
{
    // Editor menu for PaintSquadScreenCapture: in Play Mode, saves the current frame (overlay UI included)
    // to Logs/PaintSquadScreens/<scene>-<time>.png at a fixed size, whatever the Game view size is.
    public static class PaintSquadScreenCaptureMenu
    {
        private const string Menu = "Team5/Rebuild/Paint Squad/Capture Game View ";

        [MenuItem(Menu + "1920x1080")]
        public static void CaptureFullHd() => CaptureNow(1920, 1080);

        [MenuItem(Menu + "960x600")]
        public static void CaptureSmall() => CaptureNow(960, 600);

        [MenuItem(Menu + "1920x1080", true)]
        [MenuItem(Menu + "960x600", true)]
        private static bool CanCapture() => EditorApplication.isPlaying;

        public static string CaptureNow(int width, int height)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var path = PaintSquadScreenCapture.Capture($"{scene}-{DateTime.Now:HHmmss}-{width}x{height}", width, height);
            Debug.Log($"Capturing to {path}");
            return path;
        }
    }
}
