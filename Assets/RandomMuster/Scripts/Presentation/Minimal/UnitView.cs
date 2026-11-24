using Team5.BasicTowerTrial.Simulation;
using Team5.BasicTowerTrial.Session;
using Team5.BasicTowerTrial.Presentation.LegacyUI;
using UnityEngine;
using TMPro;

namespace Team5.BasicTowerTrial.Presentation
{
    public sealed class UnitView : MonoBehaviour
    {
        public const float LookScale = 2f;
        private const float HealNeighborRadius = 1.45f; // Covers all 8 neighboring slots in the 3x3 squad
        private const float LevelBadgeWidth = 32f;
        private const float LevelBadgeToUnitWidth = 0.6f;

        private UnitState state;
        private Vector3 previousPosition;
        private Vector3 currentPosition;
        private double sampleTime;
        private float sampleInterval;
        private Vector3 previousAnchor, currentAnchor, platformSlot;
        private SquadPlatformView platform;
        public bool IsRidingPlatform => platform != null && platform.IsPlatformMode;
        public bool IsPlatformRepositioning => IsRidingPlatform && (platformSlot - state.FormationOffset).sqrMagnitude > .00001f;
        public Vector3 PlatformRelativeOffset => platformSlot;
        private float RenderAlpha => sampleInterval > 0 ? Mathf.Clamp01((float)((Time.timeAsDouble - sampleTime) / sampleInterval)) : 1;
        public Vector3 InterpolatedAnchor => Vector3.Lerp(previousAnchor, currentAnchor, RenderAlpha);
        public void SetPlatform(SquadPlatformView value)
        { platform = value; platformSlot = state.FormationOffset; }
        internal void AdvancePlatformReposition(float deltaTime)
        {
            if (IsRidingPlatform)
            {
                platformSlot = Vector3.MoveTowards(platformSlot, state.FormationOffset, 4 * Mathf.Max(0, deltaTime));
                if ((platformSlot - state.FormationOffset).sqrMagnitude <= .00001f) platformSlot = state.FormationOffset;
            }
        }
        private MaterialPropertyBlock colors;
        private RectTransform levelBadge;
        private RectTransform levelBadgeLayer;

        public void CreateLevelBadge(RectTransform layer)
        {
            if (levelBadge != null) Destroy(levelBadge.gameObject);
            levelBadgeLayer = layer;
            var background = LegacyUiKit.CreateImage(layer, "Unit Level " + UnitId, Vector2.zero,
                new Vector2(LevelBadgeWidth, 17), new Color(0.035f, 0.045f, 0.065f, 0.94f), 3);
            levelBadge = background.rectTransform;
            levelBadge.pivot = Vector2.one;
            var label = LegacyUiKit.CreateText(levelBadge, "Level", $"Lv.{state.Definition.Level}",
                Vector2.zero, Vector2.zero, 11, Color.white, FontStyles.Bold);
            LegacyUiKit.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(2, 1);
            label.rectTransform.offsetMax = new Vector2(-2, -1);
            label.enableAutoSizing = true;
            label.fontSizeMin = 8;
            label.fontSizeMax = 11;
            levelBadge.gameObject.SetActive(false);
        }

        private void UpdateLevelBadge()
        {
            if (levelBadge == null || levelBadgeLayer == null) return;
            var camera = Camera.main;
            if (state == null || !state.IsAlive || camera == null || !TryGetVisibleBounds(out var bounds))
            { levelBadge.gameObject.SetActive(false); return; }
            levelBadge.gameObject.SetActive(true);
            var right = float.MinValue;
            var left = float.MaxValue;
            var top = float.MinValue;
            for (var z = -1; z <= 1; z += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var x = -1; x <= 1; x += 2)
            {
                var point = camera.WorldToScreenPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                if (point.z <= 0) { levelBadge.gameObject.SetActive(false); return; }
                right = Mathf.Max(right, point.x);
                left = Mathf.Min(left, point.x);
                top = Mathf.Max(top, point.y);
            }
            var center = camera.WorldToViewportPoint(bounds.center);
            if (center.x < 0 || center.x > 1 || center.y < 0 || center.y > 1)
            { levelBadge.gameObject.SetActive(false); return; }
            var canvasScale = levelBadgeLayer.GetComponentInParent<Canvas>().scaleFactor;
            // Match the projected model width rather than a fixed screen/font size.
            // Scale the whole badge so text, padding, background and offsets stay proportional.
            var badgeScale = (right - left) * LevelBadgeToUnitWidth / (LevelBadgeWidth * canvasScale);
            levelBadge.localScale = Vector3.one * badgeScale;
            var pixelScale = badgeScale * canvasScale;
            // Sit just inside the upper-right corner, leaving the health bar above it clear.
            var screen = new Vector2(right + 3 * pixelScale, top - 2 * pixelScale);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(levelBadgeLayer, screen, null, out var local))
                levelBadge.anchoredPosition = local;
        }

        private HealingRangeEffect healEffect;
        private float healWavePhase;
        private float healBurstBoost;
        private Animator zombieAnimator;
        private Transform zombieBillboard;
        private BattleSession zombieSession;
        private SpriteRenderer tripleGlow;
        private Sprite tripleGlowSprite;
        private float zombieHealRemaining;
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Heal = Animator.StringToHash("Heal");

        public int UnitId => state?.Id ?? 0;
        public UnitState State => state;

        public bool TryGetVisibleBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is LineRenderer || renderer == tripleGlow) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        public Vector3 PreviousPosition => previousPosition;
        public Vector3 CurrentPosition => currentPosition;

        public void Bind(UnitState unit)
        {
            state = unit;
            if (colors == null) colors = new MaterialPropertyBlock();
            previousPosition = currentPosition = unit.Position;
            previousAnchor = currentAnchor = unit.AnchorPosition;
            sampleTime = Time.timeAsDouble;
            sampleInterval = Time.fixedDeltaTime;
            transform.position = currentPosition;
            ApplyAppearance(unit.Role);
            ConfigureHealingRangeAnimation(unit.Role == UnitRole.Heal);
        }

        public void TriggerHealBurst()
        {
            if (zombieAnimator != null && state.Role == UnitRole.ShamanZombie)
            {
                zombieHealRemaining = MusterZombieRules.HealAnimationTime;
                zombieAnimator.ResetTrigger(Attack);
                zombieAnimator.SetTrigger(Heal);
                return;
            }
            healWavePhase = 0f;
            healBurstBoost = 1f;
        }

        public void SetFormationDisplaySpacing(float spacing) => healEffect?.SetScale(spacing);

        internal void BindZombie(GameObject model, BattleSession session)
        {
            zombieAnimator = model.GetComponent<Animator>();
            zombieBillboard = model.transform.Find("Billboard");
            zombieSession = session;
            if (state.Definition.Level > 1 && zombieBillboard != null) CreateTripleGlow();
            if (state.Role != UnitRole.ShamanZombie) return;
            healEffect = new HealingRangeEffect(transform, new Color(1f, .15f, .16f), new Color(1f, .31f, .29f));
            healEffect.SetVisible(false);
        }

        private void CreateTripleGlow()
        {
            var sprites = zombieBillboard.GetComponentsInChildren<SpriteRenderer>();
            if (sprites.Length == 0) return;

            var bounds = sprites[0].bounds;
            var backOrder = sprites[0].sortingOrder;
            foreach (var sprite in sprites)
            {
                bounds.Encapsulate(sprite.bounds);
                backOrder = Mathf.Min(backOrder, sprite.sortingOrder);
            }

            const int size = 48;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var offset = new Vector2((x + .5f) / size, (y + .5f) / size) * 2f - Vector2.one;
                var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.20f, 1f, offset.magnitude));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(fade * 255f));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            tripleGlow = new GameObject("Triple Zombie Glow").AddComponent<SpriteRenderer>();
            tripleGlow.transform.SetParent(zombieBillboard, false);
            tripleGlow.transform.localPosition = zombieBillboard.InverseTransformPoint(bounds.center);
            var scale = zombieBillboard.lossyScale;
            tripleGlow.transform.localScale = new Vector3(bounds.size.x / Mathf.Abs(scale.x) * 1.85f,
                bounds.size.y / Mathf.Abs(scale.y) * 1.5f, 1f);
            tripleGlowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect);
            tripleGlow.sprite = tripleGlowSprite;
            tripleGlow.sortingLayerID = sprites[0].sortingLayerID;
            tripleGlow.sortingOrder = backOrder - 1;
            tripleGlow.color = new Color(.74f, .22f, 1f, .85f);
        }

        internal void PlayZombieMelee(Vector3 target)
        {
            if (zombieAnimator == null) return;
            FaceZombie(target - transform.position);
            zombieAnimator.SetTrigger(Attack);
        }

        private void FaceZombie(Vector3 delta)
        {
            var camera = Camera.main;
            if (camera == null || zombieBillboard == null) return;
            var horizontal = Vector3.Dot(delta, camera.transform.right);
            if (Mathf.Abs(horizontal) > .0001f)
                zombieBillboard.localScale = new Vector3(horizontal < 0f ? -1f : 1f, 1f, 1f);
        }

        private void UpdateZombie()
        {
            if (tripleGlow != null)
            {
                var tint = tripleGlow.color;
                tint.a = .80f + .12f * Mathf.Sin((float)zombieSession.Simulation.ElapsedTime * 4f + UnitId);
                tripleGlow.color = tint;
            }
            if (zombieAnimator == null) return;
            var speed = zombieSession.Simulation.IsPaused ? 0f : zombieSession.EffectivePlaybackSpeed;
            zombieAnimator.speed = speed;
            var delta = currentPosition - previousPosition;
            zombieAnimator.SetBool(Moving, delta.sqrMagnitude > .000001f);
            FaceZombie(delta);
            if (healEffect == null) return;
            zombieHealRemaining = Mathf.Max(0f, zombieHealRemaining - Time.deltaTime * speed);
            healEffect.SetVisible(zombieHealRemaining > 0f);
            if (zombieHealRemaining <= 0f) return;
            var phase = 1f - zombieHealRemaining / MusterZombieRules.HealAnimationTime;
            healEffect.Draw(MusterZombieRules.HealRadius, phase, 1f - phase);
        }

        private void ConfigureHealingRangeAnimation(bool isHealer)
        {
            if (isHealer && healEffect == null)
            {
                healEffect = new HealingRangeEffect(transform,
                    new Color(.25f, .95f, .50f), new Color(.35f, 1f, .60f));
                healEffect.Draw(HealNeighborRadius, 0f, 0f);
            }
            healEffect?.SetVisible(isHealer);
        }

        private void UpdateHealingRangeAnimation()
        {
            if (state == null || !state.IsAlive || state.Role != UnitRole.Heal || healEffect == null) return;

            healBurstBoost = Mathf.MoveTowards(healBurstBoost, 0f, Time.deltaTime * 1.5f);
            healWavePhase = (healWavePhase + Time.deltaTime / SquadCombatRules.HealInterval) % 1f;
            healEffect.Draw(HealNeighborRadius, healWavePhase, healBurstBoost);
        }

        public void ApplyAppearance(UnitRole role)
        {
            if (colors == null) colors = new MaterialPropertyBlock();
            var look = LegacyUnitLooks.For(role);
            var body = role == UnitRole.Test ? new Color(1, 0.85f, 0.2f) : look.Body;
            SetPart("Outline", look.Outline, true, new Vector3(look.OuterSize * LookScale, 0.26f, look.OuterSize * LookScale));
            SetPart("Body", body, true, new Vector3(look.InnerSize * LookScale, 0.008f, look.InnerSize * LookScale));
            var showMark = role != UnitRole.Test;
            SetPart("Mark Diamond", look.MarkColor, showMark && look.Mark == SoldierMark.Diamond,
                new Vector3(SoldierLook.DiamondSize * LookScale, 0.004f, SoldierLook.DiamondSize * LookScale));
            SetPart("Mark Plate", look.MarkColor, showMark && look.Mark == SoldierMark.Plate,
                new Vector3(SoldierLook.PlateSize * LookScale, 0.004f, SoldierLook.PlateSize * LookScale));
            SetPart("Mark Across", look.MarkColor, showMark && look.Mark == SoldierMark.Cross,
                new Vector3(SoldierLook.CrossLength * LookScale, 0.004f, SoldierLook.CrossWidth * LookScale));
            SetPart("Mark Upright", look.MarkColor, showMark && look.Mark == SoldierMark.Cross,
                new Vector3(SoldierLook.CrossWidth * LookScale, 0.004f, SoldierLook.CrossLength * LookScale));
            for (var i = 0; i < SoldierLook.CrownSegments.Length; i += 2)
            {
                var partName = "Mark Crown " + i;
                var part = transform.Find(partName);
                if (part == null && look.Mark == SoldierMark.Crown)
                {
                    var template = transform.Find("Mark Plate");
                    if (template == null) continue;
                    part = Instantiate(template, transform);
                    part.name = partName;
                }
                if (part == null) continue;
                var start = SoldierLook.CrownSegments[i];
                var end = SoldierLook.CrownSegments[i + 1];
                var delta = end - start;
                var center = (start + end) * (LookScale / 2);
                part.localPosition = new Vector3(center.x, 0.268f, center.y);
                part.localRotation = Quaternion.Euler(0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 0);
                SetPart(partName, look.MarkColor, showMark && look.Mark == SoldierMark.Crown,
                    new Vector3(delta.magnitude * LookScale, 0.004f, SoldierLook.CrownStroke * LookScale));
            }
        }

        private void SetPart(string partName, Color tint, bool visible, Vector3 scale)
        {
            var part = transform.Find(partName);
            if (part == null) return;
            part.gameObject.SetActive(visible);
            part.localScale = scale;
            colors.Clear();
            colors.SetColor("_BaseColor", tint);
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.SetPropertyBlock(colors);
        }

        public void SnapToState()
        {
            if (state == null) return;
            previousPosition = currentPosition = state.Position;
            previousAnchor = currentAnchor = state.AnchorPosition;
            transform.position = currentPosition;
            sampleTime = Time.timeAsDouble;
        }

        public void CaptureState()
        {
            previousPosition = currentPosition;
            currentPosition = state.Position;
            previousAnchor = currentAnchor; currentAnchor = state.AnchorPosition;
            sampleTime = Time.timeAsDouble;
            sampleInterval = Time.fixedDeltaTime;
        }

        public void Present(float alpha)
        {
            transform.position = IsRidingPlatform
                ? platform.RenderAnchor + platformSlot + Vector3.up * SquadPlatformView.DeckHeight
                : Vector3.Lerp(previousPosition, currentPosition, Mathf.Clamp01(alpha));
        }

        private void LateUpdate()
        {
            if (IsRidingPlatform)
                AdvancePlatformReposition(Time.unscaledDeltaTime * platform.PlaybackSpeed);
            Present(sampleInterval > 0 ? (float)((Time.timeAsDouble - sampleTime) / sampleInterval) : 1);
            UpdateHealingRangeAnimation();
            UpdateZombie();
            UpdateLevelBadge();
        }

        private void OnDisable()
        {
            if (levelBadge != null) levelBadge.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (levelBadge != null) Destroy(levelBadge.gameObject);
            if (tripleGlowSprite != null)
            {
                Destroy(tripleGlowSprite.texture);
                Destroy(tripleGlowSprite);
            }
            healEffect?.DestroyMaterials();
        }
    }
}
