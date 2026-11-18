// Dev tool only: excluded from release player builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Team5.Rebuild.Presentation
{
    // Saves a PNG of the current Play Mode frame at any resolution, including Screen Space Overlay canvases,
    // independent of the Game view size (works in -batchmode with a graphics device). Used by the Paint Squad
    // screenshot tours (Tests/PlayMode/PaintSquadScreenTour.cs) and the editor menu
    // Team5/Rebuild/Paint Squad/Capture Game View.
    //
    // How: every base camera without its own target renders into one RenderTexture. Overlay canvases switch for
    // the capture to Screen Space Camera on a private camera (so their CanvasScaler lays them out for the capture
    // size), which renders them into a second texture over black and over white; that recovers the exact UI alpha
    // to blend over the scene. Screen-space HUD code that reads Camera.main pixel sizes (health bars) sees the
    // capture size for the frames in between. Everything is restored afterwards.
    public static class PaintSquadScreenCapture
    {
        public const string DefaultFolder = "Logs/PaintSquadScreens";
        private const int CaptureLayer = 31;

        /// <summary>Folder for captures: Logs/PaintSquadScreens under the project root (gitignored).</summary>
        public static string Folder => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", DefaultFolder);

        /// <summary>Full path of a capture file name (adds .png when missing).</summary>
        public static string PathFor(string fileName)
        {
            if (Path.IsPathRooted(fileName)) return fileName;
            if (!fileName.EndsWith(".png")) fileName += ".png";
            return Path.Combine(Folder, fileName);
        }

        /// <summary>Starts a capture from anywhere in Play Mode (fire and forget). Returns the target path.</summary>
        public static string Capture(string fileName, int width = 1920, int height = 1080)
        {
            var path = PathFor(fileName);
            var runner = new GameObject("Paint Squad Screen Capture").AddComponent<CaptureRunner>();
            Object.DontDestroyOnLoad(runner.gameObject);
            runner.StartCoroutine(runner.Run(CaptureRoutine(path, width, height)));
            return path;
        }

        /// <summary>Yieldable capture for Play Mode tests. Takes three frames.</summary>
        public static IEnumerator CaptureRoutine(string fileName, int width = 1920, int height = 1080)
        {
            var path = PathFor(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Paint Squad Capture" };
            target.Create();

            // Base cameras that draw to the screen now draw into the capture texture, lowest depth first.
            var cameras = new List<Camera>();
            foreach (var camera in Camera.allCameras)
            {
                if (camera.targetTexture != null || !camera.enabled) continue;
                var data = camera.GetUniversalAdditionalCameraData();
                if (data != null && data.renderType != CameraRenderType.Base) continue;
                cameras.Add(camera);
            }
            cameras.Sort((a, b) => a.depth.CompareTo(b.depth));
            GameObject fallbackCamera = null;
            if (cameras.Count == 0)
            {
                fallbackCamera = new GameObject("Paint Squad Capture Base Camera");
                var camera = fallbackCamera.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 0;
                cameras.Add(camera);
            }
            foreach (var camera in cameras) camera.targetTexture = target;

            // Overlay canvases render through a private camera into their own texture, once over black and once
            // over white: that recovers the exact UI alpha, which is then blended over the scene on the CPU.
            var uiTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Paint Squad Capture UI" };
            uiTarget.Create();
            var uiCameraObject = new GameObject("Paint Squad Capture UI Camera");
            uiCameraObject.transform.position = new Vector3(0f, -5000f, 0f);
            var uiCamera = uiCameraObject.AddComponent<Camera>();
            uiCamera.enabled = false;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.cullingMask = 1 << CaptureLayer;
            uiCamera.orthographic = true;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 200f;
            uiCamera.targetTexture = uiTarget;
            var uiData = uiCamera.GetUniversalAdditionalCameraData();
            uiData.renderPostProcessing = false;
            uiData.antialiasing = AntialiasingMode.None;
            var culling = new List<(Camera camera, int mask)>();
            foreach (var camera in cameras) { culling.Add((camera, camera.cullingMask)); camera.cullingMask &= ~(1 << CaptureLayer); }

            var canvases = new List<(Canvas canvas, Dictionary<Transform, int> layers)>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.isActiveAndEnabled) continue;
                var layers = new Dictionary<Transform, int>();
                foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) { layers[child] = child.gameObject.layer; child.gameObject.layer = CaptureLayer; }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 100f;
                canvases.Add((canvas, layers));
            }

            try
            {
                // Frames for CanvasScaler, layout and LateUpdate HUD placement at the capture size. The scene
                // cameras render into the texture each frame; after the last yield it holds a complete frame.
                for (var i = 0; i < 3; i++) yield return null;
                Canvas.ForceUpdateCanvases();
                var scene = Read(target, width, height);
                uiCamera.backgroundColor = new Color(0f, 0f, 0f, 1f);
                uiCamera.Render();
                var overBlack = Read(uiTarget, width, height);
                uiCamera.backgroundColor = new Color(1f, 1f, 1f, 1f);
                uiCamera.Render();
                var overWhite = Read(uiTarget, width, height);

                var pixels = scene.GetPixels32();
                Composite(pixels, overBlack.GetPixels32(), overWhite.GetPixels32());
                scene.SetPixels32(pixels);
                File.WriteAllBytes(path, scene.EncodeToPNG());
                Object.Destroy(scene); Object.Destroy(overBlack); Object.Destroy(overWhite);
                Debug.Log($"Paint Squad capture saved: {path} ({width}x{height})");
            }
            finally
            {
                foreach (var (canvas, layers) in canvases)
                {
                    if (canvas == null) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                    foreach (var pair in layers) if (pair.Key != null) pair.Key.gameObject.layer = pair.Value;
                }
                foreach (var (camera, mask) in culling) if (camera != null) { camera.cullingMask = mask; camera.targetTexture = null; }
                Object.Destroy(uiCameraObject);
                if (fallbackCamera != null) Object.Destroy(fallbackCamera);
                target.Release(); Object.Destroy(target);
                uiTarget.Release(); Object.Destroy(uiTarget);
            }
            yield return null;
        }

        private static Texture2D Read(RenderTexture source, int width, int height)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        // UI over black gives premultiplied color, the white minus black difference gives (1 - alpha), both in
        // linear space (the textures store sRGB). Result = ui + (1 - alpha) * scene.
        private static void Composite(Color32[] scene, Color32[] overBlack, Color32[] overWhite)
        {
            var linear = new float[256];
            for (var i = 0; i < 256; i++) linear[i] = QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.GammaToLinearSpace(i / 255f) : i / 255f;
            var encode = new byte[4097];
            for (var i = 0; i <= 4096; i++)
                encode[i] = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.LinearToGammaSpace(i / 4096f) : i / 4096f));
            for (var p = 0; p < scene.Length; p++)
            {
                var b = overBlack[p];
                var w = overWhite[p];
                var s = scene[p];
                var keep = Mathf.Clamp01((linear[w.r] - linear[b.r] + linear[w.g] - linear[b.g] + linear[w.b] - linear[b.b]) / 3f);
                scene[p] = new Color32(
                    encode[(int)(Mathf.Clamp01(linear[b.r] + keep * linear[s.r]) * 4096f)],
                    encode[(int)(Mathf.Clamp01(linear[b.g] + keep * linear[s.g]) * 4096f)],
                    encode[(int)(Mathf.Clamp01(linear[b.b] + keep * linear[s.b]) * 4096f)],
                    255);
            }
        }

        private sealed class CaptureRunner : MonoBehaviour
        {
            public IEnumerator Run(IEnumerator routine)
            {
                yield return routine;
                Destroy(gameObject);
            }
        }
    }
}
#endif
