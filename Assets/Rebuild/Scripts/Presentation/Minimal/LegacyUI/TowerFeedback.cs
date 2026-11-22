using System;
using System.Collections.Generic;
using Team5.Rebuild.Session;
using Team5.Rebuild.Simulation;
using Team5.Rebuild.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Team5.Rebuild.Presentation.LegacyUI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(250)]
    public sealed class TowerFeedback : MonoBehaviour
    {
        private static readonly Quaternion FlatToCamera = Quaternion.Euler(-90f, 0f, 0f);

        private readonly Dictionary<BattleTower, TowerBody> bodies = new Dictionary<BattleTower, TowerBody>();
        private readonly List<TowerFeedbackEvent> pending = new List<TowerFeedbackEvent>(TowerFeedbackRules.MaxQueued);
        private readonly System.Random random = new System.Random(5);
        private TowerSplash[] splashes = new TowerSplash[0];
        private TowerBurst[] bursts = new TowerBurst[0];
        private TowerBountyLabel[] labels = new TowerBountyLabel[0];
        private TowerBody[] byId = new TowerBody[0];
        private Transform[] minionLooks = new Transform[0];
        private float[] lastSplash = new float[0];
        private int nextSplash;
        private int nextBurst;
        private int nextLabel;
        private float clock;
        private bool rebound;
        private MinionMaterials materials;
        private Material splatMaterial;
        private BattleSession session;
        private BattleSimulation bound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateForFirstScene() => TryCreate(SceneManager.GetActiveScene());

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || BattleSceneFlow.IsPreviewScene(scene)) return;
            BattleSession found = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<TowerFeedback>() != null) return;
                if (found == null) found = root.GetComponentInChildren<BattleSession>(true);
            }
            if (found == null || !found.SquadCombat) return;
            var host = new GameObject("Tower Feedback");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<TowerFeedback>().Build(found);
        }

        private void Build(BattleSession battleSession)
        {
            session = battleSession;
            materials = new MinionMaterials();
            splatMaterial = materials.Glow(Color.white, materials.Splat);
            var disc = materials.Glow(Color.white, materials.Disc);
            var ring = materials.Glow(Color.white, materials.Ring);
            splashes = new TowerSplash[TowerFeedbackRules.SplashCount];
            for (var i = 0; i < splashes.Length; i++) splashes[i] = new TowerSplash(transform, materials, disc, ring);
            bursts = new TowerBurst[TowerFeedbackRules.BurstCount];
            for (var i = 0; i < bursts.Length; i++) bursts[i] = new TowerBurst(transform, materials, ring, splatMaterial, disc);
            var labelHost = new GameObject("Tower Bounty Labels", typeof(RectTransform));
            labelHost.transform.SetParent(transform, false);
            var canvas = LegacyUiKit.CreateCanvas(labelHost, 8, new Vector2(1280f, 720f), 0.5f);
            var raycaster = labelHost.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
            labels = new TowerBountyLabel[TowerFeedbackRules.LabelCount];
            for (var i = 0; i < labels.Length; i++) labels[i] = new TowerBountyLabel((RectTransform)canvas.transform);
            session.SimulationCreated += OnSimulationCreated;
            Rebind(session.Simulation);
        }

        private void OnSimulationCreated(BattleSimulation next) => Rebind(next);

        private void Rebind(BattleSimulation next)
        {
            Unbind();
            rebound = true;
            pending.Clear();
            HidePools();
            foreach (var body in bodies.Values) body.Restore();
            byId = new TowerBody[0];
            minionLooks = new Transform[0];
            lastSplash = new float[0];
            if (next == null) return;
            bound = next;
            var towers = session.Towers;
            var size = Math.Max(towers.Count, next.Towers.Count) + 1;
            byId = new TowerBody[size];
            minionLooks = new Transform[size];
            lastSplash = new float[size];
            for (var i = 0; i < size; i++) lastSplash[i] = float.NegativeInfinity;
            for (var i = 0; i < towers.Count; i++)
            {
                var tower = towers[i];
                if (tower == null) continue;
                var id = tower.RuntimeState != null ? tower.RuntimeState.Id : i + 1;
                if (id <= 0 || id >= size) continue;
                if (tower.IsMinion)
                {
                    var look = tower.transform.Find("Look");
                    minionLooks[id] = look != null ? look : tower.transform;
                    continue;
                }
                if (tower.IsWall) continue;
                if (!bodies.TryGetValue(tower, out var body))
                {
                    body = new TowerBody(tower, transform, materials.Flat, splatMaterial);
                    bodies.Add(tower, body);
                }
                byId[id] = body;
                if (id <= next.Towers.Count && !next.Towers[id - 1].IsAlive) body.Kill(TowerFeedbackRules.AwayFrom(body.Base, body.Base, Eye()), false);
            }
            next.TowerDamaged += OnTowerDamaged;
            next.TowerDestroyed += OnTowerDestroyed;
            next.TowerDied += OnTowerDied;
        }

        private void Unbind()
        {
            if (bound == null) return;
            bound.TowerDamaged -= OnTowerDamaged;
            bound.TowerDestroyed -= OnTowerDestroyed;
            bound.TowerDied -= OnTowerDied;
            bound = null;
        }

        private void OnTowerDamaged(TowerDamageEvent damage)
        {
            try
            {
                if (bound == null || pending.Count >= TowerFeedbackRules.MaxQueued) return;
                var id = damage.TowerId;
                if (id <= 0 || id >= byId.Length || byId[id] == null || id > bound.Towers.Count) return;
                var source = Source(damage.MemberId, byId[id].Base, out var paint);
                var kind = bound.IsTowerShielded(id) ? TowerFeedbackKind.Shielded : TowerFeedbackKind.Hit;
                var strength = TowerFeedbackRules.SquashStrength(damage.Amount, bound.Towers[id - 1].Definition.MaxHp);
                pending.Add(new TowerFeedbackEvent(kind, id, source, paint, strength, 0));
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        private void OnTowerDestroyed(TowerDestroyedEvent destroyed)
        {
            try
            {
                if (bound == null || pending.Count >= TowerFeedbackRules.MaxQueued * 2) return;
                var id = destroyed.TowerId;
                if (id <= 0 || id >= byId.Length || byId[id] == null) return;
                var source = Source(destroyed.KillerMemberId, byId[id].Base, out var paint);
                pending.Add(new TowerFeedbackEvent(TowerFeedbackKind.Destroyed, id, source, paint, 1f, destroyed.GoldBounty));
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        private void OnTowerDied(int id)
        {
            try
            {
                if (bound == null || pending.Count >= TowerFeedbackRules.MaxQueued * 2) return;
                if (id <= 0 || id >= minionLooks.Length || minionLooks[id] == null) return;
                pending.Add(new TowerFeedbackEvent(TowerFeedbackKind.MinionDown, id, minionLooks[id].position, MinionPalette.Glass, 1f, 0));
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        private Vector3 Source(int unitId, Vector3 fallback, out Color paint)
        {
            var units = bound.Units;
            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.Id != unitId) continue;
                paint = PaintSquadRoleLooks.Primary(unit.Role);
                return unit.Position;
            }
            paint = TowerFeedbackRules.NeutralPaint;
            return fallback;
        }

        private void LateUpdate()
        {
            if (session == null) return;
            if (!rebound || session.Simulation != bound) Rebind(session.Simulation);
            var deltaTime = Mathf.Min(Time.unscaledDeltaTime, TowerFeedbackRules.MaxFrameDelta);
            clock += deltaTime;
            var view = Camera.main;
            var eye = view != null ? view.transform.position : Eye();
            var facing = view != null ? view.transform.rotation * FlatToCamera : FlatToCamera;
            Drain(eye);
            var states = bound != null ? bound.Towers : null;
            for (var id = 1; id < byId.Length; id++)
            {
                var body = byId[id];
                if (body == null) continue;
                var dead = states != null && id <= states.Count && !states[id - 1].IsAlive;
                if (dead && !body.IsCollapsed) body.Kill(TowerFeedbackRules.AwayFrom(body.Base, body.Base, eye), false);
                body.Advance(deltaTime);
                body.Pose();
                body.Paint(dead);
            }
            for (var i = 0; i < splashes.Length; i++) splashes[i].Tick(deltaTime, facing);
            for (var i = 0; i < bursts.Length; i++) bursts[i].Tick(deltaTime, facing);
            for (var i = 0; i < labels.Length; i++) labels[i].Tick(deltaTime, view);
        }

        private void Drain(Vector3 eye)
        {
            for (var i = 0; i < pending.Count; i++)
            {
                var item = pending[i];
                if (item.Kind == TowerFeedbackKind.Destroyed) PlayKill(item, eye);
                else if (item.Kind == TowerFeedbackKind.MinionDown) PlayMinion(item);
                else if (!DestroyedLater(item.TowerId, i)) PlayHit(item, eye);
            }
            pending.Clear();
        }

        private bool DestroyedLater(int id, int from)
        {
            for (var i = from + 1; i < pending.Count; i++)
                if (pending[i].Kind == TowerFeedbackKind.Destroyed && pending[i].TowerId == id) return true;
            return false;
        }

        private void PlayHit(TowerFeedbackEvent item, Vector3 eye)
        {
            var body = byId[item.TowerId];
            if (body == null || body.IsCollapsed) return;
            var shielded = item.Kind == TowerFeedbackKind.Shielded;
            var strength = shielded ? TowerFeedbackRules.ShieldedStrength : 1f;
            body.Hit(strength, item.Strength * strength);
            if (clock - lastSplash[item.TowerId] < TowerFeedbackRules.SplashCooldown) return;
            lastSplash[item.TowerId] = clock;
            var center = body.Base;
            var direction = TowerFeedbackRules.HitDirection(center, item.Source, eye);
            var splash = splashes[nextSplash];
            nextSplash = (nextSplash + 1) % splashes.Length;
            if (shielded)
            {
                splash.PlayRipple(TowerFeedbackRules.DomePoint(center, direction), direction, center.y, MinionPalette.Glass);
                return;
            }
            splash.PlayPaint(TowerFeedbackRules.ImpactPoint(center, direction, eye), direction, center.y, item.Paint, random);
            body.AddStain(direction, item.Paint, random, eye);
        }

        private void PlayKill(TowerFeedbackEvent item, Vector3 eye)
        {
            var body = byId[item.TowerId];
            if (body == null) return;
            var center = body.Base;
            body.Kill(TowerFeedbackRules.AwayFrom(center, item.Source, eye), true);
            var burst = bursts[nextBurst];
            nextBurst = (nextBurst + 1) % bursts.Length;
            burst.PlayTower(center, item.Paint, body.DebrisMaterials, random);
            if (item.Bounty <= 0) return;
            var label = labels[nextLabel];
            nextLabel = (nextLabel + 1) % labels.Length;
            label.Play(body.TopAnchor, "+" + item.Bounty + "G");
        }

        private void PlayMinion(TowerFeedbackEvent item)
        {
            var burst = bursts[nextBurst];
            nextBurst = (nextBurst + 1) % bursts.Length;
            burst.PlayMinion(item.Source, random);
        }

        private Vector3 Eye()
        {
            var view = Camera.main;
            return view != null ? view.transform.position : transform.position + new Vector3(0f, 50f, -50f);
        }

        private void HidePools()
        {
            for (var i = 0; i < splashes.Length; i++) splashes[i].Hide();
            for (var i = 0; i < bursts.Length; i++) bursts[i].Hide();
            for (var i = 0; i < labels.Length; i++) labels[i].Hide();
        }

        private void OnDestroy()
        {
            Unbind();
            if (session != null) session.SimulationCreated -= OnSimulationCreated;
            foreach (var body in bodies.Values) body.Restore();
            bodies.Clear();
            if (materials != null) materials.Dispose();
        }
    }
}
