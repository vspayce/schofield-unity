// A freight train on the crossing line: an 1870s American 4-4-0 with its tender, a string of boxcars and
// a caboose, built in code (boxes, cylinders and cones merged per car and per material), lettered with
// world-space canvases. Each car is built in its own frame: +Z toward the front, y = 0 at the rail top.
// After world/train.js.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Schofield
{
    public class Train
    {
        public const float RailTop = 0.366f;
        const float DriverR = 0.8f, WheelR = 0.42f;

        public class Car { public Transform root; public string kind; public float front, back, off, roofY; public float[] slots = new float[0]; public Bounds hit; public readonly List<(Transform t, float r)> axles = new List<(Transform, float)>(); }

        readonly Railroad rail;
        readonly GameObject root;
        readonly List<Object> owned = new List<Object>();
        public readonly List<Car> cars = new List<Car>();
        public readonly float length;
        public float x = -1e4f, sdir = 1, speed, odo; public bool active;
        public Vector3 fwd, vel;
        public float bellSwing;
        public System.Action<int> onChuff;
        readonly Pool fx;
        readonly Color smokeDark, smokeLight, steamCol = new Color(0.96f, 0.95f, 0.94f), dustCol;
        float chuffPhase, t, smokeAcc, dustAcc;
        readonly List<Person> crew = new List<Person>();
        Transform bell, lamp;
        static readonly (string b, string ink, string num, string init)[] Boxcars = { ("#8a3a22", "#efe4c8", "1147", "T.P.R.R."), ("#a8813e", "#2a1d12", "862", "T.P.R.R."), ("#6e4630", "#eadfc4", "2215", "K.&W.") };

        Material paint, wood, iron, brass, black, glow;
        Material M2(Color c, float sm, float met) { var m = Assets.VLit(c, sm, met); owned.Add(m); return m; }

        public Train(Transform parent, Railroad rail, int ncars, LightDef light)
        {
            this.rail = rail;
            root = new GameObject("Train"); root.transform.SetParent(parent, false);
            root.SetActive(false);
            Material M(Color c, float sm, float met, string tex = null) { var m = Assets.VLit(c, sm, met, tex); owned.Add(m); return m; }
            paint = M(Color.white, 0.5f, 0.08f); wood = M(Color.white, 0.12f, 0, "wood_planks"); iron = M(MathX.Hex("#2c2a28"), 0.55f, 0.7f);
            brass = M(MathX.Hex("#d9b163"), 0.75f, 1f); black = M(MathX.Hex("#1c1b1a"), 0.35f, 0.4f);
            glow = new Material(Assets.Urp(false)); glow.SetColor(Assets.BaseColor, new Color(1, 0.82f, 0.5f) * 2.2f); owned.Add(glow);

            Loco(); Tender();
            for (int i = 0; i < ncars; i++) Boxcar(i);
            Caboose();
            float o = 0;
            for (int i = 0; i < cars.Count; i++) { var c = cars[i]; if (i > 0) o += c.front; c.off = i == 0 ? c.front : o; o = c.off - c.back; }
            length = o;

            fx = new Pool(700, false);
            var warm = light != null ? light.sunColor : new Color(1, 0.85f, 0.7f);
            smokeDark = Color.Lerp(new Color(0.3f, 0.28f, 0.26f), warm, 0.12f);
            smokeLight = Color.Lerp(new Color(0.62f, 0.58f, 0.54f), warm, 0.25f);
            dustCol = light != null ? Color.Lerp(Color.Lerp(light.ground, light.sunColor, 0.35f), Color.white, 0.25f) : new Color(0.7f, 0.6f, 0.5f);
        }

        // ------------------------------------------------------------ building
        Car NewCar(string kind, float front, float back, Bounds hit, float roofY = 0, float[] slots = null)
        {
            var c = new Car { kind = kind, front = front, back = back, hit = hit, roofY = roofY, slots = slots ?? new float[0] };
            c.root = new GameObject(kind).transform; c.root.SetParent(root.transform, false);
            cars.Add(c);
            return c;
        }
        void Put(Car c, string name, MeshKit.Builder b, Material m)
        {
            if (b.v.Count == 0) return;
            var mesh = b.Build(name); owned.Add(mesh);
            MeshKit.Show(name, mesh, m, c.root);
        }
        Mesh wheelMesh, driverMesh;
        Mesh WheelSet(float r)
        {
            var b = new MeshKit.Builder();
            foreach (float sx in new[] { -0.75f, 0.75f }) { b.Cylinder(new Vector3(sx - 0.06f, 0, 0), new Vector3(sx + 0.06f, 0, 0), r, r, 18); b.Cylinder(new Vector3(sx + (sx > 0 ? 0.06f : -0.06f), 0, 0), new Vector3(sx + (sx > 0 ? 0.09f : -0.09f), 0, 0), r * 0.25f, r * 0.2f, 10); }
            b.Cylinder(new Vector3(-0.8f, 0, 0), new Vector3(0.8f, 0, 0), 0.07f, 0.07f, 8);
            // a spoke-ish hub bar so the turning reads
            foreach (float sx in new[] { -0.75f, 0.75f }) { float o2 = sx > 0 ? 0.07f : -0.07f; b.Box(new Vector3(0.02f, r * 1.7f, 0.08f), new Vector3(sx + o2, 0, 0), Quaternion.identity); b.Box(new Vector3(0.02f, 0.08f, r * 1.7f), new Vector3(sx + o2, 0, 0), Quaternion.identity); }
            var m = b.Build("Wheelset"); owned.Add(m); return m;
        }
        void Axle(Car c, float z, float r, Material m)
        {
            if (r == WheelR && !wheelMesh) wheelMesh = WheelSet(WheelR);
            if (r == DriverR && !driverMesh) driverMesh = WheelSet(DriverR);
            var go = MeshKit.Show("Axle", r == DriverR ? driverMesh : wheelMesh, m, c.root);
            go.transform.localPosition = new Vector3(0, r, z);
            c.axles.Add((go.transform, r));
        }
        Text Letter(Transform parent, Vector3 pos, float ry, float w, float h, string s, int size, Color col)
        {
            var cgo = new GameObject("Letters", typeof(RectTransform), typeof(Canvas));
            var rt = (RectTransform)cgo.transform; rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(w * 100, h * 100); rt.localScale = Vector3.one * 0.01f; rt.localPosition = pos; rt.localRotation = Quaternion.Euler(0, ry, 0);
            cgo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var t = UIKit.Txt("t", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w * 100, h * 100), s, size, col, TextAnchor.MiddleCenter, true);
            Object.Destroy(t.GetComponent<Shadow>());
            return t;
        }
        // lettering on both sides of a car body of half-width hw (canvases face -Z, so turn them to face out)
        void Sides(Car c, float hw, float y, float w, float h, string s, int size, Color col, float z = 0)
        {
            Letter(c.root, new Vector3(hw + 0.02f, y, z), -90, w, h, s, size, col);
            Letter(c.root, new Vector3(-hw - 0.02f, y, z), 90, w, h, s, size, col);
        }

        void Loco()
        {
            var c = NewCar("loco", 7.4f, -3.5f, new Bounds(new Vector3(0, 2.1f, 1.35f), new Vector3(2.64f, 3.65f, 9.5f)));
            var P = new MeshKit.Builder(); var I = new MeshKit.Builder(); var B = new MeshKit.Builder(); var K = new MeshKit.Builder(); var W = new MeshKit.Builder();
            Color jacket = MathX.Hex("#4f5d66"), red = MathX.Hex("#7e1f16"), green = MathX.Hex("#1f3d2b"), cab = MathX.Hex("#6e4222");
            // frame and running board
            K.Box(new Vector3(0.9f, 0.32f, 9.2f), new Vector3(0, 1.0f, 2.1f), Quaternion.identity);
            foreach (float sx in new[] { -1.15f, 1.15f }) P.Box(new Vector3(0.36f, 0.05f, 6.4f), new Vector3(sx, 1.52f, 2.4f), Quaternion.identity, red);
            // boiler, Russia-iron jacket with brass bands; smokebox; stack; domes
            P.Cylinder(new Vector3(0, 2.15f, -0.3f), new Vector3(0, 2.15f, 4.9f), 0.72f, 0.72f, 24, jacket);
            foreach (float z in new[] { 0.6f, 1.9f, 3.2f, 4.4f }) B.Cylinder(new Vector3(0, 2.15f, z), new Vector3(0, 2.15f, z + 0.06f), 0.735f, 0.735f, 24);
            K.Cylinder(new Vector3(0, 2.15f, 4.9f), new Vector3(0, 2.15f, 6.05f), 0.78f, 0.78f, 24);
            B.Cylinder(new Vector3(0, 2.15f, 6.05f), new Vector3(0, 2.15f, 6.12f), 0.5f, 0.45f, 20);
            K.Cylinder(new Vector3(0, 2.8f, 5.25f), new Vector3(0, 3.7f, 5.25f), 0.2f, 0.24f, 14);
            K.Cylinder(new Vector3(0, 3.7f, 5.25f), new Vector3(0, 4.35f, 5.25f), 0.3f, 0.58f, 18);   // the diamond stack
            K.Cylinder(new Vector3(0, 4.35f, 5.25f), new Vector3(0, 4.45f, 5.25f), 0.58f, 0.5f, 18);
            B.Cylinder(new Vector3(0, 2.75f, 2.9f), new Vector3(0, 3.25f, 2.9f), 0.36f, 0.33f, 18);
            B.Cylinder(new Vector3(0, 3.25f, 2.9f), new Vector3(0, 3.35f, 2.9f), 0.33f, 0.1f, 18);
            B.Cylinder(new Vector3(0, 2.75f, 1.4f), new Vector3(0, 3.1f, 1.4f), 0.28f, 0.26f, 16);
            B.Cylinder(new Vector3(0, 2.8f, 0.4f), new Vector3(0, 3.25f, 0.4f), 0.06f, 0.05f, 8);   // whistle
            // cylinders and steam chests
            foreach (float sx in new[] { -1.05f, 1.05f }) { I.Cylinder(new Vector3(sx, 1.0f, 4.35f), new Vector3(sx, 1.0f, 5.35f), 0.3f, 0.3f, 16); P.Box(new Vector3(0.5f, 0.55f, 0.9f), new Vector3(sx * 0.92f, 1.5f, 4.85f), Quaternion.identity, green); }
            // cab, roof, windows; pilot (cowcatcher); headlamp
            P.Box(new Vector3(2.5f, 2.1f, 2.5f), new Vector3(0, 2.65f, -1.9f), Quaternion.identity, cab);
            K.Box(new Vector3(2.8f, 0.12f, 2.9f), new Vector3(0, 3.76f, -1.9f), Quaternion.identity);
            foreach (float sx in new[] { -1.26f, 1.26f }) K.Box(new Vector3(0.02f, 0.7f, 0.9f), new Vector3(sx, 3.0f, -1.6f), Quaternion.identity);
            for (int i = -3; i <= 3; i++) I.Box(new Vector3(0.08f, 0.08f, 1.5f), new Vector3(i * 0.2f, 0.55f, 6.75f), Quaternion.Euler(-32, i * 6, 0), red);
            I.Box(new Vector3(1.6f, 0.15f, 0.2f), new Vector3(0, 0.95f, 7.15f), Quaternion.identity, red);
            K.Box(new Vector3(0.6f, 0.6f, 0.5f), new Vector3(0, 3.2f, 5.6f), Quaternion.identity);
            var lampGo = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(lampGo.GetComponent<Collider>());
            lamp = lampGo.transform; lamp.SetParent(c.root, false); lamp.localPosition = new Vector3(0, 3.2f, 5.86f); lamp.localScale = Vector3.one * 0.42f; lamp.localRotation = Quaternion.Euler(0, 180, 0);
            lampGo.GetComponent<Renderer>().sharedMaterial = glow;
            Put(c, "Paint", P, paint); Put(c, "Iron", I, iron); Put(c, "Brass", B, brass); Put(c, "Black", K, black);
            var bellGo = new GameObject("Bell"); bell = bellGo.transform; bell.SetParent(c.root, false); bell.localPosition = new Vector3(0, 3.25f, 2.3f);
            var bb = new MeshKit.Builder(); bb.Cylinder(new Vector3(0, -0.32f, 0), new Vector3(0, 0, 0), 0.2f, 0.08f, 16); var bm = bb.Build("Bell"); owned.Add(bm); MeshKit.Show("Bell", bm, brass, bell);
            Axle(c, 0, DriverR, paint); Axle(c, 2.45f, DriverR, paint); Axle(c, 4.25f, WheelR, iron); Axle(c, 5.45f, WheelR, iron);
            var redM = M2(red, 0.5f, 0.1f);
            foreach (var r in c.root.GetComponentsInChildren<Renderer>()) if (r.name == "Axle" && r.GetComponent<MeshFilter>().sharedMesh == driverMesh) r.sharedMaterial = redM;
            Sides(c, 1.27f, 2.65f, 2.2f, 0.5f, "No. 7", 34, MathX.Hex("#c79a3e"), -1.9f);
            foreach (var (variant, x0, z0, ry) in new[] { ("townsman", -0.78f, -2.15f, -0.5f), ("bandit3", 0.62f, -2.7f, 0.9f) })
            {
                var p = Characters.CreateRider(variant); p.root.transform.SetParent(c.root, false);
                p.root.transform.localPosition = new Vector3(x0, 1.61f, z0); p.root.transform.localRotation = Quaternion.Euler(0, -ry * Mathf.Rad2Deg, 0);
                if (p.Has("StandIdle")) p.Play("StandIdle", 0);
                crew.Add(p);
            }
        }

        void Tender()
        {
            var c = NewCar("tender", 3.45f, -3.6f, new Bounds(new Vector3(0, 1.67f, 0), new Vector3(2.7f, 2.55f, 6.7f)));
            var P = new MeshKit.Builder(); var K = new MeshKit.Builder();
            Color green = MathX.Hex("#1f3d2b");
            K.Box(new Vector3(2.0f, 0.3f, 6.9f), new Vector3(0, 1.0f, 0), Quaternion.identity);
            P.Box(new Vector3(2.6f, 1.6f, 6.4f), new Vector3(0, 1.95f, -0.1f), Quaternion.identity, green);
            K.Box(new Vector3(2.3f, 0.5f, 3.6f), new Vector3(0, 2.85f, 1.0f), Quaternion.Euler(4, 0, 0));
            P.Box(new Vector3(2.66f, 0.08f, 6.46f), new Vector3(0, 2.77f, -0.1f), Quaternion.identity, MathX.Hex("#c79a3e"));
            Put(c, "Paint", P, paint); Put(c, "Black", K, black);
            foreach (float z in new[] { -2.96f, -1.44f, 1.44f, 2.96f }) Axle(c, z, WheelR, iron);
            Sides(c, 1.32f, 2.0f, 5.6f, 0.8f, "TERRITORIAL PACIFIC", 42, MathX.Hex("#c79a3e"));
        }

        void Boxcar(int i)
        {
            var bc = Boxcars[i % Boxcars.Length];
            var c = NewCar("boxcar", 5.05f, -5.05f, new Bounds(new Vector3(0, 2.13f, 0), new Vector3(2.72f, 3.46f, 9.4f)), 3.865f, new[] { -2.6f, 2.4f });
            var P = new MeshKit.Builder(); var Wd = new MeshKit.Builder(); var K = new MeshKit.Builder();
            Color body = MathX.Hex(bc.b);
            K.Box(new Vector3(2.0f, 0.3f, 9.8f), new Vector3(0, 1.0f, 0), Quaternion.identity);
            Wd.Box(new Vector3(2.7f, 2.75f, 9.4f), new Vector3(0, 2.45f, 0), Quaternion.identity, body);
            P.Box(new Vector3(2.92f, 0.1f, 9.6f), new Vector3(0, 3.86f, 0), Quaternion.identity, MathX.Hex("#3e3833"));
            Wd.Box(new Vector3(0.5f, 0.05f, 9.6f), new Vector3(0, 3.93f, 0), Quaternion.identity, MathX.Hex("#6b4c30"));   // the roof walk
            foreach (float sx in new[] { -1.37f, 1.37f }) Wd.Box(new Vector3(0.05f, 2.3f, 1.9f), new Vector3(sx, 2.35f, 0), Quaternion.identity, body * 0.78f);
            for (int k = -4; k <= 4; k++) foreach (float sx in new[] { -1.36f, 1.36f }) K.Box(new Vector3(0.03f, 2.7f, 0.05f), new Vector3(sx, 2.45f, k * 1.05f), Quaternion.identity);
            foreach (float z in new[] { -4.8f, 4.8f }) for (int k = 0; k < 6; k++) K.Box(new Vector3(0.4f, 0.03f, 0.05f), new Vector3(1.1f, 1.4f + k * 0.4f, z * 1.01f), Quaternion.identity);   // ladders
            Put(c, "Wood", Wd, wood); Put(c, "Paint", P, paint); Put(c, "Black", K, black);
            foreach (float z in new[] { -4.01f, -2.49f, 2.49f, 4.01f }) Axle(c, z, WheelR, iron);
            var ink = MathX.Hex(bc.ink);
            Sides(c, 1.36f, 3.2f, 6.5f, 0.5f, "TERRITORIAL PACIFIC", 40, ink, -1.8f);
            Sides(c, 1.36f, 1.7f, 2.5f, 0.45f, bc.init + "  " + bc.num, 32, ink, 2.8f);
        }

        void Caboose()
        {
            var c = NewCar("caboose", 4.12f, -4.12f, new Bounds(new Vector3(0, 2.0f, 0), new Vector3(2.6f, 3.2f, 7.6f)), 3.6f);
            var Wd = new MeshKit.Builder(); var K = new MeshKit.Builder(); var G = new MeshKit.Builder();
            Color red = MathX.Hex("#8a2a1c");
            K.Box(new Vector3(2.0f, 0.3f, 8.2f), new Vector3(0, 1.0f, 0), Quaternion.identity);
            Wd.Box(new Vector3(2.5f, 2.3f, 6.6f), new Vector3(0, 2.3f, 0), Quaternion.identity, red);
            K.Box(new Vector3(2.8f, 0.1f, 7.2f), new Vector3(0, 3.5f, 0), Quaternion.identity);
            Wd.Box(new Vector3(1.6f, 0.9f, 2.2f), new Vector3(0, 4.0f, 0), Quaternion.identity, red);
            K.Box(new Vector3(1.9f, 0.08f, 2.5f), new Vector3(0, 4.5f, 0), Quaternion.identity);
            foreach (float sx in new[] { -1.26f, 1.26f }) for (int k = -1; k <= 1; k++) G.Box(new Vector3(0.02f, 0.6f, 0.6f), new Vector3(sx, 2.7f, k * 2.0f), Quaternion.identity);
            foreach (float z in new[] { -3.7f, 3.7f }) K.Box(new Vector3(2.3f, 0.08f, 0.8f), new Vector3(0, 1.3f, z), Quaternion.identity);
            Put(c, "Wood", Wd, wood); Put(c, "Black", K, black); Put(c, "Glass", G, black);
            foreach (float z in new[] { -3.15f, -1.75f, 1.75f, 3.15f }) Axle(c, z, WheelR, iron);
            Sides(c, 1.26f, 1.7f, 4f, 0.4f, "T.P.R.R.  CABOOSE  41", 30, MathX.Hex("#efe4c8"));
        }

        // ------------------------------------------------------------ running
        public void SetActive(bool on) { active = on; root.SetActive(on); }
        public float UAt(float back) => sdir * (x - back);

        public void Place()
        {
            fwd = rail.dir * sdir;
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, pitch = Mathf.Atan(rail.grade * sdir) * Mathf.Rad2Deg;
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                var p = rail.At(UAt(c.off)); p.y += RailTop;
                float k = c.kind == "loco" ? 0.5f : 1;
                float roll = k * (0.006f * Mathf.Sin(t * 1.9f + i) + 0.003f * Mathf.Sin(t * 5.3f + i * 2)) * Mathf.Min(1, speed / 8);
                c.root.position = p;
                c.root.rotation = Quaternion.Euler(-pitch, yaw, roll * Mathf.Rad2Deg);
            }
        }

        public void Update(float dt, Vector3 camPos, Camera cam)
        {
            if (!active) { fx.Update(dt, cam); return; }
            t += dt; x += speed * dt; odo += speed * dt;
            Place();
            vel = fwd * speed;
            foreach (var c in cars) foreach (var (a, r) in c.axles) a.localRotation = Quaternion.Euler(odo / r * Mathf.Rad2Deg, 0, 0);
            bellSwing = Mathf.Max(0, bellSwing - dt * 0.35f);
            bell.localRotation = Quaternion.Euler(Mathf.Sin(t * 5.2f) * 0.6f * bellSwing * Mathf.Rad2Deg, 0, 0);
            lamp.rotation = Quaternion.LookRotation(lamp.position - camPos);
            foreach (var p in crew) p.Update(dt);
            float beats = odo / DriverR / (Mathf.PI / 2);
            if (Mathf.Floor(beats) != Mathf.Floor(chuffPhase)) Chuff((int)Mathf.Floor(beats) % 2);
            chuffPhase = beats;
            Smoke(dt);
            Dust(dt, camPos);
            fx.Update(dt, cam);
        }

        void Chuff(int side)
        {
            var L = cars[0].root;
            var top = L.TransformPoint(0, 4.4f, 5.25f);
            for (int i = 0; i < 4; i++)
                fx.Spawn(top, vel * 0.6f + new Vector3((Random.value - 0.5f) * 1.6f, 6 + Random.value * 3, (Random.value - 0.5f) * 1.6f),
                    6.5f + Random.value * 3.5f, 1.0f + Random.value * 0.4f, 10 + Random.value * 5, i < 2 ? smokeDark : smokeLight, i < 2 ? 0.72f : 0.5f, 0.35f, 0.8f, -0.35f, 0, 0.015f);
            float sx = side == 1 ? -1 : 1;
            var cock = L.TransformPoint(sx * 1.05f, 0.6f, 5.2f);
            fx.Spawn(cock, vel * 0.75f + L.TransformDirection(Vector3.right * sx) * (2 + Random.value * 1.5f), 0.7f + Random.value * 0.4f, 0.2f, 1.6f, steamCol, 0.32f, 1, 2.6f, -0.5f, 0, 0.02f);
            onChuff?.Invoke(side);
        }

        void Smoke(float dt)
        {
            smokeAcc += dt * 12;
            var L = cars[0].root;
            while (smokeAcc > 1)
            {
                smokeAcc -= 1;
                var top = L.TransformPoint((Random.value - 0.5f) * 0.4f, 4.45f, 5.25f + (Random.value - 0.5f) * 0.4f);
                fx.Spawn(top, vel * 0.55f + new Vector3((Random.value - 0.5f) * 1.2f, 3.5f + Random.value * 2, (Random.value - 0.5f) * 1.2f),
                    7 + Random.value * 4, 1.0f, 11 + Random.value * 6, Random.value < 0.55f ? smokeDark : smokeLight, 0.42f, 0.3f, 0.7f, -0.3f, 0, 0.03f);
            }
        }

        void Dust(float dt, Vector3 camPos)
        {
            dustAcc += dt * 28;
            while (dustAcc > 1)
            {
                dustAcc -= 1;
                var p = rail.At(UAt(Random.value * length));
                if (Vector3.Distance(p, camPos) > 70) continue;
                float sd = Random.value < 0.5f ? -1 : 1;
                p += rail.side * sd * (1.1f + Random.value * 0.6f); p.y += 0.3f;
                fx.Spawn(p, vel * 0.3f + rail.side * sd * (1 + Random.value * 1.5f) + new Vector3(0, 0.4f + Random.value * 0.6f, 0), 1.6f + Random.value, 0.5f, 3, dustCol, 0.28f, 1, 1.8f, 0.1f, 0, 0.1f);
            }
        }

        /// metres along the train's travel occupied: (tail, head)
        public Vector2 Span() => new Vector2(x - length, x);
        public Vector3 RoofPoint(int i, float z) => cars[i].root.TransformPoint(0, cars[i].roofY, z);

        /// nearest hit on any car body
        public (float t, Vector3 point, bool iron)? RayHit(Vector3 o, Vector3 dir, float range)
        {
            if (!active) return null;
            (float, Vector3, bool)? best = null;
            foreach (var c in cars)
            {
                var lo = c.root.InverseTransformPoint(o); var ld = c.root.InverseTransformDirection(dir);
                if (!c.hit.IntersectRay(new Ray(lo, ld), out float tt)) continue;
                if (tt < 0.5f || tt > (best.HasValue ? best.Value.Item1 : range)) continue;
                best = (tt, o + dir * tt, c.kind == "loco");
            }
            return best;
        }

        /// distance from a world point to the nearest part of the train (plan view)
        public float DistanceTo(Vector3 p)
        {
            float dx = p.x - rail.centre.x, dz = p.z - rail.centre.z;
            float u = dx * rail.dir.x + dz * rail.dir.z, lat = Mathf.Abs(dx * rail.side.x + dz * rail.side.z);
            float along = u * sdir; var sp = Span();
            float off = along > sp.y ? along - sp.y : along < sp.x ? sp.x - along : 0;
            return new Vector2(off, Mathf.Max(0, lat - 1.4f)).magnitude;
        }

        public Vector3 LocoPos => cars[0].root.position;

        public void Dispose()
        {
            foreach (var p in crew) p.anim.Dispose();
            fx.Dispose();
            if (root) Object.Destroy(root);
            foreach (var o in owned) if (o) Object.Destroy(o);
        }
    }
}
