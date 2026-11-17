using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Team5.Rebuild.Analytics;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Presentation;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEditor;
using UnityEngine;

namespace Team5.Rebuild.Tests
{
    public sealed class CharacterSetSelectionTests
    {
        private const string PaintSquadCatalog = "Assets/Rebuild/PaintSquad/RoleVisuals/PaintSquadAllRoles.asset";
        private bool hadValue;
        private string savedValue;

        [SetUp]
        public void SavePrefs()
        {
            hadValue = PlayerPrefs.HasKey(CharacterSetSelection.PrefsKey);
            savedValue = PlayerPrefs.GetString(CharacterSetSelection.PrefsKey, "");
        }

        [TearDown]
        public void RestorePrefs()
        {
            if (hadValue) PlayerPrefs.SetString(CharacterSetSelection.PrefsKey, savedValue);
            else PlayerPrefs.DeleteKey(CharacterSetSelection.PrefsKey);
            PlayerPrefs.Save();
            GameAnalytics.SetSink(null);
        }

        [Test]
        public void DefaultIsPaintSquadWhenNothingIsStored()
        {
            PlayerPrefs.DeleteKey(CharacterSetSelection.PrefsKey);
            Assert.That(CharacterSetSelection.PrefsKey, Is.EqualTo("Team5.CharacterSet"));
            Assert.That(CharacterSetSelection.Default, Is.EqualTo(CharacterSet.PaintSquad));
            Assert.That(CharacterSetSelection.Current, Is.EqualTo(CharacterSet.PaintSquad));
        }

        [Test]
        public void SelectionRoundTripsThroughPlayerPrefs()
        {
            CharacterSetSelection.Set(CharacterSet.Catgirl);
            Assert.That(PlayerPrefs.GetString(CharacterSetSelection.PrefsKey), Is.EqualTo("Catgirl"));
            Assert.That(CharacterSetSelection.Current, Is.EqualTo(CharacterSet.Catgirl));
            CharacterSetSelection.Set(CharacterSet.PaintSquad);
            Assert.That(PlayerPrefs.GetString(CharacterSetSelection.PrefsKey), Is.EqualTo("PaintSquad"));
            Assert.That(CharacterSetSelection.Current, Is.EqualTo(CharacterSet.PaintSquad));
        }

        [TestCase("Haon")]
        [TestCase("catgirl")]
        [TestCase("")]
        [TestCase("1")]
        public void UnknownStoredValueFallsBackToDefault(string stored)
        {
            PlayerPrefs.SetString(CharacterSetSelection.PrefsKey, stored);
            Assert.That(CharacterSetSelection.Current, Is.EqualTo(CharacterSet.PaintSquad));
        }

        [Test]
        public void CatalogsAssetListsBothSetsWithPreviewsAndPaintSquadPortraits()
        {
            var catalogs = Resources.Load<CharacterSetCatalogs>(CharacterSetCatalogs.ResourceName);
            Assert.That(catalogs, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(catalogs), Is.EqualTo(PaintSquadCharacterSetBuilder.CatalogsPath));
            Assert.That(catalogs.Entries.Select(e => e.set), Is.EquivalentTo(new[] { CharacterSet.PaintSquad, CharacterSet.Catgirl }));
            Assert.That(catalogs.TryGet(CharacterSet.PaintSquad, out var paint), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(paint.roleVisuals), Is.EqualTo(PaintSquadCatalog));
            Assert.That(catalogs.TryGet(CharacterSet.Catgirl, out var catgirl), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(catgirl.roleVisuals), Is.EqualTo(PaintSquadCharacterSetBuilder.CatgirlCatalogPath));
            Assert.That(paint.captainPreview, Is.Not.Null);
            Assert.That(catgirl.captainPreview, Is.Not.Null);
            foreach (var role in PaintSquadRoleVisualBuilder.Roles)
            {
                Assert.That(paint.TryGetPortrait(role, out var sprite), Is.True, role.ToString());
                Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(PaintSquadCharacterSetBuilder.PortraitPath(role)));
            }
            Assert.That(paint.TryGetPortrait(UnitRole.Test, out _), Is.True, "Test uses the Attack look");
            Assert.That(catgirl.portraits, Is.Empty, "Catgirl keeps the legacy block icons");
        }

        [Test]
        public void ResolveSwapsOnlySelectableCatalogs()
        {
            var catalogs = Resources.Load<CharacterSetCatalogs>(CharacterSetCatalogs.ResourceName);
            catalogs.TryGet(CharacterSet.PaintSquad, out var paint);
            catalogs.TryGet(CharacterSet.Catgirl, out var catgirl);
            var other = ScriptableObject.CreateInstance<UnitRoleVisualSet>();
            try
            {
                CharacterSetSelection.Set(CharacterSet.PaintSquad);
                Assert.That(CharacterSetCatalogs.Resolve(catgirl.roleVisuals), Is.SameAs(paint.roleVisuals));
                Assert.That(CharacterSetCatalogs.Resolve(null), Is.Null, "scenes without a catalog keep their unit prefab");
                Assert.That(CharacterSetCatalogs.Resolve(other), Is.SameAs(other), "unknown catalogs are left alone");
                Assert.That(CharacterSetCatalogs.TryGetSelectedPortrait(UnitRole.Captain, out _), Is.True);
                CharacterSetSelection.Set(CharacterSet.Catgirl);
                Assert.That(CharacterSetCatalogs.Resolve(catgirl.roleVisuals), Is.SameAs(catgirl.roleVisuals));
                Assert.That(CharacterSetCatalogs.Resolve(paint.roleVisuals), Is.SameAs(catgirl.roleVisuals));
                Assert.That(CharacterSetCatalogs.TryGetSelectedPortrait(UnitRole.Captain, out _), Is.False);
            }
            finally { Object.DestroyImmediate(other); }
        }

        private sealed class RecordingSink : IAnalyticsSink
        {
            public readonly System.Collections.Generic.List<AnalyticsEvent> Events = new System.Collections.Generic.List<AnalyticsEvent>();
            public void Emit(AnalyticsEvent value) => Events.Add(value);
        }

        [TestCase(CharacterSet.PaintSquad, "PaintSquad")]
        [TestCase(CharacterSet.Catgirl, "Catgirl")]
        public void RunStartCarriesTheCharacterSet(CharacterSet set, string expected)
        {
            CharacterSetSelection.Set(set);
            var sink = new RecordingSink();
            GameAnalytics.SetSink(sink);
            // BattleAnalytics subscribes OnRunStarted to BattleSceneFlow.RunStarted; call it directly.
            var handler = typeof(BattleAnalytics).GetMethod("OnRunStarted", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(handler, Is.Not.Null);
            handler.Invoke(null, null);
            var runStart = sink.Events.Single(e => e.Name == "run_start");
            Assert.That(runStart["character_set"], Is.EqualTo(expected));
            Assert.That(runStart.ToJson(), Does.Contain("\"character_set\":\"" + expected + "\""));
        }
    }
}
