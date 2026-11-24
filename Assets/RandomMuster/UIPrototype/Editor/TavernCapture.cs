using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RandomMuster.Prototype
{
    internal static class TavernCapture
    {
        private const string ScenePath = "Assets/RandomMuster/UIPrototype/CourierGuildTavern.unity";

        public static void Render()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var camera = Object.FindFirstObjectByType<Camera>();
            if (canvas == null || camera == null || canvas.transform.childCount != 8)
                throw new System.InvalidOperationException("The prototype needs a camera and all eight modules.");
            foreach (var image in canvas.GetComponentsInChildren<RawImage>())
                if (image.name != "Backdrop" && image.texture == null &&
                    image.name != "Table drop shadow" && image.name != "Brass inlay")
                    throw new System.InvalidOperationException("Missing texture on " + image.name);

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            const string output = "Logs/TacticalAssault/CourierGuild";
            Directory.CreateDirectory(output);
            Capture(camera, canvas, 800, 533, output);
            Capture(camera, canvas, 1600, 900, output);
            Debug.Log("Courier Guild artwork captured from " + scene.path);
        }

        private static void Capture(Camera camera, Canvas canvas, int width, int height, string output)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            var resolved = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                canvas.scaleFactor = height / 533f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Graphics.Blit(target, resolved);
                RenderTexture.active = resolved;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, $"CourierGuild-{width}x{height}.png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(resolved);
                Object.DestroyImmediate(image);
            }
        }
    }
}
