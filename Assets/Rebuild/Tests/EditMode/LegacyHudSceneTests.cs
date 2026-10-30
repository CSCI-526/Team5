using NUnit.Framework;
using Team5.Rebuild.Editor;
using Team5.Rebuild.Presentation.LegacyUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Tests
{
    public sealed class LegacyHudSceneTests
    {
        [TearDown] public void Close() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        [Test]
        public void HudUsesCompactControlsWithoutCurrencyAndKeepsPersistentCallbacks()
        {
            EditorSceneManager.OpenScene(SquadBattleMenu.ScenePath);
            var hud = Object.FindFirstObjectByType<LegacyBattleHud>();
            Assert.That(hud, Is.Not.Null); Assert.That(hud.Session, Is.Not.Null);
            var scaler = hud.GetComponent<CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(500, 500)));
            Assert.That(scaler.matchWidthOrHeight, Is.Zero);
            // Positions are authored by the user in Scene; do not enforce the old imported coordinates.
            Assert.That(((RectTransform)hud.SpeedSlider.transform).sizeDelta, Is.EqualTo(new Vector2(80, 15)));
            Assert.That(((RectTransform)hud.EditButton.transform).sizeDelta, Is.EqualTo(new Vector2(80, 25)));
            Assert.That(hud.SpeedSlider.transform.localScale, Is.EqualTo(Vector3.one * 0.75f));
            Assert.That(hud.EditButton.transform.localScale, Is.EqualTo(Vector3.one * 0.75f));
            Assert.That(hud.transform.Find("Gold"), Is.Null);
            Assert.That(hud.EditButton.GetComponentInChildren<TMPro.TMP_Text>().font.name, Is.EqualTo("LiberationSans SDF"));
            Assert.That(hud.SpeedSlider.wholeNumbers, Is.True);
            Assert.That(hud.SpeedSlider.maxValue, Is.EqualTo(2));
            Assert.That(hud.SpeedSlider.onValueChanged.GetPersistentTarget(0), Is.SameAs(hud));
            Assert.That(hud.SpeedSlider.onValueChanged.GetPersistentMethodName(0), Is.EqualTo("SetSpeedFromSlider"));
            Assert.That(hud.EditButton.onClick.GetPersistentTarget(0), Is.SameAs(hud));
            Assert.That(hud.EditButton.onClick.GetPersistentMethodName(0), Is.EqualTo("OpenSquadEditor"));
        }
    }
}
