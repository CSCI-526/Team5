using UnityEngine;

namespace RandomMuster
{
    public sealed class ZombieVisual : MonoBehaviour
    {
        [SerializeField] private Transform billboard;
        [SerializeField] private Animator animator;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Attack = Animator.StringToHash("Attack");

        public void SetMoving(bool moving) => animator.SetBool(Moving, moving);

        public void SetFacingLeft(bool facingLeft)
        {
            billboard.localScale = new Vector3(facingLeft ? -1 : 1, 1, 1);
        }

        public void PlayAttack() => animator.SetTrigger(Attack);

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null) billboard.rotation = camera.transform.rotation;
        }
    }
}
