// Particle pools (alpha smoke/dust + additive flashes/tracers), each one dynamic mesh of camera-facing
// quads rebuilt per frame, plus a single muzzle-flash point light. Ported from game/fx.js.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Schofield
{
    public class Pool
    {
        struct P { public Vector3 p, v, dir; public float life, max, s0, s1, a, rot, vr, drag, grav, streak, fadeIn; public Color c; public bool hasDir; }
        readonly List<P> ps = new List<P>();
        readonly int max;
        readonly Mesh mesh; readonly Material mat;
        Vector3[] verts; Color[] cols; Vector2[] uvs; int[] tris;

        public Pool(int max, bool additive)
        {
            this.max = max;
            mesh = new Mesh { name = additive ? "fx add" : "fx smoke" };
            mesh.MarkDynamic();
            verts = new Vector3[max * 4]; cols = new Color[max * 4]; uvs = new Vector2[max * 4]; tris = new int[max * 6];
            for (int i = 0; i < max; i++)
            {
                uvs[i * 4] = new Vector2(0, 0); uvs[i * 4 + 1] = new Vector2(1, 0); uvs[i * 4 + 2] = new Vector2(1, 1); uvs[i * 4 + 3] = new Vector2(0, 1);
            }
            mat = new Material(Assets.Shader("Particle"));
            mat.SetFloat("_Src", additive ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            mat.SetFloat("_Dst", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_Additive", additive ? 1 : 0);
            mat.SetTexture("_Noise", Assets.Tex("cloud_noise"));
            mat.renderQueue = additive ? 3010 : 3000;
        }

        public void Spawn(Vector3 pos, Vector3 vel = default, float life = 1, float size = 1, float size1 = -1, Color? color = null, float alpha = 1,
            float spin = 1, float drag = 1.5f, float grav = 0, float streak = 0, float fadeIn = 0.1f, Vector3? dir = null)
        {
            if (ps.Count >= max) ps.RemoveAt(0);
            ps.Add(new P
            {
                p = pos, v = vel, life = 0, max = life, s0 = size, s1 = size1 >= 0 ? size1 : size * 2, c = color ?? Color.white, a = alpha,
                rot = Random.value * 6.28f, vr = (Random.value - 0.5f) * spin, drag = drag, grav = grav, streak = streak, fadeIn = fadeIn,
                dir = dir ?? Vector3.zero, hasDir = dir.HasValue,
            });
        }

        public void Update(float dt, Camera cam)
        {
            var cr = cam.transform.right; var cu = cam.transform.up; var cf = cam.transform.forward;
            int n = 0;
            for (int i = 0; i < ps.Count; i++)
            {
                var q = ps[i];
                q.life += dt;
                if (q.life >= q.max) continue;
                float k = Mathf.Exp(-q.drag * dt);
                q.v.x *= k; q.v.y = q.v.y * k - q.grav * dt; q.v.z *= k;
                q.p += q.v * dt;
                q.rot += q.vr * dt;
                float t = q.life / q.max;
                float a = q.a * Mathf.Min(1, q.life / Mathf.Max(0.001f, q.fadeIn * q.max)) * (1 - t) * (1 - t * 0.3f);
                float size = q.s0 + (q.s1 - q.s0) * Mathf.Sqrt(t);
                Vector3 ax, ay;
                if (q.streak > 0)
                {
                    var d = q.hasDir ? q.dir : q.v;
                    if (d.sqrMagnitude < 1e-8f) d = cf;
                    d.Normalize();
                    // streak along the direction, as wide as `size`, turned to face the camera
                    var side = Vector3.Cross(d, cf); if (side.sqrMagnitude < 1e-6f) side = cr; side.Normalize();
                    ax = d * q.streak * 0.5f; ay = side * size * 0.5f;
                }
                else
                {
                    float c = Mathf.Cos(q.rot), s = Mathf.Sin(q.rot);
                    ax = (cr * c + cu * s) * size * 0.5f; ay = (-cr * s + cu * c) * size * 0.5f;
                }
                int v = n * 4;
                verts[v] = q.p - ax - ay; verts[v + 1] = q.p + ax - ay; verts[v + 2] = q.p + ax + ay; verts[v + 3] = q.p - ax + ay;
                var col = new Color(q.c.r, q.c.g, q.c.b, a);
                cols[v] = cols[v + 1] = cols[v + 2] = cols[v + 3] = col;
                int ti = n * 6;
                tris[ti] = v; tris[ti + 1] = v + 1; tris[ti + 2] = v + 2; tris[ti + 3] = v; tris[ti + 4] = v + 2; tris[ti + 5] = v + 3;
                ps[n] = q; n++;
            }
            ps.RemoveRange(n, ps.Count - n);
            for (int i = n * 6; i < tris.Length; i++) tris[i] = 0;
            mesh.Clear();
            if (n == 0) return;
            mesh.vertices = verts; mesh.colors = cols; mesh.uv = uvs;
            mesh.SetTriangles(tris, 0, false);
            mesh.bounds = new Bounds(cam.transform.position, Vector3.one * 4000);
            Graphics.RenderMesh(new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false }, mesh, 0, Matrix4x4.identity);
        }

        public void Clear() => ps.Clear();
        public void Dispose() { Object.Destroy(mesh); Object.Destroy(mat); }
    }

    public class FX
    {
        public readonly Pool smoke = new Pool(900, false), add = new Pool(300, true);
        readonly Light flash; float lightT;
        readonly Color dustCol, smokeCol = new Color(0.85f, 0.83f, 0.8f);

        public FX(Route route, Transform parent)
        {
            var go = new GameObject("MuzzleLight"); go.transform.SetParent(parent, false);
            flash = go.AddComponent<Light>();
            flash.type = LightType.Point; flash.color = new Color(1, 0.69f, 0.38f); flash.range = 6; flash.intensity = 0; flash.shadows = LightShadows.None;
            var L = route.def.light;
            dustCol = Color.Lerp(Color.Lerp(L.ground, L.sunColor, 0.35f), Color.white, 0.25f);
        }

        static Vector3 R3(float x, float y, float z) => new Vector3((Random.value - 0.5f) * x, Random.value * y, (Random.value - 0.5f) * z);

        public void Muzzle(Vector3 pos, Vector3 dir, bool big = false, bool light = false)
        {
            float s = big ? 1.1f : 0.6f;
            add.Spawn(pos, life: 0.06f, size: s, size1: s * 1.3f, color: new Color(1, 0.85f, 0.6f), spin: 0, fadeIn: 0);
            add.Spawn(pos + dir * s * 0.35f, life: 0.05f, size: s * 0.35f, streak: s * 0.9f, dir: dir, color: new Color(1, 0.7f, 0.35f), fadeIn: 0);
            for (int i = 0; i < (big ? 7 : 4); i++)
                smoke.Spawn(pos, dir * (2 + Random.value * (big ? 7 : 4)) + R3(1.5f, 0.8f, 1.5f), life: 1.4f + Random.value, size: 0.25f, size1: big ? 2.2f : 1.4f, color: smokeCol, alpha: 0.5f, drag: 2.4f, grav: -0.25f);
            if (light) { flash.transform.position = pos; flash.range = 6; flash.intensity = big ? 6 : 3.5f; lightT = 0.05f; }
        }

        public void Tracer(Vector3 from, Vector3 to)
        {
            var d = to - from; float len = d.magnitude; if (len < 0.1f) return;
            var dir = d / len; const float speed = 420;
            add.Spawn(from + dir * 2, dir * speed, life: Mathf.Min(0.35f, len / speed), size: 0.03f, streak: 3.2f, dir: dir, color: new Color(1, 0.8f, 0.5f), alpha: 0.7f, drag: 0, fadeIn: 0);
        }

        public void Dust(Vector3 pos, int amount = 6, float size = 0.6f, Color? color = null, float up = 1.5f, float spread = 2)
        {
            for (int i = 0; i < amount; i++)
                smoke.Spawn(pos + new Vector3((Random.value - 0.5f) * 0.3f, 0.05f, (Random.value - 0.5f) * 0.3f),
                    new Vector3((Random.value - 0.5f) * spread, Random.value * up + 0.3f, (Random.value - 0.5f) * spread),
                    life: 0.9f + Random.value * 0.9f, size: size, size1: size * 3.5f, color: color ?? dustCol, alpha: 0.6f, drag: 3, grav: 0.8f);
            for (int i = 0; i < amount; i++)
                smoke.Spawn(pos, new Vector3((Random.value - 0.5f) * 4, 3 + Random.value * 4, (Random.value - 0.5f) * 4), life: 0.6f, size: 0.06f, size1: 0.05f, color: new Color(0.3f, 0.24f, 0.18f), drag: 0.5f, grav: 14);
        }

        public void Blood(Vector3 pos, Vector3 dir)
        {
            for (int i = 0; i < 7; i++)
                smoke.Spawn(pos, dir * (1.5f + Random.value * 2.5f) + R3(1.5f, 1.2f, 1.5f), life: 0.45f + Random.value * 0.3f, size: 0.12f, size1: 0.45f, color: new Color(0.32f, 0.02f, 0.02f), alpha: 0.85f, drag: 3, grav: 6);
        }

        public void Splinters(Vector3 pos, Vector3 dir)
        {
            for (int i = 0; i < 8; i++)
                smoke.Spawn(pos, dir * (-2 - Random.value * 3) + R3(3, 3, 3), life: 0.7f, size: 0.07f, size1: 0.05f, color: new Color(0.45f, 0.33f, 0.2f), drag: 0.8f, grav: 12, spin: 20);
            Dust(pos, 2, 0.25f, null, 0.6f);
        }

        public void Sparks(Vector3 pos)
        {
            for (int i = 0; i < 5; i++)
                add.Spawn(pos, new Vector3((Random.value - 0.5f) * 8, Random.value * 6, (Random.value - 0.5f) * 8), life: 0.25f, size: 0.03f, streak: 0.25f, color: new Color(1, 0.75f, 0.4f), drag: 1, grav: 9.8f, fadeIn: 0);
        }

        /// dynamite: a fireball, a ring of dust, sparks thrown out, and a big flash
        public void Explosion(Vector3 pos, float radius = 15)
        {
            var up = pos + new Vector3(0, 0.7f, 0);
            add.Spawn(up, life: 0.24f, size: 6, size1: 11, color: new Color(1, 0.77f, 0.38f), spin: 0, fadeIn: 0);
            add.Spawn(up, life: 0.12f, size: 2.4f, size1: 3.8f, color: new Color(1, 0.97f, 0.78f), spin: 0, fadeIn: 0);
            for (int i = 0; i < 10; i++)
                smoke.Spawn(up + R3(3, 2, 3), R3(6, 5, 6), life: 2.5f + Random.value * 2, size: 2.5f, size1: 9, color: new Color(0.25f, 0.22f, 0.2f), alpha: 0.7f, drag: 1.6f, grav: -0.6f, fadeIn: 0.05f);
            Dust(pos, 30, 1.8f, null, 8, 12);
            for (int i = 0; i < 32; i++)
            {
                float a = Random.value * Mathf.PI * 2, sp = 7 + Random.value * 11;
                var vel = new Vector3(Mathf.Cos(a) * sp, 2 + Random.value * 9, Mathf.Sin(a) * sp);
                add.Spawn(pos, vel, life: 0.28f + Random.value * 0.3f, size: 0.055f, size1: 0.025f, streak: 0.8f, dir: vel, color: new Color(1, 0.48f + Random.value * 0.3f, 0.12f), drag: 1.2f, grav: 14, fadeIn: 0);
            }
            flash.transform.position = up; flash.range = radius * 2.5f; flash.intensity = 40; lightT = 0.18f;
        }

        public void Glint(Vector3 pos, float strength = 1)
        {
            add.Spawn(pos, life: 0.12f, size: 1.6f * strength, size1: 1.6f * strength, color: new Color(1, 0.95f, 0.8f), spin: 0, fadeIn: 0);
        }

        /// dust kicked up by hooves and wheels
        public void Trail(Vector3 pos, float rate, float dt, float size = 0.9f)
        {
            float n = rate * dt;
            int k = Mathf.FloorToInt(n) + (Random.value < n % 1 ? 1 : 0);
            while (k-- > 0)
                smoke.Spawn(pos + new Vector3((Random.value - 0.5f) * 5, size * 0.6f, (Random.value - 0.5f) * 5),
                    new Vector3((Random.value - 0.5f) * 1.5f, 0.2f + Random.value * 0.4f, (Random.value - 0.5f) * 1.5f),
                    life: 3 + Random.value * 2, size: size, size1: size * 8, color: dustCol, alpha: 0.18f, drag: 1.2f, grav: -0.03f, fadeIn: 0.25f);
        }

        public void Update(float dt, Camera cam)
        {
            smoke.Update(dt, cam); add.Update(dt, cam);
            if (lightT > 0) { lightT -= dt; if (lightT <= 0) flash.intensity = 0; }
        }

        public void Dispose() { smoke.Dispose(); add.Dispose(); if (flash) Object.Destroy(flash.gameObject); }
    }
}
