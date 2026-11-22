using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class MinionRig
    {
        public MinionRig(GameObject look, Transform bot, Transform gear, Transform muzzle)
        {
            Look = look;
            Bot = bot;
            Gear = gear;
            Muzzle = muzzle;
        }

        public GameObject Look { get; }
        public Transform Bot { get; }
        public Transform Gear { get; }
        public Transform Muzzle { get; }
    }
}
