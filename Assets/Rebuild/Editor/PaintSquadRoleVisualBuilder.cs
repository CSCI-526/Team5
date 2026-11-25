using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Editor
{
    // Builds the ten Paint Squad role prefabs, their role materials, gear meshes and the role catalog.
    // Everything is rebuilt in place (asset GUIDs are kept), so it can be rerun after art or table changes.
    public static class PaintSquadRoleVisualBuilder
    {
        public const string ArtRoot = "Assets/Rebuild/PaintSquad";
        public const string Folder = ArtRoot + "/RoleVisuals";
        public const string MaterialFolder = Folder + "/Materials";
        public const string MeshFolder = Folder + "/Meshes";
        public const string CatalogPath = Folder + "/PaintSquadAllRoles.asset";
        public const float MaxWidth = .88f;
        // Fitting margin: the fit covers the rest pose and every sampled Idle and Run pose.
        private const float FitWidth = .87f;
        private const float SoldierHeight = 1f;
        private const float HeroHeight = 1.25f;
        private const float ShadowSize = .8f;

        public static readonly UnitRole[] Roles =
        {
            UnitRole.Attack, UnitRole.Tank, UnitRole.Heal, UnitRole.Captain, UnitRole.Sniper,
            UnitRole.Vanguard, UnitRole.Demolisher, UnitRole.Frostweaver, UnitRole.Warcaller, UnitRole.Aegis,
        };

        public static string PrefabPath(UnitRole role) => $"{Folder}/PaintSquad{role}.prefab";
        public static string HelmetMaterialPath(UnitRole role) => $"{MaterialFolder}/PaintSquad{role}Helmet.mat";
        public static string BodyMaterialPath(UnitRole role) => $"{MaterialFolder}/PaintSquad{role}Body.mat";
        public static string PaintMaterialPath(UnitRole role) => $"{MaterialFolder}/PaintSquad{role}Paint.mat";
        public const string UnarmedSoldierMeshPath = MeshFolder + "/PaintSquadSoldierUnarmed.asset";
        public const string UnarmedSoldierHeavyMeshPath = MeshFolder + "/PaintSquadSoldierHeavyUnarmed.asset";
        private const string ConeMeshPath = MeshFolder + "/PaintSquadCone.asset";
        private const string CrystalMeshPath = MeshFolder + "/PaintSquadCrystal.asset";
        private const string RiotShieldMeshPath = MeshFolder + "/PaintSquadRiotShield.asset";

        // Roles whose baked paint gun is removed (no gun, or replaced by role gear held by the Gun bone).
        public static bool RemovesBakedGun(UnitRole role) => role == UnitRole.Heal || role == UnitRole.Sniper ||
            role == UnitRole.Demolisher || role == UnitRole.Frostweaver || role == UnitRole.Aegis || role == UnitRole.Tank;

        public static string UnarmedMeshPath(PaintSquadBaseModel model) => model switch
        {
            PaintSquadBaseModel.Soldier => UnarmedSoldierMeshPath,
            PaintSquadBaseModel.SoldierHeavy => UnarmedSoldierHeavyMeshPath,
            _ => null,
        };

        private sealed class Kit
        {
            public Material metal, dark, accent, wood, paint, helmet, body;
            public Mesh cube, cylinder, sphere, cone, crystal, riotShield;
        }

        private sealed class Model
        {
            public GameObject root;
            public PaintSquadBaseModel baseModel;
            public Kit kit;
            public Animator animator;
            public SkinnedMeshRenderer skin;
            public Transform muzzle;
            public readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
            public Vector3[] restVertices;
            public BoneWeight[] weights;
            public int[] helmetVertices;
        }

        [MenuItem("Team5/Rebuild/Paint Squad/Build role visuals")]
        public static void Build() => BuildRoles(Roles);

        public static void BuildRoles(params UnitRole[] rebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            EnsureFolder(Folder); EnsureFolder(MaterialFolder); EnsureFolder(MeshFolder);
            BuildMeshes();
            var catalog = LoadOrCreate<UnitRoleVisualSet>(CatalogPath);
            var report = new List<string>();
            var soldierScale = FitScale(BuildModel(UnitRole.Attack, null), SoldierHeight, out _, out _);
            var heavyScale = FitScale(BuildModel(UnitRole.Tank, null), SoldierHeight, out _, out _);
            var heroScale = FitScale(BuildModel(UnitRole.Captain, null), HeroHeight, out _, out _);
            var prefabs = new Dictionary<UnitRole, UnitView>();
            foreach (var role in Roles)
            {
                if (!rebuild.Contains(role))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<UnitView>(PrefabPath(role));
                    if (existing == null) throw new InvalidOperationException($"Missing {PrefabPath(role)}. Run Build role visuals first.");
                    prefabs[role] = existing;
                    continue;
                }
                var helmet = SaveRoleMaterial(HelmetMaterialPath(role), "M_U_TeamColor", PaintSquadRoleLooks.Primary(role));
                var body = SaveRoleMaterial(BodyMaterialPath(role), "M_U_TeamColorDark", PaintSquadRoleLooks.Dark(role));
                // Paint (body plate, gun drum) is the source rig's third team slot: the primary brightened by 1.25.
                var primary = PaintSquadRoleLooks.Primary(role);
                var paint = SaveRoleMaterial(PaintMaterialPath(role), "M_U_Paint",
                    new Color(Mathf.Clamp01(primary.r * 1.25f), Mathf.Clamp01(primary.g * 1.25f), Mathf.Clamp01(primary.b * 1.25f)));
                var kit = LoadKit(helmet, body, paint);
                var baseModel = PaintSquadRoleLooks.BaseModel(role);
                // Height comes from the bare base model (shared by its roles); gear may only shrink a role to fit the width.
                var shared = baseModel == PaintSquadBaseModel.Hero ? heroScale : baseModel == PaintSquadBaseModel.SoldierHeavy ? heavyScale : soldierScale;
                var scale = Mathf.Min(shared, FitScale(BuildModel(role, kit), float.PositiveInfinity, out var width, out var height));
                prefabs[role] = SavePrefab(role, kit, ref scale);
                report.Add($"{role}: base {baseModel}, scale {scale:F3}, height {height * scale:F3}, max posed width {width * scale:F3}");
            }
            var data = new SerializedObject(catalog);
            var entries = data.FindProperty("entries");
            var all = (UnitRole[])Enum.GetValues(typeof(UnitRole));
            entries.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("role").intValue = (int)all[i];
                entry.FindPropertyRelative("prefab").objectReferenceValue = prefabs[all[i] == UnitRole.Test ? UnitRole.Attack : all[i]];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("PAINT_SQUAD_ROLE_VISUALS_BUILT\n" + string.Join("\n", report));
        }

        // ---------- assets ----------

        private static void BuildMeshes()
        {
            SaveMesh(Unarmed(PaintSquadBaseModel.Soldier, "PaintSquadSoldierUnarmed"), UnarmedSoldierMeshPath);
            SaveMesh(Unarmed(PaintSquadBaseModel.SoldierHeavy, "PaintSquadSoldierHeavyUnarmed"), UnarmedSoldierHeavyMeshPath);
            SaveMesh(Cone(16), ConeMeshPath);
            SaveMesh(Crystal(), CrystalMeshPath);
            SaveMesh(RoundedSlab(.62f, .84f, .1f, 6), RiotShieldMeshPath);
        }

        private static Mesh Unarmed(PaintSquadBaseModel baseModel, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(baseModel));
            var skin = asset.GetComponentInChildren<SkinnedMeshRenderer>();
            var source = skin.sharedMesh;
            var unarmed = Object.Instantiate(source);
            unarmed.name = name;
            var gunBones = new HashSet<int>();
            for (var i = 0; i < skin.bones.Length; i++)
                if (skin.bones[i].name == "Gun" || skin.bones[i].name == "Muzzle") gunBones.Add(i);
            var weights = source.boneWeights;
            bool OnGun(int v) => gunBones.Contains(weights[v].boneIndex0) && weights[v].weight0 > .5f;
            for (var sub = 0; sub < source.subMeshCount; sub++)
            {
                var triangles = source.GetTriangles(sub);
                var kept = new List<int>(triangles.Length);
                for (var t = 0; t < triangles.Length; t += 3)
                    if (!(OnGun(triangles[t]) && OnGun(triangles[t + 1]) && OnGun(triangles[t + 2])))
                        kept.AddRange(new[] { triangles[t], triangles[t + 1], triangles[t + 2] });
                unarmed.SetTriangles(kept, sub, false);
            }
            unarmed.RecalculateBounds();
            return unarmed;
        }

        private static Mesh RoundedSlab(float width, float height, float radius, int cornerSegments)
        {
            var corners = new[]
            {
                new Vector2(width / 2 - radius, height / 2 - radius), new Vector2(-width / 2 + radius, height / 2 - radius),
                new Vector2(-width / 2 + radius, -height / 2 + radius), new Vector2(width / 2 - radius, -height / 2 + radius),
            };
            var outline = new List<Vector2>();
            for (var c = 0; c < 4; c++)
                for (var i = 0; i <= cornerSegments; i++)
                {
                    var a = (c + (float)i / cornerSegments) * Mathf.PI / 2;
                    var p = corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    outline.Add(new Vector2(p.x / width, p.y / height));
                }
            var n = outline.Count;
            var edges = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var d = outline[(i + 1) % n] - outline[i];
                edges[i] = new Vector2(d.y, -d.x).normalized;
            }
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                Vector3 ni = (edges[(i + n - 1) % n] + edges[i]).normalized;
                Vector3 nj = (edges[i] + edges[j]).normalized;
                var b = vertices.Count;
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -.5f)); normals.Add(ni);
                vertices.Add(new Vector3(outline[j].x, outline[j].y, -.5f)); normals.Add(nj);
                vertices.Add(new Vector3(outline[j].x, outline[j].y, .5f)); normals.Add(nj);
                vertices.Add(new Vector3(outline[i].x, outline[i].y, .5f)); normals.Add(ni);
                triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            foreach (var z in new[] { .5f, -.5f })
            {
                var centre = vertices.Count;
                var normal = new Vector3(0, 0, Mathf.Sign(z));
                vertices.Add(new Vector3(0, 0, z)); normals.Add(normal);
                for (var i = 0; i < n; i++) { vertices.Add(new Vector3(outline[i].x, outline[i].y, z)); normals.Add(normal); }
                for (var i = 0; i < n; i++)
                {
                    var a = centre + 1 + i; var b = centre + 1 + (i + 1) % n;
                    triangles.AddRange(z > 0 ? new[] { centre, a, b } : new[] { centre, b, a });
                }
            }
            var mesh = new Mesh { name = "PaintSquadRiotShield" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Cone(int segments)
        {
            // Unit cone: base radius .5 at y 0, apex at y 1. Smooth sides keep the toon outline closed.
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            var slope = new Vector2(1, .5f).normalized;
            for (var i = 0; i <= segments; i++)
            {
                var a = i * Mathf.PI * 2 / segments;
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices.Add(radial * .5f); normals.Add((radial * slope.x + Vector3.up * slope.y).normalized);
                vertices.Add(Vector3.up); normals.Add((radial * slope.x + Vector3.up * slope.y).normalized);
            }
            for (var i = 0; i < segments; i++)
            { var b = i * 2; triangles.AddRange(new[] { b, b + 1, b + 2 }); }
            var center = vertices.Count; vertices.Add(Vector3.zero); normals.Add(Vector3.down);
            for (var i = 0; i <= segments; i++)
            {
                var a = i * Mathf.PI * 2 / segments;
                vertices.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .5f); normals.Add(Vector3.down);
            }
            for (var i = 0; i < segments; i++) triangles.AddRange(new[] { center, center + 1 + i + 1, center + 1 + i });
            var mesh = new Mesh { name = "PaintSquadCone" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Crystal()
        {
            // Elongated octahedron, unit tall (y -.5 to .5), with per-vertex normals so the outline stays closed.
            var points = new[]
            {
                new Vector3(0, .5f, 0), new Vector3(0, -.5f, 0), new Vector3(.28f, 0, 0),
                new Vector3(0, 0, .28f), new Vector3(-.28f, 0, 0), new Vector3(0, 0, -.28f),
            };
            var triangles = new[] { 0, 3, 2, 0, 4, 3, 0, 5, 4, 0, 2, 5, 1, 2, 3, 1, 3, 4, 1, 4, 5, 1, 5, 2 };
            var mesh = new Mesh { name = "PaintSquadCrystal", vertices = points, triangles = triangles };
            mesh.normals = points.Select(p => p.normalized).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material SaveRoleMaterial(string path, string template, Color color)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>($"{ArtRoot}/Materials/{template}.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.CopyPropertiesFromMaterial(source);
            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Kit LoadKit(Material helmet, Material body, Material paint)
        {
            Material Slot(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{ArtRoot}/Materials/M_U_{name}.mat");
            return new Kit
            {
                metal = Slot("Metal"), dark = Slot("Dark"), accent = Slot("Accent"), wood = Slot("Wood"), paint = paint,
                helmet = helmet, body = body,
                cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"),
                cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"),
                sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx"),
                cone = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath),
                crystal = AssetDatabase.LoadAssetAtPath<Mesh>(CrystalMeshPath),
                riotShield = AssetDatabase.LoadAssetAtPath<Mesh>(RiotShieldMeshPath),
            };
        }

        private static string ModelPath(PaintSquadBaseModel model) => $"{ArtRoot}/Models/Characters/{model}.fbx";
        private static string ControllerPath(PaintSquadBaseModel model) => $"{ArtRoot}/Animations/{model}.controller";

        // ---------- model and gear ----------

        // Kit null builds the bare model with its baked gun (used for the shared Soldier scale).
        private static Model BuildModel(UnitRole role, Kit kit)
        {
            var baseModel = PaintSquadRoleLooks.BaseModel(role);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(baseModel));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Model";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            var model = new Model { root = instance, baseModel = baseModel, kit = kit, animator = instance.GetComponent<Animator>(), skin = instance.GetComponentInChildren<SkinnedMeshRenderer>() };
            foreach (var bone in model.skin.bones) model.bones[bone.name] = bone;
            model.muzzle = model.bones["Muzzle"];
            model.animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath(baseModel));
            model.animator.applyRootMotion = false;
            model.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            model.weights = model.skin.sharedMesh.boneWeights;
            var baked = new Mesh();
            model.skin.BakeMesh(baked);
            model.restVertices = baked.vertices;
            Object.DestroyImmediate(baked);
            // Helmet shell: head vertices in the team colour slots (the Hero's head plate uses the dark slot).
            var mesh = model.skin.sharedMesh;
            model.helmetVertices = Enumerable.Range(0, mesh.subMeshCount)
                .Where(i => model.skin.sharedMaterials[i] != null && model.skin.sharedMaterials[i].name.StartsWith("M_U_TeamColor"))
                .SelectMany(i => mesh.GetTriangles(i)).Distinct().Where(v => BoneName(model, v) == "Head").ToArray();
            if (kit == null) return model;
            if (RemovesBakedGun(role))
                model.skin.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(UnarmedMeshPath(baseModel));
            var materials = model.skin.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                if (materials[i].name == "M_U_TeamColor") materials[i] = kit.helmet;
                else if (materials[i].name == "M_U_TeamColorDark") materials[i] = kit.body;
                else if (materials[i].name == "M_U_Paint") materials[i] = kit.paint;
            }
            model.skin.sharedMaterials = materials;
            AddGear(role, model, kit);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off; // blob shadow instead
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            model.skin.updateWhenOffscreen = false;
            model.skin.skinnedMotionVectors = false;
            return model;
        }

        private static string BoneName(Model model, int vertex) => model.skin.bones[model.weights[vertex].boneIndex0].name;

        // Gear is authored in the unscaled model space of the rest pose (Y up, facing +Z, feet at 0),
        // then parented to the bone that should carry it.
        private static void AddGear(UnitRole role, Model model, Kit kit)
        {
            var gun = model.bones["Gun"];
            var head = model.bones["Head"];
            var spine = model.bones["Spine"];
            var leftArm = model.bones["Arm.L"];
            var gunOrigin = gun.position; // the baked gun points from the Gun bone along +Z
            switch (role)
            {
                case UnitRole.Heal:
                {
                    // White medic cross on the helmet front, tangent to the dome.
                    var direction = new Vector3(0, .55f, .83f).normalized;
                    var point = HelmetPoint(model, direction, out var center);
                    var normal = (point - center).normalized;
                    var rotation = Quaternion.LookRotation(normal, Vector3.up);
                    var at = point + normal * .006f;
                    Box(model, head, "Medic Cross H", kit.accent, at, rotation, new Vector3(.2f, .065f, .03f));
                    Box(model, head, "Medic Cross V", kit.accent, at, rotation, new Vector3(.065f, .2f, .03f));
                    model.muzzle = null;
                    break;
                }
                case UnitRole.Captain:
                {
                    // Spiked hero helmet: a tall centre spike with two shorter side spikes.
                    var top = HelmetPoint(model, Vector3.up, out _);
                    Cone(model, head, "Helmet Spike", kit.metal, top - Vector3.up * .02f, Quaternion.identity, new Vector3(.13f, .3f, .13f));
                    Cone(model, head, "Helmet Spike L", kit.metal, top + new Vector3(-.13f, -.04f, 0), Quaternion.Euler(0, 0, 28), new Vector3(.09f, .17f, .09f));
                    Cone(model, head, "Helmet Spike R", kit.metal, top + new Vector3(.13f, -.04f, 0), Quaternion.Euler(0, 0, -28), new Vector3(.09f, .17f, .09f));
                    Cylinder(model, head, "Helmet Spike Collar", kit.body, top - Vector3.up * .03f, top + Vector3.up * .02f, .09f);
                    break;
                }
                case UnitRole.Sniper:
                {
                    var y = gunOrigin.y; var x = gunOrigin.x;
                    Box(model, gun, "Rifle Stock", kit.wood, new Vector3(x, y - .01f, gunOrigin.z - .16f), Quaternion.identity, new Vector3(.065f, .11f, .22f));
                    Box(model, gun, "Rifle Body", kit.dark, new Vector3(x, y, gunOrigin.z + .05f), Quaternion.identity, new Vector3(.075f, .08f, .22f));
                    Cylinder(model, gun, "Rifle Barrel", kit.metal, new Vector3(x, y + .01f, gunOrigin.z + .18f), new Vector3(x, y + .01f, gunOrigin.z + .36f), .024f);
                    Cylinder(model, gun, "Rifle Scope", kit.helmet, new Vector3(x, y + .075f, gunOrigin.z - .03f), new Vector3(x, y + .075f, gunOrigin.z + .13f), .032f);
                    model.muzzle = Point(model, gun, "PaintSquad Muzzle", new Vector3(x, y + .01f, gunOrigin.z + .37f));
                    break;
                }
                case UnitRole.Vanguard:
                {
                    // Small round shield strapped to the outside of the left arm; the baked rifle stays.
                    var arm = BoneBounds(model, "Arm.L");
                    var at = new Vector3(arm.min.x - .02f, arm.center.y - .02f, arm.center.z + .03f);
                    var facing = Quaternion.LookRotation(new Vector3(-1, 0, .55f).normalized);
                    Disc(model, leftArm, "Shield Rim", kit.metal, at, facing, .3f, .03f);
                    Disc(model, leftArm, "Shield Face", kit.helmet, at + facing * Vector3.forward * .012f, facing, .24f, .03f);
                    Sphere(model, leftArm, "Shield Boss", kit.accent, at + facing * Vector3.forward * .03f, .07f);
                    break;
                }
                case UnitRole.Demolisher:
                {
                    var x = gunOrigin.x + .04f; var y = gunOrigin.y + .05f; var z = gunOrigin.z;
                    Cylinder(model, gun, "Bazooka Tube", kit.body, new Vector3(x, y, z - .3f), new Vector3(x, y, z + .3f), .07f);
                    Cylinder(model, gun, "Bazooka Rear", kit.dark, new Vector3(x, y, z - .32f), new Vector3(x, y, z - .24f), .08f);
                    Cylinder(model, gun, "Bazooka Front", kit.dark, new Vector3(x, y, z + .24f), new Vector3(x, y, z + .32f), .08f);
                    Box(model, gun, "Bazooka Grip", kit.dark, new Vector3(x, y - .08f, z + .02f), Quaternion.identity, new Vector3(.04f, .09f, .05f));
                    Cone(model, gun, "Bazooka Warhead", kit.paint, new Vector3(x, y, z + .3f), Quaternion.Euler(90, 0, 0), new Vector3(.1f, .1f, .1f));
                    model.muzzle = Point(model, gun, "PaintSquad Muzzle", new Vector3(x, y, z + .4f));
                    break;
                }
                case UnitRole.Frostweaver:
                {
                    // Ice staff held in the gun hand, raised forward; casting plays the Shoot take.
                    var bottom = new Vector3(gunOrigin.x + .03f, gunOrigin.y - .17f, gunOrigin.z - .1f);
                    var top = new Vector3(gunOrigin.x + .16f, gunOrigin.y + .5f, gunOrigin.z + .2f);
                    Cylinder(model, gun, "Staff Shaft", kit.wood, bottom, top, .022f);
                    Cylinder(model, gun, "Staff Collar", kit.metal, top - (top - bottom).normalized * .03f, top + (top - bottom).normalized * .02f, .035f);
                    var tip = top + (top - bottom).normalized * .17f;
                    Crystal(model, gun, "Staff Crystal", kit.helmet, tip, Quaternion.FromToRotation(Vector3.up, (top - bottom).normalized), new Vector3(.28f, .36f, .28f));
                    model.muzzle = Point(model, gun, "PaintSquad Muzzle", tip);
                    break;
                }
                case UnitRole.Warcaller:
                {
                    // Banner pole on the back, flag flying above the helmet; the baked rifle stays.
                    var outward = new Vector3(-.55f, 0, -.83f).normalized;
                    var back = HelmetPoint(model, outward, out _) + outward * .03f;
                    var foot = new Vector3(back.x, .2f, back.z);
                    var head2 = new Vector3(foot.x, HelmetPoint(model, Vector3.up, out _).y + .32f, foot.z);
                    Cylinder(model, spine, "Banner Pole", kit.wood, foot, head2, .02f);
                    Sphere(model, spine, "Banner Finial", kit.metal, head2 + Vector3.up * .02f, .05f);
                    var flagCenter = new Vector3(foot.x + .16f, head2.y - .11f, foot.z);
                    Box(model, spine, "Banner Flag", kit.helmet, flagCenter, Quaternion.identity, new Vector3(.3f, .19f, .02f));
                    Box(model, spine, "Banner Stripe", kit.accent, flagCenter + new Vector3(0, -.06f, 0), Quaternion.identity, new Vector3(.3f, .04f, .03f));
                    break;
                }
                case UnitRole.Aegis:
                {
                    // Big tower shield carried on the left arm, covering the front-left.
                    var rimY = HelmetMinY(model);
                    var top = rimY - .03f;
                    var height = top - .04f;
                    var facing = Quaternion.LookRotation(new Vector3(-.45f, 0, 1).normalized);
                    var at = new Vector3(-.24f, .04f + height / 2, .26f);
                    Box(model, leftArm, "Tower Shield Rim", kit.metal, at, facing, new Vector3(.5f, height, .05f));
                    Box(model, leftArm, "Tower Shield Face", kit.helmet, at + facing * Vector3.forward * .015f, facing, new Vector3(.43f, height - .07f, .05f));
                    Box(model, leftArm, "Tower Shield Stripe", kit.accent, at + facing * Vector3.forward * .03f, facing, new Vector3(.07f, height - .12f, .03f));
                    model.muzzle = null;
                    break;
                }
                case UnitRole.Tank:
                {
                    var facing = Quaternion.LookRotation(new Vector3(0, Mathf.Tan(6 * Mathf.Deg2Rad), 1).normalized);
                    var forward = facing * Vector3.forward;
                    var up = facing * Vector3.up;
                    var at = new Vector3(0, .44f, .36f);
                    Slab(model, spine, "Riot Shield Rim", kit.metal, at, facing, new Vector3(.64f, .86f, .05f));
                    Slab(model, spine, "Riot Shield Face", kit.helmet, at + forward * .015f, facing, new Vector3(.56f, .78f, .05f));
                    Box(model, spine, "Riot Shield Window", kit.dark, at + up * .24f + forward * .04f, facing, new Vector3(.36f, .09f, .03f));
                    Box(model, spine, "Riot Shield Band", kit.accent, at - up * .12f + forward * .04f, facing, new Vector3(.56f, .07f, .03f));
                    model.muzzle = null;
                    break;
                }
            }
        }

        private static Vector3 HelmetPoint(Model model, Vector3 direction, out Vector3 center)
        {
            var points = model.helmetVertices.Select(v => model.restVertices[v]).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            center = new Vector3(bounds.center.x, bounds.min.y + bounds.size.x * .1f, bounds.center.z);
            var c = center;
            return points.OrderByDescending(p => Vector3.Dot(p - c, direction)).First();
        }

        private static float HelmetMinY(Model model) => model.helmetVertices.Min(v => model.restVertices[v].y);

        private static Bounds BoneBounds(Model model, string bone)
        {
            var points = Enumerable.Range(0, model.restVertices.Length).Where(v => BoneName(model, v) == bone)
                .Select(v => model.restVertices[v]).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);
            return bounds;
        }

        private static Transform Part(Model model, Transform bone, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var part = new GameObject(name);
            part.transform.SetParent(model.root.transform, false);
            part.transform.SetPositionAndRotation(position, rotation);
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            part.transform.SetParent(bone, true);
            return part.transform;
        }

        private static Transform Point(Model model, Transform bone, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(model.root.transform, false);
            point.position = position;
            point.SetParent(bone, true);
            return point;
        }

        private static void Box(Model model, Transform bone, string name, Material material, Vector3 at, Quaternion rotation, Vector3 size)
            => Part(model, bone, name, model.kit.cube, material, at, rotation, size);

        private static void Sphere(Model model, Transform bone, string name, Material material, Vector3 at, float diameter)
            => Part(model, bone, name, model.kit.sphere, material, at, Quaternion.identity, Vector3.one * diameter);

        private static void Cone(Model model, Transform bone, string name, Material material, Vector3 at, Quaternion rotation, Vector3 size)
            => Part(model, bone, name, model.kit.cone, material, at, rotation, size);

        private static void Crystal(Model model, Transform bone, string name, Material material, Vector3 at, Quaternion rotation, Vector3 size)
            => Part(model, bone, name, model.kit.crystal, material, at, rotation, size);

        private static void Slab(Model model, Transform bone, string name, Material material, Vector3 at, Quaternion rotation, Vector3 size)
            => Part(model, bone, name, model.kit.riotShield, material, at, rotation, size);

        private static void Cylinder(Model model, Transform bone, string name, Material material, Vector3 from, Vector3 to, float radius)
            => Part(model, bone, name, model.kit.cylinder, material, (from + to) / 2,
                Quaternion.FromToRotation(Vector3.up, (to - from).normalized), new Vector3(radius * 2, Vector3.Distance(from, to) / 2, radius * 2));

        private static void Disc(Model model, Transform bone, string name, Material material, Vector3 at, Quaternion facing, float diameter, float thickness)
            => Part(model, bone, name, model.kit.cylinder, material, at, facing * Quaternion.Euler(90, 0, 0), new Vector3(diameter, thickness / 2, diameter));


        // ---------- fitting ----------

        // Uniform scale so the model fits FitWidth across the rest, Idle and Run poses and reaches the target height.
        private static float FitScale(Model model, float targetHeight, out float width, out float height)
        {
            try
            {
                var envelope = PoseEnvelope(model, "Idle", "Run");
                width = Mathf.Max(envelope.size.x, envelope.size.z);
                var rest = RestBounds(model.root);
                height = rest.size.y;
                return Mathf.Min(targetHeight / Mathf.Max(.001f, height), FitWidth / Mathf.Max(.001f, width));
            }
            finally { Object.DestroyImmediate(model.root); }
        }

        private static Bounds PoseEnvelope(Model model, params string[] takes)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath(model.baseModel)).OfType<AnimationClip>()
                .Where(c => takes.Contains(c.name)).ToArray();
            if (clips.Length != takes.Length) throw new InvalidOperationException($"Missing takes on {model.baseModel}.");
            // Start from the rest pose saved in the prefab, which holds the gun further out than Idle.
            var envelope = RestBounds(model.root);
            var found = true;
            foreach (var clip in clips)
                for (var sample = 0; sample < 8; sample++)
                {
                    clip.SampleAnimation(model.root, clip.length * sample / 8f);
                    var pose = RestBounds(model.root);
                    if (!found) { envelope = pose; found = true; } else envelope.Encapsulate(pose);
                }
            return envelope;
        }

        // Bounds of every visible renderer under root, with skinned meshes baked in their current pose.
        public static Bounds RestBounds(GameObject root)
        {
            var bounds = default(Bounds);
            var found = false;
            var baked = new Mesh();
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                    IEnumerable<Vector3> points;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        // Baked vertices are in the renderer's unscaled local space (as HaonUnitAnimation measures them).
                        skin.BakeMesh(baked);
                        points = baked.vertices.Select(v => skin.transform.TransformPoint(v));
                    }
                    else if (renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                        points = filter.sharedMesh.vertices.Select(v => renderer.transform.TransformPoint(v));
                    else points = new[] { renderer.bounds.min, renderer.bounds.max };
                    foreach (var p in points)
                    {
                        if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                        else bounds.Encapsulate(p);
                    }
                }
            }
            finally { Object.DestroyImmediate(baked); }
            return bounds;
        }

        // ---------- prefab ----------

        private static UnitView SavePrefab(UnitRole role, Kit kit, ref float scale)
        {
            var model = BuildModel(role, kit);
            var root = new GameObject($"PaintSquad {role}");
            try
            {
                root.AddComponent<UnitView>();
                var driver = root.AddComponent<PaintSquadUnitAnimation>();
                model.root.transform.SetParent(root.transform, false);
                model.root.transform.localScale = Vector3.one * scale;
                var rest = RestBounds(model.root);
                // Re-measure the saved pose at its final scale (as the tests do) and shrink if it still overhangs.
                for (var i = 0; i < 4 && Mathf.Max(rest.size.x, rest.size.z) > FitWidth; i++)
                {
                    scale *= FitWidth / Mathf.Max(rest.size.x, rest.size.z);
                    model.root.transform.localScale = Vector3.one * scale;
                    rest = RestBounds(model.root);
                }
                model.root.transform.localPosition = Vector3.up * -rest.min.y;
                var shadow = new GameObject("Blob Shadow");
                shadow.transform.SetParent(root.transform, false);
                shadow.transform.localPosition = new Vector3(0, .015f, 0);
                shadow.transform.localRotation = Quaternion.Euler(90, 0, 0);
                shadow.transform.localScale = new Vector3(ShadowSize, ShadowSize, 1);
                shadow.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{ArtRoot}/Meshes/M_Quad.asset");
                var shadowRenderer = shadow.AddComponent<MeshRenderer>();
                shadowRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ArtRoot}/Materials/M_BlobShadow.mat");
                shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                shadowRenderer.receiveShadows = false;
                shadowRenderer.lightProbeUsage = LightProbeUsage.Off;
                shadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var data = new SerializedObject(driver);
                data.FindProperty("modelRoot").objectReferenceValue = model.root.transform;
                data.FindProperty("characterAnimator").objectReferenceValue = model.animator;
                data.FindProperty("muzzle").objectReferenceValue = model.muzzle;
                data.FindProperty("blobShadow").objectReferenceValue = shadow.transform;
                data.FindProperty("muzzleFlashPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/VFX/PaintSquadMuzzleFlash.prefab");
                data.FindProperty("hitSplashPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtRoot}/VFX/PaintSquadHitSplash.prefab");
                data.FindProperty("hitHeight").floatValue = rest.size.y * .55f;
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(role));
                return prefab.GetComponent<UnitView>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ---------- preview ----------

        // Renders every role prefab in its Idle pose (front three-quarter row and back row), plus the Catgirl
        // Attack prefab at the right end for size, with a battle-like 45 degree orthographic camera.
        [MenuItem("Team5/Rebuild/Paint Squad/Capture role preview")]
        public static void CaptureRolePreview() => CaptureRolePreview("Logs/PaintSquadScreens/task2-roles.png");

        public static void CaptureRolePreview(string output)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var texture = new RenderTexture(3000, 900, 24) { antiAliasing = 4 };
            try
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
                const float spacing = 1.15f;
                for (var row = 0; row < 2; row++)
                    for (var i = 0; i <= Roles.Length; i++)
                    {
                        var path = i < Roles.Length ? PrefabPath(Roles[i]) : "Assets/Rebuild/CatgirlFrontTest/RoleVisuals/CatgirlAttack.prefab";
                        var unit = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                        unit.transform.position = new Vector3((i - Roles.Length / 2f) * spacing, 0, row * -1.6f);
                        var driver = unit.GetComponent<PaintSquadUnitAnimation>();
                        if (driver == null) continue;
                        driver.ModelRoot.rotation = Quaternion.Euler(0, row == 0 ? 205 : 25, 0);
                        var idle = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(driver.CharacterAnimator.avatar))
                            .OfType<AnimationClip>().First(c => c.name == "Idle");
                        idle.SampleAnimation(driver.CharacterAnimator.gameObject, .3f);
                    }
                var camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 1.95f;
                camera.transform.rotation = Quaternion.Euler(45, 0, 0);
                camera.transform.position = new Vector3(.3f, .5f, -.8f) - camera.transform.forward * 20;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.84f, .78f, .62f);
                camera.targetTexture = texture;
                camera.aspect = 3000f / 900f;
                camera.Render();
                var active = RenderTexture.active;
                RenderTexture.active = texture;
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                image.Apply();
                RenderTexture.active = active;
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                camera.targetTexture = null;
                Debug.Log("PAINT_SQUAD_ROLE_PREVIEW " + Path.GetFullPath(output));
            }
            finally
            {
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        public static void BuildAndCaptureFromCommandLine()
        {
            Build();
            CaptureRolePreview();
        }

        // ---------- helpers ----------

        private static void SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return; }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
