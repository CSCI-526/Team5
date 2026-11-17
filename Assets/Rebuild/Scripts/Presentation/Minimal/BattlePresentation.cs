using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    [System.Serializable]
    public sealed class MemberVisualOverride
    {
        public int memberId;
        public UnitView prefab;
    }

    [DisallowMultipleComponent]
    public sealed class BattlePresentation : MonoBehaviour
    {
        [SerializeField] private BattleSession session;
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private bool showTowerRanges = true;
        [SerializeField] private bool showUnitLevels = true;
        [Header("Optional single-member visual test (disabled in production scenes)")]
        [SerializeField] private bool useTestUnitVisual;
        [SerializeField] private int testVisualMemberId;
        [SerializeField] private UnitView testUnitPrefab;
        [SerializeField] private List<MemberVisualOverride> additionalTestVisuals = new List<MemberVisualOverride>();
        private readonly Dictionary<int, UnitView> views = new Dictionary<int, UnitView>();
        private readonly List<TowerRangeView> ranges = new List<TowerRangeView>();
        private BattleSimulation simulation;
        private BattleUnitInspection inspection;
        private WallOcclusionView wallOcclusion;
        private RectTransform levelBadgeLayer;
        public BattleSession Session => session;
        public UnitView UnitPrefab => unitPrefab;
        public int ViewCount => views.Count;
        public bool ShowTowerRanges
        {
            get => showTowerRanges;
            set { showTowerRanges = value; foreach (var range in ranges) if (range != null) range.SetVisible(value); }
        }
        public bool TryGetView(int unitId, out UnitView view) => views.TryGetValue(unitId, out view);
        public void SetTestVisual(int memberId, UnitView prefab)
        {
            useTestUnitVisual = prefab != null;
            testVisualMemberId = memberId;
            testUnitPrefab = prefab;
            if (simulation == null) return;
            // Replace only this member's view, preserving the simulation and all other views.
            foreach (var unit in simulation.Units)
            {
                if (unit.MemberId != memberId || !unit.IsAlive) continue;
                if (views.TryGetValue(unit.Id, out var old)) { views.Remove(unit.Id); RemoveView(old); }
                CreateView(unit);
            }
        }
        private void OnValidate()
        {
            foreach (var range in ranges) if (range != null) range.SetVisible(showTowerRanges);
        }

        private void OnEnable()
        {
            if (BattleSceneFlow.IsPreviewScene(gameObject.scene)) return;
            if (session == null || unitPrefab == null)
            {
                Debug.LogWarning("BattlePresentation needs a session and UnitView prefab; combat can still run.", this);
                return;
            }
            wallOcclusion = GetComponent<WallOcclusionView>();
            if (wallOcclusion == null) wallOcclusion = gameObject.AddComponent<WallOcclusionView>();
            wallOcclusion.Bind(this);
            wallOcclusion.enabled = true;
            if (session.ContinuousSquadMovement)
            {
                var movementView = GetComponent<ContinuousMovementView>();
                if (movementView == null) movementView = gameObject.AddComponent<ContinuousMovementView>();
                movementView.Bind(this); movementView.enabled = true;
            }
            if (session.ManualSquadMovement && !session.ContinuousSquadMovement)
            {
                var routeInput = GetComponent<SquadRouteInput>();
                if (routeInput == null) routeInput = gameObject.AddComponent<SquadRouteInput>();
                routeInput.Bind(this);
            }
            session.SimulationCreated += Bind;
            inspection = GetComponent<BattleUnitInspection>();
            if (inspection == null) inspection = gameObject.AddComponent<BattleUnitInspection>();
            inspection.Initialize(this);
            inspection.enabled = true;
            Bind(session.Simulation);
        }

        private void OnDisable()
        {
            var movementView = GetComponent<ContinuousMovementView>();
            if (movementView != null) movementView.enabled = false;
            var routeInput = GetComponent<SquadRouteInput>();
            if (routeInput != null) routeInput.Cancel();
            if (wallOcclusion != null) wallOcclusion.enabled = false;
            if (inspection != null) inspection.enabled = false;
            if (session != null) session.SimulationCreated -= Bind;
            Unbind();
            ClearViews();
            ClearRanges();
            if (levelBadgeLayer != null)
            {
                levelBadgeLayer.gameObject.SetActive(false);
                Destroy(levelBadgeLayer.gameObject);
                levelBadgeLayer = null;
            }
        }

        private void Bind(BattleSimulation next)
        {
            var routeInput = GetComponent<SquadRouteInput>();
            if (routeInput != null) routeInput.Cancel();
            if (inspection != null) inspection.CloseImmediately();
            Unbind();
            ClearViews();
            ClearRanges();
            simulation = next;
            if (simulation == null) return;
            foreach (var tower in simulation.Towers)
            {
                var range = new GameObject($"Tower Range {tower.Id}").AddComponent<TowerRangeView>();
                range.transform.SetParent(transform, false);
                range.Bind(tower);
                range.SetVisible(showTowerRanges);
                ranges.Add(range);
            }
            simulation.UnitSpawned += OnSpawned;
            simulation.UnitMoved += OnMoved;
            simulation.UnitDied += OnUnitEnded;
            simulation.UnitReachedGoal += OnUnitEnded;
            simulation.FormationChanged += OnFormationChanged;
            simulation.HealerPulsed += OnHealerPulsed;
            foreach (var unit in simulation.Units) if (unit.IsAlive) CreateView(unit);
        }

        private void Unbind()
        {
            if (simulation == null) return;
            simulation.UnitSpawned -= OnSpawned;
            simulation.UnitMoved -= OnMoved;
            simulation.UnitDied -= OnUnitEnded;
            simulation.UnitReachedGoal -= OnUnitEnded;
            simulation.FormationChanged -= OnFormationChanged;
            simulation.HealerPulsed -= OnHealerPulsed;
            simulation = null;
        }

        private void OnHealerPulsed(int healerId)
        {
            if (views.TryGetValue(healerId, out var view) && view != null)
                view.TriggerHealBurst();
        }

        private void OnSpawned(UnitSpawnedEvent value)
        {
            foreach (var unit in simulation.Units)
                if (unit.Id == value.UnitId) { CreateView(unit); break; }
        }

        private void CreateView(UnitState unit)
        {
            if (views.ContainsKey(unit.Id)) return;
            var prefab = useTestUnitVisual && unit.MemberId == testVisualMemberId && testUnitPrefab != null
                ? testUnitPrefab : unitPrefab;
            if (useTestUnitVisual)
                foreach (var entry in additionalTestVisuals)
                    if (entry != null && entry.memberId == unit.MemberId && entry.prefab != null)
                    { prefab = entry.prefab; break; }
            var view = Instantiate(prefab, transform);
            view.name = $"Unit View {unit.Id}";
            view.Bind(unit);
            if (session.ContinuousSquadMovement) view.SetFormationDisplaySpacing(session.ContinuousFormationSpacing);
            var animatedCharacter = view.GetComponent<HaonUnitAnimation>();
            if (animatedCharacter != null) animatedCharacter.Bind(view, session);
            var spriteCharacter = view.GetComponent<FWUnitVisual>();
            if (spriteCharacter != null) spriteCharacter.Bind(view, session);
            if (showUnitLevels && levelBadgeLayer == null)
            {
                var host = new GameObject("Unit Level Badges", typeof(RectTransform));
                host.transform.SetParent(transform, false);
                var canvas = LegacyUI.LegacyUiKit.CreateCanvas(host, 5, new Vector2(1280, 720), 0.5f);
                // Informational labels must never intercept battlefield clicks or drags.
                var raycaster = host.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                raycaster.enabled = false;
                levelBadgeLayer = (RectTransform)canvas.transform;
            }
            if (showUnitLevels) view.CreateLevelBadge(levelBadgeLayer);
            views.Add(unit.Id, view);
        }

        private void OnMoved(UnitMovedEvent value)
        {
            if (views.TryGetValue(value.UnitId, out var view)) view.CaptureState();
        }

        private void OnFormationChanged()
        {
            if (session.ContinuousSquadMovement) return;
            foreach (var view in views.Values) view.SnapToState();
        }

        private void OnUnitEnded(UnitEndedEvent value)
        {
            if (!views.TryGetValue(value.UnitId, out var view)) return;
            views.Remove(value.UnitId);
            RemoveView(view);
        }

        private static void RemoveView(UnitView view)
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        private void ClearViews()
        {
            foreach (var view in views.Values) RemoveView(view);
            views.Clear();
        }

        private void ClearRanges()
        {
            foreach (var range in ranges)
            {
                if (range == null) continue;
                range.gameObject.SetActive(false);
                Destroy(range.gameObject);
            }
            ranges.Clear();
        }
    }
}
