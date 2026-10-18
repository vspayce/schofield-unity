// Guns for hire: mounted escorts paid per ride. They live in road space (s, d) like the enemy riders,
// hold a slot beside the coach, peel out to fight and come back. Never in the enemies list, so the
// player's shots, aim assist, chevrons and Dead Eye all pass straight through them.
// Ported from game/escort.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class HireDef
    {
        public string id, name, tag, blurb, variant, fallback, breed, gun, snd, pic;
        public int price, guns = 1; public Color tint, coat; public Color? finish;
        public float pitch = 1, hp, range, react, interval0, interval1, pair, acc0, acc1, head, dmg, headMult, rest;
        public bool rifle; public Vector2? slot;
        public float[] stats; public static readonly string[] StatNames = { "Aim", "Speed", "Grit" };
    }

    public static class Hires
    {
        /// the escort's cut: his kills pay you this share of the bounty, he keeps the rest
        public const float Cut = 0.5f;
        /// a fresh rider is the player's for this long before a hired man may take him
        public const float PlayerPriority = 4;

        public static readonly Dictionary<string, HireDef> All = new Dictionary<string, HireDef>
        {
            { "bill", new HireDef { id = "bill", name = "Wild Bill Hickok", tag = "WILD BILL", price = 1500,
                blurb = "Late marshal of Abilene. Two ivory-handled 1851 Navies worn butt-forward, and he aims for the head. Rides your right flank. Paid up front, win or lose.",
                variant = "hickok", fallback = "drifter", tint = new Color(0.32f, 0.3f, 0.3f), breed = "thoroughbred", coat = new Color(0.42f, 0.36f, 0.34f),
                guns = 2, gun = "Schofield", finish = new Color(1.25f, 1.05f, 0.7f), snd = "schofield_shot", pitch = 1.05f,
                hp = 240, range = 50, react = 0.45f, interval0 = 1.5f, interval1 = 2.0f, pair = 0.24f, acc0 = 0.86f, acc1 = 0.5f, head = 0.65f,
                dmg = 60, headMult = 3, rest = 3.5f, slot = new Vector2(0, 1), pic = "navy", stats = new[] { 0.95f, 0.9f, 0.8f } } },
            { "rifle", new HireDef { id = "rifle", name = "Hired Rifle", tag = "HIRED GUN", price = 400,
                blurb = "A drover between herds with a Winchester and no questions. Steady at range, slow to work the lever. The first hand hired rides your left; a second takes the rear quarter.",
                variant = "lawman", fallback = "townsman", tint = new Color(1.05f, 0.92f, 0.75f), coat = new Color(0.95f, 0.7f, 0.5f),
                gun = "Winchester", snd = "schofield_shot", pitch = 0.8f, rifle = true,
                hp = 110, range = 85, react = 1.1f, interval0 = 2.7f, interval1 = 3.7f, acc0 = 0.55f, acc1 = 0.22f, head = 0.12f,
                dmg = 55, headMult = 2.5f, rest = 5, pic = "winchester", stats = new[] { 0.5f, 0.35f, 0.45f } } },
            { "pistol", new HireDef { id = "pistol", name = "Hired Pistol", tag = "HIRED GUN", price = 400,
                blurb = "A Texas gun hand who has seen the elephant once or twice. Quick enough up close; competent, not clever.",
                variant = "lawman", fallback = "drifter", tint = new Color(0.8f, 0.78f, 0.72f), coat = new Color(0.5f, 0.38f, 0.3f),
                gun = "Schofield", snd = "schofield_shot2", pitch = 0.95f,
                hp = 110, range = 40, react = 1.0f, interval0 = 2.1f, interval1 = 3.0f, acc0 = 0.5f, acc1 = 0.2f, head = 0.12f,
                dmg = 40, headMult = 2.5f, rest = 5, pic = "schofield", stats = new[] { 0.45f, 0.55f, 0.45f } } },
        };
        public static readonly string[] Order = { "bill", "rifle", "pistol" };
        public static int Fee(IEnumerable<string> ids) { int t = 0; foreach (var id in ids) if (All.TryGetValue(id, out var h)) t += h.price; return t; }
    }

    public class Escort
    {
        static readonly string[] BillLines = { "\"That's one.\"", "\"Next.\"", "\"Stay down, friend.\"", "\"Another for the undertaker.\"", "\"Too slow.\"", "\"Keep her rolling!\"", "\"He drew first.\"" };
        public readonly HireDef cfg;
        readonly Game g;
        readonly Horse horse; public readonly Person man;
        readonly List<Gun> guns = new List<Gun>();
        public float hp; public bool alive = true;
        readonly float homeDs, homeD;
        float wantDs, wantD, d, speed, side, heading, removeAt = float.PositiveInfinity;
        public float s;
        public string state = "ride";
        public Enemy target; string part = "body";
        float fireT, reactT, scanT, restT, blockT, losT, neighT; int shot; public int kills; bool clear;
        public Vector3 pos, chest, headP;
        Transform thrownO; Vector3 thrownVel; float thrownSeat, thrownAge; bool thrownLanded;

        public Escort(Game game, HireDef cfg, Vector2 slot)
        {
            g = game; this.cfg = cfg;
            var coach = game.coach;
            horse = Characters.CreateHorse(cfg.coat, true, false, cfg.breed);
            horse.root.transform.SetParent(game.world, false);
            bool has = Characters.HasLook(cfg.variant);
            man = Characters.CreateRider(has ? cfg.variant : cfg.fallback, has ? (Color?)null : cfg.tint);
            man.root.transform.SetParent(horse.mount, false);
            man.Play("Ride", 0);
            var hands = new[] { man.hand, man.handL };
            for (int i = 0; i < cfg.guns && hands[i]; i++)
            {
                var w = Characters.CreateWeapon(cfg.gun, cfg.finish);
                Characters.Hold(w, hands[i]);
                guns.Add(w);
            }
            hp = cfg.hp;
            homeDs = slot.x; homeD = slot.y * 3.6f;
            wantDs = homeDs; wantD = homeD;
            s = Mathf.Max(1, coach.s + homeDs); d = homeD; speed = coach.speed;
            side = Mathf.Sign(homeD); if (side == 0) side = 1;
            fireT = 1 + Random.value; scanT = Random.value * 0.25f; neighT = 10 + Random.value * 15;
            Update(0);
        }

        // how far off the crown he may ride here: tight on ledges, the trestle and in town
        float LaneLimit(float at)
        {
            var R = g.route;
            float m = 9;
            if (R.biome == "mountain") m = Route.RoadHalf + 0.5f;
            else if (R.biome == "gorge") m = Route.RoadHalf + 2;
            if (R.hasBridge && at > R.bridgeS0 - 14 && at < R.bridgeS1 + 14) m = Mathf.Min(m, Mathf.Min(3.45f, R.bridgeWidth * 0.5f) - 0.65f);
            var T = R.TownRange;
            if (at < 150 || at > R.len - 160 || (R.HasGhostTown && at > T.x - 30 && at < T.y + 30)) m = Mathf.Min(m, 4.8f);
            return m;
        }

        public void Update(float dt)
        {
            var R = g.route; var coach = g.coach;
            horse.Update(dt);
            if (state == "ride")
            {
                Think(dt);
                float err = coach.s + wantDs - s;
                float tSpeed = coach.speed + Mathf.Clamp(err * 0.7f, -7, 9);
                if (coach.speed < 0.5f && Mathf.Abs(err) < 2.5f) tSpeed = 0;
                speed = Mathf.Max(0, MathX.Damp(speed, tSpeed, 1.6f, dt));
                float prevD = d;
                float lim = LaneLimit(s + 4);
                float want = Mathf.Clamp(wantD, -lim, lim);
                d += Mathf.Clamp(want - d, -3.6f * dt, 3.6f * dt);
                d = Mathf.Clamp(d, -lim - 0.3f, lim + 0.3f);
                float ds = s - coach.s;
                float clr = Mathf.Min(lim, R.biome == "mountain" || lim < 4 ? 2.7f : 3.2f);
                if (ds > -4.5f && ds < coach.teamFront + 1.5f && Mathf.Abs(d) < clr) d = Mathf.Sign(d != 0 ? d : side) * clr;
                if (ds > -14 && ds < -4.5f && Mathf.Abs(d) < clr && Mathf.Abs(want) >= clr) d += Mathf.Sign(want) * 2 * dt;
                s = Mathf.Max(1, s + speed * dt);
                Place(dt, prevD);
                g.fx.Trail(pos, speed > 6 ? 7 : 0, dt, 0.7f);
                neighT -= dt;
                if (neighT < 0) { neighT = 14 + Random.value * 18; Sound.I.Play("horse_neigh", 0.4f, 1, pos); }
            }
            else if (state == "riderless")
            {
                speed = MathX.Damp(speed, 0, 0.35f, dt);
                float lim = LaneLimit(s);
                d = Mathf.Clamp(d + side * 2 * dt, -lim, lim);
                s += speed * dt;
                pos = R.WorldAt(s, d);
                horse.root.transform.position = pos;
                var f = R.Frame(s);
                horse.root.transform.rotation = Quaternion.Euler(0, (Mathf.Atan2(f.tx, f.tz) + side * 0.3f) * Mathf.Rad2Deg, 0);
                horse.SetSpeed(Mathf.Max(0.4f, Mathf.Min(1.2f, 0.5f + speed / 20)));
            }
            if (thrownO) Fall(dt);
            man.Update(dt);
            if (state == "ride") Pose();
        }

        void Place(float dt, float prevD)
        {
            var R = g.route;
            pos = R.WorldAt(s, d);
            var f = R.Frame(s);
            float lat = dt > 0 ? (d - prevD) / Mathf.Max(0.5f, speed * dt) : 0;
            heading = Mathf.Atan2(f.tx, f.tz) + Mathf.Atan(lat) * 0.8f;
            var ahead = R.WorldAt(s + 1.5f, d);
            horse.root.transform.position = pos;
            horse.root.transform.rotation = MathX.YXZ(-Mathf.Atan2(ahead.y - pos.y, 1.5f), heading, Mathf.Clamp(-lat * 2, -0.25f, 0.25f));
            string gait = speed < 2 ? "Idle" : speed < 10 ? "Canter" : "Gallop";
            if (horse.Has(gait) && horse.anim.current != gait) horse.Play(gait);
            horse.SetSpeed(gait == "Gallop" ? Mathf.Min(1.32f, 0.75f + speed / 30) : gait == "Canter" ? Mathf.Min(1.3f, Mathf.Max(0.5f, speed / 7)) : 1);
        }

        // ---------------------------------------------------------------- brains
        void Think(float dt)
        {
            var coach = g.coach; var E = g.escorts;
            fireT -= dt; reactT -= dt; restT -= dt; scanT -= dt;
            if (target != null && (!target.alive || (target is Rider tr && tr.state != "ride"))) target = null;
            if (scanT <= 0)
            {
                scanT = 0.25f;
                if (restT <= 0 && !g.over)
                {
                    var pick = Pick();
                    if (pick != null && pick != target) { target = pick; reactT = cfg.react * (0.8f + Random.value * 0.4f); }
                    else if (pick == null) target = null;
                }
                else target = null;
            }
            var tg = target;
            if (tg != null)
            {
                float ds = tg.s - coach.s;
                float tgD = tg is Rider r ? r.D : side * 6;
                float tSide = Mathf.Sign(tgD); if (tSide == 0) tSide = side;
                if (blockT > 1.2f) { wantDs = -11; wantD = side * 2.6f; }
                else if (tSide == side) { wantDs = Mathf.Clamp(ds - 3, -24, 6); wantD = side * Mathf.Clamp(Mathf.Abs(tgD) - 3, 3.4f, 8); }
                else { wantDs = Mathf.Clamp(ds - 4, -22, -6); wantD = side * 2.8f; }
            }
            else { wantDs = homeDs; wantD = homeD; }
            if (tg == null) { blockT = 0; return; }
            var aim = AimPoint(tg);
            var muzzle = Muzzle();
            losT -= dt;
            if (losT <= 0) { losT = 0.15f; clear = ClearShot(muzzle, aim); blockT = clear ? 0 : blockT + 0.15f; }
            if (!clear || reactT > 0 || fireT > 0) return;
            bool urgentTeam = tg is Rider rr && rr.AtTeam > 0.3f;
            if (g.player.hover.HasValue && g.player.hover.Value.enemy == tg && !urgentTeam) return;   // the boss has him in his sights
            var gun = guns.Count > 0 ? guns[shot % guns.Count] : null;
            var from = gun != null ? gun.muzzle.position : muzzle;
            if (!ClearShot(from, aim)) { clear = false; return; }
            float dist = Vector3.Distance(muzzle, aim);
            bool ahead = E.Ahead;
            bool pairShot = cfg.guns > 1 && shot % 2 == 0;
            fireT = pairShot ? cfg.pair : (cfg.interval0 + Random.value * (cfg.interval1 - cfg.interval0)) * (ahead ? 3 : 1);
            float chance = cfg.acc0 + (cfg.acc1 - cfg.acc0) * Mathf.Clamp01(dist / cfg.range);
            if (coach.speed > coach.cruise + 3) chance *= 0.85f;
            if (ahead) chance *= 0.5f;
            shot++;
            bool killed = g.combat.EscortShot(this, from, aim, tg, part, Random.value < chance);
            man.Kick();
            if (killed) { kills++; restT = cfg.rest * (ahead ? 1.6f : 1); target = null; fireT = Mathf.Max(fireT, cfg.react); }
        }

        // the best target he may take: a rider on the coach's flanks or tail, in range and in sight, that the
        // player has had a fair crack at first
        readonly Dictionary<Enemy, float> seen = new Dictionary<Enemy, float>();
        Enemy Pick()
        {
            var E = g.escorts;
            var mine = Muzzle();
            var playerOn = g.player.hover.HasValue ? g.player.hover.Value.enemy : null;
            Enemy best = null; float bestScore = float.PositiveInfinity;
            foreach (var e in g.enemies.list)
            {
                if (!e.alive || (e is Rider r0 && r0.state != "ride")) continue;
                if (!E.seen.TryGetValue(e, out float t0)) E.seen[e] = t0 = g.time;
                var c = e.spheres[1].c;
                float dist = Vector3.Distance(c, mine);
                if (dist > cfg.range) continue;
                bool urgent = (e is Rider r1 && r1.AtTeam > 0.3f) || dist < 6;
                if (!urgent && (g.time - t0 < Hires.PlayerPriority * (E.Ahead ? 2.5f : 1) || e == playerOn)) continue;
                if (e.type != "rider" && !cfg.rifle && dist > 30) continue;
                bool taken = false; foreach (var o in E.list) if (o != this && o.alive && o.target == e) taken = true;
                float score = dist + (taken ? 30 : 0) - (urgent ? 25 : 0) + (e.type == "rider" ? 0 : 10) - (e == target ? 6 : 0);
                if (score >= bestScore) continue;
                if (!Sees(mine, c)) continue;
                best = e; bestScore = score;
            }
            if (best != null && best != target)
            {
                bool hasHead = false; foreach (var q in best.spheres) if (q.part == "head") hasHead = true;
                part = hasHead && Random.value < cfg.head ? "head" : "body";
            }
            return best;
        }

        Vector3 AimPoint(Enemy e) { foreach (var q in e.spheres) if (q.part == part) return q.c; return e.spheres.Count > 1 ? e.spheres[1].c : e.spheres[0].c; }
        Vector3 Muzzle() => guns.Count > 0 ? guns[0].muzzle.position : chest;

        bool Sees(Vector3 from, Vector3 to)
        {
            var dv = to - from; float L = dv.magnitude; if (L < 0.01f) return true; dv /= L;
            if (g.terrain.Raycast(from, dv, L) < L - 1.5f) return false;
            var ray = new Ray(from, dv);
            foreach (var b in g.towns.solids) { if (b.Contains(from)) continue; if (b.IntersectRay(ray, out float bt) && bt < L - 1) return false; }
            return true;
        }

        // Hard rule: no shot whose line comes near the coach, the team, the player or another escort — checked
        // a few metres past the target too, since a miss carries on
        bool ClearShot(Vector3 from, Vector3 to)
        {
            if (!Sees(from, to)) return false;
            var c = g.coach;
            var end = to + (to - from).normalized * 8;
            bool Near(Vector3 p, float r)
            {
                var ab = end - from; float t = Mathf.Clamp01(Vector3.Dot(p - from, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                return (from + ab * t - p).sqrMagnitude < r * r;
            }
            foreach (float k in new[] { -2.6f, -0.8f, 1.0f, 2.6f }) { var p = c.pos + c.fwd * k; p.y = c.pos.y + 1.7f; if (Near(p, 2.1f)) return false; }
            for (int i = 0; i < c.TeamCount; i++) { var t = c.TeamRoot(i); if (t && Near(t.position + Vector3.up * 1.3f, 1.5f)) return false; }
            if (Near(g.player.HeadPos, 1.4f)) return false;
            foreach (var o in g.escorts.list) if (o != this && o.alive && Near(o.chest, 1.3f)) return false;
            return true;
        }

        void Pose()
        {
            var tg = target;
            bool two = guns.Count > 1;
            string clip = "Ride"; var hand = man.hand;
            if (tg != null)
            {
                clip = "RideAim";
                if (two)
                {
                    float lx = horse.root.transform.InverseTransformPoint(AimPoint(tg)).normalized.x;
                    // (Unity's local X is the mirror of three.js's: the web game's +x is our -x)
                    if (lx < -0.55f && man.Has("RideAimL") && man.handL) { clip = "RideAimL"; hand = man.handL; }
                    else if (man.Has("RideAimDual")) clip = "RideAimDual";
                }
            }
            else if (two && man.Has("RideHolstered")) clip = "RideHolstered";
            if (man.Has(clip) && man.anim.current != clip) man.Play(clip, 0.3f);
            if (two)
            {
                bool drawn = clip != "RideHolstered";
                foreach (var gn in guns) gn.root.SetActive(drawn);
                var sash = Assets.Find(man.root.transform, "Colts_Hickok"); if (sash) sash.gameObject.SetActive(!drawn);
            }
            aimHand = hand;
        }
        Transform aimHand;

        public void LateUpdate()
        {
            if (state == "ride" && target != null && man.chest && aimHand)
                Characters.AimBone(man.chest, aimHand, (AimPoint(target) - aimHand.position).normalized, 0.9f);
            headP = man.head ? man.head.position : man.root.transform.TransformPoint(0, 1.0f, 0);
            chest = man.chest ? man.chest.position : man.root.transform.TransformPoint(0, 0.5f, 0);
            if (thrownO) { chest = thrownO.position; headP = chest; }
        }

        // ---------------------------------------------------------------- hits
        public void Damage(float amount, Vector3 from)
        {
            if (!alive || g.over) return;
            hp -= amount;
            if (hp > 0) return;
            alive = false; state = "riderless"; target = null;
            var dir = chest - from; dir.y = 0; dir.Normalize();
            var vel = MathX.FromHeading(heading) * speed * 0.8f + dir * 3 + Vector3.up * 2.5f;
            var o = man.root.transform;
            var wp = o.position;
            o.SetParent(g.world, true);
            man.Play(man.Has("FallOff") ? "FallOff" : "Ride", 0.08f, true);
            o.rotation = Quaternion.Euler(0, heading * Mathf.Rad2Deg, 0);
            thrownO = o; thrownVel = vel; thrownSeat = wp.y - g.route.Height(wp.x, wp.z); thrownAge = 0; thrownLanded = false;
            Sound.I.Play("horse_neigh", 0.8f, 1.1f, pos);
            if (cfg.id == "bill") g.hud.Banner("Wild Bill is down!", "You're on your own on the right", 2.2f);
            else g.hud.Feed("Your hired gun is down", false);
            removeAt = g.time + 25;
        }

        void Fall(float dt)
        {
            thrownVel *= Mathf.Exp(-2.2f * dt);
            var p = thrownO.position; p.x += thrownVel.x * dt; p.z += thrownVel.z * dt; p.y = g.route.Height(p.x, p.z) + thrownSeat; thrownO.position = p;
            if (!thrownLanded && thrownAge > 0.9f) { thrownLanded = true; g.fx.Dust(p - new Vector3(0, thrownSeat, 0), 8, 0.6f); }
            thrownAge += dt;
        }

        public bool Gone => !alive && (g.time > removeAt || s < g.coach.s - 160);

        public void Credit(int bounty, bool head)
        {
            int pay = Mathf.RoundToInt(bounty * Hires.Cut);
            g.escorts.kills++; g.escorts.bounty += pay;
            string line = cfg.id == "bill" ? $"{BillLines[Random.Range(0, BillLines.Length)]} Wild Bill" : head ? "Hired gun, headshot" : "Hired gun";
            g.AddBounty(pay, $"{line} +${pay}", false);
        }

        public void Dispose()
        {
            man.anim.Dispose(); horse.anim.Dispose();
            if (horse.root) Object.Destroy(horse.root);
            if (thrownO) Object.Destroy(thrownO.gameObject);
        }
    }

    public class Escorts
    {
        readonly Game g;
        public readonly List<Escort> list = new List<Escort>();
        public readonly Dictionary<Enemy, float> seen = new Dictionary<Enemy, float>();
        public int kills, bounty;

        public Escorts(Game game, string[] ids)
        {
            g = game;
            // Bill always takes the right; the first hired gun the left, the second the rear quarter
            int hired = 0;
            foreach (var id in ids)
            {
                if (!Hires.All.TryGetValue(id, out var cfg)) continue;
                var slot = cfg.id == "bill" ? cfg.slot.Value : hired++ == 0 ? new Vector2(1, -1) : new Vector2(-9, -0.75f);
                list.Add(new Escort(game, cfg, slot));
            }
            if (list.Count > 0) g.hud.Feed(list.Count > 1 ? $"{list.Count} guns ride with you" : $"{list[0].cfg.name} rides with you", false);
        }

        /// they aren't here to win the fight for you: once they out-score the player they slow right down
        public bool Ahead => kills > g.player.stats.kills * 0.8f + 2;

        public void Update(float dt)
        {
            foreach (var e in list) e.Update(dt);
            for (int i = list.Count - 1; i >= 0; i--) if (list[i].Gone) { list[i].Dispose(); list.RemoveAt(i); }
        }

        public void LateUpdate() { foreach (var e in list) e.LateUpdate(); }

        /// an enemy about to fire may pick one of us instead of the coach: only an escort nearer to him than the
        /// player, and not every time
        public Escort PickTarget(Vector3 muzzle, bool rifle)
        {
            Escort best = null; float bestD = rifle ? 160 : 45;
            float toPlayer = Vector3.Distance(muzzle, g.player.HeadPos);
            foreach (var e in list)
            {
                if (!e.alive) continue;
                float dd = Vector3.Distance(muzzle, e.chest);
                if (dd < bestD && dd < toPlayer + 4) { best = e; bestD = dd; }
            }
            return best != null && Random.value < (best.cfg.id == "bill" ? 0.35f : 0.3f) ? best : null;
        }

        public void Dispose() { foreach (var e in list) e.Dispose(); list.Clear(); }
    }
}
