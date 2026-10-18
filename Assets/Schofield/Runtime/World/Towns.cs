// The start and end towns along the road (buildings from town.glb, the court house with its clock),
// and the boxes that stop bullets. Townsfolk, horses at the rails and the ghost town come next.
// Ported from the building half of world/town.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class Towns
    {
        static readonly string[] Kit = { "Saloon", "GeneralStore", "Sheriff", "Bank", "Hotel", "Livery", "Shack" };
        static readonly Dictionary<string, float> Width = new Dictionary<string, float>
        { { "Saloon", 10.5f }, { "GeneralStore", 9.7f }, { "Sheriff", 8.1f }, { "Bank", 8.5f }, { "Hotel", 12.5f }, { "Livery", 12.8f }, { "Shack", 5.2f }, { "Church", 8.8f }, { "WaterTower", 5.3f }, { "Gallows", 6.7f }, { "CourtHouse", 14.0f } };
        static readonly Dictionary<string, float> Depth = new Dictionary<string, float>
        { { "Saloon", 16.6f }, { "GeneralStore", 15.7f }, { "Sheriff", 13.4f }, { "Bank", 13.2f }, { "Hotel", 15.6f }, { "Livery", 17.4f }, { "Shack", 5.3f }, { "Church", 16.6f }, { "WaterTower", 5.8f }, { "Gallows", 3.1f }, { "CourtHouse", 17.5f } };

        public class Lot { public string name; public float s, side, off, w, y, yaw, P, deck; public Transform b; public bool terminus; }
        static readonly Dictionary<string, (float P, float deck)> Porch = new Dictionary<string, (float, float)>
        { { "Saloon", (2.6f, 0.4f) }, { "GeneralStore", (2.6f, 0.4f) }, { "Sheriff", (2.4f, 0.4f) }, { "Bank", (1.2f, 0.3f) }, { "Hotel", (2.6f, 0.4f) } };
        List<Lot> lots;            // where Place records what it builds (start / end town), or null
        public readonly List<Lot> startLots = new List<Lot>(), endLots = new List<Lot>();
        Townsfolk folk;

        readonly Route route;
        readonly Transform root;
        public readonly List<Bounds> solids = new List<Bounds>();
        public readonly List<(Vector3 pos, Vector3 face, float s, bool roof)> spawns = new List<(Vector3, Vector3, float, bool)>();

        public Towns(Route route, Transform parent)
        {
            this.route = route;
            root = new GameObject("Towns").transform; root.SetParent(parent, false);
            var rnd = new Rng(route.def.seed + 5);
            float L = route.len, hours = ClockHours(route.def.clock);
            // on a gorge ledge the river side is a drop: build on the other side only
            float wet = route.def.gorge != null ? route.riverSide : 0;
            float pick = rnd.Next() < 0.5f ? -1 : 1, courtSide = wet != 0 ? -wet : pick;
            lots = startLots;
            Street(20, 110, rnd, 0.8f, false, (57, 75, courtSide), wet);
            Court(66, courtSide, false, hours);
            lots = endLots;
            Street(L - 130, L - 10, rnd, 0.8f, false, (0, 0, 0), wet);
            Court(L + 24, 0, true, hours + L / 12 / 3600);
            lots = null;
            if (route.HasGhostTown) { var T = route.TownRange; Street(T.x, T.y, rnd, 1, true, (0, 0, 0)); }
            folk = new Townsfolk(this, route, root, rnd);
            folk.Populate(startLots, 20, 110);
            folk.Populate(endLots, L - 130, L - 10);
        }

        static float ClockHours(string str)
        {
            var parts = (str ?? "4:20").Split(':');
            float h = float.Parse(parts[0]), m = parts.Length > 1 ? float.Parse(parts[1]) : 0;
            return h % 12 + m / 60f;
        }

        GameObject KitBuilding(string name)
        {
            var town = Assets.Model("town");
            var src = town ? Assets.Find(town.transform, name) : null;
            if (src)
            {
                var c = Object.Instantiate(src.gameObject, root);
                c.name = name;
                c.transform.localPosition = Vector3.zero; c.transform.localRotation = Quaternion.identity;
                return c;
            }
            // stand-in box
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = name;
            float w = Width.TryGetValue(name, out var ww) ? ww - 1 : 8, d = 10, h = name == "Saloon" || name == "Hotel" ? 7.5f : 5;
            g.transform.SetParent(root, false);
            g.transform.localScale = new Vector3(w, h, d);
            g.GetComponent<Renderer>().sharedMaterial = Assets.Lit(new Color(0.42f, 0.32f, 0.22f));
            var holder = new GameObject(name); holder.transform.SetParent(root, false);
            g.transform.SetParent(holder.transform, false); g.transform.localPosition = new Vector3(0, h / 2, -d / 2);
            return holder;
        }

        float FootY(Vector3 at, float yaw, float w, float dp)
        {
            float y = float.MaxValue;
            foreach (var (a, c) in new[] { (-w, 0f), (w, 0f), (-w, -dp), (w, -dp), (0f, -dp / 2) })
            {
                float lx = Mathf.Cos(yaw) * a + Mathf.Sin(yaw) * c, lz = -Mathf.Sin(yaw) * a + Mathf.Cos(yaw) * c;
                y = Mathf.Min(y, route.Height(at.x + lx, at.z + lz));
            }
            return y;
        }

        void Place(string name, float s, float side, Rng rnd, bool ghost, float extra = 0)
        {
            var f = route.Frame(s);
            float off = 9.5f + extra + rnd.Next() * 1.2f + (name == "Livery" ? 1.5f : 0);
            var at = new Vector3(f.x + f.rx * off * side, 0, f.z + f.rz * off * side);
            var b = KitBuilding(name);
            // front (+Z local) faces the road
            float yaw = Mathf.Atan2(-f.rx * side, -f.rz * side);
            float w = (Width.TryGetValue(name, out var ww) ? ww : 9) / 2, dp = Depth.TryGetValue(name, out var dd) ? dd : 10;
            at.y = FootY(at, yaw, w, dp) - 0.05f;
            b.transform.position = at;
            b.transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            if (ghost) foreach (var r in b.GetComponentsInChildren<Renderer>()) Assets.Tint(r, new Color(0.85f, 0.85f, 0.85f));
            solids.Add(BoundsOf(b));
            if (lots != null && extra == 0)
            {
                var pr = Porch.TryGetValue(name, out var pp) ? pp : (0f, 0f);
                lots.Add(new Lot { name = name, s = s, side = side, off = off, w = w * 2, y = at.y, yaw = yaw, P = pr.Item1, deck = pr.Item2, b = b.transform });
            }
            if (ghost)
                foreach (var t in b.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("Spawn_")) spawns.Add((t.position, MathX.FromHeading(yaw), s, t.name.Contains("Roof")));
        }

        void Court(float s, float side, bool terminus, float hours)
        {
            var f = route.Frame(Mathf.Min(s, route.len));
            var b = KitBuilding("CourtHouse");
            SetClock(b.transform, hours);
            float off = terminus ? 0 : 9.5f;
            // the terminus stands square across the road's end, past the last station
            var basePos = terminus ? new Vector3(f.x + f.tx * (s - route.len), 0, f.z + f.tz * (s - route.len)) : new Vector3(f.x, 0, f.z);
            var at = basePos + new Vector3(f.rx, 0, f.rz) * off * side;
            float yaw = terminus ? Mathf.Atan2(-f.tx, -f.tz) : Mathf.Atan2(-f.rx * side, -f.rz * side);
            at.y = FootY(at, yaw, Width["CourtHouse"] / 2, Depth["CourtHouse"]) - 0.05f;
            b.transform.position = at;
            b.transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            solids.Add(BoundsOf(b));
            lots?.Add(new Lot { name = "CourtHouse", s = Mathf.Min(s, route.len), side = side, off = off, w = Width["CourtHouse"], y = at.y, yaw = yaw, b = b.transform, terminus = terminus });
        }

        // turn the clock hands (Clock_Hour_i / Clock_Minute_i, modelled at XII) about their dials' normals
        static void SetClock(Transform b, float hours)
        {
            var normals = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            for (int i = 0; i < 4; i++)
                foreach (var (k, turns) in new[] { ("Hour", hours % 12 / 12f), ("Minute", hours % 1) })
                {
                    var h = Assets.Find(b, $"Clock_{k}_{i}");
                    if (!h) continue;
                    // the web game turned -turns about the dial normal in three.js space; glTF import mirrors X,
                    // which reverses the sense of a rotation about any axis
                    h.localRotation = Quaternion.AngleAxis(turns * 360f, normals[i]) * h.localRotation;
                }
        }

        void Street(float s0, float s1, Rng rnd, float fill, bool ghost, (float s0, float s1, float side) reserve, float wet = 0)
        {
            var placed = new Dictionary<float, List<(float s, string name)>> { { -1, new List<(float, string)>() }, { 1, new List<(float, string)>() } };
            foreach (float side in new[] { -1f, 1f })
            {
                if (side == wet) continue;
                float s = s0 + rnd.Next() * 6; string last = "";
                while (s < s1)
                {
                    if (rnd.Next() > fill) { s += 8 + rnd.Next() * 10; continue; }
                    string name = Kit[0];
                    for (int k = 0; k < 8; k++)
                    {
                        name = Kit[Mathf.FloorToInt(rnd.Next() * Kit.Length) % Kit.Length];
                        var opp = placed[-side].Find(b => Mathf.Abs(b.s - s) < 12);
                        if (name != last && (opp.name == null || opp.name != name)) break;
                    }
                    float w = Width.TryGetValue(name, out var ww) ? ww : 9;
                    if (reserve.side == side && s < reserve.s1 && s + w > reserve.s0) { s = reserve.s1 + 1.5f + rnd.Next() * 2; continue; }
                    Place(name, s + w / 2, side, rnd, ghost);
                    placed[side].Add((s, name)); last = name;
                    if (ghost) Clutter(s + w * (0.2f + rnd.Next() * 0.6f), side, rnd);
                    s += w + 1.5f + rnd.Next() * 4;
                }
            }
            float mid = (s0 + s1) / 2, ls = rnd.Next() < 0.5f ? -1 : 1;
            if (wet != 0) ls = -wet;
            Place(ghost ? "Church" : "WaterTower", mid + 30, ls, rnd, ghost, 14);
            if (ghost) Place("Gallows", s0 + 40, -1, rnd, ghost, 2);
        }

        /// barrels, crates, a trough or a wreck by the street
        public void Clutter(float s, float side, Rng rnd)
        {
            var props = Assets.Model("props");
            if (!props) return;
            string[] names = { "Barrel", "Crate", "WaterTrough", "Barrel", "Crate", "Wagon_Wreck" };
            string pick = names[Mathf.FloorToInt(rnd.Next() * (rnd.Next() < 0.85f ? 5 : 6)) % 6];
            var src = Assets.Find(props.transform, pick);
            if (!src) return;
            var f = route.Frame(s);
            float off = (pick == "Wagon_Wreck" ? 7 : 6.8f) + rnd.Next() * 0.8f;
            int n = pick == "Barrel" || pick == "Crate" ? 1 + Mathf.FloorToInt(rnd.Next() * 3) : 1;
            for (int i = 0; i < n; i++)
            {
                var o = Object.Instantiate(src.gameObject, root);
                float x = f.x + f.rx * off * side + f.tx * i * 0.9f, z = f.z + f.rz * off * side + f.tz * i * 0.9f;
                o.transform.position = new Vector3(x, route.Height(x, z), z);
                o.transform.rotation = Quaternion.Euler(0, pick == "Wagon_Wreck" ? (Mathf.Atan2(f.tx, f.tz) + (rnd.Next() - 0.5f) * 0.6f) * Mathf.Rad2Deg : rnd.Next() * 360, 0);
                solids.Add(BoundsOf(o));
            }
        }

        /// a piece of the town kit at road coordinates
        public GameObject KitAt(string name, float s, float d, float yaw, bool solid = false)
        {
            var town = Assets.Model("town");
            var src = town ? Assets.Find(town.transform, name) : null;
            if (!src) return null;
            var f = route.Frame(s);
            var o = Object.Instantiate(src.gameObject, root);
            float x = f.x + f.rx * d, z = f.z + f.rz * d;
            o.transform.position = new Vector3(x, route.Height(x, z), z);
            o.transform.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            if (solid) solids.Add(BoundsOf(o));
            return o;
        }

        public void Update(float dt, Coach coach) => folk?.Update(dt, coach);

        static Bounds BoundsOf(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public void Dispose() { folk?.Dispose(); if (root) Object.Destroy(root.gameObject); }
    }
}
