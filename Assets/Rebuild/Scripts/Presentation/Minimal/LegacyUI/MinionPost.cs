using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionPost
    {
        private const float BobHeight = 0.025f;
        private const float BobSpeed = 3f;
        private const float RecoilSeconds = 0.18f;
        private const float RecoilScale = 0.14f;

        private float lastHp = float.MaxValue;
        private float recoil;

        public MinionPost(BattleTower tower, Vector3 home, Vector2Int footprint, GameObject look)
        {
            Tower = tower;
            Home = home;
            Footprint = footprint;
            Look = look;
        }

        public BattleTower Tower { get; }
        public Vector3 Home { get; }
        public Vector2Int Footprint { get; }
        public GameObject Look { get; }
        public bool Lifted { get; private set; }
        public bool IsAlive => Tower != null && Tower.RuntimeState != null && Tower.RuntimeState.IsAlive;

        public float DistanceTo(Vector3 point)
        {
            var dx = Mathf.Max(0f, Mathf.Abs(point.x - Home.x) - Footprint.x * 0.5f);
            var dz = Mathf.Max(0f, Mathf.Abs(point.z - Home.z) - Footprint.y * 0.5f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public void StepAside(float height)
        {
            if (Tower == null) return;
            Lifted = true;
            Tower.transform.position = Home + Vector3.up * height;
            if (Look != null) Look.SetActive(false);
        }

        public void Restore()
        {
            lastHp = float.MaxValue;
            recoil = 0f;
            if (Tower == null) return;
            Lifted = false;
            Tower.transform.position = Home;
            if (Look != null) Look.SetActive(true);
        }

        public void Animate(float time, float deltaTime)
        {
            if (Look == null || Lifted || !IsAlive) return;
            var hp = Tower.RuntimeState.Hp;
            if (hp < lastHp - 0.0001f && lastHp < float.MaxValue) recoil = RecoilSeconds;
            lastHp = hp;
            recoil = Mathf.Max(0f, recoil - deltaTime);
            var squash = recoil / RecoilSeconds * RecoilScale;
            var guards = Look.transform;
            for (var i = 0; i < guards.childCount; i++)
            {
                var guard = guards.GetChild(i);
                var position = guard.localPosition;
                position.y = (Mathf.Sin(time * BobSpeed + i * 2.1f) + 1f) * 0.5f * BobHeight;
                guard.localPosition = position;
                guard.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash);
            }
        }
    }
}
