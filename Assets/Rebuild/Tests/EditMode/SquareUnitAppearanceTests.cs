using System;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Team5.Rebuild.Tests
{
    public sealed class SquareUnitAppearanceTests
    {
        // MPB stores colors in linear space; an sRGB readback can differ by a few float ULPs.
        private static void SameColor(Color actual, Color expected)
            => Assert.That(Vector4.Distance(actual, expected), Is.LessThan(0.00001f));
        [TestCase(UnitRole.Attack, "Mark Diamond")]
        [TestCase(UnitRole.Tank, "Mark Plate")]
        [TestCase(UnitRole.Heal, "Mark Across")]
        public void WorldSquareUsesFormationIconColorsAndMarksWithoutChangingState(UnitRole role, string markName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<UnitView>(MinimalPresentationSetup.UnitPrefabPath);
            var view = Object.Instantiate(prefab);
            var uiRoot = new GameObject("Icon Comparison", typeof(RectTransform));
            try
            {
                var sim = new BattleSimulation(new BattleDefinition(new[] { Vector3.zero, Vector3.right * 10 },
                    Array.Empty<TowerDefinition>(), new[] { new MemberSpawnDefinition(73, 4, SquadCombatRules.Create(role)) }));
                sim.Step(0.02f);
                var state = sim.Units[0];
                var before = (state.MemberId, state.Position, state.Hp, state.AttackCooldown, state.SlotId);
                view.Bind(state);
                var icon = LegacyUnitIconUI.Create(uiRoot.transform, Vector2.zero, 100);
                icon.SetType(role);
                Color Tint(string name)
                {
                    var block = new MaterialPropertyBlock();
                    view.transform.Find(name).GetComponent<Renderer>().GetPropertyBlock(block);
                    return block.GetColor("_BaseColor");
                }
                SameColor(Tint("Body"), icon.transform.Find("Body").GetComponent<Image>().color);
                SameColor(Tint("Outline"), icon.transform.Find("Outline").GetComponent<Image>().color);
                SameColor(Tint(markName), LegacyUnitLooks.For(role).MarkColor);
                var outline = view.transform.Find("Outline");
                Assert.That(outline.localScale.x, Is.EqualTo(outline.localScale.z));
                Assert.That(outline.localScale.x, Is.LessThanOrEqualTo(1));
                Assert.That(view.GetComponentsInChildren<MeshFilter>(true).All(mesh => mesh.sharedMesh.name == "Cube"), Is.True);
                Assert.That(view.GetComponentInChildren<Collider>(true), Is.Null);
                Assert.That(view.transform.Find(markName).gameObject.activeSelf, Is.True);
                Assert.That(view.transform.Find(markName).localPosition.y, Is.GreaterThan(view.transform.Find("Body").localPosition.y));
                Assert.That(view.GetComponentsInChildren<Renderer>().All(renderer => renderer.sharedMaterial.shader.name == "Universal Render Pipeline/Unlit"), Is.True);
                foreach (var part in new[] { "Mark Diamond", "Mark Plate", "Mark Across", "Mark Upright" })
                    Assert.That(view.transform.Find(part).gameObject.activeSelf,
                        Is.EqualTo(part == markName || role == UnitRole.Heal && part == "Mark Upright"));
                Assert.That((state.MemberId, state.Position, state.Hp, state.AttackCooldown, state.SlotId), Is.EqualTo(before));
                view.ApplyAppearance(UnitRole.Tank);
                view.ApplyAppearance(role);
                SameColor(Tint("Body"), LegacyUnitLooks.For(role).Body);
                Object.DestroyImmediate(view.transform.Find("Body").GetComponent<Renderer>());
                Assert.DoesNotThrow(() => view.ApplyAppearance(role));
                Assert.That((state.MemberId, state.Position, state.Hp, state.AttackCooldown, state.SlotId), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(uiRoot); }
        }
    }
}
