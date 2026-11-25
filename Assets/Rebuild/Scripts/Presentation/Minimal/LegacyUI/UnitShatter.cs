using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class UnitShatter
    {
        private const int Bursts = 3;
        private const int Shards = 9;
        private const float Seconds = 0.6f;
        private const float Gravity = 9.8f;
        private const float ShardSize = 0.09f;

        private readonly Transform[] shards = new Transform[Bursts * Shards];
        private readonly Vector3[] velocities = new Vector3[Bursts * Shards];
        private readonly Vector3[] spins = new Vector3[Bursts * Shards];
        private readonly Vector3[] origins = new Vector3[Bursts];
        private readonly float[] floors = new float[Bursts];
        private readonly float[] ages = new float[Bursts];
        private int next;

        public UnitShatter(Transform parent, MinionMaterials materials)
        {
            var root = new GameObject("Ice Shatter").transform;
            root.SetParent(parent, false);
            var material = materials.Toon(MinionPalette.IceShell, 0.4f);
            for (var i = 0; i < shards.Length; i++)
            {
                shards[i] = MinionMaterials.Show(root, "Ice Shard", materials.Gem, material).transform;
                shards[i].gameObject.SetActive(false);
            }
            for (var burst = 0; burst < Bursts; burst++) ages[burst] = Seconds;
        }

        public void Play(Vector3 center, float floor, int seed)
        {
            var burst = next;
            next = (next + 1) % Bursts;
            ages[burst] = 0f;
            origins[burst] = center;
            floors[burst] = floor;
            for (var i = 0; i < Shards; i++)
            {
                var index = burst * Shards + i;
                var angle = seed * 1.7f + i * Mathf.PI * 2f / Shards;
                var speed = 1.1f + (i % 3) * 0.35f;
                velocities[index] = new Vector3(Mathf.Cos(angle) * speed, 1.6f + (i % 2) * 0.8f, Mathf.Sin(angle) * speed);
                spins[index] = new Vector3(90f + i * 40f, 220f + i * 25f, 60f);
                shards[index].gameObject.SetActive(true);
            }
            Place(burst);
        }

        public void Tick(float deltaTime)
        {
            for (var burst = 0; burst < Bursts; burst++)
            {
                if (ages[burst] >= Seconds) continue;
                ages[burst] += deltaTime;
                Place(burst);
            }
        }

        public void Clear()
        {
            for (var burst = 0; burst < Bursts; burst++) ages[burst] = Seconds;
            foreach (var shard in shards) shard.gameObject.SetActive(false);
        }

        private void Place(int burst)
        {
            var age = ages[burst];
            var life = 1f - age / Seconds;
            for (var i = 0; i < Shards; i++)
            {
                var index = burst * Shards + i;
                var shard = shards[index];
                var position = origins[burst] + velocities[index] * age + Vector3.down * (0.5f * Gravity * age * age);
                var visible = age < Seconds && position.y > floors[burst];
                shard.gameObject.SetActive(visible);
                if (!visible) continue;
                shard.SetPositionAndRotation(position, Quaternion.Euler(spins[index] * age));
                shard.localScale = new Vector3(ShardSize, ShardSize * 1.6f, ShardSize) * Mathf.Max(0.2f, life);
            }
        }
    }
}
