// Brings the two living towns to life: folk on the boardwalks and in the street, lawmen on patrol,
// conversation groups, people on benches and leaning on the rails, horses tied at the hitch rails, a
// parked buckboard — and everyone near the coach runs for a door at the sound of gunfire.
// Ported from the populate half of world/town.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class Townsfolk
    {
        const float WalkSpeed = 1.4f;   // the Walk clip is authored at this pace
        static readonly string[] Men = { "townsman", "player", "townsman", "bandit2" };
        static readonly string[] Women = { "woman_slate", "woman_plum", "woman_calico" };
        static readonly Color[] FolkTint = { new Color(1, 1, 1), new Color(0.72f, 0.74f, 0.8f), new Color(0.95f, 0.88f, 0.72f), new Color(0.6f, 0.62f, 0.58f), new Color(1.05f, 0.95f, 0.85f), new Color(0.8f, 0.7f, 0.66f) };
        static readonly Color[] Coats = { new Color(0.62f, 0.42f, 0.3f), new Color(0.85f, 0.55f, 0.34f), new Color(0.32f, 0.25f, 0.22f), new Color(1.1f, 1.05f, 1.0f), new Color(1.15f, 0.92f, 0.6f), new Color(0.5f, 0.36f, 0.28f) };

        class TownSet { public List<Towns.Lot>[] bySide = new List<Towns.Lot>[2]; public int left, horses; public List<float> blocked = new List<float>(); public float s0, s1; public List<Towns.Lot> Side(float sd) => bySide[sd < 0 ? 0 : 1]; }
        class Folk
        {
            public Person p; public TownSet town; public string kind, clip; public float s, d, baseYaw, yaw, phase, lift, wait, speed, a, b, dj, from, to, ts, td, t, y = float.NaN; public float side, dir = 1; public bool moving;
        }
        class TiedHorse { public Horse h; public float s, rearT; }

        readonly Towns towns; readonly Route route; readonly Transform root; readonly Rng rnd;
        readonly List<Folk> folk = new List<Folk>();
        readonly List<TiedHorse> horses = new List<TiedHorse>();
        int heard, seen;
        readonly float busy;

        public Townsfolk(Towns towns, Route route, Transform root, Rng rnd)
        {
            this.towns = towns; this.route = route; this.root = root; this.rnd = rnd;
            busy = Application.isMobilePlatform ? 0.45f : 0.8f;
            if (Sound.I) Sound.I.onPlay += OnSound;
        }

        void OnSound(string name) { if (name.Contains("shot")) heard++; }

        Vector3 XZ(float s, float d, out RoadFrame f) { f = route.Frame(s); return new Vector3(f.x + f.rx * d, 0, f.z + f.rz * d); }
        T Pick<T>(T[] a) => a[Mathf.FloorToInt(rnd.Next() * a.Length) % a.Length];
        string Anyone() => rnd.Next() < 0.42f ? Pick(Women) : Pick(Men);
        Color Tint() => Pick(FolkTint);

        // how far out a walker on one side is at station s: along the boardwalks, eased across the gaps
        float Lane(TownSet T, float side, float s)
        {
            var lots = T.Side(side);
            float At(Towns.Lot l) => l.P > 0 ? l.off + Mathf.Min(1.3f, l.P * 0.55f) : l.off - 1.0f;
            Towns.Lot prev = null;
            foreach (var l in lots)
            {
                if (s >= l.s - l.w / 2 && s <= l.s + l.w / 2) return At(l);
                if (l.s - l.w / 2 > s)
                {
                    if (prev == null) return At(l);
                    float a = prev.s + prev.w / 2, b = l.s - l.w / 2, k = (s - a) / Mathf.Max(0.1f, b - a);
                    return At(prev) + (At(l) - At(prev)) * k;
                }
                prev = l;
            }
            return prev != null ? At(prev) : 8.5f;
        }

        Person NewPerson(TownSet T, string variant, Color? tint)
        {
            if (T.left <= 0) return null;
            T.left--;
            var p = Characters.CreateRider(variant, tint);
            p.root.transform.SetParent(root, false);
            return p;
        }

        Folk Add(Folk n)
        {
            n.yaw = n.baseYaw;
            Idle(n);
            Put(n, 0);
            folk.Add(n);
            return n;
        }

        void Idle(Folk n) { var c = n.clip != null && n.p.Has(n.clip) ? n.clip : "StandIdle"; if (n.p.Has(c) && n.p.anim.current != c) n.p.Play(c, 0.3f); }
        void Move(Folk n)
        {
            bool run = n.speed > 2.4f;
            if (run && n.p.Has("Run")) { if (n.p.anim.current != "Run") n.p.Play("Run", 0.2f); n.p.anim.SetSpeed(n.speed / 3.6f); }
            else if (n.p.Has("Walk")) { if (n.p.anim.current != "Walk") n.p.Play("Walk", 0.25f); n.p.anim.SetSpeed(n.speed / WalkSpeed); }
        }

        // ground or deck under (s, d), eased so stepping onto a porch isn't a pop
        void Put(Folk n, float dt)
        {
            var p = XZ(n.s, n.d, out _);
            float y = route.Height(p.x, p.z);
            float side = Mathf.Sign(n.d);
            if (n.town != null && n.d != 0)
                foreach (var l in n.town.Side(side))
                    if (l.P > 0 && Mathf.Abs(n.s - l.s) < l.w / 2 - 0.05f && Mathf.Abs(n.d) > l.off + 0.05f && Mathf.Abs(n.d) < l.off + l.P) { y = l.y + l.deck; break; }
            y += n.lift;
            n.y = float.IsNaN(n.y) || dt <= 0 ? y : MathX.Damp(n.y, y, 12, dt);
            n.p.root.transform.position = new Vector3(p.x, n.y, p.z);
            n.p.root.transform.rotation = Quaternion.Euler(0, n.yaw * Mathf.Rad2Deg, 0);
        }

        public void Populate(List<Towns.Lot> all, float s0, float s1)
        {
            var lots = all.FindAll(l => !l.terminus);
            var T = new TownSet { s0 = s0, s1 = s1 };
            T.bySide[0] = lots.FindAll(l => l.side < 0); T.bySide[0].Sort((a, b) => a.s.CompareTo(b.s));
            T.bySide[1] = lots.FindAll(l => l.side > 0); T.bySide[1].Sort((a, b) => a.s.CompareTo(b.s));
            T.left = Mathf.RoundToInt(32 * busy);
            T.horses = Mathf.Max(2, Mathf.RoundToInt(7 * busy));
            var sides = new List<float>(); if (T.bySide[0].Count > 0) sides.Add(-1); if (T.bySide[1].Count > 0) sides.Add(1);
            if (sides.Count == 0) return;
            var used = new HashSet<Towns.Lot>();
            var leans = new List<(float s, float d, float yaw)>(); var benches = new List<(float s, float bd, Towns.Lot l)>();
            int wagons = busy > 0.7f ? 2 : 1;
            foreach (var l in lots)
            {
                if (l.name == "CourtHouse") continue;
                if (l.name == "Livery")
                {
                    if (T.horses > 0 && rnd.Next() < 0.8f * busy + 0.2f) Tie(T, l.s + l.side * 4.2f, l, l.off - 1.6f);
                    used.Add(l); continue;
                }
                if (l.P > 0 && T.horses > 0 && rnd.Next() < 0.55f * busy + 0.15f)
                {
                    float s = l.s + (rnd.Next() - 0.5f) * Mathf.Max(0, l.w - 4.5f), rd = l.off - 2.2f;
                    towns.KitAt("HitchRail", s, rd * l.side, l.yaw);
                    int nh = Mathf.Min(T.horses, 1 + (rnd.Next() < 0.5f * busy ? 1 : 0));
                    for (int i = 0; i < nh; i++) Tie(T, s + (nh > 1 ? (i - 0.5f) * 1.5f : (rnd.Next() - 0.5f) * 0.8f), l, rd);
                    if (rnd.Next() < 0.5f) leans.Add((s + (rnd.Next() < 0.5f ? -1 : 1) * 1.6f, (rd + 0.45f) * l.side, l.yaw));
                    used.Add(l);
                }
                else if (wagons > 0 && rnd.Next() < 0.35f)
                {
                    wagons--;
                    var f = route.Frame(l.s); float dr = rnd.Next() < 0.5f ? 1 : -1;
                    towns.KitAt("Buckboard", l.s, (l.off - 3.1f) * l.side, Mathf.Atan2(f.tx * dr, f.tz * dr) + (rnd.Next() - 0.5f) * 0.1f, true);
                    T.blocked.Add(l.s - 2.5f); T.blocked.Add(l.s); T.blocked.Add(l.s + 2.5f);
                    used.Add(l);
                }
                else if (rnd.Next() < 0.5f) towns.Clutter(l.s + (rnd.Next() - 0.5f) * l.w * 0.6f, l.side, rnd);
                if (l.P >= 2.4f && rnd.Next() < 0.3f + 0.3f * busy)
                {
                    float s = l.s + (rnd.Next() < 0.5f ? -1 : 1) * (l.w / 2 - 1.5f), bd = l.off + l.P - 0.55f;
                    var bench = towns.KitAt("Bench", s, bd * l.side, l.yaw);
                    if (bench) { var bp = bench.transform.position; bp.y = l.y + l.deck; bench.transform.position = bp; }
                    benches.Add((s, bd, l));
                }
            }
            int nWalk = Mathf.RoundToInt(7 * busy);
            for (int i = 0; i < nWalk; i++)
            {
                float side = sides[Mathf.FloorToInt(rnd.Next() * sides.Count) % sides.Count], len = 18 + rnd.Next() * 30;
                float a = T.s0 + 2 + rnd.Next() * Mathf.Max(1, T.s1 - T.s0 - len - 4);
                Walker(T, Anyone(), "walk", side, a, a + len);
            }
            var jail = lots.Find(l => l.name == "Sheriff") ?? all.Find(l => l.name == "CourtHouse" && !l.terminus);
            for (int i = 0; i < (busy > 0.7f ? 2 : 1); i++)
            {
                float side = jail != null ? (jail.side != 0 ? jail.side : 1) : 1, c = jail != null ? jail.s : (T.s0 + T.s1) / 2;
                Walker(T, "lawman", "patrol", side, c - 10 - rnd.Next() * 4, c + 10 + rnd.Next() * 4);
            }
            var court = all.Find(l => l.name == "CourtHouse");
            if (court != null)
            {
                int nC = Mathf.Max(1, Mathf.RoundToInt((court.terminus ? 3 : 2) * busy));
                for (int i = 0; i < nC; i++)
                {
                    var p = NewPerson(T, i == 0 ? "lawman" : Anyone(), Tint());
                    if (p == null) break;
                    var pos = court.b.TransformPoint(new Vector3((i - 1) * 1.6f + (rnd.Next() - 0.5f) * 0.5f, 0, 1.0f + rnd.Next() * 0.8f));
                    route.RoadCoords(pos.x, pos.z, out float rd, out float rs);
                    if (rs < 0) { p.anim.Dispose(); Object.Destroy(p.root); continue; }
                    Add(new Folk { p = p, town = T, kind = "stand", s = rs, d = rd, baseYaw = court.yaw + (rnd.Next() - 0.5f) * 1.2f, phase = rnd.Next() });
                }
            }
            for (int i = 0; i < (sides.Count > 1 ? Mathf.RoundToInt(2 * busy) : 0); i++)
            {
                float s = 0;
                for (int k = 0; k < 6; k++) { s = T.s0 + 10 + rnd.Next() * (T.s1 - T.s0 - 20); bool clear = true; foreach (var bl in T.blocked) if (Mathf.Abs(bl - s) < 2.5f) clear = false; if (clear) break; }
                float side = rnd.Next() < 0.5f ? -1 : 1;
                float da = Lane(T, side, s) - 2.3f, db = Lane(T, -side, s) - 2.3f;
                var p = NewPerson(T, Anyone(), Tint());
                if (p == null) break;
                Add(new Folk { p = p, town = T, kind = "cross", s = s, d = da * side, from = da * side, to = -db * side, speed = 1.2f + rnd.Next() * 0.3f, wait = 2 + rnd.Next() * 8, phase = rnd.Next() });
            }
            foreach (var q in leans)
            {
                var p = NewPerson(T, rnd.Next() < 0.7f ? Pick(Men) : Pick(Women), Tint());
                if (p == null) break;
                Add(new Folk { p = p, town = T, kind = "lean", clip = "LeanRail", s = q.s, d = q.d, baseYaw = q.yaw, phase = rnd.Next() });
            }
            foreach (var (s, bd, l) in benches)
            {
                int ns = rnd.Next() < 0.5f * busy ? 2 : 1;
                for (int i = 0; i < ns; i++)
                {
                    var p = NewPerson(T, Anyone(), Tint());
                    if (p == null) break;
                    bool sit = p.Has("Sit");
                    Add(new Folk { p = p, town = T, kind = sit ? "sit" : "stand", clip = sit ? "Sit" : null, s = s + (ns > 1 ? (i - 0.5f) * 0.9f : (rnd.Next() - 0.5f) * 0.5f),
                        d = (sit ? bd + 0.22f : bd - 0.35f) * l.side, baseYaw = l.yaw + (sit ? 0 : (rnd.Next() - 0.5f) * 0.8f), lift = sit ? 0.46f : 0, phase = rnd.Next() });
                }
            }
            // conversation groups: mostly on the boardwalks, the bank, hotel and saloon busiest
            var weighted = new List<Towns.Lot>();
            foreach (var l in lots) if (l.P > 0) { weighted.Add(l); if (l.name == "Bank" || l.name == "Hotel" || l.name == "Saloon") weighted.Add(l); }
            while (T.left > 1 && weighted.Count > 0)
            {
                var l = weighted[Mathf.FloorToInt(rnd.Next() * weighted.Count) % weighted.Count];
                bool onDeck = rnd.Next() < 0.7f || used.Contains(l);
                float cs = l.s + (rnd.Next() - 0.5f) * (l.w - 2.5f), cd = onDeck ? l.off + l.P * 0.5f : l.off - 1.4f;
                int k = Mathf.Min(T.left, 2 + (rnd.Next() < 0.35f ? 1 : 0)); float a0 = rnd.Next() * Mathf.PI * 2;
                for (int i = 0; i < k; i++)
                {
                    float an = a0 + i / (float)k * Mathf.PI * 2, rr = onDeck ? Mathf.Min(0.5f, l.P * 0.3f) : 0.6f;
                    float s = cs + Mathf.Cos(an) * rr, d = (cd + Mathf.Sin(an) * rr) * l.side;
                    var q = XZ(s, d, out _); var c = XZ(cs, cd * l.side, out _);
                    var p = NewPerson(T, Anyone(), Tint());
                    if (p == null) break;
                    Add(new Folk { p = p, town = T, kind = "stand", clip = "Talk", s = s, d = d, baseYaw = Mathf.Atan2(c.x - q.x, c.z - q.z), phase = rnd.Next() });
                }
            }
        }

        void Walker(TownSet T, string variant, string kind, float side, float a, float b)
        {
            var p = NewPerson(T, variant, Pick(FolkTint));
            if (p == null) return;
            a = Mathf.Max(T.s0, a); b = Mathf.Min(T.s1, b);
            float s = a + rnd.Next() * (b - a), dj = (rnd.Next() - 0.5f) * 0.7f;
            Add(new Folk { p = p, town = T, kind = kind, side = side, a = a, b = b, s = s, dj = dj, d = (Lane(T, side, s) + dj) * side, dir = rnd.Next() < 0.5f ? 1 : -1,
                speed = (kind == "patrol" ? 1.0f : 1.2f) + rnd.Next() * 0.3f, wait = rnd.Next() * 3, phase = rnd.Next(), t = rnd.Next() * 10 });
        }

        void Tie(TownSet T, float s, Towns.Lot l, float rd)
        {
            var h = Characters.CreateHorse(Pick(Coats), true);
            h.root.transform.SetParent(root, false);
            float d = (rd - 1.15f) * l.side;
            var q = XZ(s, d, out _);
            h.root.transform.position = new Vector3(q.x, route.Height(q.x, q.z), q.z);
            h.root.transform.rotation = Quaternion.Euler(0, (l.yaw + Mathf.PI + (rnd.Next() - 0.5f) * 0.3f) * Mathf.Rad2Deg, 0);
            if (h.Has("Idle")) h.Play("Idle", 0, false, rnd.Next());
            T.blocked.Add(s); T.horses--;
            horses.Add(new TiedHorse { h = h, s = s });
        }

        // shots near the coach: everyone close by makes for a door
        void Alarm(float cs)
        {
            foreach (var n in folk)
            {
                if (n.kind == "flee" || n.kind == "gone" || Mathf.Abs(n.s - cs) > 90 || n.town == null) continue;
                float side = Mathf.Sign(n.d); if (side == 0) side = 1;
                Towns.Lot best = null;
                foreach (var l in n.town.Side(side)) if (best == null || Mathf.Abs(l.s - n.s) < Mathf.Abs(best.s - n.s)) best = l;
                if (best == null) continue;
                n.kind = "flee"; n.lift = 0; n.wait = 0.2f + Random.value * 0.9f;
                n.ts = best.s + (Random.value - 0.5f) * 1.2f;
                n.td = (best.off + (best.name == "Livery" ? 0.6f : best.P + 0.25f)) * side;
                n.speed = 3.3f + Random.value * 0.8f;
            }
            foreach (var h in horses)
                if (Mathf.Abs(h.s - cs) < 90 && h.h.Has("Rear") && Random.value < 0.4f) { h.h.Play("Rear", 0.15f, true); h.rearT = 1.6f; }
        }

        public void Update(float dt, Coach coach)
        {
            float cs = coach != null ? coach.s : 0;
            if (heard != seen) { seen = heard; if (coach != null) Alarm(cs); }
            bool Near(float s) => s > cs - 45 && s < cs + 160;
            foreach (var h in horses)
            {
                bool v = Near(h.s);
                if (h.h.root.activeSelf != v) h.h.root.SetActive(v);
                if (!v) continue;
                if (h.rearT > 0 && (h.rearT -= dt) <= 0) h.h.Play("Idle", 0.4f);
                h.h.Update(dt);
            }
            foreach (var n in folk)
            {
                bool v = n.kind != "gone" && Near(n.s);
                if (n.p.root.activeSelf != v) n.p.root.SetActive(v);
                if (!v) continue;
                n.t += dt;
                Think(n, dt, coach, cs, Mathf.Abs(n.s - cs));
                n.p.Update(dt);
            }
        }

        void Think(Folk n, float dt, Coach coach, float cs, float away)
        {
            n.moving = false;
            switch (n.kind)
            {
                case "walk": case "patrol":
                {
                    if (n.wait > 0) { n.wait -= dt; Idle(n); break; }
                    n.s += n.dir * n.speed * dt;
                    if ((n.dir > 0 && n.s > n.b) || (n.dir < 0 && n.s < n.a)) { n.dir = -n.dir; n.wait = 1 + Random.value * (n.kind == "patrol" ? 5 : 3); }
                    else if (Random.value < dt * 0.04f) n.wait = 2 + Random.value * 4;
                    n.d = (Lane(n.town, n.side, n.s) + n.dj) * n.side;
                    var f = route.Frame(n.s);
                    n.yaw = DampA(n.yaw, Mathf.Atan2(f.tx * n.dir, f.tz * n.dir), 8, dt);
                    n.moving = true; Move(n);
                    break;
                }
                case "cross":
                {
                    bool onRoad = Mathf.Abs(n.d) < 5.6f;
                    bool coming = cs < n.s + 6 && n.s - cs < 110;
                    if (onRoad && coming && n.s - cs < 40)
                    {
                        if (Mathf.Sign(n.from) == Mathf.Sign(n.d)) (n.from, n.to) = (n.to, n.from);
                        n.speed = 3.4f; n.wait = 0;
                    }
                    if (n.wait > 0) { n.wait -= dt; if (n.wait <= 0 && coming) n.wait = 1.5f; Idle(n); break; }
                    float dd = n.to - n.d;
                    if (Mathf.Abs(dd) < 0.05f) { (n.from, n.to) = (n.to, n.from); n.wait = 4 + Random.value * 10; n.speed = 1.2f + Random.value * 0.3f; break; }
                    n.d += Mathf.Sign(dd) * Mathf.Min(Mathf.Abs(dd), n.speed * dt);
                    var f = route.Frame(n.s); float sg = Mathf.Sign(dd);
                    n.yaw = DampA(n.yaw, Mathf.Atan2(f.rx * sg, f.rz * sg), 8, dt);
                    n.moving = true; Move(n);
                    break;
                }
                case "flee":
                {
                    if (n.wait > 0) { n.wait -= dt; break; }
                    float ds = n.ts - n.s, dd = n.td - n.d, L = Mathf.Sqrt(ds * ds + dd * dd);
                    if (L < 0.3f) { n.kind = "gone"; n.p.root.SetActive(false); return; }
                    float k = Mathf.Min(1, n.speed * dt / L);
                    n.s += ds * k; n.d += dd * k;
                    var f = route.Frame(n.s);
                    n.yaw = DampA(n.yaw, Mathf.Atan2(f.tx * ds + f.rx * dd, f.tz * ds + f.rz * dd), 10, dt);
                    n.moving = true; Move(n);
                    break;
                }
                default:
                    // standing, sitting, leaning: turn to watch the stage go past
                    if (n.kind != "sit" && away < 26 && coach != null)
                    {
                        var q = n.p.root.transform.position;
                        float want = Mathf.Atan2(coach.pos.x - q.x, coach.pos.z - q.z);
                        n.yaw = DampA(n.yaw, n.baseYaw + MathX.WrapAngle(want - n.baseYaw) * (n.kind == "lean" ? 0.6f : 1), 3, dt);
                    }
                    else n.yaw = DampA(n.yaw, n.baseYaw, 2, dt);
                    break;
            }
            Put(n, dt);
        }

        static float DampA(float a, float b, float k, float dt) => a + MathX.WrapAngle(b - a) * (1 - Mathf.Exp(-k * dt));

        public void Dispose()
        {
            if (Sound.I) Sound.I.onPlay -= OnSound;
            foreach (var n in folk) n.p.anim.Dispose();
            foreach (var h in horses) h.h.anim.Dispose();
        }
    }
}
