using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public static class TowerFeedbackRules
    {
        public const float MaxFrameDelta = 0.05f;
        public const int MaxQueued = 64;
        public const int SplashCount = 10;
        public const int BurstCount = 3;
        public const int LabelCount = 4;
        public const int MaxStains = 8;
        public const float FlashSeconds = 0.12f;
        public const float FlashAmount = 0.75f;
        public const float FlashBrightness = 1.8f;
        public const float DeathFlashSeconds = 0.16f;
        public const float DeathFlashAmount = 0.85f;
        public const float SquashSeconds = 0.15f;
        public const float SquashMax = 0.06f;
        public const float SquashFloor = 0.6f;
        public const float ShieldedStrength = 0.5f;
        public const float CollapseSeconds = 0.5f;
        public const float CollapseTilt = 8f;
        public const float CollapseSink = 0.16f;
        public const float CollapseBulge = 0.06f;
        public const float SplashCooldown = 0.1f;
        public const float ImpactHeight = 0.55f;
        public const float ImpactRadius = 0.32f;
        public const float ImpactTowardCamera = 0.3f;
        public const float CameraBias = 0.7f;
        public const float DomeReach = 0.65f;
        public const float DomeHeight = 0.9f;
        public const float StainDepth = 0.32f;
        public const float StainHalfWidth = 0.21f;
        public const float StainLow = 0.45f;
        public const float StainHigh = 1.02f;
        public const float StainMinSize = 0.4f;
        public const float StainMaxSize = 0.56f;
        public const float SplashSeconds = 0.55f;
        public const float SplashFlashSeconds = 0.12f;
        public const float RippleSeconds = 0.35f;
        public const float DropletSize = 0.11f;
        public const float BurstSeconds = 1.9f;
        public const float MinionPopHeight = 0.45f;
        public const float LabelSeconds = 1.2f;
        public const float LabelRise = 44f;
        public const float LabelFontSize = 34f;
        public const float LabelLift = 0.15f;
        public const float Gravity = 9.8f;
        public static readonly Color NeutralPaint = new Color(0.94f, 0.92f, 0.86f);
        public static readonly Color Dust = new Color(0.88f, 0.87f, 0.84f);

        public static float Pulse(float age, float seconds) => age < 0f || age >= seconds ? 0f : Mathf.Sin(Mathf.PI * age / seconds);

        public static float EaseOut(float progress)
        {
            var rest = 1f - Mathf.Clamp01(progress);
            return 1f - rest * rest * rest;
        }

        public static float SquashStrength(float amount, float maxHp) =>
            maxHp <= 0f ? 1f : Mathf.Clamp(SquashFloor + amount / maxHp * 2f, SquashFloor, 1f);

        public static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);

        public static Vector3 HitDirection(Vector3 towerBase, Vector3 source, Vector3 eye)
        {
            var toCamera = Flat(eye - towerBase);
            toCamera = toCamera.sqrMagnitude > 0.0001f ? toCamera.normalized : Vector3.back;
            var toSource = Flat(source - towerBase);
            var direction = (toSource.sqrMagnitude > 0.0001f ? toSource.normalized : toCamera) + toCamera * CameraBias;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : toCamera;
        }

        public static Vector3 ImpactPoint(Vector3 towerBase, Vector3 direction, Vector3 eye)
        {
            var point = towerBase + direction * ImpactRadius + Vector3.up * ImpactHeight;
            var toCamera = eye - point;
            return toCamera.sqrMagnitude > 0.0001f ? point + toCamera.normalized * ImpactTowardCamera : point;
        }

        public static Vector3 DomePoint(Vector3 towerBase, Vector3 direction) =>
            towerBase + direction * DomeReach + Vector3.up * DomeHeight;

        public static Vector3 AwayFrom(Vector3 towerBase, Vector3 source, Vector3 eye)
        {
            var away = Flat(towerBase - source);
            if (away.sqrMagnitude > 0.0001f) return away.normalized;
            var back = Flat(towerBase - eye);
            return back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.forward;
        }

        public static Vector3 Ballistic(Vector3 origin, Vector3 velocity, float age, float floor)
        {
            var point = origin + velocity * age + Vector3.down * (0.5f * Gravity * age * age);
            if (point.y >= floor || origin.y < floor) return point;
            var landing = (velocity.y + Mathf.Sqrt(Mathf.Max(0f, velocity.y * velocity.y + 2f * Gravity * (origin.y - floor)))) / Gravity;
            var landed = origin + velocity * landing;
            landed.y = floor;
            return landed;
        }

        public static float StainHeight(int index, float jitter) =>
            Mathf.Lerp(StainLow, StainHigh, Mathf.Repeat(index * 0.37f + jitter, 1f));
    }
}
