using System;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class PaintSquadRoleVisualTests
    {
        const string Catalog = "Assets/Rebuild/PaintSquad/RoleVisuals/PaintSquadAllRoles.asset";

        // Binding role look table (helmet colour and base model), repeated here as an independent check.
        static readonly object[] Table =
        {
            new object[] { UnitRole.Attack, "DA4029", "Soldier" },
            new object[] { UnitRole.Tank, "F59A1A", "SoldierHeavy" },
            new object[] { UnitRole.Heal, "8DB52B", "Soldier" },
            new object[] { UnitRole.Captain, "E8B923", "Hero" },
            new object[] { UnitRole.Sniper, "3F7FD0", "Soldier" },
            new object[] { UnitRole.Vanguard, "229A8A", "Soldier" },
            new object[] { UnitRole.Demolisher, "8A5A34", "Soldier" },
            new object[] { UnitRole.Frostweaver, "7FD6F0", "Soldier" },
            new object[] { UnitRole.Warcaller, "DB4793", "Soldier" },
            new object[] { UnitRole.Aegis, "8F63B0", "Soldier" },
            new object[] { UnitRole.Test, "DA4029", "Soldier" },
        };

        static UnitView PrefabFor(UnitRole role)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitRoleVisualSet>(Catalog);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet(role, out var prefab), Is.True, role.ToString());
            return prefab;
        }

        [Test]
        public void CatalogHasAPaintSquadPrefabForEveryRole()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UnitRoleVisualSet>(Catalog);
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                Assert.That(catalog.TryGet(role, out var prefab), Is.True, role.ToString());
                var expected = role == UnitRole.Test ? UnitRole.Attack : role;
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(PaintSquadRoleVisualBuilder.PrefabPath(expected)));
            }
            Assert.That(catalog.Entries.Count, Is.EqualTo(Enum.GetValues(typeof(UnitRole)).Length));
        }

        [TestCaseSource(nameof(Table))]
        public void PrefabFollowsTheUnitViewContractAndRoleLook(UnitRole role, string helmetHex, string baseModel)
        {
            var prefab = PrefabFor(role);
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            var driver = prefab.GetComponent<PaintSquadUnitAnimation>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(prefab.GetComponent<HaonUnitAnimation>(), Is.Null);
            Assert.That(prefab.GetComponent<FWUnitVisual>(), Is.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(driver.CharacterAnimator, Is.SameAs(animator));
            Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                Is.EqualTo($"Assets/Rebuild/PaintSquad/Animations/{baseModel}.controller"));
            Assert.That(animator.applyRootMotion, Is.False);
            // UnitView.ApplyAppearance only touches the legacy marker parts; none may exist here.
            foreach (var legacy in new[] { "Outline", "Body", "Mark Diamond", "Mark Plate", "Mark Across", "Mark Upright" })
                Assert.That(prefab.transform.Find(legacy), Is.Null, legacy);

            var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skin.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            var helmet = skin.sharedMaterials.Single(m => m.name.EndsWith("Helmet"));
            var expected = role == UnitRole.Test ? UnitRole.Attack : role;
            Assert.That(AssetDatabase.GetAssetPath(helmet), Is.EqualTo(PaintSquadRoleVisualBuilder.HelmetMaterialPath(expected)));
            Assert.That(ColorUtility.TryParseHtmlString("#" + helmetHex, out var colour), Is.True);
            AssertColour(helmet.GetColor("_BaseColor"), colour, "helmet");
            Color.RGBToHSV(colour, out var h, out var s, out var v);
            var body = skin.sharedMaterials.Single(m => m.name.EndsWith("Body"));
            AssertColour(body.GetColor("_BaseColor"), Color.HSVToRGB(h, s, v * .65f), "body");
            Assert.That(skin.sharedMaterials.Any(m => m.name == "M_U_TeamColor" || m.name == "M_U_TeamColorDark" || m.name == "M_U_Paint"),
                Is.False, "default blue team slots must be replaced");

            var shadow = prefab.transform.Find("Blob Shadow");
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("M_BlobShadow"));
            Assert.That(Mathf.Max(shadow.localScale.x, shadow.localScale.y), Is.LessThanOrEqualTo(PaintSquadRoleVisualBuilder.MaxWidth), "shadow");

            var instance = Object.Instantiate(prefab.gameObject);
            try
            {
                // The character (model and gear) must fit one Small Cell; the flat blob shadow is checked on its own.
                var bounds = PaintSquadRoleVisualBuilder.RestBounds(instance.GetComponent<PaintSquadUnitAnimation>().ModelRoot.gameObject);
                Assert.That(Mathf.Max(bounds.size.x, bounds.size.z), Is.LessThanOrEqualTo(PaintSquadRoleVisualBuilder.MaxWidth), "width");
                Assert.That(bounds.min.y, Is.GreaterThan(-.03f), "feet at the root");
                Assert.That(bounds.size.y, Is.InRange(.8f, 1.45f), "height comparable to the other characters");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void GearMatchesTheRoleTable()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(PaintSquadRoleVisualBuilder.UnarmedSoldierMeshPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(PaintSquadRoleVisualBuilder.UnarmedSoldierHeavyMeshPath), Is.Not.Null);
            foreach (var role in PaintSquadRoleVisualBuilder.Roles)
            {
                var prefab = PrefabFor(role);
                var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var unarmedPath = PaintSquadRoleVisualBuilder.UnarmedMeshPath(PaintSquadRoleLooks.BaseModel(role));
                var unarmed = unarmedPath == null ? null : AssetDatabase.LoadAssetAtPath<Mesh>(unarmedPath);
                Assert.That(unarmed != null && skin.sharedMesh == unarmed, Is.EqualTo(PaintSquadRoleVisualBuilder.RemovesBakedGun(role)), role.ToString());
                var parts = prefab.GetComponentsInChildren<MeshFilter>(true).Select(f => f.name).ToArray();
                void Has(string part, string bone)
                {
                    var found = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == part);
                    Assert.That(found, Is.Not.Null, $"{role} {part}");
                    Assert.That(found.parent.name, Is.EqualTo(bone), $"{role} {part}");
                }
                switch (role)
                {
                    case UnitRole.Heal: Has("Medic Cross H", "Head"); break;
                    case UnitRole.Captain: Has("Helmet Spike", "Head"); break;
                    case UnitRole.Sniper: Has("Rifle Barrel", "Gun"); Has("Rifle Scope", "Gun"); break;
                    case UnitRole.Vanguard: Has("Shield Face", "Arm.L"); break;
                    case UnitRole.Demolisher: Has("Bazooka Tube", "Gun"); break;
                    case UnitRole.Frostweaver: Has("Staff Crystal", "Gun"); break;
                    case UnitRole.Warcaller: Has("Banner Flag", "Spine"); break;
                    case UnitRole.Aegis: Has("Tower Shield Face", "Arm.L"); break;
                    case UnitRole.Tank: Has("Riot Shield Face", "Spine"); Has("Riot Shield Window", "Spine"); break;
                    default: Assert.That(parts, Is.EqualTo(new[] { "Blob Shadow" }), role.ToString()); break;
                }
                var driver = prefab.GetComponent<PaintSquadUnitAnimation>();
                var shoots = SquadCombatRules.CanAttackTowers(role);
                if (shoots) Assert.That(driver.Muzzle, Is.Not.Null, role + " fires paint and needs a muzzle");
            }
        }

        [TestCase("Soldier", PaintSquadRoleVisualBuilder.UnarmedSoldierMeshPath)]
        [TestCase("SoldierHeavy", PaintSquadRoleVisualBuilder.UnarmedSoldierHeavyMeshPath)]
        public void UnarmedMeshDropsOnlyTheGun(string model, string unarmedPath)
        {
            var unarmed = AssetDatabase.LoadAssetAtPath<Mesh>(unarmedPath);
            Assert.That(unarmed, Is.Not.Null, unarmedPath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Rebuild/PaintSquad/Models/Characters/{model}.fbx")
                .GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            Assert.That(unarmed.subMeshCount, Is.EqualTo(source.subMeshCount));
            Assert.That(unarmed.bindposes.Length, Is.EqualTo(source.bindposes.Length));
            var removed = source.triangles.Length - unarmed.triangles.Length;
            Assert.That(removed, Is.GreaterThan(0));
            Assert.That(removed, Is.LessThan(source.triangles.Length / 3), "only the gun is removed");
        }

        static void AssertColour(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.01f), what);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.01f), what);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.01f), what);
        }
    }
}
