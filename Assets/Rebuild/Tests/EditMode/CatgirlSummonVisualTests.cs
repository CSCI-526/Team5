using System;
using System.Linq;
using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Presentation.LegacyUI;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class CatgirlSummonVisualTests
    {
        [Test] public void CatalogCoversAllRolesWithIndependentMatchingHairAndRetainedRig()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<UnitRoleVisualSet>(CatgirlSummonVisualBuilder.CatalogPath);
            Assert.That(catalog,Is.Not.Null);
            var roles=((UnitRole[])Enum.GetValues(typeof(UnitRole))).Where(r=>r!=UnitRole.Test).ToArray();
            Assert.That(catalog.Entries.Count,Is.EqualTo(roles.Length));
            Assert.That(catalog.Entries.Select(e=>e.prefab).Distinct().Count(),Is.EqualTo(roles.Length));
            foreach(var role in roles)
            {
                Assert.That(catalog.TryGet(role,out var prefab),Is.True,role.ToString());
                Assert.That(prefab.GetComponent<FWUnitVisual>(),Is.Not.Null);
                var rig=prefab.GetComponentsInChildren<Animator>(true).Single();
                Assert.That(rig.applyRootMotion,Is.False);
                var donor=rig.transform.Find("FW Lower Body");
                var hair=prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>(s.name=="F_HAIR_FRONT"||s.name=="F_B_HAIR")&&!s.transform.IsChildOf(donor)).ToArray();
                Assert.That(hair.Length,Is.EqualTo(2));
                foreach(var sprite in hair)Assert.That(sprite.color,Is.EqualTo(LegacyUnitLooks.For(role).Body),role+" hair");
                Assert.That(prefab.transform.Find("Body"),Is.Null);
            }
            Assert.That(catalog.TryGet(UnitRole.Test,out _),Is.False);
        }
    }
}
