// Enemies: mounted riders (chase / flank / ambush, ride alongside and shoot, go for the team) and
// ridge riflemen (lens-glint telegraph). Riders live in road space (s, d) so they follow any winding
// road. Ported from game/enemies.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class HitSphere { public string part; public Vector3 c; public float r; }

    public abstract class Enemy
    {
        protected readonly Game g;
        public string type; public bool alive = true, escaped, los;
        public float removeAt = float.PositiveInfinity, s, glintT = -1, fireT, hp = 100;
        public readonly List<HitSphere> spheres = new List<HitSphere>();
        public Person man;
        protected Enemy(Game game) { g = game; }
        public Vector3 HitPoint(string part) { foreach (var q in spheres) if (q.part == part) return q.c; return spheres[0].c; }
        public abstract void Update(float dt);
        public abstract void LateUpdate();
        public abstract bool Damage(float amount, string part, Vector3 dir);
        public abstract void Dispose();
        public virtual bool IsRidingRider => false;
        public virtual bool Rifle => false;
        public int slot;
    }

    // -------------------------------------------------------------------- rider
    public class Rider : Enemy
    {
        static readonly Color[] Coats = { new Color(1, 1, 1), new Color(0.5f, 0.38f, 0.3f), new Color(0.3f, 0.25f, 0.22f), new Color(1.3f, 1.2f, 1.1f), new Color(0.95f, 0.7f, 0.5f) };
        public static readonly Color[] Cloth = { new Color(1, 1, 1), new Color(0.8f, 0.7f, 0.6f), new Color(0.6f, 0.65f, 0.75f), new Color(1.1f, 0.9f, 0.7f) };
        readonly Horse horse; readonly Gun gun;
        float horseHp = 110, side, maxD, targetDs, targetD, d, speed, aggro, atTeam, reposT, neighT, life, heading;
        bool warned;
        public string state = "ride";
        public Vector3 pos;
        class Thrown { public Transform o; public Vector3 vel, spin; public bool landed, animated; public float seatH, age; }
        Thrown thrown;
        public override bool IsRidingRider => alive && state == "ride";

        public Rider(Game game, string from, int slot, float sideIn) : base(game)
        {
            type = "rider"; this.slot = slot;
            var R = game.route; var coach = game.coach;
            horse = Characters.CreateHorse(MathX.Pick(Coats), true);
            horse.root.transform.SetParent(game.world, false);
            man = Characters.CreateRider(MathX.Pick(R.biome == "mountain" ? Characters.RiderLooksMountain : Characters.RiderLooks), MathX.Pick(Cloth));
            man.root.transform.SetParent(horse.mount, false);
            man.Play("Ride", 0);
            gun = Characters.CreateWeapon("Schofield");
            Characters.Hold(gun, man.hand);
            side = sideIn != 0 ? sideIn : (Random.value < 0.5f ? -1 : 1);
            bool narrow = R.biome == "mountain" || R.biome == "gorge";
            maxD = narrow ? Route.RoadHalf + 0.5f : 14;
            switch (from)
            {
                case "ahead": s = coach.s + 110 + Random.value * 60; d = side * (narrow ? 3 : 9 + Random.value * 6); speed = coach.speed * 0.4f; break;
                case "flank": s = coach.s - 10 - Random.value * 25; d = side * (narrow ? 4 : 30 + Random.value * 15); speed = coach.speed + 2; break;
                default: s = coach.s - 55 - Random.value * 40; d = side * (2 + Random.value * 4); speed = coach.speed + 4; break;
            }
            float[] slots = { -5, -1, 3, -9, 6, -12 };
            targetDs = narrow ? -7 - slot * 4 : slots[slot % 6] + (Random.value - 0.5f) * 2;
            targetD = side * (narrow ? (targetDs > -4 ? 4.2f : 2.2f) : 5.5f + Random.value * 3);
            fireT = 2 + Random.value * 2;
            reposT = 6 + Random.value * 6;
            neighT = Random.value * 10;
            spheres.Add(new HitSphere { part = "head", r = 0.17f }); spheres.Add(new HitSphere { part = "body", r = 0.34f });
            spheres.Add(new HitSphere { part = "horse", r = 0.75f }); spheres.Add(new HitSphere { part = "horse", r = 0.4f });
            Update(0);
        }

        public override void Update(float dt)
        {
            var R = g.route; var coach = g.coach;
            horse.Update(dt);
            if (state == "ride" || state == "fleeing")
            {
                if (state == "ride")
                {
                    reposT -= dt;
                    if (reposT < 0)
                    {
                        reposT = 5 + Random.value * 6;
                        bool narrow = R.biome == "mountain" || R.biome == "gorge";
                        if (!narrow) { targetDs = -12 + Random.value * 18; targetD = side * (5 + Random.value * 4); }
                        if (!narrow && Random.value < 0.2f) { side *= -1; targetD *= -1; }
                    }
                    float err = coach.s + targetDs - s;
                    speed = MathX.Damp(speed, coach.speed + Mathf.Clamp(err * 0.6f, -6, 9), 1.2f, dt);
                }
                else { speed = MathX.Damp(speed, 6, 0.5f, dt); targetD = side * 30; }
                float prevD = d;
                const float maxLat = 3.2f;
                d += Mathf.Clamp(targetD - d, -maxLat * dt, maxLat * dt);
                if (state == "ride") d = Mathf.Clamp(d, -maxD, maxD);
                if (R.OnBridge(s) || R.OnBridge(s + 25)) d = Mathf.Clamp(d, -3.4f, 3.4f);   // single file over the trestle
                float ds = s - coach.s;
                float clear = R.biome == "mountain" || R.biome == "gorge" ? 2.6f : 3.2f;
                if (ds > -4 && ds < coach.teamFront + 1.5f && Mathf.Abs(d) < clear) d = Mathf.Sign(d != 0 ? d : side) * clear;
                if (ds > -14 && ds < -4 && Mathf.Abs(d) < clear && Mathf.Abs(targetD) >= clear) d += Mathf.Sign(targetD) * 2 * dt;
                s += speed * dt;
                pos = R.WorldAt(s, d);
                var f = R.Frame(s);
                float lat = dt > 0 ? (d - prevD) / Mathf.Max(0.5f, speed * dt) : 0;
                heading = Mathf.Atan2(f.tx, f.tz) + Mathf.Atan(lat) * 0.8f;
                var ahead = R.WorldAt(s + 1.5f, d);
                horse.root.transform.position = pos;
                horse.root.transform.rotation = MathX.YXZ(-Mathf.Atan2(ahead.y - pos.y, 1.5f), heading, Mathf.Clamp(-lat * 2, -0.25f, 0.25f));
                horse.SetSpeed(Mathf.Min(1.32f, 0.75f + speed / 30));
                g.fx.Trail(pos, speed > 6 ? 7 : 0, dt, 0.7f);
                if (state == "ride") Fight(dt);
                if (state == "fleeing" && (Mathf.Abs(d) > 28 || s < coach.s - 120)) removeAt = 0;
            }
            else if (state == "riderless")
            {
                speed = MathX.Damp(speed, 5, 0.4f, dt);
                d += side * 3 * dt; s += speed * dt;
                if (R.OnBridge(s)) d = Mathf.Clamp(d, -3.4f, 3.4f);
                pos = R.WorldAt(s, d);
                horse.root.transform.position = pos;
                var f = R.Frame(s);
                horse.root.transform.rotation = Quaternion.Euler(0, (Mathf.Atan2(f.tx, f.tz) + side * 0.5f) * Mathf.Rad2Deg, 0);
            }
            else if (state == "horsedown")
            {
                speed = MathX.Damp(speed, 0, 2.5f, dt);
                s += speed * dt;
                pos = R.WorldAt(s, d);
                horse.root.transform.position = pos;
            }
            if (thrown != null)
            {
                var t = thrown;
                if (t.animated)
                {
                    t.vel *= Mathf.Exp(-2.2f * dt);
                    var p = t.o.position; p.x += t.vel.x * dt; p.z += t.vel.z * dt; p.y = R.Height(p.x, p.z) + t.seatH; t.o.position = p;
                    if (!t.landed && t.age > 0.9f) { t.landed = true; g.fx.Dust(p - new Vector3(0, t.seatH, 0), 8, 0.6f); }
                    t.age += dt;
                }
                else if (!t.landed)
                {
                    t.vel.y -= 16 * dt;
                    t.o.position += t.vel * dt;
                    t.o.rotation *= Quaternion.Euler(t.spin * dt * Mathf.Rad2Deg);
                    float gh = R.Height(t.o.position.x, t.o.position.z);
                    if (t.o.position.y < gh + 0.15f)
                    {
                        t.o.position = new Vector3(t.o.position.x, gh + 0.15f, t.o.position.z);
                        if (Mathf.Abs(t.vel.y) > 3) { t.vel.y *= -0.25f; t.vel.x *= 0.4f; t.vel.z *= 0.4f; t.spin *= 0.3f; g.fx.Dust(t.o.position, 6, 0.5f); }
                        else t.landed = true;
                    }
                }
            }
            man.Update(dt);
            if (thrown == null && state == "ride")
            {
                bool wantAim = aggro > 0.2f && s - coach.s > -40;
                string clip = wantAim ? "RideAim" : "Ride";
                if (man.Has(clip)) man.Play(clip, 0.3f);
            }
            if (!alive && float.IsPositiveInfinity(removeAt) && s < coach.s - 140) removeAt = 0;
        }

        public override void LateUpdate()
        {
            if (thrown == null && state == "ride" && aggro > 0.2f && man.chest)
                Characters.AimBone(man.chest, man.hand, g.player.HeadPos - man.hand.position, 0.9f);
            var head = spheres[0]; var bodyS = spheres[1]; var hs = spheres[2]; var neck = spheres[3];
            head.c = man.head ? man.head.position + new Vector3(0, 0.06f, 0) : man.root.transform.TransformPoint(0, 1.0f, 0);
            bodyS.c = man.chest ? man.chest.position : man.root.transform.TransformPoint(0, 0.5f, 0);
            var ht = horse.root.transform;
            hs.c = ht.TransformPoint(0, 1.25f, 0);
            neck.c = ht.TransformPoint(0, 1.6f, 1.05f);
            if (state == "horsedown") { hs.c.y -= 0.7f; neck.c.y -= 1; }
        }

        void Fight(float dt)
        {
            var coach = g.coach;
            float ds = s - coach.s;
            float dist = Vector3.Distance(pos, coach.pos);
            aggro = Mathf.Clamp01(aggro + dt * (dist < 45 ? 0.6f : -0.3f));
            fireT -= dt;
            if (fireT <= 0 && dist < 42 && aggro > 0.5f)
            {
                fireT = (1.5f + Random.value * 1.8f) * g.diff.fireMult;
                g.combat.EnemyShot(this, gun.muzzle.position, dist, g.diff.riderAcc, 7 + Random.value * 4, 4 + Random.value * 3);
                man.Kick();
            }
            if (ds > 4 && ds < coach.teamFront + 1 && Mathf.Abs(d) < 5)
            {
                atTeam += dt;
                if (atTeam > 1 && !warned) { warned = true; g.hud.Banner("They're going for the team!", "", 1.5f); }
                if (atTeam > 5) { atTeam = 0; warned = false; g.DamageCoach(12); g.OnCoachDamage(); targetDs = -6; }
            }
            else atTeam = Mathf.Max(0, atTeam - dt);
            life += dt;
            if (life > 45 && aggro > 0.5f && Random.value < dt * 0.1f) { state = "fleeing"; side = Mathf.Sign(d != 0 ? d : 1); }
            neighT -= dt;
            if (neighT < 0) { neighT = 12 + Random.value * 15; Sound.I.Play("horse_neigh", 0.5f, 1, pos); }
        }

        public void Flee() { if (alive && state == "ride") { state = "fleeing"; side = Mathf.Sign(d != 0 ? d : 1); } }

        public override bool Damage(float amount, string part, Vector3 dir)
        {
            if (!alive) return false;
            if (part == "horse")
            {
                horseHp -= amount;
                if (horseHp <= 0)
                {
                    state = "horsedown";
                    horse.Play("Fall", 0.1f, true);
                    Sound.I.Play("horse_neigh", 0.9f, 0.9f, pos);
                    g.fx.Dust(pos, 16, 1.1f, null, 2.5f, 4);
                    Throw(MathX.FromHeading(heading) * speed * 0.9f + new Vector3(0, 4, 0));
                    return true;
                }
                return false;
            }
            hp -= amount;
            if (hp <= 0)
            {
                state = "riderless";
                var push = dir * (g.player.weapon.knock ? 7 : 3);
                Throw(MathX.FromHeading(heading) * speed * 0.8f + push + new Vector3(0, 2.5f, 0));
                return true;
            }
            return false;
        }

        void Throw(Vector3 vel)
        {
            alive = false;
            var o = man.root.transform;
            var wp = o.position; var wq = o.rotation;
            o.SetParent(g.world, true);
            o.position = wp; o.rotation = wq;
            bool animated = man.Has("FallOff");
            man.Play(animated ? "FallOff" : "Ride", 0.08f, true);
            float seatH = wp.y - g.route.Height(wp.x, wp.z);
            if (animated) o.rotation = Quaternion.Euler(0, heading * Mathf.Rad2Deg, 0);
            thrown = new Thrown { o = o, vel = vel, spin = new Vector3(-3 - Random.value * 3, 0, (Random.value - 0.5f) * 4), animated = animated, seatH = seatH };
        }

        public override void Dispose()
        {
            horse.anim.Dispose(); man.anim.Dispose();
            Object.Destroy(horse.root);
            if (man.root) Object.Destroy(man.root);
        }
    }

    // ------------------------------------------------------------ ridge rifleman
    public class Gunman : Enemy
    {
        readonly Gun gun; readonly bool rifle; readonly Transform root; readonly string variant;
        float yaw, losT, pop = 1; readonly bool popup; readonly Vector3 basePos;
        public override bool Rifle => rifle;

        public Gunman(Game game, Vector3 p, Vector3 face, bool rifle = true, string variant = null, string gunName = null, bool popup = false) : base(game)
        {
            this.popup = popup; basePos = p;
            if (popup) { pop = 0; p += Vector3.down * 1.2f; }      // ghost-town gunmen rise up into windows and over parapets
            type = rifle ? "rifleman" : "gunman";
            this.rifle = rifle;
            this.variant = variant ?? MathX.Pick(Characters.GunmanLooks);
            man = Characters.CreateRider(this.variant, this.variant == "bart" ? (Color?)null : MathX.Pick(Rider.Cloth));
            root = man.root.transform;
            root.SetParent(game.world, false);
            root.position = p;
            yaw = Mathf.Atan2(face.x, face.z);
            root.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            gun = Characters.CreateWeapon(gunName ?? (rifle ? "Winchester" : "Schofield"));
            Characters.Hold(gun, man.hand);
            man.Play(man.Has("StandShoot") ? "StandShoot" : "StandIdle", 0);
            fireT = (rifle ? 3 : 2) + Random.value * 2;
            spheres.Add(new HitSphere { part = "head", r = 0.17f, c = p + Vector3.up * 1.65f });
            spheres.Add(new HitSphere { part = "body", r = 0.36f, c = p + Vector3.up * 1.2f });
        }

        public override void Update(float dt)
        {
            var coach = g.coach;
            man.Update(dt);
            if (!alive) return;
            var dir = coach.pos - root.position;
            yaw = Mathf.LerpAngle(yaw * Mathf.Rad2Deg, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 1 - Mathf.Exp(-4 * dt)) * Mathf.Deg2Rad;
            root.rotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            if (popup && pop < 1) { pop = Mathf.Min(1, pop + dt * 2.5f); root.position = basePos + Vector3.up * (pop - 1) * 1.2f; }
            float dist = dir.magnitude;
            float range = rifle ? 190 : 70;
            losT -= dt;
            if (losT <= 0 && dist < range)
            {
                losT = 0.25f;
                var from = spheres[1].c; var to = g.player.HeadPos;
                var dv = to - from; float L = dv.magnitude; dv /= L;
                bool clear = g.terrain.Raycast(from, dv, L - 2) >= L - 2.5f;
                if (clear)
                {
                    var ray = new Ray(from, dv);
                    foreach (var b in g.towns.solids) { if (b.Contains(from)) continue; if (b.IntersectRay(ray, out float bt) && bt < L - 2) { clear = false; break; } }
                }
                los = clear;
            }
            if (dist < range && los)
            {
                fireT -= dt;
                if (fireT < 1.5f && fireT + dt >= 1.5f && rifle) glintT = 1.5f;
                if (!rifle && fireT < 0.6f && fireT + dt >= 0.6f) { glintT = 0.6f; Sound.I.Play("schofield_cock", 0.9f, 1, root.position); }
                if (glintT > 0)
                {
                    glintT -= dt;
                    if (Mathf.FloorToInt(glintT * 16) % 2 == 0) g.fx.Glint(spheres[0].c + new Vector3(0, -0.05f, 0), 1.5f + dist / 70);
                }
                if (fireT <= 0)
                {
                    fireT = (rifle ? 2.9f : 2.2f) * g.diff.fireMult + Random.value * 1.3f;
                    g.combat.EnemyShot(this, gun.muzzle.position, dist, rifle ? g.diff.rifleAcc : g.diff.riderAcc, rifle ? 20 : 9, rifle ? 11 : 5, rifle);
                }
            }
            if (coach.s - s > 70) { removeAt = g.time + 1; alive = false; escaped = true; }
        }

        public override void LateUpdate()
        {
            if (alive && man.chest) Characters.AimBone(man.chest, man.hand, g.player.HeadPos - man.hand.position, 0.85f);
            spheres[0].c = man.head ? man.head.position + new Vector3(0, 0.06f, 0) : root.TransformPoint(0, 1.65f, 0);
            spheres[1].c = man.chest ? man.chest.position : root.TransformPoint(0, 1.2f, 0);
        }

        public override bool Damage(float amount, string part, Vector3 dir)
        {
            if (!alive) return false;
            hp -= amount;
            if (hp > 0) return false;
            alive = false;
            if (variant == "bart") g.AddBounty(150, "BLACK BART +$150", false);
            man.Play(man.Has("DieStanding") ? "DieStanding" : "FallOff", 0.08f, true);
            removeAt = g.time + 8;
            return true;
        }

        public override void Dispose() { man.anim.Dispose(); if (root) Object.Destroy(root.gameObject); }
    }

    // ------------------------------------------------------------------ manager
    public struct RayHitInfo { public Enemy enemy; public string part; public float t; public Vector3 point; }
    public struct PickInfo { public Enemy enemy; public string part; public Vector3 aimPoint; public float dist; public HitSphere sphere; }

    public class Enemies
    {
        readonly Game g;
        public readonly List<Enemy> list = new List<Enemy>();
        readonly List<WaveDef> waves = new List<WaveDef>();
        readonly List<(float t, bool rider, System.Action fn)> spawnQ = new List<(float, bool, System.Action)>();
        int riderCap = 5;
        readonly HashSet<Vector3> usedSpawns = new HashSet<Vector3>();
        bool engaged, bartSeen;

        public Enemies(Game game)
        {
            g = game;
            foreach (var w in game.route.def.waves) waves.Add(new WaveDef(w.at, w.type, w.count, w.from));
        }

        public int AliveCount { get { int n = 0; foreach (var e in list) if (e.alive) n++; return n; } }

        public void Update(float dt)
        {
            float prog = g.coach.s / g.route.len;
            foreach (var w in waves) if (!w.done && prog >= w.at) { w.done = true; SpawnWave(w); }
            int riders = 0; foreach (var e in list) if (e.IsRidingRider) riders++;
            for (int i = spawnQ.Count - 1; i >= 0; i--)
            {
                var q = spawnQ[i]; q.t -= dt;
                if (q.t <= 0)
                {
                    if (q.rider && riders >= riderCap) { q.t = 1; spawnQ[i] = q; continue; }
                    spawnQ.RemoveAt(i); q.fn(); continue;
                }
                spawnQ[i] = q;
            }
            bool busy = AliveCount > 0 || spawnQ.Count > 0;
            if (engaged && !busy && !g.over) { g.coach.hp = Mathf.Min(100, g.coach.hp + 10); g.hud.Feed("Coach patched up +10", false); }
            engaged = busy;
            for (int i = 0; i < list.Count; i++) list[i].Update(dt);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (g.time >= e.removeAt || e.removeAt == 0) { e.Dispose(); list.RemoveAt(i); }
            }
        }

        public void LateUpdate() { foreach (var e in list) e.LateUpdate(); }

        void SpawnWave(WaveDef w)
        {
            int n = Mathf.RoundToInt(w.count * g.diff.countMult);
            if (w.type == "riders")
            {
                g.hud.Banner(w.from == "ahead" ? "Riders up ahead!" : w.from == "flank" ? "Riders on the flank!" : "Riders behind!", "", 1.3f);
                var used = new HashSet<int>(); foreach (var e in list) if (e.type == "rider" && e.alive) used.Add(e.slot);
                int slot = 0;
                for (int i = 0; i < n; i++)
                {
                    while (used.Contains(slot)) slot++;
                    int sl = slot++; float side = i % 2 == 0 ? 1 : -1; string from = w.from;
                    spawnQ.Add((i * 0.9f, true, () => list.Add(new Rider(g, from, sl, side))));
                }
            }
            else if (w.type == "town")
            {
                // gunmen in the ghost town's windows and on its roofs, each waiting for the coach to come near
                g.hud.Banner(g.route.def.name, "Nobody lives here anymore", 3);
                var free = new List<(Vector3 pos, Vector3 face, float s, bool roof)>();
                foreach (var sp in g.towns.spawns) if (!usedSpawns.Contains(sp.pos)) free.Add(sp);
                for (int i = free.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (free[i], free[j]) = (free[j], free[i]); }
                for (int i = 0; i < Mathf.Min(n, free.Count); i++)
                {
                    var sp = free[i];
                    usedSpawns.Add(sp.pos);
                    void Watch()
                    {
                        if (g.over) return;
                        if (g.coach.s > sp.s - 75) { var e = new Gunman(g, sp.pos, sp.face, Random.value < 0.3f, null, null, true) { s = sp.s }; list.Add(e); }
                        else spawnQ.Add((0.25f, false, new System.Action(Watch)));
                    }
                    spawnQ.Add((0f, false, new System.Action(Watch)));
                }
            }
            else if (w.type == "ridge")
            {
                g.hud.Banner("Riflemen on the ridge!", "Watch for the glint", 1.5f);
                for (int i = 0; i < n; i++) { int k = i; spawnQ.Add((i * 1.2f, false, () => SpawnRidge(k))); }
            }
        }

        (Vector3 p, float s)? RidgeSpot(int i, int tries = 40)
        {
            var R = g.route; var coach = g.coach;
            (Vector3, float)? best = null; float bestScore = float.NegativeInfinity;
            for (int k = 0; k < tries; k++)
            {
                float s = coach.s + 120 + Random.value * 110 + i * 25;
                if (s > R.len - 60) continue;
                float side = Random.value < 0.5f ? -1 : 1;
                float d = side * (22 + Random.value * 48);
                var p = R.WorldAt(s, d);
                float rise = p.y - R.RoadHeightAt(s);
                if (rise < 7 || rise > 45) continue;
                float hx = R.Height(p.x + 2, p.z) - p.y, hz = R.Height(p.x, p.z + 2) - p.y;
                float slope = Mathf.Sqrt(hx * hx + hz * hz) / 2;
                if (slope > 0.9f) continue;
                var chest = p + Vector3.up * 1.2f;
                bool clear = true;
                foreach (float ahead in new[] { -60f, -20f })
                {
                    var road = R.WorldAt(s + ahead, 0) + Vector3.up * 2.5f;
                    var dv = road - chest; float L = dv.magnitude; dv /= L;
                    if (g.terrain.Raycast(chest, dv, L) < L - 3) { clear = false; break; }
                }
                if (!clear) continue;
                float score = Mathf.Min(rise, 26) * 0.8f - slope * 25 - Mathf.Abs(Mathf.Abs(d) - 38) * 0.25f;
                if (score > bestScore) { bestScore = score; best = (p, s); }
            }
            return best;
        }

        void SpawnRidge(int i)
        {
            var best = RidgeSpot(i) ?? RidgeSpot(i + 4);
            if (!best.HasValue) return;
            var (p, s) = best.Value;
            var face = g.coach.pos - p; face.y = 0; face.Normalize();
            bool bart = !bartSeen && Random.value < 0.12f;
            Gunman e;
            if (bart) { bartSeen = true; g.hud.Banner("Black Bart!", "The PO8 wants the box", 2.4f); e = new Gunman(g, p, face, false, "bart", "CoachGun"); }
            else e = new Gunman(g, p, face, true);
            e.los = true; e.s = s;
            list.Add(e);
        }

        public void ArriveAll() { foreach (var e in list) if (e is Rider r) r.Flee(); }

        /// closest enemy part to the aim ray within an angular tolerance (radians)
        public PickInfo? Pick(Vector3 origin, Vector3 dir, float tol, bool noHead = false)
        {
            PickInfo? best = null; float bestA = tol;
            foreach (var e in list)
            {
                if (!e.alive) continue;
                foreach (var sp in e.spheres)
                {
                    if (noHead && sp.part == "head") continue;
                    var to = sp.c - origin; float dist = to.magnitude;
                    if (dist > 260 || dist < 0.01f) continue;
                    float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(to, dir) / dist, -1, 1)) - Mathf.Atan(sp.r / dist);
                    float a = ang - (sp.part == "head" ? 0.004f : sp.part == "body" ? 0.002f : 0);
                    if (a < bestA) { bestA = a; best = new PickInfo { enemy = e, part = sp.part, aimPoint = sp.c, dist = dist, sphere = sp }; }
                }
            }
            return best;
        }

        /// exact ray hit, or (with tol) the assisted aim point
        public RayHitInfo? Raycast(Vector3 origin, Vector3 dir, float range, float tol = 0, float maxMiss = 1)
        {
            var hit = RayHit(origin, dir, range);
            if (hit.HasValue) return hit;
            if (tol > 0)
            {
                var p = Pick(origin, dir, tol, true);
                if (p.HasValue && p.Value.dist < range)
                {
                    var sp = p.Value.sphere;
                    float t = Vector3.Dot(sp.c - origin, dir);
                    var onRay = origin + dir * t;
                    float miss = Vector3.Distance(onRay, sp.c) - sp.r;
                    if (miss > maxMiss) return null;
                    var pt = sp.c + (onRay - sp.c).normalized * sp.r * 0.7f;
                    return new RayHitInfo { point = pt, enemy = p.Value.enemy, part = p.Value.part, t = p.Value.dist };
                }
            }
            return null;
        }

        public RayHitInfo? RayHit(Vector3 origin, Vector3 dir, float range)
        {
            RayHitInfo? best = null; float bestT = range;
            foreach (var e in list)
            {
                if (!e.alive) continue;
                foreach (var sp in e.spheres)
                {
                    var oc = origin - sp.c;
                    float b = Vector3.Dot(oc, dir), c = oc.sqrMagnitude - sp.r * sp.r;
                    float h = b * b - c;
                    if (h < 0) continue;
                    float t = -b - Mathf.Sqrt(h);
                    float bias = sp.part == "horse" ? 0.3f : 0;
                    if (t > 0 && t + bias < bestT) { bestT = t + bias; best = new RayHitInfo { enemy = e, part = sp.part, t = t, point = origin + dir * t }; }
                }
            }
            return best;
        }

        public void Dispose() { foreach (var e in list) e.Dispose(); list.Clear(); }
    }
}
