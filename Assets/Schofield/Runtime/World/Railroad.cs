// A railroad crossing: graded ballast, ties, rail, a water tower and a section gang. On a route without
// a train the line is still being built — rail runs through the crossing and stops at the railhead just
// past it where the gang is working. Where the route runs a train the line is finished end to end and
// the gang stands well back. Ported from world/railroad.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class RailDef { public float at = 0.6f; public int workers = 6; public bool train; public int cars = 3, gunmen = 4; public float speed = 11; }

    public class Railroad
    {
        const float Reach = 150, ReachTrain = 360, TieGap = 0.62f, BedHalf = 4;
        readonly Route route;
        readonly GameObject root;
        readonly List<Object> owned = new List<Object>();
        public readonly List<Bounds> solids = new List<Bounds>();
        readonly List<(Person p, Transform arm, Quaternion q0, float phase, float rate)> workers = new List<(Person, Transform, Quaternion, float, float)>();
        public readonly bool hasTrain;
        public readonly float reach, s, grade, dt, dr, built, campU;
        public readonly Vector3 dir, side, centre;
        bool jolted;
        public System.Action<float> onJolt;

        public Railroad(Route route, Transform parent, RailDef def)
        {
            this.route = route;
            root = new GameObject("Railroad"); root.transform.SetParent(parent, false);
            hasTrain = def.train;
            reach = hasTrain ? ReachTrain : Reach;
            var rnd = new Rng(route.def.seed + 17);
            // graded, not draped over hills: search near the requested point for the flattest corridor
            var pick = Site(Mathf.Round(route.len * def.at), rnd);
            s = pick.s; dir = pick.dir;
            var f = route.Frame(s);
            centre = new Vector3(f.x, route.RoadHeightAt(s), f.z);
            grade = Mathf.Clamp(pick.slope, -0.018f, 0.018f);
            dt = dir.x * f.tx + dir.z * f.tz;
            dr = dir.x * f.rx + dir.z * f.rz;
            side = new Vector3(dir.z, 0, -dir.x);
            route.SetRail(new Route.Rail { x = centre.x, z = centre.z, dx = dir.x, dz = dir.z, y = centre.y, grade = grade, reach = reach, half = BedHalf });
            built = hasTrain ? reach : 24 + rnd.Next() * 18;
            campU = hasTrain ? 16 + rnd.Next() * 12 : built;
            Ballast(); Ties(rnd); Rails(); Camp(rnd, def);
        }

        (float s, Vector3 dir, float slope) Site(float want, Rng rnd)
        {
            var R = route;
            float span = Mathf.Min(0.08f * R.len, 260);
            (float, Vector3, float)? best = null; float bestScore = float.NegativeInfinity;
            for (int k = 0; k < 26; k++)
            {
                float ss = Mathf.Round(Mathf.Clamp(want + (rnd.Next() - 0.5f) * 2 * span, 140, R.len - 200));
                var f = R.Frame(ss);
                float ang = Mathf.Atan2(f.rx, f.rz) + (rnd.Next() - 0.5f) * 0.45f;
                var dv = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang));
                var us = new List<float>(); var ys = new List<float>(); int clash = 0;
                for (float u = -reach; u <= reach; u += 10)
                {
                    float x = f.x + dv.x * u, z = f.z + dv.z * u;
                    us.Add(u); ys.Add(R.Height(x, z));
                    if (Mathf.Abs(u) > 30) { R.RoadCoords(x, z, out float rd, out float rs); if (rs >= 0 && Mathf.Abs(rd) < 30 && Mathf.Abs(rs - ss) > 35) clash++; }
                }
                var T = R.TownRange;
                if (R.InTown(ss) || (R.HasGhostTown && ss > T.x - 120 && ss < T.y + 120)) clash += 5;
                if (R.OnBridge(ss) || (R.hasFalls && Mathf.Abs(ss - R.fallsS) < 200)) clash += 10;
                int n = us.Count; float my = 0; foreach (var y in ys) my += y; my /= n;
                float num = 0, den = 0;
                for (int i = 0; i < n; i++) { num += us[i] * (ys[i] - my); den += us[i] * us[i]; }
                float slope = num / den, resid = 0;
                for (int i = 0; i < n; i++) resid += Mathf.Abs(ys[i] - (my + slope * us[i]));
                float score = -resid / n - Mathf.Abs(slope) * 40 - clash * 50;
                if (score > bestScore) { bestScore = score; best = (ss, dv, slope); }
            }
            return best.Value;
        }

        /// the graded rail height u metres from the crossing (a ruling gradient, not the terrain)
        public float GradeY(float u) => centre.y + grade * u;
        public Vector3 At(float u) { var p = centre + dir * u; p.y = GradeY(u); return p; }

        Material Mat(Color c, float smooth = 0.05f, float metal = 0, string tex = null)
        {
            var m = Assets.Lit(c, smooth, metal);
            if (tex != null) m.SetTexture("_BaseMap", Assets.Tex(tex));
            owned.Add(m); return m;
        }
        void Show(string name, MeshKit.Builder b, Material m, bool shadows)
        {
            var mesh = b.Build(name); owned.Add(mesh);
            MeshKit.Show(name, mesh, m, root.transform, shadows);
        }

        // the ballast: a raised stone bed with sloped shoulders, on land already cut or filled to grade
        void Ballast()
        {
            int segs = Mathf.FloorToInt(reach * 2 / 4);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float[,] prof = { { -3.4f, -0.5f }, { -2.0f, 0.07f }, { 2.0f, 0.07f }, { 3.4f, -0.5f } };
            for (int i = 0; i <= segs; i++)
            {
                float u = -reach + i / (float)segs * reach * 2, gy = GradeY(u);
                float fade = Mathf.Min(1, (reach - Mathf.Abs(u)) / 12);
                for (int k = 0; k < 4; k++)
                {
                    float off = prof[k, 0], dy = prof[k, 1];
                    var p = centre + dir * u + side * off * (0.3f + 0.7f * fade);
                    p.y = gy + (dy + 0.5f) * fade - 0.5f;
                    v.Add(p); uv.Add(new Vector2(off / 5 + 0.5f, u / 5));
                }
                if (i < segs)
                {
                    int b0 = i * 4;
                    for (int q = 0; q < 3; q++) t.AddRange(new[] { b0 + q, b0 + q + 4, b0 + q + 1, b0 + q + 1, b0 + q + 4, b0 + q + 5 });
                }
            }
            // wind upward: check the first face against +Y and flip all if needed
            var nrm = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            if (nrm.y < 0) for (int i = 0; i < t.Count; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            var m = new Mesh { name = "Ballast", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); owned.Add(m);
            MeshKit.Show("Ballast", m, Mat(MathX.Hex("#8d8479"), 0, 0, "gravel"), root.transform, false);
        }

        // sleepers, laid from the far side through the crossing to the railhead
        void Ties(Rng rnd)
        {
            var b = new MeshKit.Builder();
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            for (float u = -reach; u <= built; u += TieGap)
            {
                var p = At(u); p.y += 0.2f;
                bool loose = !hasTrain && u > built - 9;
                var q = Quaternion.Euler(loose ? (rnd.Next() - 0.5f) * 7 : 0, yaw + 90 + (loose ? (rnd.Next() - 0.5f) * 20 : (rnd.Next() - 0.5f) * 1.7f), 0);
                b.Box(new Vector3(2.4f, 0.14f, 0.22f), p, Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, loose ? (rnd.Next() - 0.5f) * 20 : 0, 0));
            }
            Show("Ties", b, Mat(MathX.Hex("#4a3624"), 0.05f, 0, "wood_planks"), false);
        }

        // two rails ending where the gang got to, and the planked crossing flush with the rail heads
        void Rails()
        {
            float u0 = -reach, u1 = built, len = u1 - u0;
            if (len < 2) return;
            var rot = Quaternion.Euler(-Mathf.Atan(grade) * Mathf.Rad2Deg, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0);
            var iron = new MeshKit.Builder(); var shine = new MeshKit.Builder(); var wood = new MeshKit.Builder();
            foreach (float k in new[] { -0.717f, 0.717f })
                foreach (var (w, h, y, sh) in new[] { (0.1f, 0.016f, 0.278f, false), (0.018f, 0.05f, 0.31f, false), (0.056f, 0.024f, 0.345f, false), (0.044f, 0.006f, 0.36f, true) })
                    (sh ? shine : iron).Box(new Vector3(w, h, len), centre + rot * new Vector3(k, y, (u0 + u1) / 2), rot);
            float ext = (Route.RoadHalf + 0.6f) / Mathf.Max(0.3f, Mathf.Abs(dr));
            foreach (float x in new[] { -1.25f, -0.95f, -0.5f, -0.17f, 0.17f, 0.5f, 0.95f, 1.25f })
                wood.Box(new Vector3(0.28f, 0.1f, ext * 2 - Mathf.Abs(x) * 0.1f), centre + rot * new Vector3(x, 0.305f, 0), rot);
            Show("Rail iron", iron, Mat(MathX.Hex("#4a3c32"), 0.3f, 0.6f), true);
            Show("Rail heads", shine, Mat(MathX.Hex("#b8b2aa"), 0.72f, 0.95f), false);
            Show("Crossing planks", wood, Mat(MathX.Hex("#7a6248"), 0.08f, 0, "wood_planks"), false);
        }

        // water tower, material stacks and the gang
        void Camp(Rng rnd, RailDef def)
        {
            var town = Assets.Model("town");
            var tsrc = town ? Assets.Find(town.transform, "WaterTower") : null;
            if (tsrc)
            {
                var c = Object.Instantiate(tsrc.gameObject, root.transform);
                var p = At(-34) + side * 9; p.y = route.Height(p.x, p.z) - 0.05f;
                c.transform.position = p;
                c.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90, 0);
                var rs = c.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0) { var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); solids.Add(bb); }
            }
            var ties = new MeshKit.Builder();
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            for (int st = 0; st < 3; st++)
            {
                float su = hasTrain ? campU + st * 5 : built - 6 - st * 5;
                var bse = At(su) + side * ((rnd.Next() < 0.5f ? -1 : 1) * ((hasTrain ? 4.8f : 3.6f) + rnd.Next() * 1.6f));
                bse.y = route.Height(bse.x, bse.z);
                int rows = 3 + Mathf.FloorToInt(rnd.Next() * 3);
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        var p = bse + Vector3.up * (0.07f + r * 0.15f) + dir * ((c - 1) * 0.26f + (rnd.Next() - 0.5f) * 0.05f);
                        ties.Box(new Vector3(2.4f, 0.14f, 0.22f), p, Quaternion.Euler(0, yaw + (r % 2 == 1 ? 90 : 0) + (rnd.Next() - 0.5f) * 3.4f, 0));
                    }
            }
            Show("Tie stacks", ties, Mat(MathX.Hex("#5a4230"), 0.05f, 0, "wood_planks"), true);
            var props = Assets.Model("props");
            if (props)
                foreach (var (name, count) in new[] { ("Barrel", 4), ("Crate", 5) })
                {
                    var src = Assets.Find(props.transform, name);
                    if (!src) continue;
                    for (int i = 0; i < count; i++)
                    {
                        var o = Object.Instantiate(src.gameObject, root.transform);
                        var p = At(hasTrain ? campU - 2 + rnd.Next() * 18 : built - 2 - rnd.Next() * 18) + side * ((rnd.Next() < 0.5f ? -1 : 1) * ((hasTrain ? 4.2f : 3) + rnd.Next() * 4.5f));
                        p.y = route.Height(p.x, p.z);
                        o.transform.position = p; o.transform.rotation = Quaternion.Euler(0, rnd.Next() * 360, 0);
                    }
                }
            var haftM = Mat(MathX.Hex("#8a6a42"), 0.1f); var headM = Mat(MathX.Hex("#3a3a3e"), 0.5f, 0.7f);
            for (int i = 0; i < def.workers; i++)
            {
                var w = Characters.CreateRider(i % 3 == 0 ? "townsman" : "gunman");
                w.root.transform.SetParent(root.transform, false);
                float u = hasTrain ? campU - 3 + rnd.Next() * 14 : built - 1 - rnd.Next() * 12;
                var p = At(u) + side * ((i % 2 == 1 ? 1 : -1) * (hasTrain ? 3.6f + rnd.Next() * 2.4f : 1.4f + rnd.Next() * 2.6f));
                p.y = route.Height(p.x, p.z);
                w.root.transform.position = p;
                w.root.transform.rotation = Quaternion.Euler(0, (Mathf.Atan2(dir.x, dir.z) + (i % 2 == 1 ? Mathf.PI / 2 : -Mathf.PI / 2) + (rnd.Next() - 0.5f) * 0.4f) * Mathf.Rad2Deg, 0);
                if (w.Has("StandIdle")) w.Play("StandIdle", 0);
                bool swings = !hasTrain && i < def.workers - 2;
                Transform arm = null;
                if (swings && w.hand)
                {
                    var sledge = new GameObject("Sledge").transform; sledge.SetParent(w.hand, false);
                    var haft = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(haft.GetComponent<Collider>());
                    haft.transform.SetParent(sledge, false); haft.transform.localScale = new Vector3(0.048f, 0.45f, 0.048f); haft.transform.localRotation = Quaternion.Euler(90, 0, 0); haft.transform.localPosition = new Vector3(0, 0, 0.45f);
                    haft.GetComponent<Renderer>().sharedMaterial = haftM;
                    var head = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(head.GetComponent<Collider>());
                    head.transform.SetParent(sledge, false); head.transform.localScale = new Vector3(0.26f, 0.09f, 0.09f); head.transform.localPosition = new Vector3(0, 0, 0.9f);
                    head.GetComponent<Renderer>().sharedMaterial = headM;
                    foreach (var n in new[] { "upperarmR", "upperarm.R", "upper_arm.R", "upperarm_R" }) { arm = Assets.Find(w.root.transform, n); if (arm) break; }
                }
                workers.Add((w, arm, arm ? arm.localRotation : Quaternion.identity, rnd.Next() * 6.3f, 1.5f + rnd.Next() * 0.7f));
            }
        }

        public void Update(float dtime, Coach coach)
        {
            if (coach != null)
            {
                bool over = Mathf.Abs(coach.s - s) < 2.2f;
                if (over && !jolted) { jolted = true; onJolt?.Invoke(Mathf.Min(1, coach.speed / 15)); }
                else if (!over && coach.s > s + 4) jolted = false;
            }
            for (int i = 0; i < workers.Count; i++) { var w = workers[i]; w.p.Update(dtime); w.phase += dtime * w.rate; workers[i] = w; }
        }

        /// after animation: the sledge stroke — raise slow, drive down fast
        public void LateUpdate()
        {
            foreach (var w in workers)
            {
                if (!w.arm) continue;
                float t = (w.phase % (Mathf.PI * 2)) / (Mathf.PI * 2);
                float x = t < 0.72f ? -1.5f + 1.9f * (t / 0.72f) : 0.4f - 1.9f * ((t - 0.72f) / 0.28f);
                w.arm.localRotation = w.q0 * Quaternion.Euler(x * Mathf.Rad2Deg, 0, 0);
            }
        }

        public void Dispose()
        {
            foreach (var w in workers) w.p.anim.Dispose();
            if (root) Object.Destroy(root);
            foreach (var o in owned) if (o) Object.Destroy(o);
        }
    }
}
