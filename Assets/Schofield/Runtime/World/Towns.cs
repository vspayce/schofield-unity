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
            float courtSide = rnd.Next() < 0.5f ? -1 : 1;
            Street(20, 110, rnd, 0.8f, false, (57, 75, courtSide));
            Court(66, courtSide, false, hours);
            Street(L - 130, L - 10, rnd, 0.8f, false, (0, 0, 0));
            Court(L + 24, 0, true, hours + L / 12 / 3600);
            if (route.HasGhostTown) { var T = route.TownRange; Street(T.x, T.y, rnd, 1, true, (0, 0, 0)); }
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

        void Street(float s0, float s1, Rng rnd, float fill, bool ghost, (float s0, float s1, float side) reserve)
        {
            var placed = new Dictionary<float, List<(float s, string name)>> { { -1, new List<(float, string)>() }, { 1, new List<(float, string)>() } };
            foreach (float side in new[] { -1f, 1f })
            {
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
                    s += w + 1.5f + rnd.Next() * 4;
                }
            }
            float mid = (s0 + s1) / 2, ls = rnd.Next() < 0.5f ? -1 : 1;
            Place(ghost ? "Church" : "WaterTower", mid + 30, ls, rnd, ghost, 14);
            if (ghost) Place("Gallows", s0 + 40, -1, rnd, ghost, 2);
        }

        static Bounds BoundsOf(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public void Dispose() { if (root) Object.Destroy(root.gameObject); }
    }
}
