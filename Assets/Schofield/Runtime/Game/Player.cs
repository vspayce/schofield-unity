// The shotgun messenger: a chase camera behind the coach that orbits with the aim, a sidearm and a
// long gun, a scope that looks from the guard's own eyes, hit-scan shooting with aim assist, recoil,
// reloads, health regen and Dead Eye target painting. Ported from game/player.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class Player
    {
        public class Stats { public int shots, hits, kills, headshots; }
        readonly Game g;
        readonly Camera cam;
        public float yaw, pitch = -0.06f;       // relative to the coach heading; + yaw = right
        float recoil, recoilV;
        public float hp = 100, lastHit = -10;
        public float deadeye = 0.35f; public bool deadeyeOn, executing;
        public readonly List<(Enemy enemy, string part)> marks = new List<(Enemy, string)>();
        public readonly WeaponDef[] loadout;
        public readonly Dictionary<string, int> ammo = new Dictionary<string, int>();
        public WeaponDef weapon;
        public bool scoped; public float scopeT;
        float cool, swapT, holsterT, settle, dryT;
        public float reloading;
        readonly List<(float t, System.Action fn)> timers = new List<(float, System.Action)>();
        public readonly Stats stats = new Stats();
        public readonly Person model;
        readonly Dictionary<string, Gun> guns = new Dictionary<string, Gun>();
        public Vector3 camPos, aimDir = Vector3.forward;
        float fov = 60;
        public PickInfo? hover;

        public Player(Game game, string side = "schofield", string longGun = "shotgun", bool startLong = false)
        {
            g = game; cam = game.cam;
            loadout = new[] { Weapons.All.TryGetValue(side, out var a) ? a : Weapons.All["schofield"], Weapons.All.TryGetValue(longGun, out var b) ? b : Weapons.All["shotgun"] };
            foreach (var W in loadout) ammo[W.id] = W.mag;
            weapon = startLong ? loadout[1] : loadout[0];
            model = Characters.CreateRider("player");
            var seat = game.coach.seatGuard ? game.coach.seatGuard : game.coach.body;
            model.root.transform.SetParent(seat, false);
            model.Play(model.Has("SeatAim") ? "SeatAim" : model.Has("RideAim") ? "RideAim" : "Ride", 0);
            foreach (var W in loadout)
            {
                var w = Characters.CreateWeapon(W.model, W.finish);
                Characters.Hold(w, model.hand);
                w.root.SetActive(false);
                guns[W.id] = w;
            }
            ShowGun();
            g.hud.SetScope(false);
        }

        void ShowGun()
        {
            foreach (var kv in guns) kv.Value.root.SetActive(kv.Key == weapon.id);
            g.hud.SetWeapon(weapon, ammo[weapon.id]);
        }

        public bool CanScope => weapon.zoom > 1;
        public float Zoom => CanScope ? 1 + (weapon.zoom - 1) * scopeT : 1;

        public void SetScope(bool on)
        {
            if (on && !CanScope) return;
            if (on == scoped) return;
            scoped = on;
            Sound.I.Play("ui_click", 0.4f, on ? 0.7f : 0.55f);
            g.hud.SetScope(on);
        }

        public Vector3 HeadPos
        {
            get
            {
                var seat = g.coach.seatGuard ? g.coach.seatGuard : g.coach.body;
                return seat.position + new Vector3(0, 0.95f, 0);
            }
        }

        bool AssistOn => g.input.touch || g.input.pad;

        public void Update(float dt, float rdt, Frame inp)
        {
            if (inp.scope) SetScope(!scoped);
            scopeT = MathX.Damp(scopeT, scoped ? 1 : 0, 10, rdt);
            float sens = (deadeyeOn ? 0.75f : 1) / Zoom;
            float dx = inp.dx * sens, dy = inp.dy * sens;
            if (AssistOn && hover.HasValue) { dx *= 0.55f; dy *= 0.55f; }
            yaw += dx; pitch = Mathf.Clamp(pitch - dy, -1.05f, 0.5f);
            settle = Mathf.Abs(inp.dx) + Mathf.Abs(inp.dy) < 0.0025f ? settle + rdt : 0;
            if (AssistOn && hover.HasValue && Mathf.Abs(inp.dx) + Mathf.Abs(inp.dy) > 0)
            {
                Angles(hover.Value.aimPoint, out float wy, out float wp);
                yaw += MathX.WrapAngle(wy - yaw) * Mathf.Min(1, rdt * 3.5f);
                pitch += (wp - pitch) * Mathf.Min(1, rdt * 3.5f);
            }
            recoilV += (-recoil * 140 - recoilV * 16) * rdt;
            recoil += recoilV * rdt;

            // ---------------------------------------------------------- weapons
            var W = weapon;
            cool -= dt; swapT -= dt;
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                var t = timers[i]; t.t -= rdt;
                if (t.t <= 0) { timers.RemoveAt(i); t.fn(); } else timers[i] = t;
            }
            var other = loadout[0] == W ? loadout[1] : loadout[0];
            if (ammo[other.id] < other.mag) { holsterT += dt; if (holsterT > 2.5f) { ammo[other.id] = other.mag; holsterT = 0; } }
            else holsterT = 0;
            if (reloading > 0)
            {
                reloading -= dt;
                if (reloading <= 0) { ammo[W.id] = W.mag; g.hud.SetAmmo(W, ammo[W.id]); }
            }
            if (inp.swap && swapT <= 0 && !executing)
            {
                weapon = other; reloading = 0; cool = 0.3f; swapT = 0.35f;
                if (!CanScope) SetScope(false);
                Sound.I.Play("schofield_cock", 0.6f);
                ShowGun();
                W = weapon;
            }
            if (inp.reload && reloading <= 0 && ammo[W.id] < W.mag) Reload();
            if (inp.dynamite) g.dynamite?.Throw();

            if (inp.deadeye)
            {
                if (deadeyeOn) EndDeadeye();
                else if (deadeye >= 0.2f) StartDeadeye();
            }
            if (deadeyeOn)
            {
                deadeye -= rdt * 0.2f;
                if (hover.HasValue && marks.Count < ammo[W.id])
                {
                    var h = hover.Value;
                    if (h.part != "horse" && !marks.Exists(m => m.enemy == h.enemy))
                    {
                        marks.Add((h.enemy, h.part));
                        Sound.I.Play("ui_click", 0.5f, 1.4f);
                    }
                }
                if (deadeye <= 0) EndDeadeye();
            }

            if (!executing)
            {
                bool wants = W.auto ? inp.firePressed || inp.fire : inp.firePressed;
                if (wants && cool <= 0 && reloading <= 0)
                {
                    if (deadeyeOn && marks.Count > 0) EndDeadeye();
                    else if (ammo[W.id] > 0) Fire(null);
                    else { Sound.I.Play("schofield_cock", 0.5f, 1.6f); Reload(); }
                }
                else if (inp.firePressed && reloading > 0 && g.time - dryT > 0.25f)
                {
                    dryT = g.time;
                    Sound.I.Play("schofield_cock", 0.32f, 1.9f);
                    g.hud.NudgeReload();
                }
            }
            if (g.time - lastHit > 4 && hp < 100) hp = Mathf.Min(100, hp + rdt * 9);
        }

        /// after animation: camera, body turned to the aim, the gun's barrel onto the crosshair, targeting
        public void LateUpdate(float rdt)
        {
            var coach = g.coach;
            float heading = MathX.Heading(coach.fwd);
            float yawW = heading + yaw, pitchW = pitch + recoil;
            aimDir = new Vector3(Mathf.Sin(yawW) * Mathf.Cos(pitchW), Mathf.Sin(pitchW), Mathf.Cos(yawW) * Mathf.Cos(pitchW));

            // body follows the aim: SeatAim points the gun ~38° right of the body, so turn him left by that
            var root = model.root.transform;
            root.localRotation = Quaternion.Euler(0, (yaw - 0.67f) * Mathf.Rad2Deg, 0);
            bool showBody = scopeT < 0.5f;
            foreach (var r in model.root.GetComponentsInChildren<Renderer>()) r.forceRenderingOff = !showBody;
            foreach (var r in guns[weapon.id].root.GetComponentsInChildren<Renderer>()) r.forceRenderingOff = !showBody;

            // chase camera: dead behind the coach on its centreline, orbiting with the aim
            var head = HeadPos;
            var pivot = head - coach.right * Vector3.Dot(head - coach.pos, coach.right);
            var target = pivot - aimDir * 9 + new Vector3(0, 1.0f + Mathf.Max(0, -pitch) * 1.2f, 0);
            if (scopeT > 0.001f) target = Vector3.Lerp(target, head + new Vector3(0, 0.45f, 0), scopeT);
            foreach (var r in coach.driver.root.GetComponentsInChildren<Renderer>()) r.forceRenderingOff = scopeT >= 0.5f;
            float sh = (coach.shake + g.shake) * (1 - 0.8f * scopeT);
            target.x += (Random.value - 0.5f) * sh; target.y += (Random.value - 0.5f) * sh;
            float gh = g.route.Height(target.x, target.z) + 0.6f;
            if (target.y < gh) target.y = gh;
            camPos = target;
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);
            float wantFov = (deadeyeOn ? 52 : 60) / Zoom;
            fov = scopeT > 0.001f ? wantFov : MathX.Damp(fov, wantFov, 6, rdt);
            cam.fieldOfView = fov;

            if (model.chest)
            {
                var hand = model.hand.position;
                Characters.AimBone(model.chest, model.hand, camPos + aimDir * 40 - hand, 1);
            }

            float tol = (g.input.touch ? 0.1f : g.input.pad ? 0.08f : 0.05f) / Zoom;
            hover = g.enemies.Pick(camPos, aimDir, tol);
            if (hover.HasValue && hover.Value.dist > weapon.range) hover = null;
            g.hud.CrosshairEnemy(hover.HasValue);
        }

        void Angles(Vector3 p, out float y, out float pt)
        {
            float heading = MathX.Heading(g.coach.fwd);
            var d = (p - camPos).normalized;
            y = Mathf.Atan2(d.x, d.z) - heading;
            pt = Mathf.Asin(Mathf.Clamp(d.y, -1, 1));
        }

        void Reload()
        {
            var W = weapon;
            if (reloading > 0) return;
            // (the web game reloads at once; `reload` times are kept for the HUD ring and the gunsmith)
            ammo[W.id] = W.mag;
            reloading = 0;
            Sound.I.Play(W.reloadSnd, 0.9f);
            g.hud.SetAmmo(W, ammo[W.id]);
        }

        Vector3 Muzzle => guns[weapon.id].muzzle.position;

        void Fire(Vector3? forced)
        {
            var W = weapon;
            ammo[W.id]--;
            cool = W.interval;
            stats.shots += W.pellets;
            var muzzle = Muzzle;
            Vector3 aimPoint;
            if (forced.HasValue) aimPoint = forced.Value;
            else if (hover.HasValue && hover.Value.dist < W.range) aimPoint = hover.Value.aimPoint;
            else
            {
                // assist only nudges near-misses onto the body/horse; headshots must be earned
                float tol = (AssistOn ? 0.05f : 0.025f) / Zoom;
                var hit = g.enemies.Raycast(camPos, aimDir, W.range, tol, AssistOn ? 1.1f : 0.8f);
                var animal = !hit.HasValue ? g.wildlife?.Pick(camPos, aimDir, tol) : null;
                aimPoint = hit.HasValue ? hit.Value.point : animal.HasValue && animal.Value.dist < W.range ? animal.Value.aim : camPos + aimDir * 120;
            }
            var baseDir = (aimPoint - camPos).normalized;
            bool anyHit = false, killed = false, head = false; int pelletHits = 0;
            float steady = (W.kind == "revolver" && settle > 0.3f ? 0.3f : 1) * (scoped ? 0.6f : 1);
            for (int p = 0; p < W.pellets; p++)
            {
                var dir = baseDir;
                if (!forced.HasValue)
                {
                    float s = W.spread * steady * (hover.HasValue ? 0 : 1);
                    dir += new Vector3((Random.value - 0.5f) * 2 * s, (Random.value - 0.5f) * 2 * s, (Random.value - 0.5f) * 2 * s);
                    dir.Normalize();
                }
                var res = g.combat.PlayerShot(camPos, dir, W, muzzle, forced.HasValue);
                if (res.hit) { anyHit = true; pelletHits++; }
                killed |= res.killed; head |= res.head;
            }
            stats.hits += pelletHits;
            g.hud.Hitmarker(anyHit, killed, head);
            if (anyHit)
            {
                Sound.I.Play("ui_click", head ? 0.9f : 0.6f, head ? 2.2f : 0.8f);
                if (killed) g.HitStop(0.06f);
            }
            g.fx.Muzzle(muzzle, baseDir, W.kind == "shotgun" || W.kind == "rifle", true);
            Sound.I.Play(W.snd[Random.Range(0, W.snd.Length)], W.kind == "revolver" ? 0.9f : 1, W.pitch);
            if (W.kind == "revolver" && W.interval > 0.2f) Sound.I.Play("schofield_cock", 0.25f, 1, null, 0.14f);
            guns[W.id].Spin(1.05f);
            recoilV += W.kick * 60;
            g.shake += W.kind == "shotgun" ? 0.05f : W.kind == "rifle" ? 0.035f : 0.02f;
            model.Kick();
            g.hud.SetAmmo(W, ammo[W.id]);
            if (ammo[W.id] <= 0) Reload();
        }

        void StartDeadeye()
        {
            deadeyeOn = true; marks.Clear();
            SetScope(false);
            Sound.I.Play("deadeye_in", 0.9f);
            Sound.I.Loop("heartbeat_loop", 0.55f);
            g.SetTimeScale(0.3f);
        }

        public void EndDeadeye()
        {
            if (!deadeyeOn) return;
            deadeyeOn = false;
            deadeye = Mathf.Max(0, deadeye);
            Sound.I.StopLoop("heartbeat_loop", 0.3f);
            var live = marks.FindAll(m => m.enemy.alive);
            marks.Clear();
            if (live.Count == 0) { g.SetTimeScale(1); Sound.I.Play("deadeye_out", 0.7f); return; }
            // execute: rapid guaranteed shots, still in slow motion
            executing = true;
            int i = 0;
            System.Action next = null;
            next = () =>
            {
                if (g.over) return;
                if (i >= live.Count || ammo[weapon.id] <= 0) { executing = false; g.SetTimeScale(1); Sound.I.Play("deadeye_out", 0.7f); return; }
                var m = live[i++];
                if (m.enemy.alive) Fire(m.enemy.HitPoint(m.part));
                timers.Add((0.17f, next));
            };
            next();
        }

        public void AddDeadeye(float v) { if (!deadeyeOn) deadeye = Mathf.Min(1, deadeye + v); }

        public void Damage(float amount, Vector3 from)
        {
            if (g.over || g.godMode) return;
            hp -= amount; lastHit = g.time;
            bool heavy = amount >= 15;
            g.hud.DamageFrom(from, camPos, aimDir);
            g.hud.PulseHealth(heavy);
            g.shake += heavy ? 0.14f : 0.06f;
            if (heavy) g.HitStop(0.05f);
            Sound.I.Play("hit_flesh_" + (1 + Random.Range(0, 2)), heavy ? 1 : 0.8f, heavy ? 0.82f : 1);
            if (heavy) Sound.I.Play("hit_wood_1", 0.5f, 0.7f);
            if (hp <= 0) g.Fail("You were shot dead.");
        }

        public void Dispose() { model.anim.Dispose(); }
    }
}
