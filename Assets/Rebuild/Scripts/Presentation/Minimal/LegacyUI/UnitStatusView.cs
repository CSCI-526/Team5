using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    [DisallowMultipleComponent, DefaultExecutionOrder(170)]
    public sealed class UnitStatusView : MonoBehaviour
    {
        private const float HoldDelay = 0.12f;

        private readonly Dictionary<int, UnitStatusRig> rigs = new Dictionary<int, UnitStatusRig>();
        private readonly List<UnitStatusRig> spare = new List<UnitStatusRig>();
        private BattleSession session;
        private BattleSimulation bound;
        private BattlePresentation presentation;
        private MinionMaterials materials;
        private Material shellMaterial;
        private Material frostMaterial;
        private Material flameMaterial;
        private Material emberMaterial;
        private UnitShatter shatter;
        private Transform effects;
        private Camera view;
        private bool sorted;

        public void Bind(BattleSession owner, MinionMaterials shared)
        {
            if (owner == null || shared == null) return;
            session = owner;
            materials = shared;
            effects = new GameObject("Unit Status").transform;
            effects.SetParent(transform, false);
            shellMaterial = materials.Glow(MinionPalette.IceShell, materials.Facets);
            frostMaterial = materials.Glow(new Color(1f, 1f, 1f, 0.85f), materials.Frost);
            flameMaterial = materials.Glow(Color.white, materials.Flame);
            emberMaterial = materials.Glow(MinionPalette.Ember, materials.Disc);
            shatter = new UnitShatter(effects, materials);
            sorted = CharacterSetSelection.Current != CharacterSet.PaintSquad;
            Rebind(session.Simulation);
        }

        public void Rebind(BattleSimulation next)
        {
            ReleaseAll();
            if (shatter != null) shatter.Clear();
            bound = next;
            presentation = null;
            if (session == null) return;
            foreach (var candidate in FindObjectsByType<BattlePresentation>(FindObjectsSortMode.None))
                if (candidate.Session == session)
                {
                    presentation = candidate;
                    break;
                }
        }

        private void LateUpdate()
        {
            if (session == null || shatter == null) return;
            var simulation = session.Simulation;
            if (simulation != bound) Rebind(simulation);
            shatter.Tick(Time.deltaTime);
            if (simulation == null || simulation.Outcome != BattleOutcome.Running)
            {
                ReleaseAll();
                return;
            }
            if (view == null) view = Camera.main;
            var facing = view != null ? view.transform.rotation : Quaternion.identity;
            var forward = view != null ? view.transform.forward : Vector3.forward;
            var toward = new Vector3(-forward.x, 0f, -forward.z);
            toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.back;
            var side = view != null ? view.transform.right : Vector3.right;
            var now = simulation.ElapsedTime;
            var time = Time.time;
            foreach (var unit in simulation.Units)
            {
                var frozen = unit.IsAlive && unit.FrozenRemaining > 0f;
                var burning = unit.IsAlive && unit.BurnTicksLeft > 0;
                rigs.TryGetValue(unit.Id, out var rig);
                if (!frozen && !burning)
                {
                    if (rig != null) Finish(rig);
                    continue;
                }
                UnitView unitView = null;
                if (presentation != null) presentation.TryGetView(unit.Id, out unitView);
                if (rig == null) rig = Acquire(unit.Id);
                var foot = unitView != null ? unitView.transform.position : unit.Position;
                if (rig.Show(unitView, foot, frozen, burning, now, time, facing, toward, side)) shatter.Play(rig.ShellCenter, rig.Floor, unit.Id);
                if (frozen && now - rig.FrozenSince >= HoldDelay) rig.HoldPose();
            }
        }

        private UnitStatusRig Acquire(int unitId)
        {
            UnitStatusRig rig;
            if (spare.Count > 0)
            {
                rig = spare[spare.Count - 1];
                spare.RemoveAt(spare.Count - 1);
            }
            else rig = new UnitStatusRig(effects, materials, shellMaterial, frostMaterial, flameMaterial, emberMaterial, sorted);
            rig.Begin(unitId);
            rigs[unitId] = rig;
            return rig;
        }

        private void Finish(UnitStatusRig rig)
        {
            if (rig.Frozen) shatter.Play(rig.ShellCenter, rig.Floor, rig.UnitId);
            rigs.Remove(rig.UnitId);
            rig.Release();
            spare.Add(rig);
        }

        private void ReleaseAll()
        {
            foreach (var rig in rigs.Values)
            {
                rig.Release();
                spare.Add(rig);
            }
            rigs.Clear();
        }

        private void OnDisable()
        {
            ReleaseAll();
            if (shatter != null) shatter.Clear();
        }
    }
}
