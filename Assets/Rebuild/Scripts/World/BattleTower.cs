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
        [SerializeField] private bool isAoe;
        [SerializeField, Min(0)] private int goldBounty = 25;
        [SerializeField] private bool isWall;
        [SerializeField] private bool wallAlongZ;
        public bool IsWall => isWall;
        public Vector2Int Footprint => isWall ? WallFootprintForYaw(wallAlongZ, transform.eulerAngles.y) : new Vector2Int(FootprintSize, FootprintSize);
        public static Vector2Int WallFootprintForYaw(bool localAlongZ, float yaw)
        {
            var alongZ = localAlongZ ^ ((Mathf.RoundToInt(yaw / 90f) & 1) != 0);
            return alongZ ? new Vector2Int(1, 3) : new Vector2Int(3, 1);
        }

        public int FootprintSize => usesMacroFootprint ? 3 : 1;
        public float Range => isWall ? 0 : range;
        public float Damage => damage;
        public float AttackInterval => attackInterval;
        public TargetRule TargetRule => targetRule;
        public bool IsAoe => !isWall && isAoe;
        public int GoldBounty => isWall ? 0 : goldBounty > 0 ? goldBounty : (isAoe ? 40 : 25);
        public TowerState RuntimeState { get; private set; }
        public float CurrentCooldown => RuntimeState?.CooldownRemaining ?? 0;

        public TowerDefinition CaptureDefinition(int id) =>
            new TowerDefinition(id, transform.position, Range, isWall ? 0 : damage, attackInterval, targetRule, FootprintSize, maxHp, attackArea, attackWidth, IsAoe, GoldBounty, isWall, Footprint.y == 3);

        public void BindRuntimeState(TowerState state) => RuntimeState = state;

        public bool HasValidConfiguration => float.IsFinite(range) && range >= 0 &&
            (isWall || (float.IsFinite(damage) && damage > 0)) && float.IsFinite(attackInterval) && attackInterval > 0 &&
            float.IsFinite(transform.position.x) && float.IsFinite(transform.position.y) && float.IsFinite(transform.position.z) &&
            Enum.IsDefined(typeof(TargetRule), targetRule) && float.IsFinite(maxHp) && maxHp > 0 &&
            float.IsFinite(attackWidth) && attackWidth > 0 && Enum.IsDefined(typeof(AttackArea), attackArea);

        public void CollectFootprintErrors(BattleGrid grid, HashSet<Vector3Int> occupied, List<string> errors)
        {
            var position = transform.position;
            var center = SmallCellOccupancy.PositionToCell(position);
            if (usesMacroFootprint && !isWall)
            {
                var macro = BattleGrid.ToMacroCell(center);
                var expected = new Vector3(macro.x * 3 + 1, center.y * 0.5f + 0.25f, macro.z * 3 + 1);
                if (Vector3.Distance(position, expected) > 0.001f)
                    errors.Add($"{name}: 3x3 tower must be centered on a Macro Cell at its top surface.");
            }
            var footprint = Footprint;
            if (isWall && (Quaternion.Angle(transform.rotation, Quaternion.Euler(0, Mathf.Round(transform.eulerAngles.y / 90f) * 90f, 0)) > 0.01f ||
                Vector3.Distance(transform.lossyScale, Vector3.one) > 0.001f))
                errors.Add($"{name}: walls support Y rotation in 90-degree steps, no tilt, and world scale (1, 1, 1).");
            if (isWall && (Mathf.Abs(position.x - center.x) > 0.001f || Mathf.Abs(position.z - center.z) > 0.001f))
                errors.Add($"{name}: wall center must align to the small-cell grid.");
            for (var z = -footprint.y / 2; z <= footprint.y / 2; z++)
                for (var x = -footprint.x / 2; x <= footprint.x / 2; x++)
                {
                    var key = center + new Vector3Int(x, 0, z);
                    if (!occupied.Add(key)) errors.Add($"{name}: tower footprint overlap at {key}.");
                    if (!grid.TryGetCell(key, out var support) || (isWall ? support.Kind != CellKind.Road : support.Kind != CellKind.TowerPlatform) ||
                        !grid.HasClearTop(support) || Mathf.Abs(position.y - support.TopCenter.y) > 0.001f)
                        errors.Add($"{name}: footprint {key} needs a clear, equal-height support surface (Road for walls, TowerPlatform for towers).");
                }
        }
    }
}
