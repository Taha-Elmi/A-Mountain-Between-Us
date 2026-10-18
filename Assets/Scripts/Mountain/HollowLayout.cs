using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farhad
{
    // Plans which rock of a mountain is hollow: one winding way through it, decoy branches that look
    // just like it but lead nowhere, and lone hollow pockets. Everything else is solid.
    //
    // Farhad is one cell wide and two tall, and strikes only rock beside, above or below his body.
    // The way is a chain of three moves, each hollowing exactly the rock he has to break:
    //   walk  - the two cells beside him;
    //   climb - the cell over his head, then the two cells of the next column one row up (the upper
    //           one struck mid-jump);
    //   drop  - the cell under his feet.
    //
    // The way is planned in legs, one per call to Shirin: each leg winds only through rock that a call
    // made where it starts reveals, and ends where the next call is to be made. So a player who
    // remembers what each call showed, and calls again where that runs out, can always get through,
    // and the last leg breaks out at the exit. Decoys stay well inside the reveal that shows them, so
    // only the true way runs on past what a call shows. Layouts that a player could still cross with
    // one call fewer (say by calling from inside a decoy) are thrown away, so every call counts.
    public class HollowLayout
    {
        public struct Settings
        {
            public int calls;
            public float revealRadius;
            public float revealSlack;
            public int minVerticalTravel;
            public int decoys;
            public Vector2Int decoyMoves;
            public int pockets;
            public int surfaceMargin;
        }

        enum Move { Walk, Climb, Drop }

        const int Solid = int.MinValue; // label of rock that isn't hollow
        // Hollow cells side by side must belong to steps of the way this close together, so no
        // branch or pocket joins two parts of the way into a shortcut.
        const int Window = 4;
        const int EndZone = 2;
        const int ClimbSlack = 1;

        readonly bool[,] rock;
        readonly bool[,] inner; // in the mountain, far enough under its surface to be hollow
        readonly int width, height;
        readonly Vector2Int start, entrance, exit;
        readonly Settings settings;
        readonly System.Random rng;

        int[,] label;  // the step of the way each hollow cell belongs to (decoys: the step they branch off); Solid otherwise
        bool[,] floor; // rock the way stands on, which must stay unbroken
        bool[,] decoy; // hollow rock of the decoys
        readonly List<Vector2Int> way = new();       // where Farhad stands, step by step
        readonly List<Vector2Int[]> broken = new();  // broken[i]: the rock he breaks to get to way[i]
        readonly List<int> legStart = new();         // legStart[i]: the step where the leg holding way[i] starts (its call)
        readonly List<Vector2Int> cells = new();
        readonly List<Vector2Int> fresh = new();
        bool byCalls;
        float planReach;

        public bool[,] Hollow { get; private set; }
        // False when the mountain is too big for the calls, and the way needs more of them.
        public bool FitsCalls { get; private set; }
        // False when the calls reach so far that fewer of them would do.
        public bool NeedsAllCalls { get; private set; }

        HollowLayout(bool[,] rock, Vector2Int entrance, Vector2Int exit, Settings settings, System.Random rng)
        {
            this.rock = rock;
            this.entrance = entrance;
            this.exit = exit;
            this.settings = settings;
            this.rng = rng;
            width = rock.GetLength(0);
            height = rock.GetLength(1);
            start = new Vector2Int(entrance.x - 1, entrance.y);

            // Only the mountain between the entrance and the exit, not the ground on either side of it.
            int m = Math.Max(1, settings.surfaceMargin);
            inner = new bool[width, height];
            for (int x = Math.Min(entrance.x, exit.x); x <= Math.Max(entrance.x, exit.x); x++)
            for (int y = 0; y < height; y++)
            {
                bool deep = rock[x, y];
                for (int i = -m; i <= m && deep; i++)
                for (int j = -m; j <= m && deep; j++)
                    deep = IsRock(x + i, y + j);
                inner[x, y] = deep;
            }
        }

        // The rock of the mountain is rock[x, y]. Farhad starts on open ground just before entrance
        // (the first rock at his feet' height) and is through once he stands at exit.
        // Returns null if no way through could be found at all.
        public static HollowLayout Plan(bool[,] rock, Vector2Int entrance, Vector2Int exit, Settings settings, int seed)
        {
            // Best: findable with the calls, and not with fewer. Else findable with them. Else just a way through.
            var layout = new HollowLayout(rock, entrance, exit, settings, new System.Random(seed));
            for (int attempt = 0; attempt < 200; attempt++)
                if (layout.TryPlan(byCalls: true, allCalls: true)) return layout;
            for (int attempt = 0; attempt < 100; attempt++)
                if (layout.TryPlan(byCalls: true, allCalls: false)) return layout;
            for (int attempt = 0; attempt < 100; attempt++)
                if (layout.TryPlan(byCalls: false, allCalls: false)) return layout;
            return null;
        }

        bool TryPlan(bool byCalls, bool allCalls)
        {
            this.byCalls = byCalls;
            planReach = byCalls ? settings.revealRadius - settings.revealSlack : float.PositiveInfinity;
            label = new int[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                label[x, y] = Solid;
            floor = new bool[width, height];
            decoy = new bool[width, height];
            way.Clear();
            broken.Clear();
            legStart.Clear();

            way.Add(start);
            broken.Add(Array.Empty<Vector2Int>());
            legStart.Add(0);
            if (!IsRock(start.x, start.y - 1)) return false;
            MarkFloor(start);

            if (!PlanWay() || !Winds()) return false;
            // Findable with every call, even made a little short of the ideal spot.
            if (byCalls && CallsToCross(planReach, settings.calls) > settings.calls) return false;

            for (int i = 0; i < settings.decoys; i++)
                for (int tries = 0; tries < 10; tries++)
                    if (TryAddDecoy(rng.Next(1, way.Count - 1))) break;
            for (int i = 0; i < settings.pockets; i++) AddPocket(i);
            if (!CanCross()) return false;
            // But not with one call fewer.
            if (allCalls && settings.calls > 1 && CrossableWith(settings.calls - 1, settings.revealRadius)) return false;

            FitsCalls = byCalls;
            NeedsAllCalls = allCalls;
            Hollow = new bool[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                Hollow[x, y] = label[x, y] != Solid;
            return true;
        }

        // ---- The way through ----

        bool PlanWay()
        {
            int legs = byCalls ? Math.Max(1, settings.calls) : 1;
            int reachX = byCalls ? Mathf.FloorToInt(planReach + 0.35f) : width;
            for (int leg = 0; leg < legs; leg++)
            {
                // A leg that runs into a dead end is planned again from where it starts.
                int steps = way.Count;
                var savedLabel = (int[,])label.Clone();
                var savedFloor = (bool[,])floor.Clone();
                bool planned = false;
                for (int tries = 0; tries < 20 && !planned; tries++)
                {
                    if (tries > 0)
                    {
                        way.RemoveRange(steps, way.Count - steps);
                        broken.RemoveRange(steps, broken.Count - steps);
                        legStart.RemoveRange(steps, legStart.Count - steps);
                        Array.Copy(savedLabel, label, label.Length);
                        Array.Copy(savedFloor, floor, floor.Length);
                    }
                    planned = PlanLeg(legs - 1 - leg, reachX);
                }
                if (!planned) return false;
            }
            return way[^1] == exit;
        }

        bool PlanLeg(int legsAfter, int reachX)
        {
            var from = way[^1];
            int spot = way.Count - 1;
            Vector2Int target = exit;
            if (legsAfter > 0 && !PickLegEnd(from, legsAfter, reachX, out target)) return false;

            // Turns between here and the leg's end, alternating high and low so the way winds.
            int turns = byCalls ? rng.Next(1, 3) : Math.Max(2, Math.Abs(exit.x - start.x) / 6);
            var prev = from;
            for (int i = 0; i < turns; i++)
            {
                if (!PickTurn(prev, target, from, turns - i, out var turn)) continue;
                if (!WalkTo(turn, from, spot)) return false;
                prev = turn;
            }
            return WalkTo(target, from, spot);
        }

        // Where a leg ends: as far on as the call at its start shows, but at least far enough that the
        // legs after it can still reach the exit.
        bool PickLegEnd(Vector2Int from, int legsAfter, int reachX, out Vector2Int end)
        {
            int forward = Math.Sign(exit.x - from.x);
            int need = Math.Abs(exit.x - 1 - from.x) - legsAfter * reachX;
            var options = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 1; y < height - 1; y++)
            {
                var p = new Vector2Int(x, y);
                int advance = (x - from.x) * forward;
                if (advance < Math.Max(1, need) || advance > reachX) continue;
                if (Standable(p) && Shows(from, p) && Shows(from, p + Vector2Int.up) && CanClimb(from, p)) options.Add(p);
            }
            end = options.Count > 0 ? options[rng.Next(options.Count)] : default;
            return options.Count > 0;
        }

        // A turn on the way from prev toward target, inside what the call at spot shows: at the other
        // height from prev, and possibly a little backward.
        bool PickTurn(Vector2Int prev, Vector2Int target, Vector2Int spot, int turnsLeft, out Vector2Int turn)
        {
            int forward = Math.Sign(target.x - prev.x);
            if (forward == 0) forward = 1;
            int span = Math.Abs(target.x - prev.x);
            int back = byCalls ? 3 : 2;
            int ahead = Math.Max(1, span * 2 / (turnsLeft + 1));

            var options = new List<Vector2Int>();
            int low = int.MaxValue, high = int.MinValue;
            for (int x = 0; x < width; x++)
            for (int y = 1; y < height - 1; y++)
            {
                var p = new Vector2Int(x, y);
                int advance = (x - prev.x) * forward;
                if (advance < -back || advance > ahead) continue;
                if (!Standable(p) || !Shows(spot, p) || !Shows(spot, p + Vector2Int.up)) continue;
                if (!CanClimb(prev, p) || !CanClimb(p, target)) continue;
                options.Add(p);
                low = Math.Min(low, y);
                high = Math.Max(high, y);
            }
            turn = default;
            if (options.Count == 0) return false;

            float middle = (low + high) * 0.5f;
            bool goHigh = prev.y < middle;
            var other = options.FindAll(p => (p.y >= middle) == goHigh && p.y != prev.y);
            var pool = other.Count > 0 ? other : options;
            // Of a few picked at random, the one furthest up or down, so the way uses the mountain's height.
            turn = pool[rng.Next(pool.Count)];
            for (int i = 0; i < 2; i++)
            {
                var p = pool[rng.Next(pool.Count)];
                if (Math.Abs(p.y - prev.y) > Math.Abs(turn.y - prev.y)) turn = p;
            }
            return true;
        }

        // Climbing rises one row per column, so a point higher up must be further across than up,
        // with a few columns to spare for walking around what's in the way.
        static bool CanClimb(Vector2Int from, Vector2Int to) =>
            to.y <= from.y || Math.Abs(to.x - from.x) >= to.y - from.y + ClimbSlack;

        bool WalkTo(Vector2Int target, Vector2Int spot, int spotStep)
        {
            var moves = new List<(Move move, int dir)>(3);
            for (int guard = 0; way[^1] != target; guard++)
            {
                if (guard > 4 * (width + height)) return false;
                var p = way[^1];
                int dx = target.x - p.x, dy = target.y - p.y, dir = Math.Sign(dx);
                moves.Clear();
                if (dy > 0)
                {
                    if (dx == 0) return false;
                    moves.Add((Move.Climb, dir));
                    if (Math.Abs(dx) > dy) moves.Add((Move.Walk, dir));
                }
                else
                {
                    if (dx != 0) moves.Add((Move.Walk, dir));
                    if (dy < 0) moves.Add((Move.Drop, 0));
                }
                Shuffle(moves);

                bool moved = false;
                foreach (var (move, d) in moves)
                    if (TryWayStep(move, d, spot, spotStep)) { moved = true; break; }
                if (!moved) return false;
            }
            return true;
        }

        bool TryWayStep(Move move, int dir, Vector2Int spot, int spotStep)
        {
            var p = way[^1];
            var q = Apply(p, move, dir, cells);
            int step = way.Count;
            var under = new Vector2Int(q.x, q.y - 1);
            if (!InMap(under) || !rock[under.x, under.y] || IsHollow(under)) return false;

            fresh.Clear();
            foreach (var c in cells)
            {
                if (!InMap(c))
                {
                    if (c.y >= height && NearEnds(c)) continue; // open sky over the exit
                    return false;
                }
                if (!rock[c.x, c.y])
                {
                    if (!NearEnds(c)) return false; // the way stays inside the mountain
                    continue;
                }
                if (IsHollow(c)) continue; // already broken earlier on the way
                if (!inner[c.x, c.y] && !InEndZone(c)) return false;
                if (!Shows(spot, c)) return false;
                if (floor[c.x, c.y] && !(move == Move.Drop && c == p + Vector2Int.down)) return false;
                if (TouchesFarHollow(c, step)) return false;
                fresh.Add(c);
            }

            foreach (var c in fresh) label[c.x, c.y] = step;
            way.Add(q);
            broken.Add(fresh.ToArray());
            legStart.Add(spotStep);
            MarkFloor(q);
            return true;
        }

        // The way must climb and drop, not run straight across.
        bool Winds()
        {
            int travel = 0;
            for (int i = 1; i < way.Count; i++) travel += Math.Abs(way[i].y - way[i - 1].y);
            return travel >= settings.minVerticalTravel;
        }

        // ---- Decoys and pockets ----

        // A dead end branching off the way at step root, made of the same moves so it looks just like
        // it. It lies well inside what the nearest call shows, so the call shows where it ends.
        // Returns false, leaving nothing, if it couldn't get far.
        bool TryAddDecoy(int root)
        {
            if (root < 1 || root >= way.Count) return false;
            var spot = way[DecoySpot(root)];
            float reach = planReach - 1f;
            int forward = Math.Sign(exit.x - start.x);
            int dir = rng.NextDouble() < 0.7 ? forward : -forward;
            int moves = rng.Next(settings.decoyMoves.x, settings.decoyMoves.y + 1);
            var p = way[root];
            var options = new List<(Move move, int dir)>(5);
            var added = new HashSet<Vector2Int>();

            for (int i = 0; i < moves; i++)
            {
                if (rng.NextDouble() < 0.2) dir = -dir;
                options.Clear();
                options.Add((Move.Walk, dir));
                options.Add((Move.Walk, dir));
                options.Add((Move.Walk, dir));
                options.Add((Move.Climb, dir));
                options.Add((Move.Drop, 0));
                Shuffle(options);

                bool moved = false;
                foreach (var (move, d) in options)
                {
                    var q = Apply(p, move, d, cells);
                    if (!DecoyStepFits(q, spot, reach, root, added)) continue;
                    foreach (var c in cells)
                    {
                        if (IsHollow(c)) continue;
                        label[c.x, c.y] = root;
                        added.Add(c);
                    }
                    p = q;
                    moved = true;
                    break;
                }
                if (!moved) break;
            }

            if (added.Count < 3)
            {
                foreach (var c in added) label[c.x, c.y] = Solid;
                return false;
            }
            foreach (var c in added) decoy[c.x, c.y] = true;
            return true;
        }

        // The step of the call nearest to step root: the start of its leg, or of the next one.
        int DecoySpot(int root)
        {
            int spot = legStart[root];
            for (int i = root + 1; i < way.Count; i++)
                if (legStart[i] != spot)
                    return legStart[i] - root < root - spot ? legStart[i] : spot;
            return spot;
        }

        bool DecoyStepFits(Vector2Int q, Vector2Int spot, float reach, int root, HashSet<Vector2Int> added)
        {
            var under = new Vector2Int(q.x, q.y - 1);
            if (!InMap(under) || !rock[under.x, under.y]) return false;
            foreach (var c in cells)
            {
                if (IsHollow(c)) continue; // along the way, or itself
                if (!InMap(c) || !inner[c.x, c.y] || floor[c.x, c.y]) return false;
                if (byCalls && !Shows(spot, c, reach)) return false;
                if (TouchesFarHollow(c, root)) return false;
                // Decoys don't run into each other, which would make them blobs unlike the way.
                foreach (var n in Neighbours)
                {
                    var o = c + n;
                    if (InMap(o) && decoy[o.x, o.y] && !added.Contains(o)) return false;
                }
            }
            return true;
        }

        // A lone pocket of one to three hollow cells, touching no other hollow rock.
        void AddPocket(int index)
        {
            int pocket = -1000 * (index + 1);
            for (int tries = 0; tries < 30; tries++)
            {
                var c = new Vector2Int(rng.Next(width), rng.Next(1, height));
                if (!PocketFits(c, pocket)) continue;
                label[c.x, c.y] = pocket;
                int grow = rng.Next(0, 3);
                for (int i = 0; i < grow; i++)
                {
                    var next = c + Neighbours[rng.Next(4)];
                    if (!PocketFits(next, pocket)) continue;
                    label[next.x, next.y] = pocket;
                    c = next;
                }
                return;
            }
        }

        bool PocketFits(Vector2Int c, int pocket) =>
            InMap(c) && inner[c.x, c.y] && !IsHollow(c) && !floor[c.x, c.y] && !TouchesFarHollow(c, pocket);

        // ---- Checks ----

        // How many calls a player with a perfect memory needs to follow the way, calling each time at
        // the spot (of those he can stand on) that shows the most of it. maxCalls + 1 if more than that.
        int CallsToCross(float reach, int maxCalls)
        {
            var known = new bool[width, height];
            int last = way.Count - 1;
            int frontier = Frontier(known, 0, null, reach);
            for (int calls = 0; ; calls++)
            {
                if (frontier >= last) return calls;
                if (calls == maxCalls) return maxCalls + 1;
                int best = frontier, bestSpot = -1;
                for (int j = 0; j <= frontier; j++)
                {
                    // He can't go back to stand where he has since broken the floor.
                    if (j < frontier && way[j + 1] == way[j] + Vector2Int.down) continue;
                    int reached = Frontier(known, frontier, way[j], reach);
                    if (reached > best) { best = reached; bestSpot = j; }
                }
                if (bestSpot < 0) return maxCalls + 1;
                Reveal(known, way[bestSpot], reach);
                frontier = best;
            }
        }

        // The furthest step he can get to from step from, knowing known plus what a call at spot shows.
        int Frontier(bool[,] known, int from, Vector2Int? spot, float reach)
        {
            int i = from;
            while (i + 1 < way.Count)
            {
                foreach (var c in broken[i + 1])
                    if (!known[c.x, c.y] && !(spot.HasValue && Shows(spot.Value, c, reach)))
                        return i;
                i++;
            }
            return i;
        }

        void Reveal(bool[,] known, Vector2Int spot, float reach)
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (Shows(spot, new Vector2Int(x, y), reach)) known[x, y] = true;
        }

        // Whether, knowing all the hollow rock, Farhad can get from the start to the exit by breaking
        // only hollow rock.
        bool CanCross() => Reachable(null).Contains(exit);

        // Whether a player with a perfect memory could get through with just this many calls, calling
        // each time from wherever he can reach (decoys included) that lets him get furthest after.
        bool CrossableWith(int calls, float reach)
        {
            int forward = Math.Sign(exit.x - start.x);
            var known = new bool[width, height];
            for (int call = 0; ; call++)
            {
                var reachable = Reachable(known);
                if (reachable.Contains(exit)) return true;
                if (call == calls) return false;

                bool[,] best = null;
                long bestScore = long.MinValue;
                foreach (var spot in reachable)
                {
                    var after = (bool[,])known.Clone();
                    Reveal(after, spot, reach);
                    var then = Reachable(after);
                    if (then.Contains(exit) && call + 1 <= calls) return true;
                    int furthest = int.MinValue;
                    foreach (var q in then) furthest = Math.Max(furthest, (q.x - start.x) * forward);
                    long score = (long)furthest * 10000 + then.Count;
                    if (score > bestScore) { bestScore = score; best = after; }
                }
                known = best;
            }
        }

        // Everywhere Farhad can stand, breaking only the hollow rock in known (all of it if null).
        HashSet<Vector2Int> Reachable(bool[,] known)
        {
            bool Open(int x, int y) => x >= 0 && x < width && y >= 0
                && (y >= height || !rock[x, y] || (label[x, y] != Solid && (known == null || known[x, y])));
            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            void Visit(Vector2Int p)
            {
                while (p.y > 0 && !IsRock(p.x, p.y - 1)) p.y--; // falls through open space
                if (p.y < height + 2 && seen.Add(p)) queue.Enqueue(p);
            }
            Visit(start);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (int d in new[] { -1, 1 })
                {
                    if (Open(p.x + d, p.y) && Open(p.x + d, p.y + 1)) Visit(new Vector2Int(p.x + d, p.y));
                    if (Open(p.x, p.y + 2) && Open(p.x + d, p.y + 1) && Open(p.x + d, p.y + 2) && IsRock(p.x + d, p.y))
                        Visit(new Vector2Int(p.x + d, p.y + 1));
                }
                if (p.y > 0 && p.y <= height && Open(p.x, p.y - 1)) Visit(p + Vector2Int.down);
            }
            return seen;
        }

        // ---- Helpers ----

        static readonly Vector2Int[] Neighbours = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

        // Where Farhad stands after the move, with the cells it breaks put in cells.
        static Vector2Int Apply(Vector2Int p, Move move, int dir, List<Vector2Int> cells)
        {
            cells.Clear();
            switch (move)
            {
                case Move.Walk:
                    cells.Add(new Vector2Int(p.x + dir, p.y));
                    cells.Add(new Vector2Int(p.x + dir, p.y + 1));
                    return new Vector2Int(p.x + dir, p.y);
                case Move.Climb:
                    cells.Add(new Vector2Int(p.x, p.y + 2));
                    cells.Add(new Vector2Int(p.x + dir, p.y + 1));
                    cells.Add(new Vector2Int(p.x + dir, p.y + 2));
                    return new Vector2Int(p.x + dir, p.y + 1);
                default:
                    cells.Add(new Vector2Int(p.x, p.y - 1));
                    return new Vector2Int(p.x, p.y - 1);
            }
        }

        // Whether a call made standing at spot shows cell c: the same test as MountainGrid.RevealAround,
        // with his body (0.7 x 1.75) in the middle of the cell, feet on its floor.
        bool Shows(Vector2Int spot, Vector2Int c) => Shows(spot, c, planReach);

        static bool Shows(Vector2Int spot, Vector2Int c, float reach)
        {
            if (float.IsPositiveInfinity(reach)) return true;
            float px = c.x + 0.5f, py = c.y + 0.5f;
            float dx = Math.Max(0f, Math.Max(spot.x + 0.15f - px, px - (spot.x + 0.85f)));
            float dy = Math.Max(0f, Math.Max(spot.y - py, py - (spot.y + 1.75f)));
            return dx * dx + dy * dy <= reach * reach;
        }

        // Somewhere new for the way to go: two rock cells deep inside the mountain, on unbroken rock.
        bool Standable(Vector2Int p)
        {
            var head = p + Vector2Int.up;
            var under = p + Vector2Int.down;
            return InMap(head) && InMap(under) && inner[p.x, p.y] && inner[head.x, head.y]
                   && !IsHollow(p) && !IsHollow(head) && rock[under.x, under.y] && !IsHollow(under);
        }

        // Next to the entrance or the exit, where the way meets the open.
        bool NearEnds(Vector2Int c) => Chebyshev(c, entrance) <= EndZone || Chebyshev(c, exit) <= EndZone;

        // Rock near the entrance or the exit, on the mountain's side of them, which may be hollow
        // however close it is to the outside.
        bool InEndZone(Vector2Int c) =>
            (c.x >= entrance.x && Chebyshev(c, entrance) <= EndZone) || (c.x <= exit.x && Chebyshev(c, exit) <= EndZone);

        bool TouchesFarHollow(Vector2Int c, int step)
        {
            foreach (var n in Neighbours)
            {
                var o = c + n;
                if (!InMap(o)) continue;
                int l = label[o.x, o.y];
                if (l != Solid && Math.Abs((long)l - step) > Window) return true;
            }
            return false;
        }

        void MarkFloor(Vector2Int p)
        {
            if (p.y > 0) floor[p.x, p.y - 1] = true;
        }

        bool InMap(Vector2Int c) => c.x >= 0 && c.x < width && c.y >= 0 && c.y < height;
        bool IsHollow(Vector2Int c) => InMap(c) && label[c.x, c.y] != Solid;

        // Out of the map, the world's walls and the ground under it are rock, the sky over it isn't.
        bool IsRock(int x, int y) => y < 0 || x < 0 || x >= width || (y < height && rock[x, y]);

        static int Chebyshev(Vector2Int a, Vector2Int b) => Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
