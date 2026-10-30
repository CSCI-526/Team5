using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    [DisallowMultipleComponent]
    public sealed class BattlePresentation : MonoBehaviour
    {
        [SerializeField] private BattleSession session;
        [SerializeField] private UnitView unitPrefab;
        [SerializeField] private bool showTowerRanges = true;
        private readonly Dictionary<int, UnitView> views = new Dictionary<int, UnitView>();
        private readonly List<TowerRangeView> ranges = new List<TowerRangeView>();
        private BattleSimulation simulation;
        private BattleUnitInspection inspection;
        public BattleSession Session => session;
        public UnitView UnitPrefab => unitPrefab;
        public int ViewCount => views.Count;
        public bool ShowTowerRanges
        {
            get => showTowerRanges;
            set { showTowerRanges = value; foreach (var range in ranges) if (range != null) range.SetVisible(value); }
        }
        public bool TryGetView(int unitId, out UnitView view) => views.TryGetValue(unitId, out view);
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
            session.SimulationCreated += Bind;
            inspection = GetComponent<BattleUnitInspection>();
            if (inspection == null) inspection = gameObject.AddComponent<BattleUnitInspection>();
            inspection.Initialize(this);
            inspection.enabled = true;
            Bind(session.Simulation);
        }

        private void OnDisable()
        {
            if (inspection != null) inspection.enabled = false;
            if (session != null) session.SimulationCreated -= Bind;
            Unbind();
            ClearViews();
            ClearRanges();
        }

        private void Bind(BattleSimulation next)
        {
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
            simulation = null;
        }

        private void OnSpawned(UnitSpawnedEvent value)
        {
            // Look up the read-only state by ID, rather than giving the View simulation commands.
            foreach (var unit in simulation.Units)
                if (unit.Id == value.UnitId) { CreateView(unit); break; }
        }

        private void CreateView(UnitState unit)
        {
            if (views.ContainsKey(unit.Id)) return;
            var view = Instantiate(unitPrefab, transform);
            view.name = $"Unit View {unit.Id}";
            view.Bind(unit);
            views.Add(unit.Id, view);
        }

        private void OnMoved(UnitMovedEvent value)
        {
            if (views.TryGetValue(value.UnitId, out var view)) view.CaptureState();
        }

        private void OnFormationChanged()
        {
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
            view.gameObject.SetActive(false); // Hide immediately; Destroy completes at the frame boundary.
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
