// Wildlife: the herd that crosses the road and makes you haul on the reins, plus the odd buffalo, bear,
// goat and hawk scattered along the route, and now and then a dead horse by the road with turkey
// vultures working it. Animals live in road space (s, d) like riders do; they are not enemies (never in
// Enemies, so chevrons and Dead Eye ignore them) and combat checks them separately. Birds fly in world
// space around an anchor. Ported from game/wildlife.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class Species
    {
        public string node; public float hp, r, longL, tall, mass, walk, cy, dihedral, grazeB = 0.3f, grazeA = 0.28f; public int meat, hide;
        public static readonly Dictionary<string, Species> All = new Dictionary<string, Species>
        {
            { "buffalo", new Species { node = "Buffalo", hp = 260, r = 1.15f, longL = 3.1f, tall = 1.85f, meat = 25, hide = 20, mass = 1, walk = 2.4f, grazeB = 0.3f, grazeA = 0.35f } },
            { "bear", new Species { node = "Bear", hp = 200, r = 0.8f, longL = 2.0f, tall = 1.1f, meat = 18, hide = 30, mass = 0.7f, walk = 1.8f } },
            { "goat", new Species { node = "Goat", hp = 70, r = 0.45f, longL = 1.2f, tall = 0.9f, meat = 10, hide = 12, mass = 0.15f, walk = 2.0f } },
            { "hawk", new Species { node = "Hawk", hp = 25, r = 0.35f, longL = 0.55f, tall = 0.3f, meat = 5, hide = 25, dihedral = 0.1f } },
            { "vulture", new Species { node = "Vulture", hp = 25, r = 0.4f, longL = 0.72f, tall = 0.5f, meat = 2, hide = 4, cy = 0.24f, dihedral = 0.3f } },
        };
    }

    public abstract class Beast
    {
        protected readonly Game g;
        public readonly string kind; public readonly Species cfg;
        public bool alive = true; public float removeAt = float.PositiveInfinity, hp, s, d = 99, scale;
        public Vector3 pos; public Vector3 centre; public float radius;
        public GameObject root;
        protected Beast(Game game, string kind) { g = game; this.kind = kind; cfg = Species.All[kind]; hp = cfg.hp; }
        public abstract void Update(float dt);
        public virtual bool Damage(float amount, Vector3 dir)
        {
            if (!alive) return false;
            hp -= amount;
            if (hp > 0) return false;
            alive = false; removeAt = g.time + 25;
            return true;
        }
        public void Dispose() { if (root) Object.Destroy(root); }
    }

    /// pieces of an animal model the game animates
    public class AnimalRig
    {
        public GameObject root; public readonly List<Transform> legs = new List<Transform>(); public Transform head, tail, wingL, wingR, folded, legsNode, hornBull, hornCow, beard;
        public Quaternion headQ0, tailQ0, wingLQ0, wingRQ0; public readonly List<Quaternion> legQ0 = new List<Quaternion>();

        public static AnimalRig Build(string kind, Transform parent)
        {
            var cfg = Species.All[kind];
            var r = new AnimalRig();
            var lib = Assets.Model("animals");
            var src = lib ? Assets.Find(lib.transform, cfg.node) : null;
            if (src)
            {
                r.root = Object.Instantiate(src.gameObject, parent);
                r.root.transform.localPosition = Vector3.zero; r.root.transform.localRotation = Quaternion.identity;
                foreach (var rr in r.root.GetComponentsInChildren<Renderer>(true)) rr.shadowCastingMode = kind == "hawk" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                Transform N(string n) => Assets.Find(r.root.transform, cfg.node + "_" + n);
                foreach (var n in new[] { "LegFL", "LegFR", "LegRL", "LegRR" }) { var l = N(n); if (l) r.legs.Add(l); }
                r.head = N("Head"); r.tail = N("Tail"); r.wingL = N("WingL"); r.wingR = N("WingR"); r.folded = N("Folded"); r.legsNode = N("Legs");
                r.hornBull = N("HornBull"); r.hornCow = N("HornCow"); r.beard = N("Beard");
            }
            else
            {
                // blocks, so the game still plays without the model
                r.root = new GameObject(cfg.node); r.root.transform.SetParent(parent, false);
                var hide = Assets.Lit(kind == "buffalo" ? MathX.Hex("#4a3728") : kind == "bear" ? MathX.Hex("#3a2a1e") : kind == "goat" ? MathX.Hex("#b8a488") : MathX.Hex("#2a211c"), 0.1f);
                GameObject P(PrimitiveType t, Transform par, Vector3 pos, Vector3 sc) { var o = GameObject.CreatePrimitive(t); Object.Destroy(o.GetComponent<Collider>()); o.transform.SetParent(par, false); o.transform.localPosition = pos; o.transform.localScale = sc; o.GetComponent<Renderer>().sharedMaterial = hide; return o; }
                float L = cfg.longL, H = cfg.tall;
                if (kind == "hawk" || kind == "vulture")
                {
                    P(PrimitiveType.Capsule, r.root.transform, new Vector3(0, cfg.cy, 0), new Vector3(0.16f, 0.2f, 0.16f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        var w = new GameObject("Wing").transform; w.SetParent(r.root.transform, false); w.localPosition = new Vector3(0, cfg.cy, 0);
                        P(PrimitiveType.Cube, w, new Vector3(sx * 0.35f, 0, 0), new Vector3(0.6f, 0.02f, 0.18f));
                        if (sx < 0) r.wingL = w; else r.wingR = w;
                    }
                }
                else
                {
                    P(PrimitiveType.Capsule, r.root.transform, new Vector3(0, H * 0.72f, 0), new Vector3(H * 0.6f, L * 0.45f, H * 0.6f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                    var head = new GameObject("Head").transform; head.SetParent(r.root.transform, false); head.localPosition = new Vector3(0, H * 0.72f, L * 0.34f);
                    P(PrimitiveType.Cube, head, new Vector3(0, -H * 0.1f, L * 0.1f), new Vector3(H * 0.3f, H * 0.28f, L * 0.22f));
                    r.head = head;
                    foreach (var (sx, sz) in new[] { (-1f, 1f), (1f, 1f), (-1f, -1f), (1f, -1f) })
                    {
                        var leg = new GameObject("Leg").transform; leg.SetParent(r.root.transform, false); leg.localPosition = new Vector3(sx * H * 0.2f, H * 0.62f, sz * L * 0.24f);
                        P(PrimitiveType.Capsule, leg, new Vector3(0, -H * 0.3f, 0), new Vector3(H * 0.11f, H * 0.3f, H * 0.11f));
                        r.legs.Add(leg);
                    }
                }
            }
            if (r.head) r.headQ0 = r.head.localRotation;
            if (r.tail) r.tailQ0 = r.tail.localRotation;
            if (r.wingL) r.wingLQ0 = r.wingL.localRotation;
            if (r.wingR) r.wingRQ0 = r.wingR.localRotation;
            foreach (var l in r.legs) r.legQ0.Add(l.localRotation);
            return r;
        }
    }

    // A four-legged animal, animated by swinging the leg and head nodes the model ships (no skinning).
    // Falls on its side when killed.
    public class Animal : Beast
    {
        public bool crossing; public float speed, heading, spooked;
        readonly AnimalRig rig; float phase, graze, fallT, legX, headX, tailX;

        public Animal(Game game, string kind, float s, float d, bool crossing = false, float speed = 0, float heading = 0) : base(game, kind)
        {
            this.s = s; this.d = d; this.crossing = crossing; this.speed = speed; this.heading = heading;
            phase = Random.value * Mathf.PI * 2; graze = Random.value;
            root = new GameObject("Animal " + kind); root.transform.SetParent(game.world, false);
            rig = AnimalRig.Build(kind, root.transform);
            bool bull = Random.value < 0.3f;
            if (rig.hornBull) rig.hornBull.gameObject.SetActive(bull);
            if (rig.hornCow) rig.hornCow.gameObject.SetActive(!bull);
            if (rig.beard && !bull) rig.beard.localScale = new Vector3(0.8f, 0.6f, 0.6f);
            float size = kind != "buffalo" ? 0.9f + Random.value * 0.2f : bull ? 0.96f + Random.value * 0.09f : 0.79f + Random.value * 0.07f;
            scale = size * Wildlife.Show;
            root.transform.localScale = Vector3.one * scale;
            radius = cfg.r * scale;
            Update(0);
        }

        public override void Update(float dt)
        {
            var R = g.route;
            if (alive)
            {
                if (crossing) { d += speed * dt; if (Mathf.Abs(d) > 70) removeAt = 0; }
                else if (spooked > 0) { spooked -= dt; d += Mathf.Sign(d != 0 ? d : 1) * cfg.walk * 1.8f * dt; }
            }
            else fallT = Mathf.Min(1, fallT + dt * 2.2f);
            pos = R.WorldAt(s, d);
            root.transform.position = pos;
            var f = R.Frame(s);
            float roadYaw = Mathf.Atan2(f.tx, f.tz);
            float want = crossing ? roadYaw + (speed > 0 ? Mathf.PI / 2 : -Mathf.PI / 2) : roadYaw + heading;
            root.transform.rotation = MathX.YXZ(0, want, alive ? 0 : -fallT * 1.5f);
            if ((pos - g.player.camPos).sqrMagnitude < 90 * 90) Animate(dt);
            centre = pos + new Vector3(0, cfg.tall * 0.55f * scale, 0);
            if (!alive && float.IsPositiveInfinity(removeAt) && g.coach.s - s > 120) removeAt = 0;
            if (alive && g.coach.s - s > 160) removeAt = 0;
        }

        void Animate(float dt)
        {
            bool moving = alive && (crossing || spooked > 0);
            phase += dt * (moving ? 5.5f : 0.8f);
            if (rig.tail) { tailX = MathX.Damp(tailX, spooked > 0 ? 1.3f : moving ? 0.25f : 0, 3, dt); rig.tail.localRotation = rig.tailQ0 * Quaternion.Euler(tailX * Mathf.Rad2Deg, 0, Mathf.Sin(phase * 0.9f) * (alive ? 0.18f : 0) * Mathf.Rad2Deg); }
            for (int i = 0; i < rig.legs.Count; i++)
            {
                float x = !alive ? 0.5f : moving ? Mathf.Sin(phase + (i < 2 ? 0 : Mathf.PI) + (i % 2) * Mathf.PI) * 0.5f : 0;
                if (!moving) x = Mathf.LerpAngle(LegX(i), x, 1 - Mathf.Exp(-3 * dt));
                SetLeg(i, x);
            }
            if (rig.head)
            {
                float hx = !alive ? 0.3f : moving ? Mathf.Sin(phase * 0.5f) * 0.06f : cfg.grazeB + Mathf.Sin(graze += dt * 0.12f) * cfg.grazeA;
                headX = MathX.Damp(headX, hx, moving ? 20 : 4, dt);
                rig.head.localRotation = rig.headQ0 * Quaternion.Euler(headX * Mathf.Rad2Deg, 0, 0);
            }
        }
        readonly float[] legXs = new float[4];
        float LegX(int i) => legXs[i];
        void SetLeg(int i, float x) { legXs[i] = x; rig.legs[i].localRotation = rig.legQ0[i] * Quaternion.Euler(x * Mathf.Rad2Deg, 0, 0); }

        public override bool Damage(float amount, Vector3 dir) { spooked = 4; return base.Damage(amount, dir); }
    }

    // A hawk on a thermal or a turkey vulture. Vultures stand about a carcass, flush when the coach comes
    // close, climb into a kettle over the site and finally drift off. Shot, a bird drops to the ground.
    public class Bird : Beast
    {
        readonly AnimalRig rig; public string state; float t, stateT, delay, vy, yaw, flushDir;
        Vector3 anchor; float circR, circW, circA, alt, altWant, leaveAt; public Vector3 drift; float floorY;
        public readonly CarcassSite site;

        public Bird(Game game, string kind, Vector3 p, string state, float s, Vector3 anchor, float alt, float r = -1, float speed = 8.5f, float yaw = float.NaN, CarcassSite site = null) : base(game, kind)
        {
            this.state = state; this.s = s; this.site = site; pos = p;
            this.yaw = float.IsNaN(yaw) ? Random.value * Mathf.PI * 2 : yaw;
            t = Random.value * 10;
            root = new GameObject("Bird " + kind); root.transform.SetParent(game.world, false);
            rig = AnimalRig.Build(kind, root.transform);
            scale = (0.92f + Random.value * 0.16f) * Wildlife.Show;
            root.transform.localScale = Vector3.one * scale;
            radius = cfg.r * scale;
            this.anchor = anchor; circR = r > 0 ? r : 8 + Random.value * 10;
            circW = (Random.value < 0.5f ? -1 : 1) * speed / circR; circA = Random.value * Mathf.PI * 2;
            this.alt = alt; altWant = alt; leaveAt = 25 + Random.value * 25;
            floorY = p.y;
            Update(0);
        }

        bool Airborne => state == "kettle" || state == "flush" || state == "leave";

        public void Flush(float delay)
        {
            if (!alive || Airborne) return;
            state = "flush"; stateT = 0; this.delay = delay;
            var c = g.coach.pos;
            flushDir = Mathf.Atan2(pos.x - c.x, pos.z - c.z) + (Random.value - 0.5f) * 1.2f;
            alt = 0; altWant = 16 + Random.value * 22;
        }

        void Show(bool spread)
        {
            if (rig.wingL) rig.wingL.gameObject.SetActive(spread);
            if (rig.wingR) rig.wingR.gameObject.SetActive(spread);
            if (rig.folded) rig.folded.gameObject.SetActive(!spread);
        }
        void Wings(float ax, float ay, float az)
        {
            // (mirrored: rotations about Y and Z change sign from the web game's)
            if (rig.wingL) rig.wingL.localRotation = rig.wingLQ0 * Quaternion.Euler(ax * Mathf.Rad2Deg, -ay * Mathf.Rad2Deg, -az * Mathf.Rad2Deg);
            if (rig.wingR) rig.wingR.localRotation = rig.wingRQ0 * Quaternion.Euler(ax * Mathf.Rad2Deg, ay * Mathf.Rad2Deg, az * Mathf.Rad2Deg);
        }

        public override void Update(float dt)
        {
            t += dt; stateT += dt;
            float pitch = 0, roll = 0;
            if (!alive)
            {
                float gy = g.route.Height(pos.x, pos.z);
                if (site != null && Mathf.Abs(pos.y - floorY) < 1) gy = Mathf.Max(gy, floorY);
                if (pos.y > gy + 0.01f) { vy -= 9.8f * dt; pos.y = Mathf.Max(gy, pos.y + vy * dt); pitch = t * 3; roll = t * 5; Wings(0, 0, -0.3f); }
                else { pos.y = gy; Show(true); Wings(0, 0.2f, -0.15f); roll = -1.45f; }
            }
            else if (state == "flush" && stateT >= delay)
            {
                float k = stateT - delay;
                Show(true);
                if (rig.legsNode) rig.legsNode.gameObject.SetActive(k < 0.4f);
                float flap = Mathf.Sin(k * 17) * 0.75f;
                yaw = Mathf.LerpAngle(yaw * Mathf.Rad2Deg, flushDir * Mathf.Rad2Deg, 1 - Mathf.Exp(-2 * dt)) * Mathf.Deg2Rad;
                float sp = Mathf.Min(6, 2 + k * 3);
                pos.x += Mathf.Sin(yaw) * sp * dt; pos.z += Mathf.Cos(yaw) * sp * dt; pos.y += (k < 2.2f ? 2.6f : 1.2f) * dt;
                Wings(0, 0, cfg.dihedral + flap);
                if (k > 2.6f)
                {
                    state = "kettle"; stateT = 0;
                    float dx = pos.x - anchor.x, dz = pos.z - anchor.z;
                    circA = Mathf.Atan2(dz, dx); circR = Mathf.Clamp(Mathf.Sqrt(dx * dx + dz * dz), 7, 18); alt = pos.y - anchor.y;
                }
            }
            else if (Airborne && state != "flush")
            {
                Show(true);
                if (rig.legsNode) rig.legsNode.gameObject.SetActive(false);
                if (state == "kettle" && site != null && stateT > leaveAt) { state = "leave"; stateT = 0; }
                if (state == "leave") { anchor += drift * dt; altWant += 1.5f * dt; }
                alt = MathX.Damp(alt, altWant, 0.35f, dt);
                circA += circW * dt;
                pos = new Vector3(anchor.x + Mathf.Cos(circA) * circR, anchor.y + alt, anchor.z + Mathf.Sin(circA) * circR);
                float sg = Mathf.Sign(circW);
                yaw = Mathf.Atan2(-Mathf.Sin(circA) * sg, Mathf.Cos(circA) * sg);
                float teeter = kind == "vulture" ? 0.16f : 0.05f;
                roll = -(sg * 0.22f + Mathf.Sin(t * 2.1f) * teeter + Mathf.Sin(t * 5.3f) * teeter * 0.3f);
                float every = kind == "vulture" ? 19 : 9, ph = t % every;
                float flap = ph < 1.1f ? Mathf.Sin(ph * 14) * 0.45f * Mathf.Sin(ph / 1.1f * Mathf.PI) : 0;
                Wings(0, 0, cfg.dihedral + flap);
            }
            else
            {
                // on the ground: feeding, wings spread to the sun, or hunched
                if (rig.legsNode) rig.legsNode.gameObject.SetActive(true);
                bool spread = state == "spread";
                Show(spread);
                if (spread) Wings(0, 0.1f, -0.12f + Mathf.Sin(t * 0.8f) * 0.04f);
                pitch = state == "feed" ? -0.45f + 0.6f : -0.45f - (spread ? 0.3f : 0.25f);
                if (rig.head) rig.head.localRotation = rig.headQ0 * Quaternion.Euler((state == "feed" ? 0.5f + Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 5.5f)), 3) * 0.28f : 0.9f) * Mathf.Rad2Deg, Mathf.Sin(t * 0.37f) * 0.6f * Mathf.Rad2Deg, 0);
            }
            root.transform.position = pos;
            root.transform.rotation = MathX.YXZ(pitch, yaw, roll);
            centre = pos + new Vector3(0, cfg.cy * scale, 0);
            float behind = g.coach.s - s;
            if (behind > 220 || (state == "leave" && Vector3.Distance(pos, g.player.camPos) > 240)) removeAt = 0;
        }

        public override bool Damage(float amount, Vector3 dir)
        {
            site?.Flush();
            bool k = base.Damage(amount, dir);
            if (k) vy = 0;
            return k;
        }
    }

    // A dead horse by the road and the vultures on it. The carcass is scenery; the birds are in the list.
    public class CarcassSite
    {
        readonly Game g; public readonly float s; public readonly Vector3 pos; bool flushed;
        readonly GameObject root; readonly List<Bird> birds = new List<Bird>();

        public CarcassSite(Game game, float s, float d, List<Beast> list)
        {
            g = game; this.s = s;
            var R = g.route;
            pos = R.WorldAt(s, d);
            var f = R.Frame(s);
            float yaw = Mathf.Atan2(f.tx, f.tz) + (Random.value - 0.5f) * 1.4f + (Random.value < 0.5f ? Mathf.PI : 0);
            var lib = Assets.Model("animals");
            var src = lib ? Assets.Find(lib.transform, "Carcass") : null;
            if (src) root = Object.Instantiate(src.gameObject, g.world);
            else
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Capsule); Object.Destroy(root.GetComponent<Collider>());
                root.transform.SetParent(g.world, false); root.transform.localScale = new Vector3(0.7f, 0.7f, 0.5f);
                root.GetComponent<Renderer>().sharedMaterial = Assets.Lit(MathX.Hex("#8a7560"), 0.05f);
            }
            float Hh(float x, float z) => R.Height(x, z);
            var n = new Vector3(Hh(pos.x - 1, pos.z) - Hh(pos.x + 1, pos.z), 2, Hh(pos.x, pos.z - 1) - Hh(pos.x, pos.z + 1)).normalized;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0, yaw * Mathf.Rad2Deg, src ? 0 : 90);
            root.transform.position = pos - new Vector3(0, 0.03f, 0);
            if (src) root.transform.localScale = Vector3.one * Wildlife.Show;
            int n0 = 3 + Random.Range(0, 10);
            void Add(Vector3 p, float yw, string st) { var b = new Bird(g, "vulture", p, st, s, pos, 0, -1, 8.5f, yw, this); birds.Add(b); list.Add(b); }
            int feeders = Mathf.Min(n0 - 1, 1 + Random.Range(0, 3));
            for (int i = 0; i < n0; i++)
            {
                float a = Random.value * 6.28f, r = (i < feeders ? 0.8f + Random.value * 0.6f : 2 + Random.value * 6) * Wildlife.Show;
                var q = new Vector3(pos.x + Mathf.Cos(a) * r, 0, pos.z + Mathf.Sin(a) * r); q.y = R.Height(q.x, q.z);
                string st = i < feeders ? "feed" : i == feeders ? "spread" : "stand";
                Add(q, Mathf.Atan2(pos.x - q.x, pos.z - q.z) + (Random.value - 0.5f) * 1.6f, st);
            }
            for (int i = 0; i < Random.Range(0, 3); i++)
            {
                var b = new Bird(g, "vulture", pos, "kettle", s, pos, 25 + Random.value * 15, (10 + Random.value * 10) * Wildlife.Show, 8.5f, float.NaN, this);
                birds.Add(b); list.Add(b);
            }
            float dr = Random.value * 6.28f;
            foreach (var b in birds) b.drift = new Vector3(Mathf.Cos(dr) * 3, 0, Mathf.Sin(dr) * 3);
        }

        public void Flush()
        {
            if (flushed) return;
            flushed = true;
            foreach (var b in birds) b.Flush(Random.value * 1.2f);
        }

        /// true when it can go
        public bool Update()
        {
            if (!flushed && (g.coach.pos - pos).sqrMagnitude < 30 * 30) Flush();
            return g.coach.s - s > 220;
        }

        public void Dispose() { if (root) Object.Destroy(root); }
    }

    public class WildDef { public string[] kinds; public float every = 110; public int carcass = 2; public WildDef(float every, params string[] kinds) { this.every = every; this.kinds = kinds; } }

    public class Wildlife
    {
        /// drawn larger than life so they read from the coach roof at a gallop
        public const float Show = 1.4f;
        readonly Game g;
        public readonly List<Beast> list = new List<Beast>();
        readonly List<CarcassSite> sites = new List<CarcassSite>();
        readonly List<WaveDef> herds = new List<WaveDef>();
        readonly List<float> carcassAt = new List<float>();
        readonly WildDef cfg;
        float ambientAt, hitCd; bool warned; float herdS = -1;

        public Wildlife(Game game)
        {
            g = game;
            cfg = game.route.def.wildlife;
            foreach (var w in game.route.def.waves) if (w.type == "herd") herds.Add(new WaveDef(w.at, w.type, w.count, w.from) { kind = w.kind, lead = w.lead });
            int n = cfg != null ? cfg.carcass : 0;
            for (int i = 0; i < n; i++) carcassAt.Add(0.14f + 0.72f * (i + 0.2f + Random.value * 0.6f) / n);
        }

        public void Update(float dt)
        {
            var coach = g.coach;
            float prog = coach.s / g.route.len;
            foreach (var h in herds) if (!h.done && prog >= h.at) { h.done = true; StartHerd(h); }
            if (cfg != null) Ambient();
            Carcass();
            for (int i = 0; i < list.Count; i++) list[i].Update(dt);
            HerdWarning();
            Collide(dt);
            for (int i = list.Count - 1; i >= 0; i--) if (g.time >= list[i].removeAt || list[i].removeAt == 0) { list[i].Dispose(); list.RemoveAt(i); }
            for (int i = sites.Count - 1; i >= 0; i--) if (sites[i].Update()) { sites[i].Dispose(); sites.RemoveAt(i); }
        }

        void StartHerd(WaveDef h)
        {
            var R = g.route;
            float lead = h.lead > 0 ? h.lead : 300;
            float s0 = g.coach.s + lead;
            if (s0 > R.len - 80) return;
            float side = Random.value < 0.5f ? -1 : 1;
            int n = h.count > 0 ? h.count : 14;
            string kind = h.kind ?? "buffalo";
            float walk = Species.All[kind].walk;
            // start them far enough out that the herd is astride the road when a coach at cruise gets here
            float arrive = lead / Mathf.Max(6, g.coach.cruise);
            float d0 = walk * arrive;
            var taken = new List<Vector2>(); float gap = 3.8f * Show / 1.4f;
            for (int i = 0; i < n; i++)
            {
                float s = 0, d = 0;
                for (int k = 0; k < 14; k++)
                {
                    s = s0 + (Random.value - 0.5f) * 34 * Show;
                    d = side * (d0 + ((Random.value - 0.5f) * 26 + (i % 5) * 2.2f) * Show);
                    bool ok = true; foreach (var t in taken) if (Vector2.Distance(t, new Vector2(s, d)) <= gap) { ok = false; break; }
                    if (ok) break;
                }
                taken.Add(new Vector2(s, d));
                list.Add(new Animal(g, kind, s, d, true, -side * walk * (0.9f + Random.value * 0.2f)));
            }
            herdS = s0; warned = false;
        }

        void HerdWarning()
        {
            if (herdS < 0 || warned) return;
            float away = herdS - g.coach.s;
            if (away > 380 || away < 0) return;
            warned = true;
            g.hud.Banner("BUFFALO HERD AHEAD", "Rein in now — they are crossing the road", 4.2f);
            Sound.I.Play("horse_neigh", 1, 0.78f);
        }

        // anything of size standing on the road when the coach arrives is a crash
        void Collide(float dt)
        {
            var coach = g.coach;
            if (g.over) return;
            hitCd = Mathf.Max(0, hitCd - dt);
            if (hitCd > 0) return;
            foreach (var b in list)
            {
                if (!(b is Animal a) || !a.alive || a.cfg.mass < 0.3f) continue;
                if (Mathf.Abs(a.d) > Route.RoadHalf - 0.4f) continue;
                float ds = a.s - coach.s;
                if (ds < -1.5f || ds > 5.5f) continue;
                float dmg = Mathf.Clamp(Mathf.Max(0, coach.speed) * 1.5f * a.cfg.mass, 4, 26);
                g.DamageCoach(dmg);
                coach.speed = Mathf.Max(2, coach.speed * 0.45f);
                g.shake += 0.5f; g.HitStop(0.09f);
                hitCd = 2;
                a.alive = false; a.removeAt = g.time + 20;
                Sound.I.Play("hit_flesh_1", 1, 0.7f); Sound.I.Play("horse_neigh", 0.9f, 0.85f);
                g.fx.Dust(a.pos, 20, 1.3f, null, 2.5f, 4);
                g.hud.Feed("Ran down a " + a.kind + "!", false);
                g.player.Damage(6, a.pos);
                g.OnCoachDamage();
                break;
            }
        }

        // animals dotted along the route in small groups, spawned ahead of the coach
        void Ambient()
        {
            var R = g.route;
            if (g.coach.s < ambientAt) return;
            ambientAt = g.coach.s + cfg.every * (0.7f + Random.value * 0.6f);
            string kind = cfg.kinds[Random.Range(0, cfg.kinds.Length)];
            float s = g.coach.s + 120 + Random.value * 110;
            if (s > R.len - 60 || R.OnBridge(s)) return;
            if (kind == "hawk")
            {
                float dd = (Random.value < 0.5f ? -1 : 1) * (14 + Random.value * 30);
                var at = R.WorldAt(s, dd);
                for (int i = 0; i < 1 + (Random.value < 0.5f ? 1 : 0); i++)
                {
                    float altv = 13 + Random.value * 10;
                    list.Add(new Bird(g, "hawk", at + Vector3.up * altv, "kettle", s, at, altv, 6 + Random.value * 9, 7));
                }
                return;
            }
            int want = kind == "buffalo" ? 2 + Random.Range(0, 3) : 1 + Random.Range(0, 3);
            float sideSign = Random.value < 0.5f ? -1 : 1;
            if (R.def.gorge != null) sideSign = -R.riverSide;
            int placed = 0;
            for (int k = 0; k < 24 && placed < want; k++)
            {
                float d = sideSign * (8 + Random.value * 20);
                float ss = s + (Random.value - 0.5f) * 16;
                var p = R.WorldAt(ss, d);
                float slope = new Vector2(R.Height(p.x + 2, p.z) - p.y, R.Height(p.x, p.z + 2) - p.y).magnitude / 2;
                if (slope > 0.55f) continue;
                list.Add(new Animal(g, kind, ss, d, false, 0, Random.value * 6.3f));
                placed++;
            }
        }

        void Carcass()
        {
            var R = g.route;
            if (carcassAt.Count == 0 || g.coach.s < carcassAt[0] * R.len - 250) return;
            float s = carcassAt[0] * R.len; carcassAt.RemoveAt(0);
            if (s > R.len - 60 || R.InTown(s) || R.OnBridge(s)) return;
            float side = Random.value < 0.5f ? -1 : 1;
            if (R.def.gorge != null) side = -R.riverSide;
            for (int k = 0; k < 10; k++)
            {
                float d = (k % 2 == 1 && R.def.gorge == null ? -side : side) * (9 + Random.value * 8);
                var p = R.WorldAt(s, d);
                float slope = new Vector2(R.Height(p.x + 2, p.z) - p.y, R.Height(p.x, p.z + 2) - p.y).magnitude / 2;
                if (slope > 0.28f) continue;
                sites.Add(new CarcassSite(g, s, d, list));
                return;
            }
        }

        // ------------------------------------------------------------- hits
        public (Beast b, Vector3 aim, float dist)? Pick(Vector3 o, Vector3 dir, float tol)
        {
            (Beast, Vector3, float)? best = null; float bestA = tol;
            foreach (var a in list)
            {
                if (!a.alive) continue;
                var to = a.centre - o; float dist = to.magnitude;
                if (dist > 260 || dist < 0.001f) continue;
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(to, dir) / dist, -1, 1)) - Mathf.Atan(a.radius / dist);
                if (ang < bestA) { bestA = ang; best = (a, a.centre, dist); }
            }
            return best;
        }

        public (Beast b, float t, Vector3 point)? RayHit(Vector3 o, Vector3 dir, float range)
        {
            (Beast, float, Vector3)? best = null; float bestT = range;
            foreach (var a in list)
            {
                if (!a.alive) continue;
                var oc = o - a.centre;
                float b = Vector3.Dot(oc, dir), c = oc.sqrMagnitude - a.radius * a.radius, h = b * b - c;
                if (h < 0) continue;
                float t = -b - Mathf.Sqrt(h);
                if (t > 0 && t < bestT) { bestT = t; best = (a, t, o + dir * t); }
            }
            return best;
        }

        /// a kill pays a hide price and, for anything worth butchering, sends up meat that heals you
        public void OnKill(Beast a)
        {
            if (a.cfg.hide > 0) g.AddBounty(a.cfg.hide, $"{char.ToUpper(a.kind[0])}{a.kind.Substring(1)} hide +${a.cfg.hide}", false);
            if (a.cfg.meat > 0 && g.player.hp < 100) { g.player.hp = Mathf.Min(100, g.player.hp + a.cfg.meat); g.hud.Feed($"Fresh meat +{a.cfg.meat} health", true); }
        }

        /// dynamite: everything within the radius
        public int Blast(Vector3 at, float radius)
        {
            int n = 0;
            foreach (var a in list.ToArray())
            {
                if (!a.alive || Vector3.Distance(a.pos, at) > radius) continue;
                if (!a.Damage(999, (a.pos - at).normalized)) continue;
                OnKill(a); n++;
            }
            return n;
        }

        public void Dispose()
        {
            foreach (var a in list) a.Dispose(); list.Clear();
            foreach (var s in sites) s.Dispose(); sites.Clear();
        }
    }
}
