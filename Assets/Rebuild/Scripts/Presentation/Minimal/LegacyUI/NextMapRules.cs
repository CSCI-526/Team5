using System;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class NextMapRules
    {
        public const float SampleStep = 0.05f;

        public static bool Inside(TowerDefinition tower, Vector3 position) =>
            SquadCombatRules.Contains(position - tower.Position, tower.Area, tower.Range, tower.AttackWidth);

        public static float FirstContact(NextMapRoute route, TowerDefinition tower)
        {
            for (var distance = 0f; distance <= route.Length + 0.0001f; distance += SampleStep)
            {
                var anchor = route.Sample(distance);
                for (var slot = 0; slot < 9; slot++)
                    if (Inside(tower, anchor + SquadCombatRules.Offset(slot))) return distance;
            }
            return float.PositiveInfinity;
        }

        public static int[] RoadOrder(NextMapRoute route, IReadOnlyList<NextMapTower> towers)
        {
            var contact = new float[towers.Count];
            var order = new int[towers.Count];
            for (var i = 0; i < towers.Count; i++)
            {
                contact[i] = FirstContact(route, towers[i].Definition);
                order[i] = i;
            }
            Array.Sort(order, (a, b) =>
            {
                var compare = contact[a].CompareTo(contact[b]);
                return compare != 0 ? compare : a.CompareTo(b);
            });
            return order;
        }
    }
}
