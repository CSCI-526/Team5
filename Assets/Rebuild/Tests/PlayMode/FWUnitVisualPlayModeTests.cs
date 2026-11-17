#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Team5.Rebuild.Tests
{
    public sealed class FWUnitVisualPlayModeTests
    {
        // These scenes are authored for Catgirl; pin that set so the player's stored choice cannot change them.
        private CharacterSetPrefsScope characterSet;
        [SetUp] public void PinCatgirlSet() => characterSet = new CharacterSetPrefsScope(CharacterSet.Catgirl);
        [TearDown] public void RestoreCharacterSet() => characterSet?.Dispose();
        private Scene initialScene;
        [SetUp]
        public void Begin()
        {
            initialScene = SceneManager.GetActiveScene();
            Directory.CreateDirectory("Logs/FWVisual");
            File.AppendAllText("Logs/FWVisual/Stages.log", TestContext.CurrentContext.Test.Name + " starting\n");
            Application.runInBackground = true;
        }

        [UnityTest]
        public IEnumerator URPSpritesRenderAndAnOpaque3DWallOccludesThem()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Rendering requires a graphics device; run without -nographics.");
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/FWTest/FWUnitVisual.prefab"));
            root.GetComponent<UnitView>().enabled = false; // Standalone render fixture has no simulation binding.
            var cameraObject = new GameObject("FW render verification camera");
            var camera = cameraObject.AddComponent<Camera>();
            var texture = new RenderTexture(384, 384, 24);
            GameObject wall = null;
            Material wallMaterial = null;
            try
            {
                // Dedicated off-map render fixture. The game's Camera and map are untouched.
                root.transform.position = new Vector3(1000, 0, 1000);
                camera.transform.position = root.transform.position + new Vector3(0, 2.5f, -5);
                camera.transform.LookAt(root.transform.position + Vector3.up * .75f);
                camera.orthographic = true;
                camera.orthographicSize = 1.1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.05f, .1f, .15f, 1);
                camera.targetTexture = texture;
                var driver = root.GetComponent<FWUnitVisual>();
                driver.SetLocomotion(Vector3.back, false, false, camera);
                yield return null; yield return null;
                var baseline = Capture(camera, texture, "FrontIdle");
                var background = baseline[0];
                var spritePixels = baseline.Select((c, i) => (c, i)).Where(p => Difference(p.c, background) > 20).Select(p => p.i).ToArray();
                Assert.That(spritePixels.Length, Is.GreaterThan(500), "Character should be visibly rendered by the 3D URP renderer.");
                foreach (var away in new[] { false, true })
                {
                    driver.SetLocomotion(away ? Vector3.forward : Vector3.back, true, false, camera);
                    driver.ActiveAnimator.Play("Run", 0, .3f);
                    driver.ActiveAnimator.Update(0);
                    yield return null; yield return null;
                    Capture(camera, texture, away ? "BackRun" : "FrontRun");
                    driver.SetLocomotion(Vector3.zero, false, true, camera);
                    yield return null;
                    Capture(camera, texture, away ? "BackIdle" : "FrontIdle");
                }
                driver.SetLocomotion(Vector3.back, true, false, camera);
                driver.SetLocomotion(Vector3.back, false, true, camera);
                Assert.That(driver.FacingBack, Is.False);
                yield return null; yield return null;
                baseline = Capture(camera, texture, "FrontIdle");
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = root.transform.position + new Vector3(0, .8f, -.8f);
                wall.transform.localScale = new Vector3(4, 4, .2f);
                wallMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                wallMaterial.SetColor("_BaseColor", new Color(.2f, .65f, .3f, 1));
                wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
                yield return null;
                var blocked = Capture(camera, texture, "OpaqueWallOcclusion");
                var green = spritePixels.Count(i => blocked[i].g > blocked[i].r * 1.6f && blocked[i].g > blocked[i].b * 1.4f);
                Assert.That(green, Is.GreaterThan(spritePixels.Length * .95f), "3D wall must depth-occlude the sprite, rather than sprite drawing through it.");
            }
            finally
            {
                camera.targetTexture = null;
                Object.Destroy(root); Object.Destroy(cameraObject);
                if (wall != null) Object.Destroy(wall);
                if (wallMaterial != null) Object.Destroy(wallMaterial);
                texture.Release(); Object.Destroy(texture);
            }
        }

        private static int Difference(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        [UnityTest]
        public IEnumerator WorldZRunningChangesRenderedPoseWhileUpdatingSortingEveryFrame()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/FWTest/FWUnitVisual_WorldZ.prefab"));
            root.GetComponent<UnitView>().enabled = false;
            var cameraObject = new GameObject("World Z animation regression camera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(384, 384, 24);
            try
            {
                camera.transform.position = new Vector3(0, 2.5f, -5);
                camera.transform.LookAt(Vector3.up * .75f);
                camera.orthographic = true; camera.orthographicSize = 1.1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.05f, .1f, .15f, 1);
                camera.targetTexture = target;
                var driver = root.GetComponent<FWUnitVisual>();
                foreach (var phase in new[] { .1f, .55f })
                {
                    driver.SetLocomotion(Vector3.back, true, false, camera);
                    driver.ActiveAnimator.Play("Run", 0, phase);
                    driver.ActiveAnimator.Update(0);
                    driver.ActiveAnimator.speed = 0;
                    for (var frame = 0; frame < 3; frame++)
                    {
                        driver.SetLocomotion(Vector3.back, true, false, camera, 0);
                        yield return null;
                    }
                    var pixels = Capture(camera, target, "WorldZRunPose" + (phase < .2f ? "A" : "B"));
                    if (phase < .2f) firstRunPose = pixels;
                    else
                    {
                        var changes = pixels.Where((c, i) => Difference(c, firstRunPose[i]) > 25).Count();
                        Assert.That(changes, Is.GreaterThan(1500), "Run must deform the final sprite image, not only animate bone transforms.");
                    }
                }
            }
            finally
            {
                firstRunPose = null;
                camera.targetTexture = null;
                Object.Destroy(root); Object.Destroy(cameraObject); target.Release(); Object.Destroy(target);
            }
        }
        private Color32[] firstRunPose;

        [UnityTest]
        public IEnumerator SmallerHeadsKeepBothHairRigsAttachedAndPersistThroughIdleRun()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/FWTest/FWUnitVisual_WorldZ.prefab"));
            root.GetComponent<UnitView>().enabled = false;
            try
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                Assert.That(root.transform.Find("Camera Facing Visual/Mirror Entire Character").localScale, Is.EqualTo(Vector3.one * .8f));
                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                {
                    animator.gameObject.SetActive(true);
                    var front = animator.name == "miho_forward";
                    var head = transforms.Single(t => t.name == (front ? "F_HEAD_bone_4" : "B_HEAD_bone_28"));
                    var body = transforms.Single(t => t.name == (front ? "F_B_bone_1" : "B_BODY_bone_25"));
                    var hair = transforms.Single(t => t.name == (front ? "F_HAIR_bone_15" : "B_HAIR_bone_39"));
                    Assert.That(hair.IsChildOf(head), Is.True, "Hair bones must inherit the same head scale.");
                    var bodyScale = body.localScale;
                    foreach (var state in new[] { "Idle", "Run", "Idle" })
                    {
                        animator.SetBool("IsMoving", state == "Run");
                        animator.Play(state, 0, .3f); animator.Update(0);
                        yield return null;
                        Assert.That(Vector3.Distance(head.localScale, Vector3.one * .7f), Is.LessThan(.0001f));
                        Assert.That(body.localScale, Is.EqualTo(bodyScale), "The torso must retain its original scale.");
                    }
                    animator.gameObject.SetActive(false);
                }
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator DisplayTiltChangesOnlyVisualPlaneAndCanReturnToCameraFacing()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/FWTest/FWUnitVisual_WorldZ.prefab"));
            root.GetComponent<UnitView>().enabled = false;
            var cameraObject = new GameObject("Display tilt test camera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(384, 384, 24);
            try
            {
                root.transform.position = new Vector3(1000, 0, 1000);
                camera.transform.position = root.transform.position + new Vector3(0, 5, -2);
                camera.transform.LookAt(root.transform.position + Vector3.up * .75f);
                camera.orthographic = true; camera.orthographicSize = 1.1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.05f, .1f, .15f, 1); camera.targetTexture = target;
                var driver = root.GetComponent<FWUnitVisual>();
                var visual = root.transform.Find("Camera Facing Visual");
                var position = root.transform.position;
                var cameraPosition = camera.transform.position;
                var cameraRotation = camera.transform.rotation;
                Assert.That(driver.DisplayTiltDegrees, Is.EqualTo(30));
                foreach (var tilt in new[] { 0f, 15f, 30f })
                {
                    driver.DisplayTiltDegrees = tilt;
                    driver.SetLocomotion(Vector3.zero, false, true, camera);
                    yield return null; yield return null;
                    Assert.That(Quaternion.Angle(cameraRotation, visual.rotation), Is.EqualTo(tilt).Within(.01f));
                    Assert.That(Vector3.Angle(visual.up, Vector3.up), Is.EqualTo(Vector3.Angle(camera.transform.up, Vector3.up) - tilt).Within(.01f));
                    Assert.That(root.transform.position, Is.EqualTo(position));
                    Assert.That(visual.TransformPoint(Vector3.zero), Is.EqualTo(position));
                    Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
                    Assert.That(camera.transform.rotation, Is.EqualTo(cameraRotation));
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                        Capture(camera, target, "DisplayTilt" + tilt);
                }
            }
            finally
            {
                camera.targetTexture = null; Object.Destroy(root); Object.Destroy(cameraObject);
                target.Release(); Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator Scene1SlotExchangesPreserveFacingAndNormalTravelStillTurns()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/1.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; yield return null; yield return null;
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/1.unity"));
            var presentation = Object.FindFirstObjectByType<BattlePresentation>();
            var sim = presentation.Session.Simulation;
            var drivers = sim.Units.Select(u => { presentation.TryGetView(u.Id, out var v); return v.GetComponent<FWUnitVisual>(); }).ToArray();
            foreach (var driver in drivers) driver.SetLocomotion(Vector3.forward + Vector3.right, true, false, Camera.main);
            var back = drivers.Select(d => d.FacingBack).ToArray();
            var mirrors = drivers.Select(d => d.Mirrored).ToArray();
            var swap = new[] { 0, 1, 2, 4, 3, 5, 6, 7, 8 };
            Assert.That(sim.TryApplyFormation(swap), Is.True);
            var animated = false;
            var deadline = Time.unscaledTime + 6;
            while (Time.unscaledTime < deadline)
            {
                yield return null;
                animated |= drivers.Any(d => d.IsMoving);
                Assert.That(drivers.Select(d => d.FacingBack), Is.EqualTo(back), "An in-place exchange must preserve front/back facing, including the final tick.");
                Assert.That(drivers.Select(d => d.Mirrored), Is.EqualTo(mirrors), "An in-place exchange must preserve left/right facing.");
                if (!sim.IsFormationAdjusting) break;
            }
            Assert.That(sim.IsFormationAdjusting, Is.False);
            Assert.That(animated, Is.True, "Slot corrections must still animate movement.");
            deadline = Time.unscaledTime + 2;
            while (Time.unscaledTime < deadline && drivers.Any(d => !d.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle")))
                yield return null;
            Assert.That(drivers.All(d => d.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle")), Is.True);
            Assert.That(drivers.Select(d => d.FacingBack), Is.EqualTo(back));
            Assert.That(drivers.Select(d => d.Mirrored), Is.EqualTo(mirrors));

            Assert.That(sim.RequestMovement(new MovementRequest(sim.SquadReferencePosition + Vector3.back * 3)), Is.True);
            for (var frame = 0; frame < 90 && !drivers[0].IsMoving; frame++) yield return null;
            Assert.That(drivers[0].IsMoving, Is.True);
            yield return null;
            Assert.That(drivers[0].FacingBack, Is.False, "Normal travel must still turn to face its direction.");
            back = drivers.Select(d => d.FacingBack).ToArray();
            mirrors = drivers.Select(d => d.Mirrored).ToArray();
            Assert.That(sim.TryApplyFormation(swap), Is.True);
            deadline = Time.unscaledTime + 6;
            while (Time.unscaledTime < deadline)
            {
                yield return null;
                Assert.That(drivers.Select(d => d.FacingBack), Is.EqualTo(back), "Moving exchanges should face the squad's forward movement.");
                Assert.That(drivers.Select(d => d.Mirrored), Is.EqualTo(mirrors), "Sideways slot corrections must not mirror the character.");
                if (!sim.IsFormationAdjusting) break;
            }
            Assert.That(sim.IsFormationAdjusting, Is.False);
        }

        [UnityTest]
        public IEnumerator Scene1RoleHairMatchesIconsWithoutTintingClothes()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/1.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; yield return null; yield return null;
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/1.unity"));
            var presentation = Object.FindFirstObjectByType<BattlePresentation>();
            presentation.Session.Simulation.SetPaused(true);
            var views = presentation.Session.Simulation.Units.Select(u => { presentation.TryGetView(u.Id, out var v); return v; }).ToArray();
            var cameraObject = new GameObject("Role hair render verification camera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(384, 384, 24);
            try
            {
                camera.orthographic = true; camera.orthographicSize = 1.1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.05f, .1f, .15f, 1); camera.targetTexture = target;
                // Keep every skin registered while inspecting different roles. Repeatedly
                // deactivating whole frozen rigs can leave stale batched skin offsets in Unity.
                for (var index = 0; index < views.Length; index++)
                {
                    views[index].enabled = false;
                    views[index].GetComponent<FWUnitVisual>().enabled = false;
                    views[index].transform.position = new Vector3(1000 + index * 10, 0, 1000);
                }
                foreach (var view in views)
                {
                    var driver = view.GetComponent<FWUnitVisual>(); driver.enabled = false;
                    var theme = Team5.Rebuild.Presentation.LegacyUI.LegacyUnitLooks.For(view.State.Role).Body;
                    var sprites = view.GetComponentsInChildren<SpriteRenderer>(true);
                    var donor = driver.ActiveAnimator.transform.Find("FW Lower Body");
                    Assert.That(donor, Is.Not.Null, "Scene 1 must use the current slim-legged hybrid.");
                    var hair = sprites.Where(s => (s.name == "F_HAIR_FRONT" || s.name == "F_B_HAIR") && !s.transform.IsChildOf(donor)).ToArray();
                    Assert.That(hair.Length, Is.EqualTo(2));
                    foreach (var sprite in hair) Assert.That(Vector4.Distance(sprite.color, theme), Is.LessThan(.00001f));
                    foreach (var sprite in sprites.Except(hair))
                        Assert.That(sprite.color, Is.EqualTo(Color.white), sprite.name + " must retain its original colors.");
                    camera.transform.position = view.transform.position + new Vector3(0, 2.5f, -5);
                    camera.transform.LookAt(view.transform.position + Vector3.up * .75f);
                    foreach (var run in new[] { false, true })
                    {
                        driver.SetLocomotion(Vector3.right, run, false, camera, 0);
                        driver.ActiveAnimator.Play(run ? "Run" : "Idle", 0, .2f);
                        driver.ActiveAnimator.Update(0);
                        yield return null; yield return null; yield return null;
                        Assert.That(driver.FacingBack, Is.False);
                        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                            Capture(camera, target, "Catgirl" + view.State.Role + (run ? "Run" : "Idle"));
                    }
                    var flags = sprites.Select(s => s.enabled).ToArray();
                    var lines = view.GetComponentsInChildren<LineRenderer>();
                    var lineFlags = lines.Select(l => l.enabled).ToArray();
                    foreach (var line in lines) line.enabled = false;
                    for (var i = 0; i < sprites.Length; i++) sprites[i].enabled = hair.Contains(sprites[i]);
                    yield return null; yield return null;
                    if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                    {
                        var pixels = Capture(camera, target, "Catgirl" + view.State.Role + "HairOnly");
                        bool Matches(Color32 c)
                        {
                            if (Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < 80) return false;
                            switch (view.State.Role)
                            {
                                case UnitRole.Attack: return c.r > c.g * 1.2f && c.r > c.b * 1.2f;
                                case UnitRole.Tank: return c.b > c.r * 1.06f && c.b > c.g * 1.025f;
                                case UnitRole.Heal: return c.g > c.r * 1.2f && c.g > c.b * 1.1f;
                                case UnitRole.Captain: return c.r > c.g * 1.04f && c.g > c.b * 1.2f;
                                default: return false;
                            }
                        }
                        Assert.That(pixels.Count(Matches), Is.GreaterThan(300), "Rendered hair must visibly use the role color, not just serialize it.");
                    }
                    for (var i = 0; i < sprites.Length; i++) sprites[i].enabled = flags[i];
                    for (var i = 0; i < lines.Length; i++) lines[i].enabled = lineFlags[i];
                }
            }
            finally
            {
                camera.targetTexture = null; Object.Destroy(cameraObject);
                target.Release(); Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator Scene1ContinuousMovementUsesAnimatedWorldZViewsAndIndependentMaterials()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/1.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; yield return null; yield return null;
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/1.unity"));
            var presentation = Object.FindFirstObjectByType<BattlePresentation>();
            var session = presentation.Session;
            var simulation = session.Simulation;
            Assert.That(simulation, Is.Not.Null);
            Assert.That(session.ContinuousSquadMovement, Is.True);
            var views = simulation.Units.Where(u => u.IsAlive).Select(u =>
            { Assert.That(presentation.TryGetView(u.Id, out var v), Is.True); return v; }).ToArray();
            Assert.That(views.Length, Is.EqualTo(4));
            var materials = views.Select(v => v.GetComponentInChildren<SpriteRenderer>().sharedMaterial).ToArray();
            Assert.That(materials.Distinct().Count(), Is.EqualTo(views.Length), "Each soldier owns its foot-Z material value.");
            foreach (var view in views) Assert.That(view.GetComponent<FWUnitVisual>().WorldZPriority, Is.True);
            var anchor = views[0].State.Position - views[0].State.FormationOffset;
            var requested = false;
            foreach (var direction in new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back })
            {
                var target = anchor + direction * 3;
                if (simulation.CanPlaceSquadAt(target) && simulation.RequestMovement(new MovementRequest(target)))
                { requested = true; break; }
            }
            Assert.That(requested, Is.True, "Scene 1 should accept a nearby movement command.");
            var driver = views[0].GetComponent<FWUnitVisual>();
            for (var i = 0; i < 90 && !driver.IsMoving; i++) yield return null;
            Assert.That(driver.IsMoving, Is.True);
            yield return new WaitForSeconds(.2f);
            Assert.That(driver.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Run"), Is.True);
            Assert.That(driver.ActiveAnimator.speed, Is.GreaterThan(0));
            foreach (var view in views)
                Assert.That(view.GetComponentInChildren<SpriteRenderer>().sharedMaterial.GetFloat("_FWFootZ"),
                    Is.EqualTo(view.transform.position.z).Within(.001f));
            simulation.SetPaused(true);
            yield return null; yield return null;
            Assert.That(driver.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(driver.ActiveAnimator.speed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WorldZPriorityDrawsAheadOfRearOpaqueAndTransparentObjectsButKeepsFrontOcclusion()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/FWTest/FWUnitVisual_WorldZ.prefab"));
            root.GetComponent<UnitView>().enabled = false;
            var cameraObject = new GameObject("FW world Z render camera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(384, 384, 24);
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var sorter = new GameObject("FW scene Z sort fixture");
            sorter.AddComponent<WorldZSceneSorting>();
            try
            {
                camera.orthographic = true; camera.orthographicSize = 1.1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.05f, .1f, .15f, 1);
                camera.targetTexture = target;
                // Deliberately reverse camera Z: priority must follow world Z, not camera distance.
                camera.transform.position = new Vector3(0, 2.5f, 5);
                camera.transform.LookAt(Vector3.up * .75f);
                var driver = root.GetComponent<FWUnitVisual>();
                driver.SetLocomotion(Vector3.zero, false, true, camera);
                material.SetColor("_BaseColor", new Color(.2f, .65f, .3f, 1));
                obstacle.GetComponent<Renderer>().sharedMaterial = material;
                obstacle.transform.localScale = new Vector3(4, 4, .2f);
                obstacle.SetActive(false);
                yield return null; yield return null;
                var baseline = Capture(camera, target, "WorldZBaseline");
                var indices = baseline.Select((c, i) => (c, i)).Where(p => Difference(p.c, baseline[0]) > 20).Select(p => p.i).ToArray();
                Assert.That(indices.Length, Is.GreaterThan(500));
                obstacle.transform.position = new Vector3(0, .8f, .8f);
                obstacle.SetActive(true);
                yield return null; yield return null;
                var rear = Capture(camera, target, "WorldZRearOpaque");
                Assert.That(indices.Count(i => Difference(rear[i], baseline[i]) < 15), Is.GreaterThan(indices.Length * .9f),
                    "A world-Z rear object must not hide the character even when closer to the camera.");

                // Return to the gameplay camera side to check normal foreground obstruction.
                camera.transform.position = new Vector3(0, 2.5f, -5);
                camera.transform.LookAt(Vector3.up * .75f);
                driver.SetLocomotion(Vector3.zero, false, true, camera);
                obstacle.SetActive(false);
                yield return null; yield return null;
                baseline = Capture(camera, target, "WorldZFrontBaseline");
                indices = baseline.Select((c, i) => (c, i)).Where(p => Difference(p.c, baseline[0]) > 20).Select(p => p.i).ToArray();
                obstacle.transform.position = new Vector3(0, .8f, -.8f);
                obstacle.SetActive(true);
                yield return null; yield return null;
                var front = Capture(camera, target, "WorldZFrontOpaque");
                Assert.That(indices.Count(i => front[i].g > front[i].r * 1.6f && front[i].g > front[i].b * 1.4f),
                    Is.GreaterThan(indices.Length * .95f));

                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("DepthOnly", false);
                material.renderQueue = 3000;
                yield return new WaitForSecondsRealtime(.6f);
                front = Capture(camera, target, "WorldZFrontTransparent");
                Assert.That(obstacle.GetComponent<Renderer>().sortingOrder, Is.GreaterThan(root.GetComponentInChildren<SortingGroup>().sortingOrder));
                Assert.That(indices.Count(i => front[i].g > front[i].r * 1.6f && front[i].g > front[i].b * 1.4f), Is.GreaterThan(indices.Length * .95f));
                obstacle.transform.position = new Vector3(0, .8f, .8f);
                yield return null; yield return null;
                rear = Capture(camera, target, "WorldZRearTransparent");
                Assert.That(obstacle.GetComponent<Renderer>().sortingOrder, Is.LessThan(root.GetComponentInChildren<SortingGroup>().sortingOrder));
                Assert.That(indices.Count(i => Difference(rear[i], baseline[i]) < 15), Is.GreaterThan(indices.Length * .9f));
            }
            finally
            {
                camera.targetTexture = null;
                Object.Destroy(root); Object.Destroy(cameraObject); Object.Destroy(obstacle);
                Object.Destroy(sorter); Object.Destroy(material); target.Release(); Object.Destroy(target);
            }
        }

        private static Color32[] Capture(Camera camera, RenderTexture target, string name)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/FWVisual");
                File.WriteAllBytes("Logs/FWVisual/" + name + ".png", image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.Destroy(image); }
        }

        [UnityTest]
        public IEnumerator SimulationDrivesOneFWMemberAndPauseAndVisualSwitchPreserveBattle()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Rebuild/Scenes/FWUnitVisualTest.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; yield return null;
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/FWUnitVisualTest.unity"));
            var presentation = Object.FindFirstObjectByType<BattlePresentation>();
            var session = presentation.Session;
            Assert.That(session.Simulation, Is.Not.Null);
            Assert.That(presentation.TryGetView(111, out var view), Is.True);
            var driver = view.GetComponent<FWUnitVisual>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(view.State.SlotId, Is.EqualTo(4));
            Assert.That(view.transform.Find("Body"), Is.Null);
            Assert.That(view.GetComponentInChildren<SkinnedMeshRenderer>(), Is.Null);
            Assert.That(presentation.TryGetView(110, out var captain), Is.True);
            Assert.That(captain.GetComponent<FWUnitVisual>(), Is.Null);
            var cameraPosition = Camera.main.transform.position;
            var cameraRotation = Camera.main.transform.rotation;
            for (var i = 0; i < 120 && !driver.IsMoving; i++) yield return null;
            Assert.That(driver.IsMoving, Is.True);
            yield return new WaitForSeconds(1.2f);
            Assert.That(driver.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Run"), Is.True);
            Assert.That(driver.ActiveAnimator.applyRootMotion, Is.False);
            Assert.That(Vector3.Distance(view.transform.position, view.State.Position), Is.LessThan(.1f));
            session.Simulation.SetPaused(true);
            yield return null; yield return null;
            Assert.That(driver.IsMoving, Is.False);
            Assert.That(driver.ActiveAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            Assert.That(driver.ActiveAnimator.speed, Is.Zero);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                var screenshot = new RenderTexture(1024, 768, 24);
                Capture(Camera.main, screenshot, "BattleView");
                screenshot.Release(); Object.Destroy(screenshot);
            }
            var stopped = view.State.Position;
            var state = view.State;
            var simulation = session.Simulation;
            var toggle = Object.FindFirstObjectByType<FWVisualTestSwitch>();
            toggle.Use2D = false;
            yield return null;
            Assert.That(presentation.TryGetView(111, out view), Is.True);
            Assert.That(view.GetComponent<HaonUnitAnimation>(), Is.Not.Null);
            Assert.That(view.State, Is.SameAs(state));
            toggle.Use2D = true;
            yield return null;
            Assert.That(presentation.TryGetView(111, out view), Is.True);
            driver = view.GetComponent<FWUnitVisual>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(view.State.Position, Is.EqualTo(stopped));
            Assert.That(session.Simulation, Is.SameAs(simulation));
            Assert.That(presentation.TryGetView(110, out var sameCaptain), Is.True);
            Assert.That(sameCaptain, Is.SameAs(captain));
            session.Simulation.SetPaused(false);
            for (var i = 0; i < 120 && !driver.IsMoving; i++) yield return null;
            Assert.That(driver.IsMoving, Is.True);
            Assert.That(Camera.main.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(Camera.main.transform.rotation, Is.EqualTo(cameraRotation));
        }

        [UnityTest]
        public IEnumerator FrontBackIdleRunAnimateBonesPreservePhaseAndNeverMoveUnitRoot()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/FWTest/FWUnitVisual.prefab");
            var root = Object.Instantiate(source);
            root.GetComponent<UnitView>().enabled = false;
            File.AppendAllText("Logs/FWVisual/Stages.log", "Standalone FW prefab instantiated\n");
            var cameraObject = new GameObject("FW direction test camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.rotation = Quaternion.Euler(50, 0, 0);
                root.transform.position = new Vector3(3, 2, 7);
                var origin = root.transform.position;
                var driver = root.GetComponent<FWUnitVisual>();
                foreach (var away in new[] { false, true })
                {
                    driver.SetLocomotion(away ? Vector3.forward : Vector3.back, true, false, camera);
                    var animator = driver.ActiveAnimator;
                    Assert.That(driver.FacingBack, Is.EqualTo(away));
                    foreach (var state in new[] { "Idle", "Run" })
                    {
                        animator.SetBool("IsMoving", state == "Run");
                        animator.Play(state, 0, .15f);
                        animator.Update(0);
                        var bones = animator.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("bone")).ToArray();
                        Assert.That(bones.Length, Is.GreaterThan(10));
                        var poses = bones.Select(t => t.localRotation).ToArray();
                        animator.Update(.17f);
                        Assert.That(bones.Where((t, i) => Quaternion.Angle(t.localRotation, poses[i]) > .01f).Any(), Is.True, away + ":" + state);
                        Assert.That(root.transform.position, Is.EqualTo(origin));
                    }
                }
                driver.ActiveAnimator.Play("Run", 0, .43f);
                driver.ActiveAnimator.Update(0);
                driver.SetLocomotion(Vector3.back + Vector3.right, true, false, camera);
                Assert.That(driver.FacingBack, Is.False);
                Assert.That(driver.Mirrored, Is.True);
                Assert.That(Mathf.Repeat(driver.ActiveAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1), Is.EqualTo(.43f).Within(.02f));
                driver.SetLocomotion(Vector3.back + Vector3.left, true, false, camera);
                Assert.That(driver.Mirrored, Is.False);
                Assert.That(root.transform.position, Is.EqualTo(origin));
                yield return null;
            }
            finally { Object.Destroy(root); Object.Destroy(cameraObject); }
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            // Preserve Unity's initial scene: it owns the coroutine test runner itself.
            if (initialScene.IsValid() && initialScene.isLoaded) SceneManager.SetActiveScene(initialScene);
            var scene = SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/FWUnitVisualTest.unity");
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            scene = SceneManager.GetSceneByPath("Assets/Rebuild/Scenes/1.unity");
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            yield return null;
        }
    }
}
#endif
