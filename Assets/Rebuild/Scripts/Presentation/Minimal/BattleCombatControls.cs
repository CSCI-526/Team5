using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Disposable prototype observer/input adapter. No gameplay is computed here.
    [RequireComponent(typeof(BattleSession))]
    public sealed class BattleCombatControls : MonoBehaviour
    {
        private BattleSession session;
        private BattleSimulation bound;
        private readonly int[] draft = new int[9];
        private int selected = -1;
        private bool editing;
        private bool showDetails;
        private Vector2 detailsScroll;
        private string message;
        private readonly Dictionary<int, Transform> shots = new Dictionary<int, Transform>();
        private readonly Dictionary<Renderer, MaterialPropertyBlock> originalColors = new Dictionary<Renderer, MaterialPropertyBlock>();
        private MaterialPropertyBlock color;
        private Material shotMaterial;
        public bool ShowDebugPanel { get; set; } = true;

        private void OnEnable()
        {
            session = GetComponent<BattleSession>();
            color = new MaterialPropertyBlock();
        }
        private void ResetDraft()
        {
            for (var i = 0; i < 9; i++) draft[i] = i;
            selected = -1; message = null;
        }
        private void LateUpdate()
        {
            var sim = session.Simulation;
            if (sim != bound)
            {
                ClearVisuals(); bound = sim; editing = false; ResetDraft();
            }
            if (sim == null || sim.Objective != BattleObjective.ReachGoal) return;
            foreach (var shot in sim.Projectiles)
            {
                if (!shot.IsActive)
                {
                    if (shots.TryGetValue(shot.Id, out var old)) { Destroy(old.gameObject); shots.Remove(shot.Id); }
                    continue;
                }
                if (!shots.TryGetValue(shot.Id, out var view))
                {
                    var model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    model.name = $"Projectile View {shot.Id}";
                    model.transform.SetParent(transform, false);
                    model.transform.localScale = Vector3.one * 0.18f;
                    Destroy(model.GetComponent<Collider>());
                    if (shotMaterial == null) shotMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
                    var renderer = model.GetComponent<Renderer>();
                    renderer.sharedMaterial = shotMaterial;
                    color.SetColor("_BaseColor", shot.TargetKind == ProjectileTargetKind.Tower ? Color.yellow : Color.magenta);
                    renderer.SetPropertyBlock(color);
                    view = model.transform; shots.Add(shot.Id, view);
                }
                view.position = shot.Position + Vector3.up * 0.5f;
            }
            for (var i = 0; i < session.Towers.Count; i++)
            {
                var tower = session.Towers[i];
                if (tower == null || tower.RuntimeState == null) continue;
                foreach (var renderer in tower.GetComponentsInChildren<Renderer>())
                {
                    if (!originalColors.ContainsKey(renderer))
                    {
                        var original = new MaterialPropertyBlock(); renderer.GetPropertyBlock(original); originalColors.Add(renderer, original);
                    }
                    if (tower.RuntimeState.IsAlive) renderer.SetPropertyBlock(originalColors[renderer]);
                    else { color.SetColor("_BaseColor", Color.gray); renderer.SetPropertyBlock(color); }
                }
            }
        }

        private void OnGUI()
        {
            if (!ShowDebugPanel) return;
            var sim = session != null ? session.Simulation : null;
            if (sim == null || sim.Objective != BattleObjective.ReachGoal) return;
            var panelHeight = Mathf.Min(Screen.height - 20, editing ? 430 : showDetails ? 480 : 165);
            GUILayout.BeginArea(new Rect(10, 10, 300, panelHeight), GUI.skin.box);
            GUILayout.Label($"Squad Battle | {sim.Outcome} | {sim.ElapsedTime:F1}s");
            GUILayout.Label($"Alive {sim.AliveCount}  Pending {sim.PendingSpawnCount}");
            if (sim.Outcome == BattleOutcome.Running)
            {
                if (GUILayout.Button(sim.IsPaused ? "Resume" : "Pause"))
                {
                    sim.SetPaused(!sim.IsPaused); editing = false;
                }
                GUI.enabled = sim.PendingSpawnCount == 0;
                if (GUILayout.Button("Edit Formation"))
                {
                    sim.SetPaused(true); editing = true; ResetDraft();
                }
                GUI.enabled = true;
            }
            if (editing && sim.Outcome == BattleOutcome.Running)
            {
                GUILayout.Label("Select two slots to swap. Apply to confirm.");
                // Row 2 is +Z (top in the existing battle view); no legacy XY mapping.
                for (var row = 2; row >= 0; row--)
                {
                    GUILayout.BeginHorizontal();
                    for (var column = 0; column < 3; column++)
                    {
                        var slot = row * 3 + column;
                        var member = sim.GetMemberAtSlot(draft[slot]);
                        var label = $"{slot}: " + (member == null ? "Empty" : $"{member.Role}\n#{member.MemberId} {member.Hp:F1}");
                        if (GUILayout.Button((selected == slot ? "* " : "") + label, GUILayout.Width(91), GUILayout.Height(48)))
                        {
                            if (selected < 0) selected = slot;
                            else { var previous = draft[selected]; draft[selected] = draft[slot]; draft[slot] = previous; selected = -1; }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Apply & Resume"))
                {
                    if (sim.TryApplyFormation(draft)) { editing = false; sim.SetPaused(false); }
                    else message = "Formation rejected; no state was changed.";
                }
                if (GUILayout.Button("Cancel")) { editing = false; ResetDraft(); sim.SetPaused(false); }
                if (message != null) GUILayout.Label(message);
            }
            if (!editing)
            {
                if (GUILayout.Button(showDetails ? "Hide Details" : "Show Details")) showDetails = !showDetails;
                if (showDetails)
                {
                    detailsScroll = GUILayout.BeginScrollView(detailsScroll, GUILayout.Height(Mathf.Min(280, Screen.height * 0.4f)));
                    foreach (var unit in sim.Units)
                        GUILayout.Label($"#{unit.MemberId} {unit.Role} | Slot {unit.SlotId} | {unit.Hp:F1}/{unit.MaxHp} | {unit.LifeState}");
                    foreach (var tower in sim.Towers)
                        GUILayout.Label($"Tower {tower.Id} | {tower.Hp:F1}/{tower.Definition.MaxHp} | {(tower.IsAlive ? "Alive" : "Destroyed")}");
                    GUILayout.EndScrollView();
                }
            }
            if (GUILayout.Button("Restart Battle")) session.StartBattle();
            GUILayout.EndArea();
        }

        private void ClearVisuals()
        {
            foreach (var view in shots.Values) if (view != null) Destroy(view.gameObject);
            shots.Clear();
            foreach (var pair in originalColors) if (pair.Key != null) pair.Key.SetPropertyBlock(pair.Value);
            originalColors.Clear();
        }
        private void OnDisable()
        {
            ClearVisuals(); bound = null;
            if (shotMaterial != null) Destroy(shotMaterial);
            // Disabling this component never resumes, resets or otherwise changes Simulation.
        }
    }
}
