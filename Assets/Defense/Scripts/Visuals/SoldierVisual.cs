using ReverseTD.Shared;
using UnityEngine;

namespace ReverseTD.Defense
{
    public class SoldierVisual : MonoBehaviour
    {
        private const float DesignSize = 0.4f;
        private const float TurnSpeed = 540f;
        private const float AimSpeed = 720f;
        private const float AimHoldTime = 1.5f;
        private const float FlashTime = 0.1f;
        private const float HitFlashTime = 0.12f;
        private const float HitFlashAlpha = 0.4f;
        private const float ShadowAlpha = 0.5f;
        private const float FlashGlowAlpha = 0.25f;
        private const float HandShade = 0.8f;
        private const float HeadTint = 0.3f;
        private const float BarWidth = 0.44f;
        private const float BarHeight = 0.075f;
        private const float BarInset = 0.016f;
        private const float BarOffset = 0.4f;
        private const float MinMoveSqr = 0.000001f;

        private static readonly Vector2 gunLine = new Vector2(0f, -0.14f);
        private static readonly Vector2 muzzle = new Vector2(0.42f, -0.14f);
        private static readonly Color gunColor = new Color(0.125f, 0.14f, 0.165f);
        private static readonly Color gunTipColor = new Color(0.29f, 0.32f, 0.365f);

        private Transform shadow;
        private Transform lower;
        private Transform upper;
        private SpriteRenderer flashGlow;
        private SpriteRenderer flashCore;
        private SpriteRenderer hitFlash;
        private HealthBar healthBar;
        private GameObject healthBarObject;
        private Vector3 lastPosition;
        private Vector3 aimPoint;
        private float facingAngle;
        private float targetFacing;
        private float aimAngle;
        private float aimTimer;
        private float flashTimer;
        private float hitTimer;
        private float shownHealth = 1f;
        private bool hasFaced;

        public static SoldierVisual Create(Transform soldier, float size, Color bodyColor, Color outlineColor)
        {
            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(soldier, false);

            var visual = visualObject.AddComponent<SoldierVisual>();
            visual.Build(size, bodyColor, outlineColor);
            return visual;
        }

        public Vector3 Fire(Vector3 targetPoint)
        {
            aimPoint = targetPoint;
            aimTimer = AimHoldTime;
            flashTimer = FlashTime;

            Vector2 toTarget = targetPoint - transform.position;
            if (toTarget.sqrMagnitude > MinMoveSqr)
            {
                aimAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            }

            Apply();
            return upper.TransformPoint(muzzle);
        }

        public void ShowHealth(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction < shownHealth)
            {
                hitTimer = HitFlashTime;
            }
            shownHealth = fraction;

            if (fraction < 1f && !healthBarObject.activeSelf)
            {
                healthBarObject.SetActive(true);
            }
            healthBar.SetFraction(fraction);
        }

        /** 
         * 
         * Can use this only after midterm, replaced with just square
        private void Build(float size, Color bodyColor, Color outlineColor)
        {
            float scale = size / DesignSize;
            transform.localScale = new Vector3(scale, scale, 1f);
            lastPosition = transform.position;

            int order = SortingOrders.Soldier;
            var bodySize = new Vector2(0.34f, 0.46f);

            shadow = ShapeSprites.AddPart(transform, "Shadow", ShapeSprites.Circle, new Vector2(0.03f, -0.05f), bodySize, new Color(0f, 0f, 0f, ShadowAlpha), order).transform;

            lower = AddGroup("Lower");
            ShapeSprites.AddPart(lower, "Outline", ShapeSprites.Circle, Vector2.zero, bodySize, outlineColor, order + 1);
            ShapeSprites.AddPart(lower, "Body", ShapeSprites.Circle, Vector2.zero, new Vector2(0.26f, 0.38f), bodyColor, order + 2);
            hitFlash = ShapeSprites.AddPart(lower, "Hit Flash", ShapeSprites.Circle, Vector2.zero, bodySize, Color.white, order + 7);

            upper = AddGroup("Upper");
            ShapeSprites.AddPart(upper, "Gun", ShapeSprites.Square, gunLine + new Vector2(0.2f, 0f), new Vector2(0.4f, 0.07f), gunColor, order + 3);
            ShapeSprites.AddPart(upper, "Gun Tip", ShapeSprites.Square, gunLine + new Vector2(0.38f, 0f), new Vector2(0.04f, 0.1f), gunTipColor, order + 4);
            ShapeSprites.AddPart(upper, "Hand", ShapeSprites.Circle, gunLine + new Vector2(0.13f, 0f), new Vector2(0.09f, 0.09f), Palette.Shade(bodyColor, HandShade), order + 4);
            ShapeSprites.AddPart(upper, "Head Outline", ShapeSprites.Circle, Vector2.zero, new Vector2(0.23f, 0.23f), outlineColor, order + 5);
            ShapeSprites.AddPart(upper, "Head", ShapeSprites.Circle, Vector2.zero, new Vector2(0.17f, 0.17f), Palette.Tint(bodyColor, HeadTint), order + 6);

            Vector2 flashPosition = gunLine + new Vector2(0.47f, 0f);
            flashGlow = ShapeSprites.AddPart(upper, "Flash Glow", ShapeSprites.Circle, flashPosition, new Vector2(0.252f, 0.252f), Palette.WithAlpha(Palette.ShotColor(Team.Defender), FlashGlowAlpha), SortingOrders.MuzzleFlash);
            flashCore = ShapeSprites.AddPart(upper, "Flash Core", ShapeSprites.Circle, flashPosition, new Vector2(0.14f, 0.14f), Palette.FlashCore, SortingOrders.MuzzleFlash + 1);

            healthBarObject = new GameObject("HP Bar");
            healthBarObject.transform.SetParent(transform, false);
            healthBarObject.transform.localPosition = new Vector3(0f, BarOffset, 0f);
            healthBar = healthBarObject.AddComponent<HealthBar>();
            healthBar.Initialize(BarWidth, BarHeight, BarInset);
            healthBarObject.SetActive(false);

            Apply();
        }
        **/

        private void Build(float size, Color bodyColor, Color outlineColor)
        {
            float scale = size / DesignSize;
            transform.localScale = new Vector3(scale, scale, 1f);
            lastPosition = transform.position;

            int order = SortingOrders.Soldier;
            var squareSize = new Vector2(0.4f, 0.4f);
            var innerSize = new Vector2(0.34f, 0.34f);

            // Drop shadow
            shadow = ShapeSprites.AddPart(transform, "Shadow", ShapeSprites.Square, new Vector2(0.03f, -0.05f), squareSize, new Color(0f, 0f, 0f, ShadowAlpha), order).transform;

            // Body (Outline + Fill)
            lower = AddGroup("Lower");
            ShapeSprites.AddPart(lower, "Outline", ShapeSprites.Square, Vector2.zero, squareSize, outlineColor, order + 1);
            ShapeSprites.AddPart(lower, "Body", ShapeSprites.Square, Vector2.zero, innerSize, bodyColor, order + 2);
            hitFlash = ShapeSprites.AddPart(lower, "Hit Flash", ShapeSprites.Square, Vector2.zero, squareSize, Color.white, order + 3);

            // Upper layer (remains empty so aim/muzzle calculations still resolve)
            upper = AddGroup("Upper");

            // Optional dummy flash renderers to prevent NullReferenceException in Apply()
            flashGlow = ShapeSprites.AddPart(upper, "Flash Glow", ShapeSprites.Square, Vector2.zero, Vector2.zero, Color.clear, order);
            flashCore = ShapeSprites.AddPart(upper, "Flash Core", ShapeSprites.Square, Vector2.zero, Vector2.zero, Color.clear, order);

            // HP Bar
            healthBarObject = new GameObject("HP Bar");
            healthBarObject.transform.SetParent(transform, false);
            healthBarObject.transform.localPosition = new Vector3(0f, BarOffset, 0f);
            healthBar = healthBarObject.AddComponent<HealthBar>();
            healthBar.Initialize(BarWidth, BarHeight, BarInset);
            healthBarObject.SetActive(false);

            Apply();
        }

        private void LateUpdate()
        {
            Vector3 position = transform.position;
            Vector2 moved = position - lastPosition;
            lastPosition = position;
            if (moved.sqrMagnitude > MinMoveSqr)
            {
                targetFacing = Mathf.Atan2(moved.y, moved.x) * Mathf.Rad2Deg;
                if (!hasFaced)
                {
                    hasFaced = true;
                    facingAngle = targetFacing;
                    if (aimTimer <= 0f)
                    {
                        aimAngle = targetFacing;
                    }
                }
            }

            float delta = Time.deltaTime;
            facingAngle = Mathf.MoveTowardsAngle(facingAngle, targetFacing, TurnSpeed * delta);

            float wantedAim = facingAngle;
            if (aimTimer > 0f)
            {
                Vector2 toTarget = aimPoint - position;
                if (toTarget.sqrMagnitude > MinMoveSqr)
                {
                    wantedAim = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                }
            }
            aimAngle = Mathf.MoveTowardsAngle(aimAngle, wantedAim, AimSpeed * delta);

            Apply();

            aimTimer = Mathf.Max(0f, aimTimer - delta);
            flashTimer = Mathf.Max(0f, flashTimer - delta);
            hitTimer = Mathf.Max(0f, hitTimer - delta);
        }

        private void Apply()
        {
            Quaternion facing = Quaternion.Euler(0f, 0f, facingAngle);
            shadow.rotation = facing;
            lower.rotation = facing;
            upper.rotation = Quaternion.Euler(0f, 0f, aimAngle);

            bool flashing = flashTimer > 0f;
            flashGlow.enabled = flashing;
            flashCore.enabled = flashing;

            bool hit = hitTimer > 0f;
            hitFlash.enabled = hit;
            if (hit)
            {
                hitFlash.color = new Color(1f, 1f, 1f, HitFlashAlpha * hitTimer / HitFlashTime);
            }
        }

        private Transform AddGroup(string groupName)
        {
            Transform group = new GameObject(groupName).transform;
            group.SetParent(transform, false);
            return group;
        }
    }
}
