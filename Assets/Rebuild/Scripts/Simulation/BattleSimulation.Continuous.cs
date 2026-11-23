using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Team5.Rebuild.Simulation
{
    public enum MovementCommandSource { Player, AI }
    public readonly struct MovementRequest
    {
        public readonly Vector3 Target;
        public readonly int? MemberId;
        public readonly MovementCommandSource Source;
        public MovementRequest(Vector3 target, int? memberId = null, MovementCommandSource source = MovementCommandSource.Player)
        { Target = target; MemberId = memberId; Source = source; }
    }

    public sealed partial class BattleSimulation
    {
        private ContinuousMovement continuous;
        public bool ContinuousMovementEnabled => continuous != null;
        public Vector3 SquadReferencePosition => continuous != null ? continuous.Reference : definition.Waypoints[0];
        public Vector3 SquadDestination => continuous != null ? continuous.Destination : definition.Waypoints[0];
        public bool IsSquadMoving => continuous != null && continuous.Moving;
        public bool IsFormationAdjusting => continuous != null && continuous.Reforming;
        // Decimal Inspector values can straddle this boundary under Mono floating-point evaluation.
        public static bool ValidContinuousBodySettings(float formationSpacing, float soldierRadius, bool compactSquad = false) =>
            BattleNumbers.Finite(formationSpacing) && BattleNumbers.Finite(soldierRadius) && soldierRadius >= .02f &&
            (compactSquad ? formationSpacing >= .05f : formationSpacing + .00001f >= soldierRadius * 2 + .01f);
        public void EnableContinuousMovement(float formationSpacing = .48f, float soldierRadius = .2f, bool compactSquad = false)
        {
            if (!ValidContinuousBodySettings(formationSpacing, soldierRadius, compactSquad))
                throw new ArgumentException("Invalid continuous formation spacing or obstacle collision radius.");
            EnableManualMovement();
            continuous = new ContinuousMovement(this, definition.Waypoints[0], formationSpacing, soldierRadius, compactSquad);
        }
        public bool RequestMovement(MovementRequest request) => !stepping && continuous != null &&
            Outcome == BattleOutcome.Running && PendingSpawnCount == 0 && continuous.Request(request);

        public IReadOnlyList<Vector3> PreviewSquadPath(Vector3 from, Vector3 to) =>
            continuous?.PreviewPath(from, to);

        public bool CanStandOnSquadRoute(Vector3 point) => continuous != null && continuous.CanStandOnRoute(point);

        // Continuous navigation owns no transforms, physics bodies or SlotIds. Callers specify actual endpoints.
        private sealed class ContinuousMovement
        {
            private readonly BattleSimulation sim;
            public Vector3 Reference;
            private Vector3 destination;
            public Vector3 Destination => destination;
            private Vector3 lastHeading = Vector3.forward;
            private List<Vector3> referencePath = new List<Vector3>();
            private readonly Dictionary<int, Travel> travels = new Dictionary<int, Travel>();
            private readonly float Spacing, Radius;
            private readonly bool Compact;
            private bool reforming, pendingReform, continueAfterReform;
            private Dictionary<int, Vector3> previousOffsets = new Dictionary<int, Vector3>();
            private sealed class Travel
            {
                public Vector3 Goal;
                public List<Vector3> Path;
                public int Next;
                public float Blocked;
                public float Departed;
                public Vector3 QueueForward;
                public bool Yielding;
                public int? WaitFor;
                public int[] WaitForPaths, YieldFor;
                public List<int> SharedStages;
                public MovementCommandSource Source;
                public Travel Copy() => new Travel { Goal = Goal, Path = new List<Vector3>(Path), Next = Next, Blocked = Blocked, Departed = Departed, QueueForward = QueueForward, Source = Source, Yielding = Yielding, WaitFor = WaitFor,
                    WaitForPaths = WaitForPaths == null ? null : (int[])WaitForPaths.Clone(), YieldFor = YieldFor == null ? null : (int[])YieldFor.Clone(),
                    SharedStages = SharedStages == null ? null : new List<int>(SharedStages) };
            }
            public bool Moving => travels.Any(t => sim.units.Any(u => u.Id == t.Key && u.IsAlive &&
                (t.Value.Next < t.Value.Path.Count || (u.Position - t.Value.Goal).sqrMagnitude > .000001f)));
            public bool Reforming => reforming || pendingReform;
            private bool IsTravelling(int id) => sim.units.Any(u => u.Id == id && u.IsAlive) &&
                travels.TryGetValue(id, out var travel) && travel.Next < travel.Path.Count;
            public ContinuousMovement(BattleSimulation owner, Vector3 origin, float spacing, float radius, bool compact = false)
            { sim = owner; Reference = destination = origin; Spacing = spacing; Radius = radius; Compact = compact; }
            public bool CanPlaceFormation(Vector3 anchor) => (sim.definition.Ladders.Count>0||Mathf.Abs(anchor.y-Reference.y)<.01f) && sim.units.Any(u => u.IsAlive) &&
                sim.units.Where(u => u.IsAlive).All(u => Stand(anchor + u.FormationOffset, Radius));
            public ContinuousMovement Copy(BattleSimulation owner)
            {
                var copy = new ContinuousMovement(owner, Reference, Spacing, Radius, Compact) { destination = destination, lastHeading = lastHeading, referencePath = new List<Vector3>(referencePath) };
                foreach (var item in travels) copy.travels.Add(item.Key, item.Value.Copy());
                copy.reforming = reforming; copy.pendingReform = pendingReform; copy.continueAfterReform = continueAfterReform;
                copy.previousOffsets = new Dictionary<int, Vector3>(previousOffsets);
                return copy;
            }
            public void Spawn(UnitState unit)
            {
                unit.ContinuousPosition = true;
                unit.BodyRadius = Radius;
                unit.FormationOffset = SquadCombatRules.Offset(unit.SlotId) * Spacing;
                unit.Position = Reference + unit.FormationOffset;
                unit.SquadReference = Reference;
            }
            public bool ApplyFormation(int[] destinations)
            {
                var living = sim.units.Where(u => u.IsAlive).ToArray();
                if (living.All(u => destinations[u.SlotId] == u.SlotId)) return true;
                if (!reforming && !pendingReform) continueAfterReform = Moving;
                previousOffsets = living.ToDictionary(u => u.Id, u => u.FormationOffset);
                for (var i = 0; i < living.Length; i++)
                {
                    living[i].FormationSlot = SquadCombatRules.Slot(destinations[living[i].SlotId]);
                    living[i].FormationOffset = SquadCombatRules.Offset(living[i].SlotId) * Spacing;
                }
                pendingReform = true;
                TryBeginReform();
                sim.FormationChanged?.Invoke();
                return true;
            }
            private void TryBeginReform()
            {
                var living = sim.units.Where(u => u.IsAlive).ToArray();
                if (living.Length == 0) return;
                var center = living.Aggregate(Vector3.zero, (sum, u) => sum + u.Position - previousOffsets[u.Id]) / living.Length;
                // A bottleneck is allowed to finish first; do not pull scattered members across the map.
                if (living.Any(u => Vector3.Distance(u.Position, center) > Spacing * 2 + Radius)) return;
                var goals = living.Select(u => previousOffsets[u.Id] == u.FormationOffset ? u.Position : center + u.FormationOffset).ToArray();
                for (var i = 0; i < goals.Length; i++)
                {
                    if (!Stand(goals[i], Radius)) return;
                    if (!Compact) for (var j = 0; j < i; j++) if (Vector3.Distance(goals[i], goals[j]) < Radius * 2 + .005f) return;
                }
                var forward = continueAfterReform ? FindPath(center, destination, Radius) : new List<Vector3>();
                if (forward == null) return;
                var local = new Dictionary<int, Travel>();
                for (var i = 0; i < living.Length; i++)
                {
                    var path = FindPath(living[i].Position, goals[i], Radius);
                    if (path == null) return;
                    local.Add(living[i].Id, new Travel { Goal = goals[i], Path = path, Next = 1, Source = MovementCommandSource.Player });
                }
                PlanShortExchanges(living, local);
                Reference = center; referencePath = forward;
                travels.Clear(); foreach (var item in local) travels.Add(item.Key, item.Value);
                reforming = true; pendingReform = false;
            }
            private void PlanShortExchanges(UnitState[] living, Dictionary<int, Travel> local)
            {
                if (Compact) return; // Squad mates can cross directly; do not add avoidance arcs or nudges.
                for (var i = 0; i < living.Length; i++) for (var j = i + 1; j < living.Length; j++)
                {
                    var a = living[i]; var b = living[j];
                    if ((local[a.Id].Goal - b.Position).sqrMagnitude > .0001f || (local[b.Id].Goal - a.Position).sqrMagnitude > .0001f) continue;
                    var midpoint = (a.Position + b.Position) * .5f;
                    var arcs = new[] { new List<Vector3> { a.Position }, new List<Vector3> { b.Position } };
                    for (var sample = 1; sample <= 16; sample++)
                    {
                        var angle = sample * Mathf.PI / 16;
                        foreach (var pair in new[] { (unit: a, path: arcs[0]), (unit: b, path: arcs[1]) })
                        {
                            var delta = pair.unit.Position - midpoint;
                            pair.path.Add(midpoint + new Vector3(delta.x * Mathf.Cos(angle) - delta.z * Mathf.Sin(angle), 0,
                                delta.x * Mathf.Sin(angle) + delta.z * Mathf.Cos(angle)));
                        }
                    }
                    arcs[0][16] = local[a.Id].Goal; arcs[1][16] = local[b.Id].Goal;
                    if (arcs.Any(path => Enumerable.Range(1, path.Count - 1).Any(k => !Segment(path[k - 1], path[k], Radius)))) continue;
                    var yields = new Dictionary<int, Vector3>(); var valid = true;
                    foreach (var neighbour in living.Where(u => u != a && u != b))
                    {
                        var clearance = Radius + neighbour.CollisionRadius + .008f;
                        Func<Vector3, bool> clearsArcs = point => arcs.All(path => Enumerable.Range(1, path.Count - 1).All(k =>
                            DistanceToSegment(point, path[k - 1], path[k]) >= clearance * clearance));
                        if (clearsArcs(neighbour.Position)) continue;
                        if ((local[neighbour.Id].Goal - neighbour.Position).sqrMagnitude > .0001f) { valid = false; break; }
                        var away = (neighbour.Position - midpoint).normalized; var found = false;
                        for (var distance = .025f; distance <= Radius + .151f; distance += .025f)
                        {
                            var candidate = neighbour.Position + away * distance;
                            if (!clearsArcs(candidate) || !Segment(neighbour.Position, candidate, Radius, neighbour.Id) ||
                                yields.Values.Any(p => Vector3.Distance(p, candidate) < Radius * 2 + .008f)) continue;
                            yields[neighbour.Id] = candidate; found = true; break;
                        }
                        if (!found) { valid = false; break; }
                    }
                    if (!valid) continue;
                    foreach (var item in yields)
                    {
                        var neighbour = living.First(u => u.Id == item.Key);
                        var travel = local[item.Key];
                        travel.Path = new List<Vector3> { neighbour.Position, item.Value }; travel.Next = 1;
                        travel.YieldFor = new[] { a.Id, b.Id };
                    }
                    local[a.Id].Path = arcs[0]; local[b.Id].Path = arcs[1];
                    local[a.Id].WaitForPaths = local[b.Id].WaitForPaths = yields.Keys.ToArray();
                }
            }
            public bool Request(MovementRequest request)
            {
                if (!BattleNumbers.Finite(request.Target) || sim.definition.Ladders.Count==0 && Mathf.Abs(request.Target.y - Reference.y) > .01f) return false;
                var living = sim.units.Where(u => u.IsAlive && (!request.MemberId.HasValue || u.Id == request.MemberId)).OrderBy(u => u.Id).ToArray();
                if (living.Length == 0) return false;
                if (request.Source == MovementCommandSource.AI && living.Any(u => travels.TryGetValue(u.Id, out var active) &&
                    active.Source == MovementCommandSource.Player && active.Next < active.Path.Count)) return false;
                var target = request.Target;
                // A single blocked ideal slot near a wall does not mean the surrounding area is a corridor.
                if (!request.MemberId.HasValue && TryNearbyFormation(target, living, out var adjusted)) target = adjusted;
                var centerPath = FindPath(Reference, target, Radius);
                if (!request.MemberId.HasValue && centerPath == null) return false;
                if (Compact && !request.MemberId.HasValue && centerPath.All(p=>Mathf.Abs(p.y-centerPath[0].y)<.01f)) centerPath = SampleCompactRoute(centerPath);
                var heading = lastHeading;
                if (centerPath != null)
                    for (var i = centerPath.Count - 1; i > 0; i--)
                    {
                        var segment = centerPath[i] - centerPath[i - 1];
                        if (segment.sqrMagnitude < .000001f) continue;
                        segment.y=0;if(segment.sqrMagnitude<.000001f)continue;
                        heading = segment.normalized; break;
                    }
                var goals = living.Select(u => target + (request.MemberId.HasValue ? Vector3.zero : u.FormationOffset)).ToArray();
                var queueDestination = false;
                // A narrow destination can hold a queue instead of an impossible full formation.
                if (goals.Any(g => !Stand(g, Radius)))
                {
                    var direction = heading;
                    living = living.OrderByDescending(u => Vector3.Dot(u.Position, direction)).ThenBy(u => u.Id).ToArray();
                    for (var i = 0; i < living.Length; i++) goals[i] = target - direction * (i * Spacing);
                    queueDestination = true;
                    if(sim.definition.Ladders.Count>0 && goals.Any(g=>!Stand(g,Radius)))
                    {
                        var found=false;
                        foreach(var axis in new[]{Vector3.right,Vector3.forward})
                        {
                            for(var shift=0;shift<=16&&!found;shift++)foreach(var sign in new[]{1,-1})
                            {
                                var center=target+axis*(shift*.125f*sign);
                                var line=Enumerable.Range(0,living.Length).Select(i=>center+axis*((i-(living.Length-1)*.5f)*Spacing)).ToArray();
                                if(line.Any(g=>!Stand(g,Radius)))continue;
                                goals=line;target=center;found=true;break;
                            }
                            if(found)break;
                        }
                        if(!found)return false;
                        centerPath=FindPath(Reference,target,Radius);if(centerPath==null)return false;
                    }
                }
                var pending = new Dictionary<int, Travel>();
                for (var i = 0; i < living.Length; i++)
                {
                    if (!Compact) foreach (var other in sim.units)
                    {
                        if (!other.IsAlive || living.Any(u => u.Id == other.Id)) continue;
                        var reserved = travels.TryGetValue(other.Id, out var otherTravel) ? otherTravel.Goal : other.Position;
                        if (Vector3.Distance(goals[i], reserved) < Radius + other.CollisionRadius + .005f) return false;
                    }
                    var path = FindPath(living[i].Position, goals[i], Radius);
                    if (path == null) return false;
                    var travel = new Travel { Goal = goals[i], Path = path, Next = 1, Source = request.Source,
                        WaitFor = !Compact && queueDestination && i > 0 ? (int?)living[i - 1].Id : null, QueueForward = heading };
                    if (Compact && !request.MemberId.HasValue && !BuildSharedRoute(living[i], travel, centerPath, queueDestination)) return false;
                    pending.Add(living[i].Id, travel);
                }
                foreach (var item in pending) travels[item.Key] = item.Value;
                if (!request.MemberId.HasValue)
                { destination = target; referencePath = centerPath; lastHeading = heading; reforming = pendingReform = false; }
                return true;
            }
            private bool BuildSharedRoute(UnitState unit, Travel travel, List<Vector3> guide, bool queue)
            {
                var path = new List<Vector3> { unit.Position };
                var stages = new List<int> { 0 };
                for (var i = 1; i < guide.Count; i++)
                {
                    var target = i == guide.Count - 1 ? travel.Goal : guide[i] + (queue ? Vector3.zero : unit.FormationOffset);
                    // Every offset waypoint must reconnect to the same guide corner, so no member
                    // independently selects the opposite side of a building or obstacle.
                    if (!Stand(target, Radius) || !Segment(guide[i], target, Radius))
                    {
                        // Prefer a modest contraction before merging onto the guide itself.
                        var contracted = guide[i] + (queue ? Vector3.zero : unit.FormationOffset * .75f);
                        target = i < guide.Count - 1 && Stand(contracted, Radius) && Segment(guide[i], contracted, Radius)
                            ? contracted : guide[i];
                    }
                    if (!Segment(path[path.Count - 1], target, Radius))
                    {
                        var join = FindPath(path[path.Count - 1], guide[i - 1], Radius);
                        if (join == null) return false;
                        foreach (var point in join.Skip(1)) { path.Add(point); stages.Add(i - 1); }
                        if (!Segment(path[path.Count - 1], target, Radius)) target = guide[i];
                    }
                    path.Add(target); stages.Add(i);
                }
                if ((path[path.Count - 1] - travel.Goal).sqrMagnitude > .000001f)
                {
                    var finish = FindPath(path[path.Count - 1], travel.Goal, Radius);
                    if (finish == null) return false;
                    foreach (var point in finish.Skip(1)) { path.Add(point); stages.Add(guide.Count); }
                }
                travel.Path = path; travel.SharedStages = stages;
                return true;
            }
            private static List<Vector3> SampleCompactRoute(List<Vector3> path)
            {
                // Clearance can change in the middle of a long straight segment. Sampling lets
                // members expand again just after a door, rather than staying merged until arrival.
                var result = new List<Vector3> { path[0] };
                for (var i = 1; i < path.Count; i++)
                {
                    var steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(path[i - 1], path[i]) / .5f));
                    for (var step = 1; step <= steps; step++)
                        result.Add(Vector3.Lerp(path[i - 1], path[i], (float)step / steps));
                }
                return result;
            }
            private bool TryNearbyFormation(Vector3 target, UnitState[] living, out Vector3 adjusted)
            {
                adjusted = target;
                Func<Vector3, bool> fits = center => Stand(center, Radius) && living.All(u =>
                    Stand(center + u.FormationOffset, Radius) && Segment(center, center + u.FormationOffset, Radius));
                if (fits(target)) return true;
                // Keep the original click invalid if it is itself on a wall/outside the map.
                if (!Stand(target, Radius)) return false;
                var offsets = new List<Vector3>();
                var distance = Spacing * 1.5f;
                var steps = Mathf.CeilToInt(distance / .125f);
                for (var x = -steps; x <= steps; x++) for (var z = -steps; z <= steps; z++)
                {
                    var offset = new Vector3(x * .125f, 0, z * .125f);
                    if (offset.sqrMagnitude <= distance * distance) offsets.Add(offset);
                }
                foreach (var offset in offsets.OrderBy(p => p.sqrMagnitude))
                {
                    var candidate = target + offset;
                    if (!Segment(target, candidate, Radius) || !fits(candidate)) continue;
                    adjusted = candidate; return true;
                }
                return false;
            }
            public void Step(float dt)
            {
                // A destroyed wall removes its upper floor and links. Falling is simulated,
                // never a view-only coordinate correction or a teleport to the ground.
                if(sim.definition.WallSurfaces.Count>0)foreach(var u in sim.units.Where(u=>u.IsAlive))
                {
                    if(Stand(u.Position,Radius)||ActiveLadders().Any(l=>l.Between(u.Position,l.Top)!=null))continue;
                    var ground=new Vector3(u.Position.x,sim.definition.Waypoints[0].y,u.Position.z);
                    if(u.Position.y>ground.y+.001f && Stand(ground,Radius))
                    {u.Position=Vector3.MoveTowards(u.Position,ground,3*dt);travels.Remove(u.Id);}
                }
                if(sim.definition.WallSurfaces.Count>0&&!Stand(Reference,Radius)&&!ActiveLadders().Any(l=>l.Between(Reference,l.Top)!=null))
                {var standing=sim.units.Where(u=>u.IsAlive).ToArray();if(standing.Length>0&&standing.All(u=>Stand(u.Position,Radius)))Reference=standing.Aggregate(Vector3.zero,(p,u)=>p+u.Position)/standing.Length;}
                if (pendingReform) TryBeginReform();
                var activeReform = reforming;
                var advance = Vector3.zero;
                if (activeReform && continueAfterReform && referencePath.Count > 1)
                {
                    advance = Vector3.MoveTowards(Reference, referencePath[1], sim.definition.Unit.Speed * dt * .35f) - Reference;
                    if (sim.units.Where(u => u.IsAlive).Any(u => !Segment(u.Position, u.Position + advance, Radius) ||
                        travels.TryGetValue(u.Id, out var t) && !Stand(t.Goal + advance, Radius))) advance = Vector3.zero;
                    if (advance.sqrMagnitude > 0)
                    {
                        Reference += advance;
                        foreach (var unit in sim.units.Where(u => u.IsAlive)) { unit.Position += advance; unit.RouteProgress += advance.magnitude; }
                        foreach (var travel in travels.Values)
                        {
                            travel.Goal += advance;
                            for (var i = 0; i < travel.Path.Count; i++) travel.Path[i] += advance;
                        }
                        if ((Reference - referencePath[1]).sqrMagnitude < .000001f) referencePath.RemoveAt(0);
                    }
                }
                // Independent paths converge through narrow passages and fan back out to the original slots.
                // Front/near-destination members move first; swept body checks prevent passing through one another.
                var living = sim.units.Where(u => u.IsAlive).OrderBy(u => travels.TryGetValue(u.Id, out var t) ?
                    (u.Position - t.Goal).sqrMagnitude : float.MaxValue).ThenBy(u => u.Id).ToArray();
                var centroid = living.Length > 0 ? living.Aggregate(Vector3.zero, (sum, u) => sum + u.Position) / living.Length : Reference;
                var cohesionRadius = Mathf.Max(Spacing * 1.5f, (living.Length - 1) * (Radius * 2 + .02f) * .5f + Spacing * .5f);
                var compactLeash = Spacing * 2.5f;
                if (Compact) foreach (var a in living) foreach (var b in living)
                    compactLeash = Mathf.Max(compactLeash, Vector3.Distance(a.FormationOffset, b.FormationOffset) + .15f);
                foreach (var unit in living)
                {
                    if (!travels.TryGetValue(unit.Id, out var travel)) continue;
                    if (travel.WaitForPaths != null && travel.WaitForPaths.Any(IsTravelling)) continue;
                    if (travel.WaitFor.HasValue && travels.TryGetValue(travel.WaitFor.Value, out var ahead))
                    {
                        var member = sim.units.Find(u => u.Id == travel.WaitFor.Value);
                        if (member != null && member.IsAlive && (member.Position - ahead.Goal).sqrMagnitude > .000001f &&
                            (ahead.Departed < Spacing || Vector3.Distance(member.Position, unit.Position) < Radius + member.CollisionRadius + .025f)) continue;
                        if (member == null || !member.IsAlive || (member.Position - ahead.Goal).sqrMagnitude <= .000001f) travel.WaitFor = null;
                    }
                    if (travel.Next >= travel.Path.Count && (unit.Position - travel.Goal).sqrMagnitude > .000001f)
                    {
                        if (travel.YieldFor != null && travel.YieldFor.Any(IsTravelling)) continue;
                        travel.Blocked += dt;
                        if (travel.Blocked > .4f)
                        {
                            var retry = FindPath(unit.Position, travel.Goal, Radius, unit.Id);
                            if (retry != null) { travel.Path = retry; travel.Next = 1; travel.Yielding = false; travel.SharedStages=null; }
                            travel.Blocked = 0;
                        }
                    }
                    var remaining = Mathf.Max(0, unit.Speed * dt - advance.magnitude);
                    if (!Compact && !activeReform && travel.Next < travel.Path.Count && Vector3.Distance(unit.Position, centroid) > cohesionRadius &&
                        Vector3.Dot(travel.Path[travel.Next] - unit.Position, unit.Position - centroid) > 0) remaining *= .45f;
                    while (remaining > .000001f && travel.Next < travel.Path.Count)
                    {
                        var target = travel.Path[travel.Next];
                        if(Mathf.Abs(target.y-unit.Position.y)>.001f)remaining=Mathf.Min(remaining,unit.Speed*dt*.55f);
                        var next = Vector3.MoveTowards(unit.Position, target, remaining);
                        if (Compact && !activeReform && travel.SharedStages != null && living.Any(other =>
                            other != unit && travels.TryGetValue(other.Id, out var follower) && follower.SharedStages != null && follower.Next < follower.Path.Count &&
                            (travel.SharedStages[travel.Next] > follower.SharedStages[follower.Next] ||
                             travel.SharedStages[travel.Next] == follower.SharedStages[follower.Next] &&
                             Vector3.Distance(unit.Position, target) + Spacing * .25f < Vector3.Distance(other.Position, follower.Path[follower.Next])) &&
                            Vector3.Distance(next, other.Position) > compactLeash &&
                            (next - other.Position).sqrMagnitude > (unit.Position - other.Position).sqrMagnitude + .0000001f)) break;
                        if (travel.WaitFor.HasValue)
                        {
                            var preceding = sim.units.Find(u => u.Id == travel.WaitFor.Value && u.IsAlive);
                            if (preceding != null && Vector3.Dot(next - unit.Position, travel.QueueForward) > 0 &&
                                Vector3.Dot(preceding.Position - next, travel.QueueForward) < Radius + preceding.CollisionRadius + .025f) break;
                        }
                        if (!Segment(unit.Position, next, Radius, unit.Id))
                        {
                            // Another climber temporarily owns this rung. Keep the shared
                            // route/stage indices intact instead of replacing it with a
                            // shorter individual path and stranding the rest of the squad.
                            if(ActiveLadders().Any(l=>l.ContainsSegment(unit.Position,next)))
                            {travel.Blocked=0;break;}
                            travel.Blocked += dt;
                            if (travel.Blocked > .4f)
                            {
                                var detour = FindPath(unit.Position, travel.Goal, Radius, unit.Id);
                                if (detour != null) { travel.Path = detour; travel.Next = 1; travel.Yielding = false; travel.SharedStages=null; }
                                else if (!travel.Yielding && sim.units.Any(other => other.IsAlive && other.Id > unit.Id &&
                                    DistanceToSegment(other.Position, unit.Position, next) < (Radius + other.CollisionRadius + .005f) * (Radius + other.CollisionRadius + .005f)))
                                {
                                    // Stable ID priority breaks head-on exchanges: one member temporarily steps aside.
                                    var yieldPath = NearestYieldPath(unit, travel.Goal);
                                    if (yieldPath != null) { travel.Path = yieldPath; travel.Next = 1; travel.Yielding = true; }
                                }
                                travel.Blocked = 0;
                            }
                            break;
                        }
                        var distance = Vector3.Distance(unit.Position, next);
                        unit.Position = next; unit.RouteProgress += distance; travel.Departed += distance; remaining -= distance;
                        travel.Blocked = 0;
                        if ((next - target).sqrMagnitude < .0000001f) travel.Next++; else break;
                    }
                }
                if (!activeReform && referencePath.Count > 1 && (!Compact || living.All(u =>
                    Vector3.Distance(u.Position, Reference) <= compactLeash ||
                    Vector3.Dot(Reference - u.Position, referencePath[1] - Reference) <= 0)))
                {
                    var next = Vector3.MoveTowards(Reference, referencePath[1], sim.definition.Unit.Speed * dt);
                    Reference = next;
                    if ((next - referencePath[1]).sqrMagnitude < .000001f) referencePath.RemoveAt(0);
                }
                foreach (var unit in living) unit.SquadReference = Reference;
                if (activeReform && !Moving)
                {
                    reforming = false;
                    if (continueAfterReform) Request(new MovementRequest(destination));
                    else { destination = Reference; referencePath.Clear(); }
                }
            }
            private List<Vector3> NearestYieldPath(UnitState unit, Vector3 goal)
            {
                List<Vector3> best = null; var bestCost = float.PositiveInfinity;
                var distance = Radius * 2 + .025f;
                for (var direction = 0; direction < 8; direction++)
                {
                    var angle = direction * Mathf.PI / 4;
                    var aside = unit.Position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                    if (!Segment(aside, aside, Radius, unit.Id)) continue;
                    var path = FindPath(unit.Position, aside, Radius, unit.Id);
                    if (path == null) continue;
                    var length = 0f; for (var i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
                    if (length > distance * 1.8f) continue;
                    var cost = length + Vector3.Distance(aside, goal);
                    if (cost >= bestCost) continue;
                    bestCost = cost; best = path;
                }
                return best;
            }
            private bool BlockedCell(int x, int z,float height)
            {
                var key = new Vector3Int(x, SmallCellOccupancy.PositionToCell(new Vector3(0,height,0)).y, z);
                var surface=sim.definition.WallSurfaces.FirstOrDefault(s=>s.Cells.Contains(key));
                if(surface!=null)return !OwnerAlive(surface.TowerId)||Mathf.Abs(surface.Height-height)>.01f;
                return sim.walkable != null && !sim.walkable.Contains(key) || sim.HasTowerFootprint(key);
            }
            private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
            {
                var delta = to - from;
                var t = delta.sqrMagnitude < .0000001f ? 0 : Mathf.Clamp01(Vector3.Dot(point - from, delta) / delta.sqrMagnitude);
                return (point - from - delta * t).sqrMagnitude;
            }
            private bool Stand(Vector3 point, float radius)
            {
                for (var x = Mathf.FloorToInt(point.x - radius + .5f); x <= Mathf.FloorToInt(point.x + radius + .5f); x++)
                    for (var z = Mathf.FloorToInt(point.z - radius + .5f); z <= Mathf.FloorToInt(point.z + radius + .5f); z++)
                    {
                        if (!BlockedCell(x, z,point.y)) continue;
                        var dx = Mathf.Max(Mathf.Abs(point.x - x) - .5f, 0);
                        var dz = Mathf.Max(Mathf.Abs(point.z - z) - .5f, 0);
                        if (dx * dx + dz * dz < radius * radius - .000001f) return false;
                    }
                return true;
            }
            private bool Segment(Vector3 from, Vector3 to, float radius, int? member = null)
            {
                if(ActiveLadders().Any(l=>l.ContainsSegment(from,to)))
                {
                    if(member.HasValue&&(!Compact||Mathf.Abs(from.y-to.y)>.001f))foreach(var other in sim.units)
                        if(other.IsAlive&&other.Id!=member && ((other.Position-from).sqrMagnitude<.000001f
                            ? other.Id<member.Value : Vector3.Dot(other.Position-from,to-from)>.000001f) &&
                            (other.Position-to).sqrMagnitude<Mathf.Pow(radius+other.CollisionRadius+.01f,2))return false;
                    return true;
                }
                if(Mathf.Abs(from.y-to.y)>.001f)return false;
                // Conservative swept circle vs inflated cell AABBs; never cut an obstacle corner.
                for (var x = Mathf.FloorToInt(Mathf.Min(from.x, to.x) - radius + .5f); x <= Mathf.FloorToInt(Mathf.Max(from.x, to.x) + radius + .5f); x++)
                    for (var z = Mathf.FloorToInt(Mathf.Min(from.z, to.z) - radius + .5f); z <= Mathf.FloorToInt(Mathf.Max(from.z, to.z) + radius + .5f); z++)
                    {
                        if (!BlockedCell(x, z,from.y)) continue;
                        float enter = 0, exit = 1;
                        var delta = to - from;
                        if (ClipWallAxis(from.x - x, delta.x, .5f + radius - .0001f, ref enter, ref exit) &&
                            ClipWallAxis(from.z - z, delta.z, .5f + radius - .0001f, ref enter, ref exit)) return false;
                    }
                if (member.HasValue && !Compact)
                    foreach (var other in sim.units)
                        if (other.IsAlive && other.Id != member && DistanceToSegment(other.Position, from, to) <
                            (radius + other.CollisionRadius + .005f) * (radius + other.CollisionRadius + .005f)) return false;
                return true;
            }
            public List<Vector3> PreviewPath(Vector3 from, Vector3 to) => FindPath(from, to, Radius);
            public bool CanStandOnRoute(Vector3 point) => Stand(point, Radius);

            private List<Vector3> FindPath(Vector3 from, Vector3 to, float radius, int? member = null)
            {
                if(sim.definition.Ladders.Count==0)return FindFlatPath(from,to,radius,member);
                var links=ActiveLadders().ToArray();
                var nodes=new List<Vector3>{from,to};foreach(var l in links){nodes.Add(l.Bottom);nodes.Add(l.Top);}
                var costs=Enumerable.Repeat(float.PositiveInfinity,nodes.Count).ToArray();var done=new bool[nodes.Count];
                var prior=new int[nodes.Count];var routes=new List<Vector3>[nodes.Count];costs[0]=0;
                for(var iteration=0;iteration<nodes.Count;iteration++)
                {
                    var a=-1;for(var i=0;i<nodes.Count;i++)if(!done[i]&&(a<0||costs[i]<costs[a]))a=i;
                    if(a<0||float.IsPositiveInfinity(costs[a]))break;if(a==1)
                    {var path=new List<Vector3>();var chain=new List<int>();for(var n=1;n!=0;n=prior[n])chain.Add(n);chain.Reverse();path.Add(from);foreach(var n in chain)path.AddRange(routes[n].Skip(1));return path;}
                    done[a]=true;
                    for(var b=1;b<nodes.Count;b++)
                    {
                        if(a==b||done[b])continue;List<Vector3> path=null;
                        foreach(var l in links){var transit=l.Between(nodes[a],nodes[b]);if(transit!=null){path=transit;break;}}
                        if(path==null&&Mathf.Abs(nodes[a].y-nodes[b].y)<.001f)path=FindFlatPath(nodes[a],nodes[b],radius,member);
                        if(path==null)continue;var cost=costs[a];for(var i=1;i<path.Count;i++)cost+=Vector3.Distance(path[i-1],path[i]);
                        if(cost>=costs[b])continue;costs[b]=cost;prior[b]=a;routes[b]=path;
                    }
                }
                return null;
            }
            private bool OwnerAlive(int id)=>sim.towers.Any(t=>t.Id==id&&t.IsAlive);
            private IEnumerable<LadderDefinition> ActiveLadders()=>sim.definition.Ladders.Where(l=>OwnerAlive(l.TowerId));
            private List<Vector3> FindFlatPath(Vector3 from, Vector3 to, float radius, int? member = null)
            {
                if(Mathf.Abs(from.y-to.y)>.001f)return null;
                if (!Stand(from, radius) || !Stand(to, radius)) return null;
                if (member.HasValue && !Segment(to, to, radius, member)) return null;
                if (Segment(from, to, radius, member)) return new List<Vector3> { from, to };
                if (sim.walkable == null || sim.walkable.Count == 0) return null;
                const float resolution = .25f;
                var minX = sim.walkable.Min(c => c.x) * 4 - 1; var maxX = sim.walkable.Max(c => c.x) * 4 + 1;
                var minZ = sim.walkable.Min(c => c.z) * 4 - 1; var maxZ = sim.walkable.Max(c => c.z) * 4 + 1;
                Func<Vector2Int, Vector3> world = n => new Vector3(n.x * resolution, from.y, n.y * resolution);
                var start = new Vector2Int(Mathf.RoundToInt(from.x * 4), Mathf.RoundToInt(from.z * 4));
                var open = new List<(Vector2Int node, float score)>();
                var costs = new Dictionary<Vector2Int, float>();
                var parents = new Dictionary<Vector2Int, Vector2Int>();
                var closed = new HashSet<Vector2Int>();
                Action<Vector2Int, float> push = (node, score) =>
                {
                    open.Add((node, score)); var i = open.Count - 1;
                    while (i > 0) { var p = (i - 1) / 2; if (open[p].score <= open[i].score) break; var tmp = open[p]; open[p] = open[i]; open[i] = tmp; i = p; }
                };
                for (var x = -1; x <= 1; x++) for (var z = -1; z <= 1; z++)
                {
                    var n = start + new Vector2Int(x, z); var p = world(n);
                    if (!Segment(from, p, radius, member)) continue;
                    costs[n] = Vector3.Distance(from, p); push(n, costs[n] + Vector3.Distance(p, to));
                }
                var visits = 0;
                while (open.Count > 0 && visits++ < 50000)
                {
                    var current = open[0].node;
                    open[0] = open[open.Count - 1]; open.RemoveAt(open.Count - 1);
                    var index = 0;
                    while (index * 2 + 1 < open.Count)
                    {
                        var child = index * 2 + 1;
                        if (child + 1 < open.Count && open[child + 1].score < open[child].score) child++;
                        if (open[index].score <= open[child].score) break;
                        var tmp = open[index]; open[index] = open[child]; open[child] = tmp; index = child;
                    }
                    if (!closed.Add(current)) continue;
                    var position = world(current);
                    if ((position - to).sqrMagnitude <= .25f && Segment(position, to, radius, member))
                    {
                        var result = new List<Vector3> { to, position };
                        while (parents.TryGetValue(current, out var prior)) { current = prior; result.Add(world(current)); }
                        result.Add(from); result.Reverse();
                        // String-pull with the same swept collision test, preserving the exact destination.
                        for (var i = 0; i + 2 < result.Count; i++)
                            for (var j = result.Count - 1; j > i + 1; j--)
                                if (Segment(result[i], result[j], radius, member)) { result.RemoveRange(i + 1, j - i - 1); break; }
                        return result;
                    }
                    for (var x = -1; x <= 1; x++) for (var z = -1; z <= 1; z++)
                    {
                        if (x == 0 && z == 0) continue;
                        var n = current + new Vector2Int(x, z);
                        if (n.x < minX || n.x > maxX || n.y < minZ || n.y > maxZ || closed.Contains(n)) continue;
                        var p = world(n); var cost = costs[current] + Vector3.Distance(position, p);
                        if (costs.TryGetValue(n, out var old) && old <= cost || !Segment(position, p, radius, member)) continue;
                        costs[n] = cost; parents[n] = current; push(n, cost + Vector3.Distance(p, to));
                    }
                }
                return null;
            }
        }
    }
}
