using ReverseTD.Shared;
using UnityEngine;

namespace ReverseTD.Defense
{
    public class TowerVisual : MonoBehaviour
    {
        public const float DesignSize = 0.8f;

        private const float MuzzleDistance = 0.62f;
        private const float TurnSpeed = 720f;
        private const float RecoilDistance = 0.07f;
        private const float RecoilTime = 0.15f;
        private const float FlashTime = 0.1f;
        private const float HitFlashTime = 0.12f;
        private const float HitFlashAlpha = 0.35f;
        private const float ShadowAlpha = 0.43f;
        private const float FlashGlowAlpha = 0.15f;
        private const float MinAimDistanceSqr = 0.0001f;

        private Transform barrel;
        private Transform slide;
        private SpriteRenderer flashGlow;
        private SpriteRenderer flashCore;
        private SpriteRenderer hitFlash;
        private float aimAngle;
        private float targetAngle;
        private float recoilTimer;
        private float flashTimer;
        private float hitTimer;

        /**
         * 
         * Changed to no sprites for now
        public void Initialize(TowerDefinition towerDefinition)
        {
            float scale = towerDefinition.Size / DesignSize;
            transform.localScale = new Vector3(scale, scale, 1f);

            Color color = towerDefinition.Color;
            Color dark = Palette.Dark(color);
            Color mid = Palette.Mid(color);
            Color light = Palette.Light(color);
            int order = SortingOrders.Tower;

            AddSquare(transform, "Shadow", new Vector2(0.07f, -0.09f), new Vector2(0.8f, 0.8f), new Color(0f, 0f, 0f, ShadowAlpha), order);
            AddSquare(transform, "Outline", Vector2.zero, new Vector2(0.8f, 0.8f), dark, order + 1);
            AddSquare(transform, "Base", Vector2.zero, new Vector2(0.68f, 0.68f), color, order + 2);
            AddSquare(transform, "Plate", Vector2.zero, new Vector2(0.46f, 0.46f), mid, order + 3);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    AddSquare(transform, "Bolt", new Vector2(0.275f * x, 0.275f * y), new Vector2(0.07f, 0.07f), dark, order + 3);
                }
            }

            barrel = AddGroup(transform, "Barrel");
            slide = AddGroup(barrel, "Slide");
            AddSquare(slide, "Tube", new Vector2(0.29f, 0f), new Vector2(0.5f, 0.14f), dark, order + 4);
            AddSquare(slide, "Muzzle", new Vector2(0.51f, 0f), new Vector2(0.09f, 0.2f), dark, order + 4);
            AddSquare(slide, "Strip", new Vector2(0.2925f, 0f), new Vector2(0.345f, 0.045f), mid, order + 5);

            var muzzle = new Vector2(MuzzleDistance, 0f);
            flashGlow = AddCircle(barrel, "Flash Glow", muzzle, 0.36f, Palette.WithAlpha(Palette.ShotColor(Team.Attacker), FlashGlowAlpha), SortingOrders.MuzzleFlash);
            flashCore = AddCircle(barrel, "Flash Core", muzzle, 0.2f, Palette.FlashCore, SortingOrders.MuzzleFlash + 1);

            AddCircle(transform, "Turret Outline", Vector2.zero, 0.46f, dark, order + 6);
            AddCircle(transform, "Turret", Vector2.zero, 0.36f, light, order + 7);
            AddCircle(transform, "Cap", Vector2.zero, 0.13f, dark, order + 8);
            hitFlash = AddSquare(transform, "Hit Flash", Vector2.zero, new Vector2(0.8f, 0.8f), Color.white, order + 9);

            Apply();
        }
        **/

        public void Initialize(TowerDefinition towerDefinition)
        {
            float scale = towerDefinition.Size / DesignSize;
            transform.localScale = new Vector3(scale, scale, 1f);

            Color color = towerDefinition.Color;
            Color dark = Palette.Dark(color);
            int order = SortingOrders.Tower;

            var fullSize = new Vector2(0.8f, 0.8f);
            var innerSize = new Vector2(0.68f, 0.68f);

            // Shadow & Square Body (Outline + Fill)
            AddSquare(transform, "Shadow", new Vector2(0.07f, -0.09f), fullSize, new Color(0f, 0f, 0f, ShadowAlpha), order);
            AddSquare(transform, "Outline", Vector2.zero, fullSize, dark, order + 1);
            AddSquare(transform, "Base", Vector2.zero, innerSize, color, order + 2);
            hitFlash = AddSquare(transform, "Hit Flash", Vector2.zero, fullSize, Color.white, order + 3);

            // Empty barrel/slide groups to maintain aim rotation and recoil mechanics
            barrel = AddGroup(transform, "Barrel");
            slide = AddGroup(barrel, "Slide");

            // Invisible flash markers so Fire() returns the correct muzzle position
            var muzzle = new Vector2(MuzzleDistance, 0f);
            flashGlow = AddSquare(barrel, "Flash Glow", muzzle, Vector2.zero, Color.clear, SortingOrders.MuzzleFlash);
            flashCore = AddSquare(barrel, "Flash Core", muzzle, Vector2.zero, Color.clear, SortingOrders.MuzzleFlash + 1);

            Apply();
        }

        public void Face(Vector3 worldPoint)
        {
            AimAt(worldPoint);
            aimAngle = targetAngle;
            Apply();
        }

        internal void ResetCombatEffects()
        {
            recoilTimer = flashTimer = hitTimer = 0f;
            Apply();
        }

        public void AimAt(Vector3 worldPoint)
        {
            Vector2 direction = worldPoint - transform.position;
            if (direction.sqrMagnitude > MinAimDistanceSqr)
            {
                targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }
        }

        public Vector3 Fire(Vector3 worldPoint)
        {
            AimAt(worldPoint);
            aimAngle = targetAngle;
            recoilTimer = RecoilTime;
            flashTimer = FlashTime;
            Apply();
            return flashCore.transform.position;
        }

        public void FlashHit()
        {
            hitTimer = HitFlashTime;
        }

        private void LateUpdate()
        {
            float delta = Time.deltaTime;
            aimAngle = Mathf.MoveTowardsAngle(aimAngle, targetAngle, TurnSpeed * delta);
            Apply();

            recoilTimer = Mathf.Max(0f, recoilTimer - delta);
            flashTimer = Mathf.Max(0f, flashTimer - delta);
            hitTimer = Mathf.Max(0f, hitTimer - delta);
        }

        private void Apply()
        {
            barrel.rotation = Quaternion.Euler(0f, 0f, aimAngle);
            slide.localPosition = new Vector3(-RecoilDistance * recoilTimer / RecoilTime, 0f, 0f);

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

        private static Transform AddGroup(Transform parent, string groupName)
        {
            Transform group = new GameObject(groupName).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static SpriteRenderer AddSquare(Transform parent, string partName, Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            return ShapeSprites.AddPart(parent, partName, ShapeSprites.Square, position, size, color, sortingOrder);
        }

        private static SpriteRenderer AddCircle(Transform parent, string partName, Vector2 position, float diameter, Color color, int sortingOrder)
        {
            return ShapeSprites.AddPart(parent, partName, ShapeSprites.Circle, position, new Vector2(diameter, diameter), color, sortingOrder);
        }
    }
}
