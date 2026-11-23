
using System.Collections.Generic;
using Team5.BasicTowerTrial.Analytics;
using Team5.BasicTowerTrial.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Team5.BasicTowerTrial.Presentation.LegacyUI
{
    [DisallowMultipleComponent]
    public sealed class LegendarySkillPanel : MonoBehaviour
    {
        private sealed class SkillIcon
        {
            public int MemberId;
            public int Level;
            public UnitRole Role;
            public Button Button;
            public Image Badge;
            public TMP_Text CooldownText;
        }

        private readonly List<SkillIcon> icons = new List<SkillIcon>();
        private readonly List<RaycastResult> pointerHits =
            new List<RaycastResult>();

        private LegacyBattleHud hud;
        private BattleSimulation simulation;

        private RectTransform root;
        private RectTransform tooltipPanel;
        private TMP_Text tooltipText;
        private float uiScale = 1f;

        private bool aiming;
        private int aimingMemberId;

        private LineRenderer radiusRing;
        private Material ringMaterial;

        public static LegendarySkillPanel Attach(LegacyBattleHud hud)
        {
            var panel = hud.GetComponent<LegendarySkillPanel>();

            if (panel == null)
                panel = hud.gameObject.AddComponent<LegendarySkillPanel>();

            panel.Bind(hud);
            return panel;
        }

        private void Bind(LegacyBattleHud owner)
        {
            hud = owner;

            // Hide the two unused squad buttons.
            // Dynamic legendary icons replace them.
            for (int i = 1; i <= 2; i++)
            {
                var button = hud.SquadButton(i);

                if (button != null)
                    button.gameObject.SetActive(false);
            }

            if (root == null)
                Build();

            root.gameObject.SetActive(true);
        }

        private void Build()
        {
            var scaler = hud.GetComponent<CanvasScaler>();

            if (scaler != null &&
                scaler.uiScaleMode ==
                CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                uiScale = scaler.referenceResolution.x / 1280f;
            }

            root = LegacyUiKit.CreateRect(
                hud.transform,
                "Dynamic Legendary Skills",
                Vector2.zero,
                Vector2.zero
            );

            root.anchorMin = Vector2.up;
            root.anchorMax = Vector2.up;
            root.pivot = Vector2.up;
            root.anchoredPosition = Vector2.zero;

            // No skill buttons are created here.
            // They are generated from deployed members.
            BuildTooltip();
        }

        private void BuildTooltip()
        {
            var image = LegacyUiKit.CreateImage(
                root,
                "Skill Tooltip",
                Vector2.zero,
                new Vector2(340f, 84f) * uiScale,
                new Color(0.07f, 0.09f, 0.14f, 0.96f),
                8
            );

            image.raycastTarget = false;
            tooltipPanel = image.rectTransform;

            tooltipPanel.anchorMin = Vector2.up;
            tooltipPanel.anchorMax = Vector2.up;
            tooltipPanel.pivot = Vector2.up;

            tooltipText = LegacyUiKit.CreateText(
                tooltipPanel,
                "Tooltip Text",
                "",
                Vector2.zero,
                new Vector2(320f, 74f) * uiScale,
                13f * uiScale,
                LegacyUiKit.TextColor
            );

            tooltipText.alignment =
                TextAlignmentOptions.MidlineLeft;

            tooltipText.textWrappingMode =
                TextWrappingModes.Normal;

            tooltipText.enableAutoSizing = true;
            tooltipText.fontSizeMin = 10f * uiScale;
            tooltipText.fontSizeMax = 13f * uiScale;
            tooltipText.raycastTarget = false;

            tooltipPanel.gameObject.SetActive(false);
        }

        private void RebuildIcons()
        {
            CancelAiming();

            // Remove buttons from the previous battle.
            foreach (var icon in icons)
            {
                if (icon.Button != null)
                {
                    icon.Button.gameObject.SetActive(false);
                    Destroy(icon.Button.gameObject);
                }
            }

            icons.Clear();

            if (simulation == null ||
                simulation.PlannedMembers == null)
                return;

            var seenRoles = new HashSet<UnitRole>();

            // IMPORTANT:
            // PlannedMembers now follows formation insertion order.
            foreach (var member in simulation.PlannedMembers)
            {
                if (member == null)
                    continue;

                UnitRole role = member.Unit.Role;

                if (member.Unit.Rarity != UnitRarity.Legendary)
                    continue;

                if (!LegendarySkillRules.HasActiveSkill(role))
                    continue;

                if (!seenRoles.Add(role))
                    continue;

                CreateSkillIcon(
                    member.MemberId,
                    member.Unit.Level,
                    role,
                    icons.Count
                );
            }
        }

        private void CreateSkillIcon(
            int memberId,
            int level,
            UnitRole role,
            int index)
        {
            // Captain is positioned at Y = -124.
            // Skills fill positions below it, in insertion order.
            float y = -192f - index * 68f;

            var button = LegacyUiKit.CreateButton(
                root,
                role + " Skill",
                Vector2.zero,
                new Vector2(56f, 56f) * uiScale,
                LegacyUiKit.PanelLineColor,
                4
            );

            var rect = (RectTransform)button.transform;

            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;

            rect.anchoredPosition =
                new Vector2(24f, y) * uiScale;

            // Display the actual legendary unit's icon.
            var unitIcon = LegacyUnitIconUI.Create(
                rect,
                Vector2.zero,
                110f * uiScale
            );

            unitIcon.Rect.anchorMin = Vector2.up;
            unitIcon.Rect.anchorMax = Vector2.up;

            unitIcon.Rect.anchoredPosition =
                new Vector2(28f, -28f) * uiScale;

            unitIcon.SetType(role);

            // Cooldown badge.
            var badge = LegacyUiKit.CreateCircle(
                rect,
                "Cooldown Badge",
                Vector2.zero,
                27f * uiScale,
                new Color(0.08f, 0.1f, 0.16f, 0.95f)
            );

            badge.rectTransform.anchorMin = Vector2.one;
            badge.rectTransform.anchorMax = Vector2.one;
            badge.rectTransform.pivot = Vector2.one;
            badge.rectTransform.anchoredPosition = Vector2.zero;

            var cooldownText = LegacyUiKit.CreateText(
                badge.transform,
                "Cooldown",
                "",
                Vector2.zero,
                new Vector2(26f, 25f) * uiScale,
                14f * uiScale,
                Color.white,
                FontStyles.Bold
            );

            cooldownText.alignment =
                TextAlignmentOptions.Center;

            cooldownText.raycastTarget = false;

            var entry = new SkillIcon
            {
                MemberId = memberId,
                Level = level,
                Role = role,
                Button = button,
                Badge = badge,
                CooldownText = cooldownText
            };

            icons.Add(entry);

            button.onClick.AddListener(() => Activate(entry));
        }

        private UnitState FindCaster(int memberId)
        {
            if (simulation == null)
                return null;

            foreach (var unit in simulation.Units)
            {
                if (unit.MemberId == memberId)
                    return unit;
            }

            return null;
        }

        private bool IsHudFree =>
            hud != null &&
            hud.Session != null &&
            !hud.Session.IsUnitInspectionActive &&
            !hud.TutorialOwnsPause &&
            (hud.FormationEditor == null ||
             !hud.FormationEditor.gameObject.activeSelf);

        private void Update()
        {
            if (hud == null || hud.Session == null || root == null)
                return;

            var next = hud.Session.Simulation;

            if (next != simulation)
            {
                simulation = next;
                RebuildIcons();
            }

            RefreshIcons();

            if (aiming)
                UpdateTargeting();
            else
                UpdateTooltip();
        }

        private void RefreshIcons()
        {
            foreach (var icon in icons)
            {
                var caster = FindCaster(icon.MemberId);

                bool spawned = caster != null;
                bool alive = spawned && caster.IsAlive;

                bool ready =
                    alive &&
                    IsHudFree &&
                    !aiming &&
                    simulation.CanUseLegendarySkill(
                        icon.MemberId
                    );

                icon.Button.interactable = ready;

                icon.Button.image.color = alive
                    ? LegacyUiKit.PanelLineColor
                    : LegacyUiKit.NodeGreyColor;

                float cooldown = spawned
                    ? caster.SkillCooldownRemaining
                    : 0f;

                bool showBadge =
                    !alive || cooldown > 0.01f;

                icon.Badge.enabled = showBadge;
                icon.CooldownText.enabled = showBadge;

                if (showBadge)
                {
                    icon.CooldownText.text = !spawned
                        ? "..."
                        : !alive
                            ? "X"
                            : Mathf.CeilToInt(cooldown).ToString();
                }
            }
        }

        private void Activate(SkillIcon icon)
        {
            if (!IsHudFree ||
                simulation == null ||
                simulation.IsPaused)
                return;

            var caster = FindCaster(icon.MemberId);

            if (caster == null ||
                !simulation.CanUseLegendarySkill(icon.MemberId))
                return;

            if (icon.Role == UnitRole.Frostweaver)
            {
                aiming = true;
                aimingMemberId = icon.MemberId;

                EnsureRadiusRing();

                ShowTooltip(
                    icon,
                    "Click a tower area on the map to freeze it. " +
                    "Press Esc to cancel."
                );

                return;
            }

            // Warcaller and Aegis activate around themselves.
            if (!simulation.TryUseLegendarySkill(
                    icon.MemberId,
                    caster.Position))
                return;

            GameAnalytics.TrackBattle(
                "legendary_skill_used",
                ("role", icon.Role.ToString()),
                ("skill", LegendarySkillRules.ShortName(icon.Role))
            );
        }

        private void UpdateTargeting()
        {
            if (!IsHudFree ||
                simulation == null ||
                !simulation.CanUseLegendarySkill(
                    aimingMemberId))
            {
                CancelAiming();
                return;
            }

            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelAiming();
                return;
            }

            if (Mouse.current == null)
                return;

            Vector2 mouse = Mouse.current.position.ReadValue();

            bool hasPoint =
                TryGroundPoint(mouse, out Vector3 ground);

            if (radiusRing != null)
                radiusRing.enabled = hasPoint;

            if (hasPoint)
                DrawRadius(ground);

            if (!Mouse.current.leftButton.wasPressedThisFrame ||
                !hasPoint ||
                PointerOverUI(mouse))
                return;

            if (simulation.TryUseLegendarySkill(
                    aimingMemberId,
                    ground))
            {
                GameAnalytics.TrackBattle(
                    "legendary_skill_used",
                    ("role", "Frostweaver"),
                    ("skill", "Time Lock")
                );

                CancelAiming();
            }
            else
            {
                foreach (var icon in icons)
                {
                    if (icon.MemberId == aimingMemberId)
                    {
                        ShowTooltip(
                            icon,
                            "Place the circle over at least one living tower."
                        );
                        break;
                    }
                }
            }
        }

        private void UpdateTooltip()
        {
            if (tooltipPanel == null)
                return;

            tooltipPanel.gameObject.SetActive(false);

            if (Mouse.current == null)
                return;

            Vector2 mouse = Mouse.current.position.ReadValue();

            foreach (var icon in icons)
            {
                var rect = (RectTransform)icon.Button.transform;

                if (!RectTransformUtility
                    .RectangleContainsScreenPoint(
                        rect,
                        mouse,
                        null))
                    continue;

                string description =
                    LegendarySkillRules.Description(
                        icon.Role,
                        icon.Level
                    );

                ShowTooltip(icon, description);
                break;
            }
        }

        private void ShowTooltip(
            SkillIcon icon,
            string message)
        {
            if (tooltipPanel == null)
                return;

            int index = icons.IndexOf(icon);

            if (index < 0)
                return;

            float y = -192f - index * 68f;

            tooltipPanel.anchoredPosition =
                new Vector2(92f, y + 10f) * uiScale;

            tooltipText.text =
                LegendarySkillRules.ShortName(icon.Role) +
                "\n" + message;

            tooltipPanel.gameObject.SetActive(true);
            tooltipPanel.SetAsLastSibling();
        }

        private bool TryGroundPoint(
            Vector2 screen,
            out Vector3 ground)
        {
            ground = default;

            var camera = Camera.main;

            if (camera == null ||
                simulation == null ||
                simulation.Towers.Count == 0)
                return false;

            var ray = camera.ScreenPointToRay(screen);

            var plane = new Plane(
                Vector3.up,
                simulation.Towers[0].Position
            );

            if (!plane.Raycast(ray, out float distance) ||
                distance < 0)
                return false;

            ground = ray.GetPoint(distance);
            return true;
        }

        private bool PointerOverUI(Vector2 point)
        {
            if (EventSystem.current == null)
                return false;

            pointerHits.Clear();

            var data = new PointerEventData(
                EventSystem.current
            )
            {
                position = point
            };

            EventSystem.current.RaycastAll(data, pointerHits);

            return pointerHits.Count > 0;
        }

        private void EnsureRadiusRing()
        {
            if (radiusRing != null)
                return;

            var marker = new GameObject(
                "Time Lock Target Radius"
            );

            marker.transform.SetParent(transform, false);

            radiusRing = marker.AddComponent<LineRenderer>();

            radiusRing.useWorldSpace = true;
            radiusRing.loop = true;
            radiusRing.positionCount = 64;

            radiusRing.startWidth = 0.08f;
            radiusRing.endWidth = 0.08f;

            var color = new Color(
                0.4f, 0.9f, 1f, 0.95f
            );

            radiusRing.startColor = color;
            radiusRing.endColor = color;

            var shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                ringMaterial = new Material(shader);
                radiusRing.material = ringMaterial;
            }
        }

        private void DrawRadius(Vector3 center)
        {
            if (radiusRing == null)
                return;

            float radius =
                LegendarySkillRules.FreezeRadius;

            for (int i = 0;
                 i < radiusRing.positionCount;
                 i++)
            {
                float angle =
                    i * 2f * Mathf.PI /
                    radiusRing.positionCount;

                radiusRing.SetPosition(
                    i,
                    new Vector3(
                        center.x + Mathf.Cos(angle) * radius,
                        center.y + 0.12f,
                        center.z + Mathf.Sin(angle) * radius
                    )
                );
            }
        }

        private void CancelAiming()
        {
            aiming = false;
            aimingMemberId = 0;

            if (radiusRing != null)
                radiusRing.enabled = false;

            if (tooltipPanel != null)
                tooltipPanel.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            CancelAiming();

            if (root != null)
                root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (radiusRing != null)
                Destroy(radiusRing.gameObject);

            if (ringMaterial != null)
                Destroy(ringMaterial);
        }
    }
}
