using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Sprites;

namespace Team5.Rebuild.Editor
{
    public static class FWHairMaskBuilder
    {
        private const string MaskPath = "Assets/Rebuild/FWTest/FWFrontHairMask.png";
        [MenuItem("Team5/Rebuild/FW Visual/Build front hair mask")]
        public static void Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/FWTest/FWUnitVisual_WorldZ.prefab");
            var face = prefab.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name == "F_FACE").sprite;
            var source = face.texture;
            var uv = DataUtility.GetOuterUV(face);
            var width = source.width; var height = source.height;
            var render = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var previousSrgb = GL.sRGBWrite;
            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var mask = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, render);
                RenderTexture.active = render;
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0); readable.Apply();
                var pixels = readable.GetPixels32();
                var output = new Color32[pixels.Length];
                var visited = new bool[pixels.Length];
                var queue = new Queue<int>();
                var left = Mathf.FloorToInt(uv.x * width); var right = Mathf.CeilToInt(uv.z * width);
                var bottom = Mathf.FloorToInt(uv.y * height); var top = Mathf.CeilToInt(uv.w * height);
                Func<int, bool> candidate = index =>
                {
                    var p = pixels[index];
                    // Brown hair, excluding bright skin and violet eyes. Connectivity from the
                    // top of the head prevents isolated eyebrows/mouth details becoming hair.
                    return p.a > 5 && p.r > 12 && p.r < 228 && p.r > p.g * 1.08f && p.g >= p.b * .88f;
                };
                for (var y = bottom + (top - bottom) * 2 / 3; y < top; y++)
                    for (var x = left; x < right; x++)
                    { var i = y * width + x; if (!candidate(i)) continue; visited[i] = true; queue.Enqueue(i); }
                var count = 0;
                while (queue.Count > 0)
                {
                    var i = queue.Dequeue(); output[i] = new Color32(255, 255, 255, 255); count++;
                    var x = i % width; var y = i / width;
                    foreach (var neighbour in new[] { i - 1, i + 1, i - width, i + width })
                    {
                        var nx = neighbour % width; var ny = neighbour / width;
                        if (nx < left || nx >= right || ny < bottom || ny >= top || Math.Abs(nx - x) + Math.Abs(ny - y) != 1 || visited[neighbour] || !candidate(neighbour)) continue;
                        visited[neighbour] = true; queue.Enqueue(neighbour);
                    }
                }
                if (count < 100) throw new InvalidOperationException("Front hair mask could not find the hair region.");
                mask.SetPixels32(output); mask.Apply(); File.WriteAllBytes(MaskPath, mask.EncodeToPNG());
                AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(MaskPath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false; importer.mipmapEnabled = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var imported = AssetDatabase.LoadAllAssetsAtPath(MaskPath).OfType<Texture2D>().FirstOrDefault();
                if (imported == null) throw new InvalidOperationException("Hair mask texture did not load after import.");
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/FWTest/FWWorldZSprite.mat");
                material.SetTexture("_FWHairMask", imported);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
                Debug.Log("FW front hair mask built: " + count + " connected hair pixels; original PSB files unchanged.");
            }
            finally
            {
                RenderTexture.active = previous; GL.sRGBWrite = previousSrgb;
                RenderTexture.ReleaseTemporary(render);
                UnityEngine.Object.DestroyImmediate(readable); UnityEngine.Object.DestroyImmediate(mask);
            }
        }
    }
}
