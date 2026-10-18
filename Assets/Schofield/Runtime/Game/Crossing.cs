// The train at the crossing: the whistle, the gunmen riding the boxcar roofs, and what happens to a coach
// that tries to beat it. Timing works like the buffalo herd: the train is dispatched when the coach is
// `lead` metres out and paced so its middle is on the crossing just as a coach holding cruise would get
// there. After the warning its speed is fixed, so hauling on the reins lets it by.
// Ported from game/crossing.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class Crossing
    {
        const float Lane = 1.3f, Half = 1.5f;
        readonly Game g; readonly Railroad rail; readonly RailDef def;
        public readonly Train train;
        float lead, baseSpeed, bellT, hitCd, b0, b1, u0, u1;
        string state = "idle";
        bool hit, held;
        readonly List<Gunman> gunmen = new List<Gunman>();
        readonly List<(float t, string name)> whistles = new List<(float, string)>();

        public Crossing(Game game, Railroad rail, RailDef def)
        {
            g = game; this.rail = rail; this.def = def;
            Synth.MakeTrainSounds();
            train = new Train(game.world, rail, def.cars, game.route.def.light);
            lead = 330; baseSpeed = def.speed;
            train.onChuff = side => Snd("train_chuff", train.LocoPos, 0.55f, 26, 0.92f + Random.value * 0.1f);
            Band();
        }

        // where the train's envelope crosses the coach's lane, in road stations and in rail u
        void Band()
        {
            var R = rail; var f = g.route.Frame(R.s);
            float st = R.side.x * f.tx + R.side.z * f.tz, sr = R.side.x * f.rx + R.side.z * f.rz;
            float dr = Mathf.Abs(R.dr) > 0.2f ? R.dr : Mathf.Sign(R.dr != 0 ? R.dr : 1) * 0.2f;
            float s0 = float.MaxValue, s1 = float.MinValue; u0 = float.MaxValue; u1 = float.MinValue;
            foreach (float d in new[] { -Lane, Lane })
                foreach (float v in new[] { -Half, Half })
                {
                    float u = (d - v * sr) / dr, s = u * R.dt + v * st;
                    s0 = Mathf.Min(s0, s); s1 = Mathf.Max(s1, s); u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                }
            b0 = R.s + s0; b1 = R.s + s1;
        }

        float TeamFront => Mathf.Max(4, g.coach.teamFront) + 0.1f;
        float Eta(float speed) => Mathf.Max(0.5f, (b0 - (g.coach.s + TeamFront)) / Mathf.Max(4, speed));
        Vector2 LaneX() { float a = train.sdir * u0, b = train.sdir * u1; return new Vector2(Mathf.Min(a, b), Mathf.Max(a, b)); }
        bool Occupied { get { if (!train.active) return false; var sp = train.Span(); var l = LaneX(); return sp.y > l.x && sp.x < l.y; } }

        public void Update(float dt)
        {
            var c = g.coach;
            if (state == "idle" && c.s >= rail.s - lead) { if (c.s < b0 - 60) Dispatch(); else state = "gone"; }
            if (state == "coming") Pace(dt);
            if (state == "coming" && Eta(c.cruise) < 10.5f) Warn();
            train.Update(dt, g.cam.transform.position, g.cam);
            if (!train.active) { if (state != "idle") c.limitS = null; return; }
            Sounds(dt); Rumble(); Block(); Collide(dt);
            var sp = train.Span();
            if (sp.x > rail.reach - 20 || train.x > rail.reach + train.length)
            {
                train.SetActive(false);
                Sound.I.StopLoop("train_clatter", 1.5f);
                foreach (var e in gunmen) if (e.alive) { e.alive = false; e.escaped = true; e.removeAt = g.time; }
                c.limitS = null;
                state = "gone";
            }
        }

        void Dispatch()
        {
            var T = train;
            T.sdir = Random.value < 0.5f ? 1 : -1;
            float L = T.length, eta = Eta(g.coach.cruise), V = baseSpeed;
            var lx = LaneX(); float mid = (lx.x + lx.y) / 2;
            float x = mid - V * eta + L / 2, minX = -(rail.reach - 12) + L;
            if (x < minX) { x = minX; V = Mathf.Clamp((mid + L / 2 - x) / eta, 6, 18); }
            T.x = x; T.speed = V; T.odo = 0;
            T.SetActive(true); T.Place();
            state = "coming";
            SpawnGunmen();
            Sound.I.Loop("train_clatter", 0);
            Whistle(new[] { "long" }, 0.6f);
        }

        void Pace(float dt)
        {
            var c = g.coach;
            float eta = Eta(Mathf.Max(c.cruise, c.speed));
            var l = LaneX(); float midNow = train.x - train.length / 2;
            float want = Mathf.Clamp(((l.x + l.y) / 2 - midNow) / eta, 6, 18);
            train.speed = MathX.Damp(train.speed, want, 0.6f, dt);
        }

        void Warn()
        {
            state = "warned";
            g.hud.Banner("Train coming!", "Rein in and let it pass", 2.6f);
            Whistle(new[] { "long", "long", "short", "long" }, 0);
            bellT = 0.01f;
        }

        void SpawnGunmen()
        {
            var slots = new List<(int ci, float z, int k)>();
            for (int ci = 0; ci < train.cars.Count; ci++) for (int k = 0; k < train.cars[ci].slots.Length; k++) slots.Add((ci, train.cars[ci].slots[k], k));
            slots.Sort((a, b) => a.k != b.k ? a.k.CompareTo(b.k) : a.ci.CompareTo(b.ci));
            int n = Mathf.Min(def.gunmen, slots.Count);
            var face = g.coach.pos - train.LocoPos; face.y = 0; face.Normalize();
            for (int i = 0; i < n; i++)
            {
                var (ci, z, _) = slots[i];
                int cci = ci; float zz = z;
                var e = new Gunman(g, train.RoofPoint(ci, z), face, i % 2 == 0) { s = rail.s, carrier = () => train.RoofPoint(cci, zz) };
                e.fireT += 1 + Random.value * 2;
                g.enemies.list.Add(e);
                gunmen.Add(e);
            }
        }

        // hold a slow coach at the edge while the train is across (or about to be); a fast one gets no help
        void Block()
        {
            var c = g.coach; var l = LaneX();
            bool soon = train.x < l.x && (l.x - train.x) < train.speed * 1.6f;
            float front = c.s + TeamFront;
            bool hold = (Occupied || soon) && front <= b0 + 0.05f && (c.speed < 4.5f || hit);
            if (hold)
            {
                c.limitS = b0 - TeamFront - 0.35f;
                if (!held && c.speed > 0.5f) { held = true; Sound.I.Play("horse_neigh", 0.6f, 0.95f); }
            }
            else { c.limitS = null; if (!Occupied) { held = false; hit = false; } }
        }

        void Collide(float dt)
        {
            var c = g.coach;
            hitCd = Mathf.Max(0, hitCd - dt);
            if (g.over || hitCd > 0 || !Occupied) return;
            float back = c.s - 2.7f, front = c.s + TeamFront;
            if (front < b0 || back > b1) return;
            float v = Mathf.Max(0, c.speed);
            float dmg = Mathf.Clamp(14 + v * 2.4f, 16, 55);
            g.DamageCoach(dmg);
            c.speed = 0;
            float mid = (back + front) / 2;
            bool across = mid > (b0 + b1) / 2;
            c.s = across ? b1 + 2.9f : b0 - TeamFront - 0.6f;
            c.limitS = across ? (float?)null : c.s;
            hit = true; hitCd = 3;
            g.shake += 1.3f; g.HitStop(0.16f);
            var at = rail.At(0) + Vector3.up * 1.2f;
            g.fx.Dust(at, 34, 1.5f, null, 3.2f, 7);
            for (int i = 0; i < 4; i++) g.fx.Splinters(at + new Vector3((Random.value - 0.5f) * 2, Random.value * 1.5f, (Random.value - 0.5f) * 2), Vector3.up);
            Sound.I.Play("hit_wood_1", 1, 0.55f); Sound.I.Play("hit_wood_2", 1, 0.7f, null, 0.06f); Sound.I.Play("horse_neigh", 1, 0.8f, null, 0.1f);
            Snd("train_whistle_short", at, 1.2f, 60);
            g.hud.Banner("Hit by the train!", $"Coach -{Mathf.RoundToInt(dmg)}", 2.2f);
            g.player.Damage(8 + v * 0.7f, at);
            g.OnCoachDamage();
        }

        void Snd(string name, Vector3 pos, float vol, float falloff = 30, float pitch = 1)
        {
            float d = Vector3.Distance(pos, g.cam.transform.position);
            float k = 1 / (1 + Mathf.Pow(d / falloff, 1.3f));
            if (k * vol < 0.01f) return;
            Sound.I.Play(name, Mathf.Min(1, vol * k), pitch);
        }

        void Whistle(string[] pattern, float delay)
        {
            float t = delay;
            foreach (var p in pattern) { whistles.Add((t, p == "long" ? "train_whistle" : "train_whistle_short")); t += p == "long" ? 2.35f : 0.95f; }
        }

        void Sounds(float dt)
        {
            var loco = train.LocoPos;
            for (int i = whistles.Count - 1; i >= 0; i--)
            {
                var w = whistles[i]; w.t -= dt;
                if (w.t <= 0) { whistles.RemoveAt(i); Snd(w.name, loco, 1.6f, 70); } else whistles[i] = w;
            }
            if (bellT > 0)
            {
                bellT -= dt;
                if (bellT <= 0)
                {
                    bellT = 0.8f; train.bellSwing = 1;
                    Snd("train_bell", loco, 0.5f, 35);
                    if (train.x > LaneX().y + 30) bellT = 0;
                }
            }
        }

        void Rumble()
        {
            float d = train.DistanceTo(g.cam.transform.position);
            Sound.I.LoopVolume("train_clatter", Mathf.Min(1, 1.4f / (1 + Mathf.Pow(d / 22, 1.4f))), 0.2f);
            Sound.I.LoopRate("train_clatter", Mathf.Clamp(train.speed / 11, 0.7f, 1.4f));
            float k = Mathf.Clamp01(1 - d / 40);
            g.shake = Mathf.Max(g.shake, 0.16f * k * k * Mathf.Min(1.3f, train.speed / 11));
        }

        public void Dispose() { Sound.I.StopLoop("train_clatter", 0.3f); train.Dispose(); }
    }
}
