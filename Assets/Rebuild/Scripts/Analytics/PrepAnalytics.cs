using System;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Analytics
{
    // One upgrade-scene visit. The visit ends on the next full scene load.
    public static class PrepAnalytics
    {
        private static SquadPreparation preparation;
        private static float enteredAt;
        private static bool previewed;
        private static int summons;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { preparation = null; previewed = false; summons = 0; }

        public static void Enter(SquadPreparation value)
        {
            if (!GameAnalytics.IsActive || value == null) return;
            try
            {
                preparation = value;
                enteredAt = Time.realtimeSinceStartup;
                previewed = false;
                summons = 0;
                var shop = BattleSceneFlow.CardShop;
                GameAnalytics.Track("prep_enter",
                    ("gold", BattleSceneFlow.GoldBalance),
                    ("warehouse_count", value.Members.Count),
                    ("deployed_count", value.DeployedCount),
                    ("summon_price", shop != null ? shop.NextPrice : 0));
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }

        public static string Describe(UnitDefinition unit) => unit != null ? $"{unit.Role}/{unit.Rarity}/Lv{unit.Level}" : "";

        internal static void Observe(AnalyticsEvent value)
        {
            if (preparation == null) return;
            if (value.Name == "map_preview_open") previewed = true;
            else if (value.Name == "summon_purchase") summons++;
        }

        internal static void Leave()
        {
            if (preparation == null) return;
            var visit = preparation;
            preparation = null;
            try
            {
                var deployed = new List<UnitDefinition>();
                for (var slot = 0; slot < 9; slot++)
                {
                    var member = visit.FindMember(visit.MemberAtSlot(slot));
                    if (member != null) deployed.Add(member.Unit);
                }
                GameAnalytics.Track("prep_exit",
                    ("dwell_s", Time.realtimeSinceStartup - enteredAt),
                    ("gold_unspent", BattleSceneFlow.GoldBalance),
                    ("deployed_count", deployed.Count),
                    ("roles", BattleAnalyticsTracker.Roles(deployed)),
                    ("avg_unit_level", deployed.Count > 0 ? deployed.Average(unit => unit.Level) : 0),
                    ("max_rarity", BattleAnalyticsTracker.MaxRarity(deployed)),
                    ("previewed", previewed),
                    ("summons_this_visit", summons));
            }
            catch (Exception error) { GameAnalytics.Fail(error); }
        }
    }
}
