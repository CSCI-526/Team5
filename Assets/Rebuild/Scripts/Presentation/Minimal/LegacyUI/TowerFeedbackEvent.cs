using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public readonly struct TowerFeedbackEvent
    {
        public TowerFeedbackEvent(TowerFeedbackKind kind, int towerId, Vector3 source, Color paint, float strength, int bounty)
        {
            Kind = kind;
            TowerId = towerId;
            Source = source;
            Paint = paint;
            Strength = strength;
            Bounty = bounty;
        }

        public TowerFeedbackKind Kind { get; }
        public int TowerId { get; }
        public Vector3 Source { get; }
        public Color Paint { get; }
        public float Strength { get; }
        public int Bounty { get; }
    }
}
