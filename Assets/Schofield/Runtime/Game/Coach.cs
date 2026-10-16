// The stagecoach: follows the route, sways on its thoroughbraces, spins its wheels and drags a
// six-horse team; the driver up front, the player on the roof. Ported from game/coach.js (the full
// harness from hitch.js is simplified to the pole and the six lines).
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class Coach
    {
        // Clydesdale wheelers on the pole, a Cleveland Bay swing pair, Thoroughbred leaders: [breed, x, z past the pole tip]
        static readonly (string breed, float x, float z)[] TeamDef =
        {
            ("clydesdale", 0.58f, 0.30f), ("clydesdale", -0.58f, 0.30f),
            ("clevelandbay", 0.55f, 3.95f), ("clevelandbay", -0.55f, 3.95f),
            ("thoroughbred", 0.52f, 7.60f), ("thoroughbred", -0.52f, 7.60f),
        };

        class TeamHorse { public Horse h; public float offX, offZ, phase; }
        class Wheel { public Transform t; public float r; public Quaternion baseQ; }

        readonly Route route;
        public readonly GameObject root;
        readonly GameObject model;
        public readonly Transform body, seatGuard, seatDriver, hitch;
        readonly Vector3 bodyBase; readonly Quaternion bodyQ;
        readonly List<Wheel> wheels = new List<Wheel>();
        readonly List<TeamHorse> team = new List<TeamHorse>();
        public readonly Person driver;
        readonly LineRenderer[] lines = new LineRenderer[6];
        readonly LineRenderer pole;
        public float teamFront;

        public float s, speed, cruise = 15, stamina = 1, hp = 100, shake;
        public float? limitS;
        public bool stopping;
        float reinHold; bool reinStopPending, reinStopped;
        float roll, rollV, pitchS, pitchV, bounce, bounceV, wheelAngle;
        public Vector3 pos, fwd = Vector3.forward, right = Vector3.right, vel;

        public Coach(Route route, Transform parent, string modelName = "stagecoach_treasure")
        {
            this.route = route;
            root = new GameObject("Coach");
            root.transform.SetParent(parent, false);
            var prefab = Assets.Model(modelName) ?? Assets.Model("stagecoach");
            if (prefab)
            {
                model = Object.Instantiate(prefab, root.transform);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(model.GetComponent<Collider>());
                model.transform.SetParent(root.transform, false); model.transform.localScale = new Vector3(1.5f, 2, 3.5f); model.transform.localPosition = new Vector3(0, 1.6f, 0);
            }
            var t = model.transform;
            body = Assets.Find(t, "Body") ?? t;
            foreach (var n in new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" })
            {
                var w = Assets.Find(t, n); if (!w) continue;
                float r = 0.5f;
                var rs = w.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0) { var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds); r = Mathf.Max(0.3f, b.size.y / 2); }
                wheels.Add(new Wheel { t = w, r = r, baseQ = w.localRotation });
            }
            seatGuard = Assets.Find(t, "Seat_Guard"); seatDriver = Assets.Find(t, "Seat_Driver"); hitch = Assets.Find(t, "Hitch");
            bodyBase = body.localPosition; bodyQ = body.localRotation;

            float hitchZ = hitch ? root.transform.InverseTransformPoint(hitch.position).z : 4.2f;
            for (int i = 0; i < TeamDef.Length; i++)
            {
                var d = TeamDef[i];
                var h = Characters.CreateHorse(Color.white, false, true, d.breed);
                h.root.transform.SetParent(parent, false);
                team.Add(new TeamHorse { h = h, offX = d.x, offZ = hitchZ + d.z, phase = i * 0.13f });
            }
            teamFront = 0; foreach (var th in team) teamFront = Mathf.Max(teamFront, th.offZ); teamFront += 1.4f;

            driver = Characters.CreateRider("driver");
            driver.root.transform.SetParent(seatDriver ? seatDriver : body, false);
            driver.Play(driver.Has("Drive") ? "Drive" : "Ride", 0);

            // the lines from the driver's hands to each pair, and the pole to the wheelers
            var leather = new Material(Shader.Find("Universal Render Pipeline/Unlit")); leather.SetColor("_BaseColor", new Color(0.09f, 0.06f, 0.04f));
            for (int i = 0; i < 6; i++) lines[i] = Line("Line", leather, 0.018f, parent);
            pole = Line("Pole", leather, 0.07f, parent);
        }

        static LineRenderer Line(string n, Material m, float w, Transform parent)
        {
            var go = new GameObject(n); go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = m; lr.widthMultiplier = w; lr.positionCount = 2; lr.numCapVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            return lr;
        }

        public void Update(float dt, Frame ctl, bool control)
        {
            var R = route;
            float target = cruise;
            bool whip = control && ctl.whip, brake = control && ctl.brake;
            if (whip && stamina > 0.02f) { target = cruise + 11; stamina = Mathf.Max(0, stamina - dt * 0.22f); }
            else stamina = Mathf.Min(1, stamina + dt * 0.07f);
            if (brake)
            {
                reinHold += dt;
                if (reinHold >= 2) reinStopPending = true;
                if (!reinStopPending) target = 2.5f;
            }
            else if (!reinStopPending && !reinStopped) reinHold = 0;
            if (reinStopPending || reinStopped) target = 0;
            if (reinStopped && whip && !brake && stamina > 0.02f) { reinStopped = reinStopPending = false; reinHold = 0; target = cruise + 11; }
            if (stopping) target = 0;
            if (s < 30 && !stopping) target = Mathf.Min(target, 4 + s * 0.5f);
            float rate = stopping ? 0.9f : (brake || reinStopPending || reinStopped ? 2.5f : 0.7f);
            speed = MathX.Damp(speed, target, rate, dt);
            if (reinStopPending && speed < 0.15f) { speed = 0; reinStopPending = false; reinStopped = true; }
            s = Mathf.Min(R.len, s + speed * dt);
            if (limitS.HasValue && s > limitS.Value) { s = Mathf.Max(limitS.Value, s - speed * dt); speed = 0; }

            var fr = R.Frame(s);
            var back = R.WorldAt(s - 1.4f, 0); var front = R.WorldAt(s + 1.4f, 0);
            var prev = pos;
            pos = new Vector3(fr.x, (back.y + front.y) / 2, fr.z);
            vel = (pos - prev) / Mathf.Max(dt, 1e-4f);
            float pitch = Mathf.Atan2(front.y - back.y, 2.8f);
            float yaw = Mathf.Atan2(fr.tx, fr.tz);
            // (the web game's right is the mirror of ours: sample on its sides so the numbers match)
            float hl = R.Height(fr.x - fr.rx * 0.8f, fr.z - fr.rz * 0.8f), hr = R.Height(fr.x + fr.rx * 0.8f, fr.z + fr.rz * 0.8f);
            float bank = Mathf.Atan2(hl - hr, 1.6f) * 0.6f;
            root.transform.position = pos;
            root.transform.rotation = MathX.YXZ(-pitch, yaw, bank);
            fwd = fr.Fwd; right = fr.Right;

            // body sway on the thoroughbraces (springs)
            float v = speed;
            float rough = R.noise.Noise(s * 0.35f, 3.3f) * 0.5f + R.noise.Noise(s * 1.3f, 7.1f) * 0.25f;
            float lat = fr.curv * v * v;
            rollV += (-lat * 0.02f - roll * 26 - rollV * 3.2f) * dt; roll += rollV * dt;
            pitchV += (rough * 0.9f * (v / 15) - pitchS * 20 - pitchV * 2.5f) * dt; pitchS += pitchV * dt;
            bounceV += (Mathf.Abs(rough) * 0.35f * (v / 15) - bounce * 60 - bounceV * 6) * dt; bounce += bounceV * dt;
            body.localPosition = bodyBase + new Vector3(0, bounce * 0.4f, 0);
            body.localRotation = bodyQ * MathX.YXZ(pitchS * 0.6f, 0, Mathf.Clamp(roll, -0.25f, 0.25f));
            shake = Mathf.Abs(bounceV) * 0.02f + Mathf.Abs(rough) * 0.004f * v / 15;

            wheelAngle += v * dt;
            foreach (var w in wheels) w.t.localRotation = w.baseQ * Quaternion.AngleAxis(wheelAngle / w.r * Mathf.Rad2Deg, Vector3.right);

            // team follows the road ahead of the coach
            foreach (var th in team)
            {
                float hs = s + th.offZ;
                var p = R.WorldAt(hs, th.offX);
                var f2 = R.Frame(hs);
                var ahead = R.WorldAt(hs + 1.2f, th.offX);
                th.h.root.transform.position = p;
                th.h.root.transform.rotation = MathX.YXZ(-Mathf.Atan2(ahead.y - p.y, 1.2f), Mathf.Atan2(f2.tx, f2.tz), 0);
                string gait = v < 3 ? "Idle" : v < 11 ? "Canter" : "Gallop";
                if (th.h.Has(gait) && th.h.anim.current != gait) th.h.Play(gait, 0.25f, false, th.phase * 2.7f % 1);
                th.h.SetSpeed(gait == "Gallop" ? Mathf.Min(1.32f, 0.75f + v / 30) : gait == "Canter" ? Mathf.Min(1.3f, v / 7) : 1);
                th.h.Update(dt);
            }
            driver.Update(dt);
        }

        /// after animation: hang the lines between the driver's hand and the team
        public void LateUpdate()
        {
            var hand = driver.hand ? driver.hand.position : (seatDriver ? seatDriver.position : pos);
            for (int i = 0; i < 6; i++)
            {
                var h = team[i].h;
                var bit = Assets.Find(h.root.transform, i % 2 == 0 ? "Bit_L" : "Bit_R") ?? Assets.Find(h.root.transform, "Bit_L");
                var to = bit ? bit.position : h.root.transform.position + h.root.transform.forward * 1.1f + Vector3.up * 1.5f;
                lines[i].SetPosition(0, hand + (i % 2 == 0 ? -1 : 1) * right * 0.05f);
                lines[i].SetPosition(1, to);
            }
            var hp = hitch ? hitch.position : pos + fwd * 4;
            var wheelers = (team[0].h.root.transform.position + team[1].h.root.transform.position) * 0.5f + Vector3.up * 1.0f;
            pole.SetPosition(0, hp); pole.SetPosition(1, wheelers + fwd * 1.3f);
        }

        /// a random spot on the coach, for enemies to aim at
        public Vector3 TargetPoint()
        {
            var local = new Vector3((Random.value - 0.5f) * 1.4f, 1.2f + Random.value * 1.6f, 0);
            return pos + Quaternion.Euler(0, MathX.Heading(fwd) * Mathf.Rad2Deg, 0) * local + fwd * ((Random.value - 0.5f) * 3);
        }

        public void Dispose()
        {
            Object.Destroy(root);
            foreach (var th in team) { th.h.anim.Dispose(); Object.Destroy(th.h.root); }
            foreach (var l in lines) if (l) Object.Destroy(l.gameObject);
            if (pole) Object.Destroy(pole.gameObject);
            driver.anim.Dispose();
        }
    }
}
