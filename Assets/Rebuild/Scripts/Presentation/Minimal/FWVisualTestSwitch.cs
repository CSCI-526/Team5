using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Exists only in FWUnitVisualTest. Toggle in Play Mode without resetting the battle.
    [DefaultExecutionOrder(-200)]
    public sealed class FWVisualTestSwitch : MonoBehaviour
    {
        [SerializeField] private BattlePresentation presentation;
        [SerializeField] private UnitView visual3D;
        [SerializeField] private UnitView visual2D;
        [SerializeField] private int memberId = 111;
        [SerializeField] private bool use2D = true;
        private bool applied;
        public bool Use2D { get => use2D; set { use2D = value; Apply(); } }
        private void OnEnable() => Apply();
        private void Update() { if (applied != use2D) Apply(); }
        private void Apply()
        {
            if (presentation == null || visual3D == null || visual2D == null) return;
            presentation.SetTestVisual(memberId, use2D ? visual2D : visual3D);
            applied = use2D;
        }
    }
}
