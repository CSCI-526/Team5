using System.Collections;
using System.Linq;
using Team5.BasicTowerTrial.World;
using UnityEngine;

namespace RandomMuster
{
    public sealed class ShamanZombieBattleTest : MonoBehaviour
    {
        [SerializeField] private ZombieUnit[] frontLine;
        [SerializeField] private ZombieUnit shaman;
        [SerializeField] private BattleTower archer;
        [SerializeField] private GameObject gate;
        [SerializeField] private Vector3[] route;
        [SerializeField, Min(1)] private int gateRouteIndex = 6;
        [SerializeField, Min(1)] private int gateMaxHealth = 80;

        private ZombieUnit[] squad;
        private float[] progress;
        private bool[] arrived;
        private Material arrowMaterial;
        private LineRenderer arrow;
        public int GateHealth { get; private set; }
        public int ArrowHits { get; private set; }
        public bool ReachedEnd { get; private set; }
        private bool finished;

        private IEnumerator Start()
        {
            GateHealth = gateMaxHealth;
            squad = frontLine.Append(shaman).ToArray();
            progress = new float[squad.Length];
            arrived = new bool[squad.Length];
            CreateArrow();
            StartCoroutine(FireArrows());
            var walks = new Coroutine[squad.Length];
            for (var i = 0; i < squad.Length; i++)
                walks[i] = StartCoroutine(WalkRoute(i));
            foreach (var walk in walks) yield return walk;

            ReachedEnd = arrived.Any(value => value);
            finished = true;
        }

        private IEnumerator WalkRoute(int memberIndex)
        {
            var unit = squad[memberIndex];
            var isShaman = unit == shaman;
            var lane = isShaman ? 0f : (memberIndex - 1) * .85f;
            for (var index = 1; index < route.Length && unit.IsAlive; index++)
            {
                var direction = (route[index] - route[index - 1]).normalized;
                var side = Vector3.Cross(Vector3.up, direction);
                var destination = route[index] + side * lane;
                var camera = Camera.main;
                var horizontal = Vector3.Dot(direction, camera != null ? camera.transform.right : Vector3.right);
                if (Mathf.Abs(horizontal) > .001f)
                    unit.Visual.SetFacingLeft(horizontal < 0f);

                if (index == gateRouteIndex && GateHealth > 0)
                {
                    var stop = destination - direction * (isShaman ? 1.5f : .85f);
                    if (Vector3.Dot(stop - unit.transform.position, direction) > 0)
                        yield return MoveTo(unit, memberIndex, stop, index);
                    unit.Visual.SetMoving(false);
                    if (isShaman)
                    {
                        while (unit.IsAlive && GateHealth > 0 && frontLine.Any(front => front.IsAlive))
                            yield return null;
                    }
                    else yield return AttackGate(unit, memberIndex * .15f);
                    if (GateHealth > 0) break;
                }

                yield return MoveTo(unit, memberIndex, destination, index);
                if (index == route.Length - 1 && unit.IsAlive) arrived[memberIndex] = true;
            }
            if (unit.IsAlive) unit.Visual.SetMoving(false);
        }

        private IEnumerator MoveTo(ZombieUnit unit, int memberIndex, Vector3 destination, int index)
        {
            var mover = unit.transform;
            unit.Visual.SetMoving(true);
            while (unit.IsAlive && mover.position != destination)
            {
                mover.position = Vector3.MoveTowards(mover.position, destination, unit.MoveSpeed * Time.deltaTime);
                progress[memberIndex] = index - Vector3.Distance(mover.position, destination);
                yield return null;
            }
        }

        private IEnumerator AttackGate(ZombieUnit unit, float delay)
        {
            yield return new WaitForSeconds(delay);
            while (unit.IsAlive && GateHealth > 0)
            {
                var damage = unit.Attack();
                yield return new WaitForSeconds(.36f);
                if (unit.IsAlive && GateHealth > 0)
                {
                    GateHealth = Mathf.Max(0, GateHealth - damage);
                    if (GateHealth == 0) gate.SetActive(false);
                }
                yield return new WaitForSeconds(Mathf.Max(0f, unit.AttackInterval - .36f));
            }
        }

        private IEnumerator FireArrows()
        {
            var stats = archer.CaptureDefinition(1);
            var origin = archer.transform.position + Vector3.up * 2.4f;
            const float flightTime = .36f;
            while (!finished)
            {
                ZombieUnit target = null;
                var targetIndex = -1;
                var bestProgress = float.NegativeInfinity;
                for (var i = 0; i < squad.Length; i++)
                {
                    var unit = squad[i];
                    var delta = unit.transform.position - archer.transform.position;
                    if (!unit.IsAlive || arrived[i] || delta.x * delta.x + delta.z * delta.z > stats.Range * stats.Range ||
                        progress[i] <= bestProgress) continue;
                    target = unit;
                    targetIndex = i;
                    bestProgress = progress[i];
                }
                if (target == null)
                {
                    yield return null;
                    continue;
                }

                var destination = target.transform.position + Vector3.up * .45f;
                var fired = Time.time;
                while (Time.time - fired < flightTime && !finished)
                {
                    var fraction = Mathf.Clamp01((Time.time - fired) / flightTime);
                    arrow.enabled = true;
                    arrow.SetPosition(0, ArrowPoint(origin, destination, Mathf.Max(0f, fraction - .09f)));
                    arrow.SetPosition(1, ArrowPoint(origin, destination, fraction));
                    yield return null;
                }
                arrow.enabled = false;
                if (!finished && target.IsAlive && !arrived[targetIndex])
                {
                    target.TakeDamage(Random.Range(stats.MinimumShotDamage, stats.Damage));
                    ArrowHits++;
                    if (!target.IsAlive) target.gameObject.SetActive(false);
                }
                yield return new WaitForSeconds(Mathf.Max(0f, stats.AttackInterval - flightTime));
            }
        }

        private static Vector3 ArrowPoint(Vector3 from, Vector3 to, float t) =>
            Vector3.Lerp(from, to, t) + Vector3.up * (2.88f * t * (1f - t));

        private void CreateArrow()
        {
            arrow = new GameObject("Archer projectile").AddComponent<LineRenderer>();
            arrow.transform.SetParent(transform, false);
            arrowMaterial = new Material(Shader.Find("Sprites/Default"));
            arrow.sharedMaterial = arrowMaterial;
            arrow.startColor = arrow.endColor = new Color(1f, .79f, .36f);
            arrow.startWidth = arrow.endWidth = .055f;
            arrow.positionCount = 2;
            arrow.enabled = false;
        }

        private void OnGUI()
        {
            if (squad == null) return;
            GUI.Box(new Rect(14, 14, 305, 130), "Shaman Test");
            GUI.Label(new Rect(26, 42, 280, 22), $"Wooden gate: {GateHealth} / {gateMaxHealth} HP");
            GUI.Label(new Rect(26, 64, 280, 22),
                $"Brutes: {Mathf.CeilToInt(frontLine[0].Health)} / {Mathf.CeilToInt(frontLine[1].Health)} / {Mathf.CeilToInt(frontLine[2].Health)} HP");
            GUI.Label(new Rect(26, 86, 280, 22), $"Shaman: {Mathf.CeilToInt(shaman.Health)} / {shaman.MaxHealth} HP");
            GUI.Label(new Rect(26, 108, 280, 22), ReachedEnd ? "Reached the exit" : $"Archer hits: {ArrowHits}");
        }

        private void OnDestroy()
        {
            if (arrowMaterial != null) Destroy(arrowMaterial);
        }
    }
}
