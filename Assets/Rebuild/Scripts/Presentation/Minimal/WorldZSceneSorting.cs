using System.Collections.Generic;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Team5.Rebuild.Presentation
{
    // Scene-local companion for FW world-Z sprites. Opaque geometry retains its normal depth buffer.
    [DefaultExecutionOrder(350)]
    public sealed class WorldZSceneSorting : MonoBehaviour
    {
        private sealed class Entry
        {
            public Renderer Renderer;
            public Transform Anchor;
            public int OriginalOrder;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Material> materials = new List<Material>();
        private readonly HashSet<Renderer> known = new HashSet<Renderer>();
        private float nextScan;

        public static int Order(float worldZ) => Mathf.Clamp(Mathf.RoundToInt(-worldZ * 100), -30000, 30000);

        private void Scan()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (known.Contains(renderer) || renderer.GetComponentInParent<Canvas>() != null ||
                        renderer.GetComponentInParent<SortingGroup>() != null ||
                        renderer.GetComponent<TacticalRoadView>() != null ||
                        renderer.GetComponentInParent<TowerRangeView>() != null ||
                        (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null &&
                         renderer.sharedMaterial.shader.name == "Team5/Rebuild/BattlefieldContactShadow")) continue;
                    renderer.GetSharedMaterials(materials);
                    var tower = renderer.GetComponentInParent<BattleTower>();
                    var transparent = renderer is LineRenderer || renderer is SpriteRenderer || tower != null;
                    foreach (var material in materials)
                        if (material != null && material.renderQueue >= 2501) transparent = true;
                    if (!transparent) continue;
                    var unit = renderer.GetComponentInParent<UnitView>();
                    entries.Add(new Entry { Renderer = renderer, OriginalOrder = renderer.sortingOrder,
                        Anchor = tower != null ? tower.transform : unit != null ? unit.transform : null });
                    known.Add(renderer);
                }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan) { Scan(); nextScan = Time.unscaledTime + .5f; }
            foreach (var entry in entries)
                if (entry.Renderer != null)
                    entry.Renderer.sortingOrder = Order(entry.Anchor != null ? entry.Anchor.position.z : entry.Renderer.bounds.center.z);
        }

        private void OnDisable()
        {
            foreach (var entry in entries)
                if (entry.Renderer != null) entry.Renderer.sortingOrder = entry.OriginalOrder;
            entries.Clear(); known.Clear(); nextScan = 0;
        }
    }
}
