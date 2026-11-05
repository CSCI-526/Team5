using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class NextMapTower
    {
        public NextMapTower(TowerDefinition definition, Bounds model, bool hasModel)
        {
            Definition = definition;
            Model = model;
            HasModel = hasModel;
        }

        public TowerDefinition Definition { get; }
        public Bounds Model { get; }
        public bool HasModel { get; }

        public Bounds Lane => SquadCombatRules.AreaBounds(Definition.Position, Definition.Area, Definition.Range, Definition.AttackWidth);

        public Vector3 BadgeAnchor => HasModel
            ? new Vector3(Model.center.x, Model.max.y, Model.center.z)
            : Definition.Position + Vector3.up * 1.2f;
    }
}
