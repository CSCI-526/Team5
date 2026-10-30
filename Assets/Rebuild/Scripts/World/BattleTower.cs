using System;
using System.Collections.Generic;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.World
{
    [DisallowMultipleComponent]
    public sealed class BattleTower : MonoBehaviour
    {
        [SerializeField, Min(0)] private float range = 2.4f;
        [SerializeField, Min(0.01f)] private float damage = 18;
        [SerializeField, Min(0.01f)] private float attackInterval = 0.8f;
        [SerializeField] private TargetRule targetRule = TargetRule.FurthestAlongRoute;
        [SerializeField] private bool usesMacroFootprint;
        [SerializeField, Min(0.01f)] private float maxHp = 20;
        [SerializeField] private AttackArea attackArea = AttackArea.Square;
        [SerializeField, Min(0.01f)] private float attackWidth = 0.3f;
        public int FootprintSize => usesMacroFootprint ? 3 : 1;
        public float Range => range;
        public float Damage => damage;
        public float AttackInterval => attackInterval;
        public TargetRule TargetRule => targetRule;
        public TowerState RuntimeState { get; private set; }
        public float CurrentCooldown => RuntimeState?.CooldownRemaining ?? 0;
        public TowerDefinition CaptureDefinition(int id) => new TowerDefinition(id, transform.position, range, damage, attackInterval, targetRule, FootprintSize, maxHp, attackArea, attackWidth);
        public void BindRuntimeState(TowerState state) => RuntimeState = state;

        public bool HasValidConfiguration => float.IsFinite(range) && range >= 0 &&
            float.IsFinite(damage) && damage > 0 && float.IsFinite(attackInterval) && attackInterval > 0 &&
            float.IsFinite(transform.position.x) && float.IsFinite(transform.position.y) && float.IsFinite(transform.position.z) &&
            Enum.IsDefined(typeof(TargetRule), targetRule) && float.IsFinite(maxHp) && maxHp > 0 &&
            float.IsFinite(attackWidth) && attackWidth > 0 && Enum.IsDefined(typeof(AttackArea), attackArea);

        public void CollectFootprintErrors(BattleGrid grid, HashSet<Vector3Int> occupied, List<string> errors)
        {
            var position = transform.position;
            var center = SmallCellOccupancy.PositionToCell(position);
            if (usesMacroFootprint)
            {
                var macro = BattleGrid.ToMacroCell(center);
                var expected = new Vector3(macro.x * 3 + 1, center.y * 0.5f + 0.25f, macro.z * 3 + 1);
                if (Vector3.Distance(position, expected) > 0.001f)
                    errors.Add($"{name}: 3x3 tower must be centered on a Macro Cell at its top surface.");
            }
            var radius = FootprintSize / 2;
            for (var z = -radius; z <= radius; z++)
            for (var x = -radius; x <= radius; x++)
            {
                var key = center + new Vector3Int(x, 0, z);
                if (!occupied.Add(key)) errors.Add($"{name}: tower footprint overlap at {key}.");
                if (!grid.TryGetCell(key, out var support) || support.Kind != CellKind.TowerPlatform ||
                    !grid.HasClearTop(support) || Mathf.Abs(position.y - support.TopCenter.y) > 0.001f)
                    errors.Add($"{name}: footprint {key} needs a clear, equal-height TowerPlatform surface.");
            }
        }
    }
}
