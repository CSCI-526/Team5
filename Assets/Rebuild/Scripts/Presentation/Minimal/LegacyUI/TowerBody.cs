using System.Collections.Generic;
using Team5.Rebuild.World;
using UnityEngine;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    public sealed class TowerBody
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Bright = new Color(TowerFeedbackRules.FlashBrightness, TowerFeedbackRules.FlashBrightness, TowerFeedbackRules.FlashBrightness, 1f);

        private readonly Transform root;
        private readonly Vector3 restScale;
        private readonly Quaternion restRotation;
        private readonly MeshRenderer[] renderers;
        private readonly Color[] baseColors;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock stainBlock = new MaterialPropertyBlock();
        private readonly MeshRenderer[] stains = new MeshRenderer[TowerFeedbackRules.MaxStains];
        private readonly Vector3[] stainPoints = new Vector3[TowerFeedbackRules.MaxStains];
        private readonly Quaternion[] stainTurns = new Quaternion[TowerFeedbackRules.MaxStains];
        private readonly float[] stainSizes = new float[TowerFeedbackRules.MaxStains];
        private readonly float topHeight;
        private int stainCount;
        private int nextStain;
        private float flashLeft;
        private float flashPeak;
        private float squashAge = -1f;
        private float squashPeak;
        private float deathFlashLeft;
        private float collapseAge;
        private bool painting;
        private bool posed;
        private Vector3 away = Vector3.forward;

        public TowerBody(BattleTower tower, Transform effects, Mesh flat, Material stainMaterial)
        {
            Tower = tower;
            root = tower.transform;
            restScale = root.localScale;
            restRotation = root.rotation;
            renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            baseColors = new Color[renderers.Length];
            var debris = new List<Material>();
            var top = root.position.y + 1.5f;
            var found = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].sharedMaterial;
                baseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
                if (!renderers[i].enabled || !renderers[i].gameObject.activeInHierarchy) continue;
                if (material != null && !material.name.StartsWith("StageV3", System.StringComparison.Ordinal) && !debris.Contains(material)) debris.Add(material);
                top = found ? Mathf.Max(top, renderers[i].bounds.max.y) : renderers[i].bounds.max.y;
                found = true;
            }
            topHeight = top - root.position.y;
            DebrisMaterials = debris.ToArray();
            for (var i = 0; i < stains.Length; i++)
            {
                stains[i] = MinionMaterials.Show(effects, "Tower Stain", flat, stainMaterial);
                stains[i].gameObject.SetActive(false);
            }
        }

        public BattleTower Tower { get; }
        public Material[] DebrisMaterials { get; }
        public bool IsCollapsed { get; private set; }
        public Vector3 Base => root != null ? root.position : Vector3.zero;
        public Vector3 TopAnchor => Base + Vector3.up * (topHeight + TowerFeedbackRules.LabelLift);

        private float CurrentFlash => flashPeak * Mathf.Clamp01(flashLeft / TowerFeedbackRules.FlashSeconds);
        private float CurrentSquash => squashAge < 0f ? 0f : squashPeak * TowerFeedbackRules.Pulse(squashAge, TowerFeedbackRules.SquashSeconds);

        public void Hit(float strength, float squashStrength)
        {
            if (IsCollapsed) return;
            flashPeak = Mathf.Max(CurrentFlash, TowerFeedbackRules.FlashAmount * strength);
            flashLeft = TowerFeedbackRules.FlashSeconds;
            var squash = TowerFeedbackRules.SquashMax * squashStrength;
            if (squashAge < 0f || squashAge >= TowerFeedbackRules.SquashSeconds * 0.5f)
            {
                squashPeak = squash;
                squashAge = 0f;
            }
            else squashPeak = Mathf.Max(squashPeak, squash);
        }

        public void Kill(Vector3 direction, bool animate)
        {
            IsCollapsed = true;
            away = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            collapseAge = animate ? 0f : TowerFeedbackRules.CollapseSeconds;
            deathFlashLeft = animate ? TowerFeedbackRules.DeathFlashSeconds : 0f;
            flashLeft = 0f;
            flashPeak = 0f;
            squashAge = -1f;
        }

        public void AddStain(Vector3 direction, Color paint, System.Random random, Vector3 eye)
        {
            if (IsCollapsed || root == null) return;
            var inverse = Quaternion.Inverse(restRotation);
            var local = inverse * TowerFeedbackRules.Flat(eye - root.position);
            var normal = Mathf.Abs(local.x) > Mathf.Abs(local.z) ? new Vector3(Mathf.Sign(local.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z));
            var side = Vector3.Cross(Vector3.up, normal);
            var across = Vector3.Dot(inverse * direction, side) * TowerFeedbackRules.StainHalfWidth + ((float)random.NextDouble() - 0.5f) * 0.12f;
            across = Mathf.Clamp(across, -TowerFeedbackRules.StainHalfWidth, TowerFeedbackRules.StainHalfWidth);
            var height = TowerFeedbackRules.StainHeight(stainCount, (float)random.NextDouble() * 0.2f);
            var index = nextStain;
            nextStain = (nextStain + 1) % stains.Length;
            stainCount++;
            stainPoints[index] = normal * TowerFeedbackRules.StainDepth + side * across + Vector3.up * height;
            stainSizes[index] = Mathf.Lerp(TowerFeedbackRules.StainMinSize, TowerFeedbackRules.StainMaxSize, (float)random.NextDouble());
            stainTurns[index] = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            var stain = stains[index];
            if (stain == null) return;
            stainBlock.SetColor(BaseColorId, new Color(paint.r, paint.g, paint.b, 0.95f));
            stain.SetPropertyBlock(stainBlock);
            stain.gameObject.SetActive(true);
        }

        public void Advance(float deltaTime)
        {
            flashLeft = Mathf.Max(0f, flashLeft - deltaTime);
            deathFlashLeft = Mathf.Max(0f, deathFlashLeft - deltaTime);
            if (squashAge >= 0f)
            {
                squashAge += deltaTime;
                if (squashAge >= TowerFeedbackRules.SquashSeconds) squashAge = -1f;
            }
            if (IsCollapsed) collapseAge = Mathf.Min(TowerFeedbackRules.CollapseSeconds, collapseAge + deltaTime);
        }

        public void Pose()
        {
            if (root == null) return;
            var squash = CurrentSquash;
            var collapse = IsCollapsed ? TowerFeedbackRules.EaseOut(collapseAge / TowerFeedbackRules.CollapseSeconds) : 0f;
            var moving = squash > 0f || (IsCollapsed && collapseAge < TowerFeedbackRules.CollapseSeconds);
            if (moving || posed || collapse > 0f && !Settled(collapse))
            {
                var vertical = (1f - squash) * (1f - TowerFeedbackRules.CollapseSink * collapse);
                var spread = (1f + squash * 0.5f) * (1f + TowerFeedbackRules.CollapseBulge * collapse);
                root.localScale = Vector3.Scale(restScale, new Vector3(spread, vertical, spread));
                var axis = Vector3.Cross(Vector3.up, away);
                var tilt = collapse > 0f && axis.sqrMagnitude > 0.0001f ? Quaternion.AngleAxis(TowerFeedbackRules.CollapseTilt * collapse, axis.normalized) : Quaternion.identity;
                root.rotation = tilt * restRotation;
                posed = moving;
            }
            PlaceStains();
        }

        public void Paint(bool dead)
        {
            var amount = dead
                ? TowerFeedbackRules.DeathFlashAmount * Mathf.Clamp01(deathFlashLeft / TowerFeedbackRules.DeathFlashSeconds)
                : CurrentFlash;
            if (amount <= 0f && !painting) return;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;
                block.SetColor(BaseColorId, Color.Lerp(dead ? Color.gray : baseColors[i], Bright, amount));
                renderer.SetPropertyBlock(block);
            }
            painting = amount > 0f;
        }

        public void Restore()
        {
            if (painting)
                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    block.SetColor(BaseColorId, baseColors[i]);
                    renderers[i].SetPropertyBlock(block);
                }
            painting = false;
            IsCollapsed = false;
            flashLeft = 0f;
            flashPeak = 0f;
            squashAge = -1f;
            deathFlashLeft = 0f;
            collapseAge = 0f;
            stainCount = 0;
            nextStain = 0;
            posed = false;
            foreach (var stain in stains)
                if (stain != null) stain.gameObject.SetActive(false);
            if (root == null) return;
            root.localScale = restScale;
            root.rotation = restRotation;
        }

        private bool Settled(float collapse)
        {
            var target = Vector3.Scale(restScale, new Vector3(1f + TowerFeedbackRules.CollapseBulge * collapse, 1f - TowerFeedbackRules.CollapseSink * collapse, 1f + TowerFeedbackRules.CollapseBulge * collapse));
            return (root.localScale - target).sqrMagnitude < 0.000001f;
        }

        private void PlaceStains()
        {
            for (var i = 0; i < stains.Length; i++)
            {
                var stain = stains[i];
                if (stain == null || !stain.gameObject.activeSelf) continue;
                var place = stain.transform;
                place.SetPositionAndRotation(root.TransformPoint(stainPoints[i]), root.rotation * stainTurns[i]);
                place.localScale = Vector3.one * stainSizes[i];
            }
        }
    }
}
