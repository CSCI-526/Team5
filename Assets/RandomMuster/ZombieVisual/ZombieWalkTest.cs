using System.Collections;
using UnityEngine;

namespace RandomMuster
{
    public sealed class ZombieWalkTest : MonoBehaviour
    {
        [SerializeField] private ZombieUnit zombie;
        [SerializeField] private ZombieUnit companion;
        [SerializeField] private Vector3[] route;
        [SerializeField] private Transform woodenGate;
        [SerializeField, Min(1)] private int gateRouteIndex = 6;
        [SerializeField, Min(1)] private int gateHealth = 8;
        [SerializeField, Min(0)] private float attackImpactTime = .14f;

        private IEnumerator Start()
        {
            if (zombie == null || route == null || route.Length < 2) yield break;
            while (true)
            {
                zombie.ResetHealth();
                if (woodenGate != null) woodenGate.gameObject.SetActive(true);
                Coroutine companionWalk = null;
                if (companion != null)
                {
                    companion.ResetHealth();
                    companionWalk = StartCoroutine(WalkRoute(companion, false));
                }
                yield return WalkRoute(zombie, true);
                if (companionWalk != null) yield return companionWalk;
                yield return new WaitForSeconds(.9f);
            }
        }

        private IEnumerator WalkRoute(ZombieUnit unit, bool opensGate)
        {
            var mover = unit.transform;
            var visual = unit.Visual;
            var laneOffset = opensGate ? Vector3.zero : Vector3.back * .45f;
            mover.position = route[0] + laneOffset;
            FaceSegment(visual, route[1] - route[0]);
            yield return new WaitForSeconds(opensGate ? .45f : 1.5f);
            visual.SetMoving(true);
            for (var i = 1; i < route.Length; i++)
            {
                var destination = route[i] + laneOffset;
                FaceSegment(visual, destination - mover.position);
                if (woodenGate != null && woodenGate.gameObject.activeSelf && i == gateRouteIndex)
                {
                    var previous = route[i - 1] + laneOffset;
                    yield return MoveTo(unit, Vector3.MoveTowards(destination, previous, opensGate ? .85f : 1.4f));
                    visual.SetMoving(false);
                    if (opensGate)
                        yield return AttackGate();
                    else
                        while (woodenGate.gameObject.activeSelf) yield return null;
                    visual.SetMoving(true);
                }
                yield return MoveTo(unit, destination);
            }
            visual.SetMoving(false);
        }

        private static IEnumerator MoveTo(ZombieUnit unit, Vector3 destination)
        {
            var mover = unit.transform;
            while (mover.position != destination)
            {
                mover.position = Vector3.MoveTowards(mover.position, destination, unit.MoveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator AttackGate()
        {
            zombie.Visual.SetMoving(false);
            var rest = woodenGate.localPosition;
            var remainingHealth = gateHealth;
            yield return new WaitForSeconds(.15f);
            while (remainingHealth > 0 && zombie.IsAlive)
            {
                var damage = zombie.Attack();
                yield return new WaitForSeconds(attackImpactTime);
                remainingHealth -= damage;
                if (remainingHealth <= 0)
                    woodenGate.gameObject.SetActive(false);
                else
                    woodenGate.localPosition = rest + (woodenGate.position - zombie.transform.position).normalized * .08f;
                yield return new WaitForSeconds(.10f);
                woodenGate.localPosition = rest;
                yield return new WaitForSeconds(Mathf.Max(0, zombie.AttackInterval - attackImpactTime - .10f));
            }
        }

        private static void FaceSegment(ZombieVisual visual, Vector3 direction)
        {
            var camera = Camera.main;
            var horizontal = Vector3.Dot(direction, camera != null ? camera.transform.right : Vector3.right);
            if (Mathf.Abs(horizontal) > .001f)
                visual.SetFacingLeft(horizontal < 0);
        }
    }
}
