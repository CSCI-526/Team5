using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    // Old HealthBar palette and left-aligned fill, adapted to an overlay for the 3D scene.
    public sealed class LegacyHealthBars : MonoBehaviour
    {
        private sealed class Bar { public RectTransform Root; public Image Fill; public float Width, Height, Inset; }
        private static readonly Color backgroundColor = new Color(0.055f, 0.067f, 0.082f, 0.92f);
        private static readonly Color fullColor = new Color(0.35f, 0.85f, 0.4f);
        private static readonly Color emptyColor = new Color(0.9f, 0.25f, 0.2f);
        private readonly Dictionary<int, Bar> unitBars = new Dictionary<int, Bar>();
        private readonly Dictionary<int, Bar> towerBars = new Dictionary<int, Bar>();
        private readonly HashSet<int> damaged = new HashSet<int>();
        private BattleSimulation simulation;
        private BattleSession session;
        private BattlePresentation presentation;
        public void Bind(BattleSession owner, BattleSimulation next)
        {
            if (simulation != null) simulation.UnitDamaged -= OnDamaged;
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            unitBars.Clear(); towerBars.Clear(); damaged.Clear();
            session = owner; simulation = next;
            presentation = null;
            foreach (var view in Object.FindObjectsByType<BattlePresentation>(FindObjectsSortMode.None))
                if (view.Session == owner) { presentation = view; break; }
            if (simulation != null) simulation.UnitDamaged += OnDamaged;
        }
        private void OnDamaged(DamageEvent value) => damaged.Add(value.UnitId);
        private void LateUpdate()
        {
            var camera = Camera.main;
            if (simulation == null || camera == null) return;
            foreach (var unit in simulation.Units)
            {
                if (!unitBars.TryGetValue(unit.Id, out var bar))
                { bar = Create("Unit HP " + unit.Id, 9, 1.5f, 0.3f); unitBars.Add(unit.Id, bar); }
                var position = unit.Position + Vector3.up * 0.9f;
                Transform model = null;
                if (presentation != null && presentation.TryGetView(unit.Id, out var view)) model = view.transform;
                Present(bar, position, unit.Hp / unit.MaxHp, unit.IsAlive && (damaged.Contains(unit.Id) || unit.Hp < unit.MaxHp), camera, model);
            }
            for (var i = 0; i < simulation.Towers.Count; i++)
            {
                var tower = simulation.Towers[i];
                if (!towerBars.TryGetValue(tower.Id, out var bar))
                { bar = Create("Tower HP " + tower.Id, 28, 3.5f, 0.6f); towerBars.Add(tower.Id, bar); }
                Present(bar, tower.Position + Vector3.up * 1.5f, tower.Hp / tower.Definition.MaxHp, tower.IsAlive, camera, session.Towers[i].transform);
            }
        }
        private Bar Create(string name, float width, float height, float inset)
        {
            var background = LegacyUiKit.CreateImage(transform, name, Vector2.zero, new Vector2(width, height), backgroundColor);
            var rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            var fill = LegacyUiKit.CreateImage(rect, "Fill", Vector2.zero, new Vector2(width - inset * 2, height - inset * 2), fullColor);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0, 0.5f);
            fill.rectTransform.pivot = new Vector2(0, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(inset, 0);
            return new Bar { Root = rect, Fill = fill, Width = width, Height = height, Inset = inset };
        }
        private void Present(Bar bar, Vector3 position, float fraction, bool visible, Camera camera, Transform model)
        {
            var screen = camera.WorldToScreenPoint(position);
            if (model != null)
            {
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    var top = float.MinValue;
                    for (var z = -1; z <= 1; z += 2)
                    for (var y = -1; y <= 1; y += 2)
                    for (var x = -1; x <= 1; x += 2)
                        top = Mathf.Max(top, camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))).y);
                    screen = camera.WorldToScreenPoint(bounds.center);
                    screen.y = top + (bar.Height / 2 + 1) * GetComponentInParent<Canvas>().scaleFactor;
                }
            }
            bar.Root.gameObject.SetActive(visible && screen.z > 0);
            if (!visible) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out var local))
                bar.Root.anchoredPosition = local;
            fraction = Mathf.Clamp01(fraction);
            bar.Fill.rectTransform.sizeDelta = new Vector2((bar.Width - bar.Inset * 2) * fraction, bar.Height - bar.Inset * 2);
            bar.Fill.color = Color.Lerp(emptyColor, fullColor, fraction);
        }
        private void OnDisable() { if (simulation != null) simulation.UnitDamaged -= OnDamaged; }
    }
}
