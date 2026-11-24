using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionPost
    {
        private const float BobHeight = 0.03f;
        private const float BobSpeed = 3f;
        private const float RecoilSeconds = 0.18f;
        private const float RecoilScale = 0.14f;
        private const float KickSeconds = 0.25f;
        private const float ShieldThrust = 0.08f;
        private const float MortarTilt = 10f;
        private const float TurnSpeed = 420f;
        private const float HopLift = 0.08f;

        private readonly Quaternion startFacing;
        private Quaternion facing;
        private Vector3 slideFrom;
        private float slideLeft;
        private float slideTotal;
        private float lastHp = float.MaxValue;
        private float recoil;
        private float kick;

        public MinionPost(BattleTower tower, MinionKind kind, int guardedTowerId, MinionRig rig)
        {
            Tower = tower;
            Kind = kind;
            GuardedTowerId = guardedTowerId;
            Rig = rig;
            Home = tower != null ? tower.transform.position : Vector3.zero;
            startFacing = rig != null && rig.Look != null ? rig.Look.transform.localRotation : Quaternion.identity;
            facing = startFacing;
        }

        public BattleTower Tower { get; }
        public MinionKind Kind { get; }
        public int GuardedTowerId { get; }
        public MinionRig Rig { get; }
        public Vector3 Home { get; }
        public bool IsAlive => Tower != null && Tower.RuntimeState != null && Tower.RuntimeState.IsAlive;
        public bool IsFrozen => Tower != null && Tower.RuntimeState != null && Tower.RuntimeState.FrozenRemaining > 0f;
        public bool IsSliding => slideLeft > 0f;
        public Vector3 Position => Tower != null ? Tower.transform.position : Vector3.zero;
        public Vector2Int Cell => new Vector2Int(Mathf.RoundToInt(Position.x), Mathf.RoundToInt(Position.z));
        public Vector3 VisualPosition => Rig != null && Rig.Look != null ? Rig.Look.transform.position : Position;
        public Vector3 MuzzlePosition => Rig != null && Rig.Muzzle != null ? Rig.Muzzle.position : Position + Vector3.up * 0.6f;

        public void Restore()
        {
            lastHp = float.MaxValue;
            recoil = 0f;
            kick = 0f;
            slideLeft = 0f;
            facing = startFacing;
            if (Tower != null) Tower.transform.position = Home;
            if (Rig == null || Rig.Look == null) return;
            Rig.Look.SetActive(Tower != null && (Tower.RuntimeState == null || Tower.RuntimeState.IsAlive));
            Rig.Look.transform.localPosition = Vector3.zero;
            Rig.Look.transform.localRotation = startFacing;
        }

        public void Kick() => kick = KickSeconds;

        public void Face(Vector3 point)
        {
            var flat = new Vector3(point.x - Position.x, 0f, point.z - Position.z);
            if (flat.sqrMagnitude > 0.0001f) facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        public void Hop(Vector3 top, float seconds)
        {
            if (Tower == null) return;
            var from = VisualPosition;
            Tower.transform.position = top;
            slideFrom = new Vector3(from.x - top.x, 0f, from.z - top.z);
            slideTotal = Mathf.Max(0.01f, seconds);
            slideLeft = slideTotal;
        }

        public void Animate(float time, float deltaTime, float simulatedDelta, int index)
        {
            if (Rig == null || Rig.Bot == null) return;
            // The death burst is only feedback; it never hides the old model. Leaving it
            // visible makes defeated lobbers appear alive but incapable of firing.
            if (Rig.Look != null && Rig.Look.activeSelf != IsAlive) Rig.Look.SetActive(IsAlive);
            if (!IsAlive) return;
            slideLeft = Mathf.Max(0f, slideLeft - simulatedDelta);
            var slide = slideLeft / Mathf.Max(0.01f, slideTotal);
            if (Rig.Look != null)
            {
                Rig.Look.transform.localPosition = slideFrom * slide + Vector3.up * (Mathf.Sin(slide * Mathf.PI) * HopLift);
                Rig.Look.transform.localRotation = Quaternion.RotateTowards(Rig.Look.transform.localRotation, facing, TurnSpeed * deltaTime);
            }
            var hp = Tower.RuntimeState.Hp;
            if (hp < lastHp - 0.0001f && lastHp < float.MaxValue) recoil = RecoilSeconds;
            lastHp = hp;
            recoil = Mathf.Max(0f, recoil - deltaTime);
            kick = Mathf.Max(0f, kick - deltaTime);
            var squash = recoil / RecoilSeconds * RecoilScale;
            var bob = IsFrozen ? 0f : (Mathf.Sin(time * BobSpeed + index * 2.1f) + 1f) * 0.5f * BobHeight;
            Rig.Bot.localPosition = new Vector3(0f, bob, 0f);
            Rig.Bot.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash);
            if (Rig.Gear == null) return;
            var pulse = Mathf.Sin(kick / KickSeconds * Mathf.PI);
            if (Kind == MinionKind.Bearer) Rig.Gear.localPosition = Vector3.forward * (pulse * ShieldThrust);
            else Rig.Gear.localRotation = Quaternion.Euler(-pulse * MortarTilt, 0f, 0f);
        }
    }
}
