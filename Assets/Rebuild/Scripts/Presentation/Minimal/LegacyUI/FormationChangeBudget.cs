using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    [DisallowMultipleComponent]
    public sealed class FormationChangeBudget : MonoBehaviour
    {
        private BattleSession session;
        private BattleSimulation tracked;
        private bool subscribed;
        private int used;

        public int Allowed => session != null ? FormationChangeRules.AllowedChanges(session.DisplayedLevelNumber) : FormationChangeRules.Unlimited;
        public bool IsLimited => Allowed != FormationChangeRules.Unlimited;
        public bool CaptainDefeated
        {
            get
            {
                if(tracked==null)return false;
                var defeated=false;
                foreach(var unit in tracked.Units)
                {
                    if(unit.Role!=UnitRole.Captain)continue;
                    if(unit.IsAlive)return false;
                    if(unit.LifeState==UnitLifeState.Dead)defeated=true;
                }
                return defeated;
            }
        }
        public int Remaining => CaptainDefeated ? 0 : IsLimited ? Mathf.Max(0, Allowed - used) : FormationChangeRules.Unlimited;
        public bool HasChangesLeft => !CaptainDefeated && (!IsLimited || used < Allowed);

        public static FormationChangeBudget Attach(LegacyBattleHud hud, Button squadButton)
        {
            if (hud == null || hud.Session == null || squadButton == null) return null;
            var budget = hud.GetComponent<FormationChangeBudget>();
            if (budget == null) budget = hud.gameObject.AddComponent<FormationChangeBudget>();
            budget.Bind(hud, squadButton);
            return budget;
        }

        public static void Spend(LegacyBattleHud owner)
        {
            if (owner == null || owner.FormationBudget == null) return;
            owner.FormationBudget.SpendOne();
        }

        public static void ShowNote(LegacyBattleHud owner, Transform content)
        {
            if (owner == null || owner.FormationBudget == null || content == null || !owner.FormationBudget.IsLimited) return;
            var line = content.Find("Paused");
            var label = line != null ? line.GetComponent<TMP_Text>() : null;
            if (label != null) label.text = FormationChangeRules.EditorNote(owner.FormationBudget.Remaining);
        }

        private void Bind(LegacyBattleHud hud, Button squadButton)
        {
            if (session == null)
            {
                session = hud.Session;
                FormationChangeBadge.Attach(this, hud, squadButton);
            }
            Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (subscribed && session != null) session.SimulationCreated -= OnSimulationCreated;
            subscribed = false;
        }

        private void Subscribe()
        {
            if (subscribed || session == null) return;
            session.SimulationCreated += OnSimulationCreated;
            subscribed = true;
            OnSimulationCreated(session.Simulation);
        }

        private void OnSimulationCreated(BattleSimulation next)
        {
            if (next == null || next == tracked) return;
            tracked = next;
            used = 0;
        }

        private void SpendOne()
        {
            if (IsLimited && used < Allowed) used++;
        }
    }
}
