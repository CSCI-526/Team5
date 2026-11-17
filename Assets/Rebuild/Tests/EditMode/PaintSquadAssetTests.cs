using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    /// <summary>Checks that the Paint Squad art ported from Paint Squad Wars imports cleanly in Team5.</summary>
    public sealed class PaintSquadAssetTests
    {
        const string Root = "Assets/Rebuild/PaintSquad/";

        static readonly string[] CharacterModels = { "Soldier", "SoldierHeavy", "Hero" };
        static readonly string[] CharacterTakes = { "Idle", "Run", "Shoot", "Hit", "Die", "Cheer" };

        static readonly string[] EnvironmentModels =
        {
            "Tree_A", "Tree_B", "Tree_C", "Bush_A", "Bush_B", "Rock_A", "Rock_B", "Rock_C",
            "GrassTuft", "Flower_A", "Flower_B", "Fence", "Crate", "Barrel", "Log", "Tent",
        };

        static readonly string[] Shaders =
        {
            "TWToon.shader", "TWToonOutline.shader", "TWDecal.shader", "TWParticle.shader",
        };

        static readonly string[] ShaderNames = { "TW/Toon", "TW/ToonOutline", "TW/Decal", "TW/Particle" };

        static readonly string[] FontAssets = { "Fredoka SDF.asset", "LilitaOne SDF.asset" };

        static readonly string[] VfxPrefabs = { "PaintSquadPaintBall", "PaintSquadMuzzleFlash", "PaintSquadHitSplash" };

        [TestCaseSource(nameof(CharacterModels))]
        public void CharacterModelLoadsWithAllTakesAndResolvedMaterials(string name)
        {
            var path = Root + "Models/Characters/" + name + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, path);
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).Select(c => c.name).ToArray();
            Assert.That(clips, Is.SupersetOf(CharacterTakes), path);
            Assert.That(model.GetComponentInChildren<SkinnedMeshRenderer>(true), Is.Not.Null, path);
            AssertRenderersUsePortedMaterials(model, path);
        }

        [TestCaseSource(nameof(CharacterModels))]
        public void AnimatorControllerAndUpperMaskLoadAndUseTheirModelClips(string name)
        {
            var path = Root + "Animations/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            Assert.That(controller, Is.Not.Null, path);
            var modelPath = Root + "Models/Characters/" + name + ".fbx";
            Assert.That(controller.animationClips, Is.Not.Empty, path);
            foreach (var clip in controller.animationClips)
            {
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(AssetDatabase.GetAssetPath(clip), Is.EqualTo(modelPath), clip.name);
            }
            Assert.That(controller.animationClips.Select(c => c.name).Distinct(), Is.EquivalentTo(CharacterTakes), path);
            var maskPath = Root + "Animations/" + name + "_Upper.mask";
            Assert.That(AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath), Is.Not.Null, maskPath);
        }

        [TestCaseSource(nameof(EnvironmentModels))]
        public void EnvironmentModelLoadsWithResolvedMaterials(string name)
        {
            var path = Root + "Models/Environment/" + name + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null, path);
            Assert.That(model.GetComponentInChildren<MeshRenderer>(true), Is.Not.Null, path);
            AssertRenderersUsePortedMaterials(model, path);
        }

        [Test]
        public void ShadersLoadAndCompileForUrp()
        {
            for (var i = 0; i < Shaders.Length; i++)
            {
                var path = Root + "Shaders/" + Shaders[i];
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Assert.That(shader, Is.Not.Null, path);
                Assert.That(shader.name, Is.EqualTo(ShaderNames[i]), path);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, path);
                Assert.That(shader.isSupported, Is.True, path);
                Assert.That(Shader.Find(ShaderNames[i]), Is.SameAs(shader), path);
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<Object>(Root + "Shaders/TWToonCommon.hlsl"), Is.Not.Null);
        }

        [Test]
        public void EveryPortedMaterialUsesAPortedOrTextMeshProShader()
        {
            // Only .mat files: a TTF also reports its built-in font material as a Material sub-asset.
            var paths = AssetDatabase.FindAssets("t:Material", new[] { Root.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".mat")).Distinct().ToArray();
            // Role materials generated under RoleVisuals are covered by PaintSquadRoleVisualTests.
            Assert.That(paths.Count(p => !p.StartsWith(Root + "RoleVisuals/")), Is.EqualTo(38));
            foreach (var path in paths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null, path);
                Assert.That(material.shader, Is.Not.Null, path);
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, path);
                var shaderPath = AssetDatabase.GetAssetPath(material.shader);
                Assert.That(shaderPath.StartsWith(Root + "Shaders/") || shaderPath.StartsWith("Assets/TextMesh Pro/Shaders/"),
                    Is.True, path + " uses " + shaderPath);
            }
        }

        [TestCaseSource(nameof(FontAssets))]
        public void FontAssetLoadsWithSourceFontAndMaterial(string name)
        {
            var path = Root + "Fonts/" + name;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Assert.That(font, Is.Not.Null, path);
            Assert.That(font.material, Is.Not.Null, path);
            Assert.That(font.material.shader, Is.Not.Null, path);
            Assert.That(ShaderUtil.ShaderHasError(font.material.shader), Is.False, path);
            Assert.That(font.atlasTexture, Is.Not.Null, path);
            Assert.That(AssetDatabase.GetAssetPath(font.sourceFontFile), Does.StartWith(Root + "Fonts/"), path);
        }

        [Test]
        public void FontOutlinePresetsAndLicencesArePresent()
        {
            foreach (var name in new[] { "Fredoka SDF Outline.mat", "LilitaOne SDF Outline.mat", "LilitaOne SDF Outline Thin.mat" })
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(Root + "Fonts/" + name), Is.Not.Null, name);
            foreach (var name in new[] { "OFL_Fredoka.txt", "OFL_LilitaOne.txt", "LICENSES.md" })
                Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Fonts/" + name), Is.Not.Null, name);
        }

        [Test]
        public void UiShapesAndIconsImportAsSprites()
        {
            foreach (var folder in new[] { "UI/Shapes", "UI/Icons" })
            {
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root + folder });
                Assert.That(guids, Is.Not.Empty, folder);
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path), Is.Not.Null, path);
                }
            }
        }

        [TestCaseSource(nameof(VfxPrefabs))]
        public void VfxPrefabLoadsWithoutScriptsAndWithPortedMaterials(string name)
        {
            var path = Root + "VFX/" + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), Is.Zero, t.name);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, path);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, path);
            AssertRenderersUsePortedMaterials(prefab, path);
        }

        static void AssertRenderersUsePortedMaterials(GameObject root, string context)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.sharedMaterials, Is.Not.Empty, context + "/" + renderer.name);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, context + "/" + renderer.name);
                    Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith(Root + "Materials/"),
                        context + "/" + renderer.name + " uses " + material.name);
                    Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, material.name);
                }
                if (renderer is MeshRenderer)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    Assert.That(filter != null && filter.sharedMesh != null, Is.True, context + "/" + renderer.name + " mesh");
                }
                if (renderer is SkinnedMeshRenderer skinned)
                    Assert.That(skinned.sharedMesh, Is.Not.Null, context + "/" + renderer.name + " mesh");
            }
        }
    }
}
