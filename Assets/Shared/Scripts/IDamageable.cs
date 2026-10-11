using UnityEngine;

namespace ReverseTD.Shared
{
    public interface IDamageable
    {
        Team Team { get; }

        bool IsAlive { get; }

        Vector3 Position { get; }

        float TakeDamage(float amount);
    }
}
