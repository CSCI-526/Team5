using System.Collections.Generic;
using UnityEngine;

namespace Team5.Rebuild.World
{
    [DisallowMultipleComponent]
    public sealed class BattleCell : MonoBehaviour
    {
        public const float LayerHeight = 0.5f;
        public static Vector3 Size => new Vector3(1, LayerHeight, 1);
        [SerializeField] private CellKind kind;
        public CellKind Kind => kind;
        public Vector3Int Occupancy => new Vector3Int(Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y / LayerHeight), Mathf.RoundToInt(transform.position.z));
        public Vector3 TopCenter => transform.position + Vector3.up * (LayerHeight / 2);
        public bool IsRouteSurface => kind == CellKind.Road || kind == CellKind.Spawn || kind == CellKind.Goal;

        public void CollectErrors(List<string> errors)
        {
            var position = transform.position;
            var key = Occupancy;
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z) ||
                Vector3.Distance(position, new Vector3(key.x, key.y * LayerHeight, key.z)) > 0.001f)
                errors.Add($"{name}: cell center must align to X/Z = 1 and Y = 0.5.");
            if (Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.01f ||
                Vector3.Distance(transform.lossyScale, Vector3.one) > 0.001f)
                errors.Add($"{name}: cells require identity world rotation and scale; physical size is 1 x 0.5 x 1.");
        }
    }
}
