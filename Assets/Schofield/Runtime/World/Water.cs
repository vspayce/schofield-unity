// Thunder Gorge water: the river on the valley floor (a ribbon that follows the route, its surface
// anchored to the deepest point of the channel), the falls that pour off a stone arch across the road
// (a curtain of streaked sheets and the churning pool at its foot), mist and spray, the roar, and the
// moment the coach bursts through — a splash, a jolt, and water running down the lens.
// Ported from world/water.js and world/fallsrock.js.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Schofield
{
    public class Water
    {
        const float LStep = 2;

        class Fall
        {
            public float s, d, lipY, botY, drop, halfW, span, phiRoad, cx, cz, ax, az, nx, nz, arc, thick = 3.2f;
            public readonly List<Vector3> drips = new List<Vector3>();
            public float Lat(float phi) => span * (Mathf.Clamp01(phi) - phiRoad) / (1 - phiRoad);
            public Vector3 At(float phi)
            {
                float l = Lat(phi), a = arc * Mathf.Sqrt(Mathf.Clamp01(phi));
                return new Vector3(cx + ax * l + nx * a, lipY - drop * Mathf.Clamp01(phi), cz + az * l + nz * a);
            }
        }

        readonly Route route;
        readonly Simplex2D noise;
        readonly GameObject root;
        readonly List<Object> owned = new List<Object>();
        readonly float riverFrom, riverTo, riverD, riverW, riverDepth;
        float[] level, rapid; int lN;
        readonly List<(GameObject go, Vector3 c, float r)> chunks = new List<(GameObject, Vector3, float)>();
        readonly List<Fall> falls = new List<Fall>();
        readonly Pool mist;
        readonly Color foamCol, mistCol;
        float emitAcc, roadAcc, coachAcc, rapidAcc, dripAcc;
        bool coachIn;
        public float wet, sheet;
        public System.Action<float> onBurst;

        // lens + sound
        readonly Canvas lensCanvas; readonly RawImage lensStreaks, lensDirt; readonly Image lensVeil;
        readonly AudioSource roar, splash; readonly AudioLowPassFilter roarLp;

        public Water(Route route, Transform parent)
        {
            this.route = route;
            var L = route.def.light;
            noise = new Simplex2D(route.def.seed * 13 + 7);
            root = new GameObject("Water");
            root.transform.SetParent(parent, false);
            riverFrom = 0; riverTo = route.len; riverD = 48 * route.riverSide; riverW = 24; riverDepth = 2.8f;
            BuildLevel();

            foamCol = Color.Lerp(MathX.Hex("#eef6f6"), L.sunColor, 0.18f);
            mistCol = Color.Lerp(foamCol, L.hemiSky, 0.25f);
            Material Mat(float kind)
            {
                var m = new Material(Assets.Shader("Water"));
                m.SetTexture("_Noise", Assets.Tex("cloud_noise"));
                m.SetFloat("_Kind", kind);
                m.SetColor("_Shallow", Color.Lerp(MathX.Hex("#86a894"), L.sunColor, 0.3f));
                m.SetColor("_Deep", MathX.Hex("#0b2a2e"));
                m.SetColor("_FoamCol", foamCol);
                m.SetColor("_Zenith", L.zenith); m.SetColor("_Horizon", L.horizon);
                m.SetFloat("_SpecPow", Application.isMobilePlatform ? 200 : 380);
                m.renderQueue = 2990 + (int)kind;
                owned.Add(m);
                return m;
            }
            BuildRiver(Mat(0));
            if (route.hasFalls) BuildFalls(Mat(1), Mat(2));

            mist = new Pool(Application.isMobilePlatform ? 160 : 260, false);
            foreach (var f in falls) for (int i = 0; i < 18; i++) MistPuff(f);

            // lens water (above the world, under the HUD)
            lensCanvas = UIKit.Canvas("Lens", 5);
            lensCanvas.transform.SetParent(root.transform, false);
            var full = UIKit.Fill("Full", lensCanvas.transform);
            lensVeil = full.gameObject.AddComponent<Image>(); lensVeil.color = new Color(0.86f, 0.92f, 0.94f, 0); lensVeil.raycastTarget = false;
            lensDirt = UIKit.Fill("Drops", lensCanvas.transform).gameObject.AddComponent<RawImage>();
            lensDirt.texture = Assets.Tex("lens_dirt"); lensDirt.color = new Color(0.9f, 0.96f, 1, 0); lensDirt.raycastTarget = false;
            lensStreaks = UIKit.Fill("Streaks", lensCanvas.transform).gameObject.AddComponent<RawImage>();
            var st = StreakTex(); owned.Add(st);
            lensStreaks.texture = st; lensStreaks.color = new Color(1, 1, 1, 0); lensStreaks.raycastTarget = false;
            lensStreaks.uvRect = new Rect(0, 0, 3, 1);

            // the roar: looped brown noise through a low-pass that opens up as you close in
            var clip = NoiseClip(); owned.Add(clip);
            var ro = new GameObject("Falls Roar"); ro.transform.SetParent(root.transform, false);
            roar = ro.AddComponent<AudioSource>(); roar.clip = clip; roar.loop = true; roar.volume = 0; roar.spatialBlend = 0; roar.Play();
            roarLp = ro.AddComponent<AudioLowPassFilter>(); roarLp.cutoffFrequency = 400;
            var sp = new GameObject("Splash"); sp.transform.SetParent(root.transform, false);
            splash = sp.AddComponent<AudioSource>(); splash.clip = clip; splash.spatialBlend = 0; splash.volume = 0;
            sp.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1800;
        }

        // ------------------------------------------------------------- water level
        // The surface follows the deepest point across the channel, smoothed within each reach (a sheer step
        // survives as a fall) and raised by the depth.
        void BuildLevel()
        {
            int n = Mathf.Max(2, Mathf.CeilToInt((riverTo - riverFrom) / LStep) + 1);
            lN = n;
            var floor = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = riverFrom + i * LStep;
                var fr = route.Frame(s);
                float lo = float.MaxValue;
                for (int j = 0; j < 7; j++)
                {
                    float dd = riverD + (j / 6f - 0.5f) * riverW * 0.8f;
                    lo = Mathf.Min(lo, route.Height(fr.x + fr.rx * dd, fr.z + fr.rz * dd));
                }
                floor[i] = lo;
            }
            var cut = new bool[n];
            for (int i = 1; i < n; i++) if (floor[i - 1] - floor[i] > 2.5f) cut[i] = true;
            const int win = 14;
            float[] a = floor, b = new float[n];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    float acc = a[i]; int cnt = 1;
                    for (int k = 1; k <= win; k++) { int j = i + k; if (j >= n || cut[j]) break; acc += a[j]; cnt++; }
                    for (int k = 1; k <= win; k++) { int j = i - k; if (j < 0 || cut[j + 1]) break; acc += a[j]; cnt++; }
                    b[i] = acc / cnt;
                }
                (a, b) = (b, a);
            }
            for (int i = 1; i < n; i++) a[i] = Mathf.Min(a[i], a[i - 1] + 0.04f * LStep);
            for (int i = 0; i < n; i++) a[i] += riverDepth;
            for (int i = 1; i < n; i++)
            {
                if (!cut[i]) continue;
                for (int k = 1; k <= 5; k++) { int j = i - k; if (j < 0) break; a[j] -= 0.45f * (1 - (k - 1) / 5f); }
            }
            level = a;
            rapid = new float[n];
            for (int i = 0; i < n; i++)
            {
                int i0 = Mathf.Max(0, i - 2), i1 = Mathf.Min(n - 1, i + 2);
                float g = (a[i0] - a[i1]) / ((i1 - i0) * LStep);
                float patch = noise.Fbm((riverFrom + i * LStep) / 70, 3.3f, 3) * 0.5f + 0.5f;
                rapid[i] = Mathf.Clamp01(MathX.Smoothstep(0.018f, 0.09f, g) * (0.25f + 1.05f * MathX.Smoothstep(0.35f, 0.8f, patch)));
            }
        }

        public float LevelAt(float s) => Sample(level, s);
        public float RapidsAt(float s) => Sample(rapid, s);
        float Sample(float[] arr, float s)
        {
            float f = Mathf.Clamp((s - riverFrom) / LStep, 0, lN - 1);
            int i = Mathf.Min(lN - 2, (int)f);
            return Mathf.Lerp(arr[i], arr[i + 1], f - i);
        }

        // ------------------------------------------------------------ river ribbon
        // ~250 m chunks; the outer column of each row is dropped under the surface so the seam hides in the bank
        void BuildRiver(Material mat)
        {
            const float step = 4; const int COLS = 9;
            int rows = Mathf.Max(2, Mathf.FloorToInt((riverTo - riverFrom) / step) + 1);
            int CH = Mathf.RoundToInt(250 / step);
            for (int r0 = 0; r0 < rows - 1; r0 += CH)
            {
                int r1 = Mathf.Min(rows - 1, r0 + CH), nr = r1 - r0 + 1;
                var v = new Vector3[nr * COLS]; var u0 = new Vector2[v.Length]; var u1 = new Vector2[v.Length]; var u2 = new Vector2[v.Length];
                for (int i = 0; i < nr; i++)
                {
                    float s = Mathf.Min(riverTo, riverFrom + (r0 + i) * step);
                    var fr = route.Frame(s);
                    float lv = LevelAt(s), rp = RapidsAt(s);
                    float meander = noise.Noise(s / 110, 1.9f) * riverW * 0.12f;
                    float w = riverW * (1 + noise.Noise(s / 170 + 7, 4.4f) * 0.16f);
                    for (int j = 0; j < COLS; j++)
                    {
                        int p = i * COLS + j;
                        float u = j / (COLS - 1f), e = Mathf.Abs(u * 2 - 1);
                        bool skirt = j == 0 || j == COLS - 1;
                        float dd = riverD + meander + (u - 0.5f) * w;
                        float x = fr.x + fr.rx * dd, z = fr.z + fr.rz * dd;
                        float bed = route.Height(x, z);
                        v[p] = new Vector3(x, lv - (skirt ? 0.8f : 0), z);
                        u0[p] = new Vector2(s, e); u1[p] = new Vector2(lv - bed, rp); u2[p] = new Vector2(fr.tx, fr.tz);
                    }
                }
                var tris = new int[(nr - 1) * (COLS - 1) * 6]; int k2 = 0;
                for (int bb = 0; bb < nr - 1; bb++) for (int a = 0; a < COLS - 1; a++)
                    {
                        int v0 = bb * COLS + a, v1 = v0 + 1, v2 = v0 + COLS, v3 = v2 + 1;
                        tris[k2++] = v0; tris[k2++] = v2; tris[k2++] = v1; tris[k2++] = v1; tris[k2++] = v2; tris[k2++] = v3;
                    }
                var m = new Mesh { name = "River" };
                m.vertices = v; m.uv = u0; m.uv2 = u1; m.SetUVs(2, u2); m.triangles = tris;
                var nrm = new Vector3[v.Length]; for (int q = 0; q < nrm.Length; q++) nrm[q] = Vector3.up; m.normals = nrm;
                m.RecalculateBounds(); owned.Add(m);
                var go = MeshKit.Show("River", m, mat, root.transform, false);
                chunks.Add((go, m.bounds.center, m.bounds.extents.magnitude));
            }
        }

        // ------------------------------------------------------------- the falls
        Fall Resolve(float s, float d, float width, float drop, float top)
        {
            s = Mathf.Clamp(s, 0, route.len);
            var fr = route.Frame(s);
            float poolY = float.MaxValue;
            for (int k = -4; k <= 12; k += 2) poolY = Mathf.Min(poolY, LevelAt(s + k));
            float roadY = route.RoadHeightAt(s);
            float lipY = roadY + top;                              // ref: 'road'
            float botY = Mathf.Max(lipY - drop, poolY - 0.5f);
            drop = Mathf.Max(2, lipY - botY);
            // lean: march toward the river until the ground has fallen to the foot of the fall
            float side = Mathf.Sign(riverD - d); if (side == 0) side = 1;
            float span = 0;
            if (Mathf.Abs(lipY - botY) > 4)
            {
                for (float k = 2; k <= 60; k += 1.5f)
                {
                    float dd = d + side * k;
                    if (route.Height(fr.x + fr.rx * dd, fr.z + fr.rz * dd) <= botY + 0.6f) { span = side * k; break; }
                }
                span = Mathf.Clamp(span, -44, 44);
            }
            return new Fall
            {
                s = s, d = d, lipY = lipY, botY = botY, drop = drop, halfW = width * 0.5f, span = span,
                phiRoad = Mathf.Clamp((lipY - roadY) / drop, 0.03f, 0.75f),
                cx = fr.x + fr.rx * d, cz = fr.z + fr.rz * d, ax = fr.rx, az = fr.rz, nx = fr.tx, nz = fr.tz,
                arc = Mathf.Min(5.5f, 1.1f + drop * 0.075f),
            };
        }

        void BuildFalls(Material curtainMat, Material poolMat)
        {
            var f = Resolve(route.fallsS, 0, route.fallsWidth, route.fallsDrop + 5, 11);
            falls.Add(f);
            BuildArch(f);
            const int SHELLS = 3, ROWS = 12, LIP = 2, COLS = 15, VR = ROWS + LIP + 1;
            var v = new List<Vector3>(); var u0 = new List<Vector2>(); var u1 = new List<Vector2>(); var u2 = new List<Vector2>(); var t = new List<int>();
            for (int k = 0; k < SHELLS; k++)
            {
                float nOff = (k - (SHELLS - 1) / 2f) * 0.85f, phase = k * 0.41f, weight = k == SHELLS / 2 ? 1 : 0.72f;
                float arc = f.arc * (1 + (k - (SHELLS - 1) / 2f) * 0.12f);
                int v0 = v.Count;
                for (int r = -LIP; r <= ROWS; r++)
                {
                    float lipT = r < 0 ? -r / (float)LIP : 0;
                    float phi = r < 0 ? -0.001f * -r : r / (float)ROWS;
                    float y, along, fallM;
                    if (r < 0) { y = f.lipY + 0.55f * Mathf.Pow(lipT, 1.6f); along = -4.2f * Mathf.Pow(lipT, 1.25f); fallM = -3 * lipT; }
                    else { y = f.lipY - f.drop * phi; along = arc * Mathf.Sqrt(phi); fallM = f.drop * phi; }
                    float lean = f.Lat(phi), pp = Mathf.Max(0, phi);
                    for (int c = 0; c <= COLS; c++)
                    {
                        float u = c / (float)COLS, e = Mathf.Abs(u * 2 - 1);
                        float lat = (u - 0.5f) * f.halfW * 2 * (1 + 0.18f * pp);
                        float nA = noise.Noise(u * 5.5f + k * 13, pp * 5 + 2.1f), nB = noise.Noise(u * 2.1f - k * 7, pp * 2.4f - 5.7f);
                        float nL = noise.Noise(u * 4.3f + 17.1f, 3.3f), nL2 = noise.Noise(u * 9.1f - 4.2f, 8.8f);
                        float yy = y + nA * 0.55f * pp - e * e * 0.6f * pp + nL * 0.9f + nL2 * 0.35f;
                        float aa = along + nOff + nB * 1.2f * pp + e * e * 1.6f * pp + nL * 0.9f;
                        float ll = lat + lean;
                        v.Add(new Vector3(f.cx + f.ax * ll + f.nx * aa, yy, f.cz + f.az * ll + f.nz * aa));
                        u0.Add(new Vector2(lat, fallM)); u1.Add(new Vector2(phi, e)); u2.Add(new Vector2(phase, weight));
                    }
                }
                for (int r = 0; r < VR - 1; r++) for (int c = 0; c < COLS; c++)
                    {
                        int a = v0 + r * (COLS + 1) + c, b = a + 1, d2 = a + COLS + 1, e2 = d2 + 1;
                        t.AddRange(new[] { a, d2, b, b, d2, e2 });
                    }
            }
            var m = new Mesh { name = "Falls" };
            m.SetVertices(v); m.SetUVs(0, u0); m.SetUVs(1, u1); m.SetUVs(2, u2); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds(); owned.Add(m);
            MeshKit.Show("Falls", m, curtainMat, root.transform, false);

            // the churning pool where the sheet lands
            const int RING = 6, SEG = 22;
            float pRad = Mathf.Max(6, f.halfW * 1.35f);
            var foot = f.At(1);
            var pv = new List<Vector3>(); var pu0 = new List<Vector2>(); var pu1 = new List<Vector2>(); var pt = new List<int>();
            for (int ri = 0; ri <= RING; ri++)
            {
                float r01 = ri / (float)RING, rm = r01 * pRad;
                for (int si = 0; si <= SEG; si++)
                {
                    float ang = si / (float)SEG * Mathf.PI * 2;
                    float lx = Mathf.Cos(ang) * rm, lz = Mathf.Sin(ang) * rm;
                    pv.Add(new Vector3(foot.x + f.ax * lx + f.nx * lz, f.botY + (1 - r01) * 0.35f, foot.z + f.az * lx + f.nz * lz));
                    pu0.Add(new Vector2(r01, ang / (Mathf.PI * 2))); pu1.Add(new Vector2(rm, 0));
                }
            }
            for (int ri = 0; ri < RING; ri++) for (int si = 0; si < SEG; si++)
                {
                    int a = ri * (SEG + 1) + si, b = a + 1, d2 = a + SEG + 1, e2 = d2 + 1;
                    pt.AddRange(new[] { a, d2, b, b, d2, e2 });
                }
            var pm = new Mesh { name = "Falls pool" };
            pm.SetVertices(pv); pm.SetUVs(0, pu0); pm.SetUVs(1, pu1); pm.SetTriangles(pt, 0);
            pm.RecalculateNormals(); pm.RecalculateBounds(); owned.Add(pm);
            MeshKit.Show("Pool", pm, poolMat, root.transform, false);
        }

        // The rock the falls pour off: a natural arch over the road, built from lumpy boulders merged into one
        // mesh — the stream bed on top, banks either side, the inland abutment and an outer pier.
        void BuildArch(Fall f)
        {
            var rnd = new Rng(route.def.seed * 31 + 5);
            var nz = new Simplex2D(route.def.seed * 7 + 3);
            float roadY = route.RoadHeightAt(f.s);
            float lip = f.lipY - roadY;
            float lipL = f.Lat(0) - f.halfW, lipR = f.Lat(0) + f.halfW;
            float side = Mathf.Sign(f.span); if (side == 0) side = 1;
            float inner = side > 0 ? lipL : -lipR, outer = side > 0 ? lipR : -lipL;
            Vector3 World(float d, float y, float n) => new Vector3(f.cx + f.ax * d * side + f.nx * n, roadY + y, f.cz + f.az * d * side + f.nz * n);
            float Ground(float d, float n = -4) { var p = World(d, 0, n); return route.Height(p.x, p.z) - roadY; }
            var rocks = new List<(float d, float y, float n, float r, float sy)>();
            void Add(float d, float y, float n, float r, float sy = 1) => rocks.Add((d, y, n, r, sy));

            for (float d = inner - 1; d <= outer + 1; d += 2.2f + rnd.Next() * 0.5f)
            {
                foreach (var (n, lift) in new[] { (-2.6f, 0f), (-5.4f, 0.25f), (-8.3f, 0.6f) })
                {
                    float r = 2.3f + rnd.Next() * 0.7f;
                    Add(d + (rnd.Next() - 0.5f) * 0.8f, lip - 0.2f + lift - r * 0.72f * 0.97f, n + (rnd.Next() - 0.5f) * 0.6f, r, 0.72f);
                }
                if (rnd.Next() < 0.7f)
                {
                    float r = 1.4f + rnd.Next() * 0.8f, y = lip - 3.2f - rnd.Next() * 0.8f;
                    if (Mathf.Abs(d) > 6 || y - r * 0.8f > 5.6f) Add(d, y, -3.5f - rnd.Next() * 4, r, 0.8f);
                }
            }
            foreach (var (d0, d1) in new[] { (inner - 6, inner - 1.5f), (outer + 1.5f, outer + 5) })
                for (float d = d0; d <= d1; d += 1.8f)
                    foreach (float n in new[] { -2.4f, -5.6f, -8.8f })
                    {
                        float r = 2.4f + rnd.Next() * 0.9f;
                        Add(d, lip + 0.6f + rnd.Next() * 1.4f - r * 0.4f, n + (rnd.Next() - 0.5f), r, 0.85f);
                    }
            for (float d = inner - 1.5f; d > inner - 26; d -= 2.4f)
            {
                float g = Ground(d);
                float topY = Mathf.Max(lip + 1.2f, g + 1.5f);
                if (g > lip + 3) break;
                for (float y = g - 1.2f; y < topY; y += 2.3f)
                    foreach (float n in new[] { -2.5f, -5.6f, -8.7f }) Add(d + (rnd.Next() - 0.5f) * 1.2f, y, n + (rnd.Next() - 0.5f), 2.4f + rnd.Next() * 0.9f, 0.9f);
            }
            float pierD = outer + 5.5f, foot = float.MaxValue;
            for (float d = pierD - 4; d <= pierD + 4; d += 1) foot = Mathf.Min(foot, Ground(d));
            foot = Mathf.Max(foot, f.botY - roadY + 1);
            for (float y = foot - 1.5f; y < lip + 1.5f; y += 2.2f)
            {
                float taper = 1 - 0.18f * Mathf.Clamp01((lip - y) / Mathf.Max(0.01f, lip - foot));
                foreach (float n in new[] { -2.8f, -6.2f }) Add(pierD + (rnd.Next() - 0.5f) * 2.2f, y, n + (rnd.Next() - 0.5f), (2.6f + rnd.Next() * 0.8f) * (0.8f + 0.3f * taper), 0.9f);
                if (rnd.Next() < 0.5f) Add(pierD + 2.5f + rnd.Next() * 1.5f, y, -4.5f, 2 + rnd.Next(), 0.9f);
            }

            var (iv, it) = MeshKit.Icosphere(Application.isMobilePlatform ? 1 : 2);
            var B = new MeshKit.Builder();
            var verts = new Vector3[iv.Length];
            Color wetCol = new Color(0.66f, 0.7f, 0.72f), moss = new Color(0.72f, 0.86f, 0.56f);
            foreach (var R in rocks)
            {
                float ox = rnd.Next() * 50, oy = rnd.Next() * 50;
                var q = Quaternion.Euler(rnd.Next() * 360, rnd.Next() * 360, rnd.Next() * 360);
                var c0 = World(R.d, R.y, R.n);
                for (int j = 0; j < iv.Length; j++)
                {
                    var p = iv[j];
                    float a = nz.Noise(p.x * 1.1f + ox, p.y * 1.1f + p.z * 0.7f + oy);
                    float b = nz.Noise(p.z * 2.7f - oy, p.x * 2.7f + p.y * 1.3f + ox);
                    float facet = Mathf.Round(a * 3) / 3;
                    p *= 1 + facet * 0.15f + a * 0.1f + b * 0.07f;
                    p = q * p;
                    p = new Vector3(p.x * R.r, p.y * R.r * R.sy, p.z * R.r);
                    verts[j] = c0 + new Vector3(f.ax * p.x * side + f.nx * p.z, p.y, f.az * p.x * side + f.nz * p.z);
                }
                // colour: wet where the water runs, moss on top, darker underneath
                for (int i = 0; i < it.Length; i += 3)
                {
                    Vector3 A0 = verts[it[i]], A1 = verts[it[i + 1]], A2 = verts[it[i + 2]];
                    var nn = Vector3.Cross(A1 - A0, A2 - A0).normalized;
                    var cen = (A0 + A1 + A2) / 3;
                    float dx = cen.x - f.cx, dz = cen.z - f.cz;
                    float ld = (dx * f.ax + dz * f.az) * side, ln = dx * f.nx + dz * f.nz, yRel = cen.y - roadY;
                    bool inCh = ld > inner - 0.5f && ld < outer + 0.5f;
                    bool isWet = (inCh && yRel > lip - 1.2f && ln > -9) || (inCh && ln > -1.2f) || Mathf.Abs(ld - inner) < 1.2f || Mathf.Abs(ld - outer) < 1.2f;
                    var col = Color.white;
                    if (nn.y > 0.55f && !isWet) col = Color.Lerp(col, moss, 0.55f * (nn.y - 0.55f) / 0.45f + 0.2f);
                    if (isWet) col *= wetCol;
                    if (nn.y < -0.3f) col *= 0.72f + 0.28f * (1 + nn.y);
                    col *= 0.86f + nz.Noise(cen.x * 0.35f, cen.z * 0.35f + cen.y * 0.2f) * 0.14f; col.a = 1;
                    B.Flat(new[] { A0, A1, A2 }, new[] { 0, 1, 2 }, col, RockArches.TriplanarUV);
                }
            }
            var mesh = B.Build("Falls arch"); owned.Add(mesh);
            var mat = Assets.Lit(Color.Lerp(route.def.terrain.rockTint, Color.white, 0.55f), 0.08f);
            mat.SetTexture("_BaseMap", Assets.Tex("rock"));
            owned.Add(mat);
            MeshKit.Show("Falls Arch", mesh, mat, root.transform);
            foreach (var R in rocks)
                if (R.y < lip - 1.5f && R.d > inner && R.d < outer) f.drips.Add(World(R.d, R.y - R.r * R.sy * 0.8f, R.n));
        }

        // ----------------------------------------------------------------- spray
        Vector3 OnSheet(Fall f, float phi, float spread = 1)
        {
            var p = f.At(phi);
            float a = (Random.value - 0.5f) * f.halfW * 1.9f * spread, b = (Random.value - 0.3f) * f.halfW * 0.9f * spread;
            return p + new Vector3(f.ax * a + f.nx * b, 0, f.az * a + f.nz * b);
        }

        void MistPuff(Fall f)
        {
            float phi = 1 - Random.value * 0.4f;
            mist.Spawn(OnSheet(f, phi), new Vector3((Random.value - 0.5f) * 2.2f + f.nx * 1.4f, 1.6f + Random.value * 2.6f, (Random.value - 0.5f) * 2.2f + f.nz * 1.4f),
                5.5f + Random.value * 4, 3 + Random.value * 3, 13 + Random.value * 9, mistCol, 0.2f + Random.value * 0.12f, 0.25f, 0.55f, -0.22f, 0, 0.3f);
        }

        void EmitFalls(float dt, Vector3 cam)
        {
            float rate = Application.isMobilePlatform ? 14 : 24;
            foreach (var f in falls)
            {
                float dist = Mathf.Sqrt((cam.x - f.cx) * (cam.x - f.cx) + (cam.z - f.cz) * (cam.z - f.cz));
                if (dist > 500) continue;
                float near = 1 - MathX.Smoothstep(60, 500, dist);
                emitAcc += rate * (0.35f + near) * dt;
                while (emitAcc >= 1)
                {
                    emitAcc -= 1;
                    MistPuff(f);
                    var p = OnSheet(f, 1 - Random.value * 0.06f, 0.9f); p.y += 0.3f + Random.value * 2.5f;
                    mist.Spawn(p, new Vector3((Random.value - 0.5f) * 7 + f.nx * 3, 3.5f + Random.value * 7, (Random.value - 0.5f) * 7 + f.nz * 3),
                        1.1f + Random.value * 1.1f, 0.5f, 3.4f, foamCol, 0.55f, 1, 1.9f, 5.5f, 0, 0.08f);
                }
                if (dist < 160)
                {
                    // where the sheet slams onto the roadway: white bursting back up
                    roadAcc += dt * 16 * (0.4f + near);
                    while (roadAcc >= 1)
                    {
                        roadAcc -= 1;
                        var p = f.At(f.phiRoad);
                        float a = (Random.value - 0.5f) * (Route.RoadHalf * 2 + 3);
                        p += new Vector3(f.ax * a + f.nx * (Random.value - 0.3f) * 1.5f, 0, f.az * a + f.nz * (Random.value - 0.3f) * 1.5f);
                        p.y = route.RoadHeightAt(f.s) + 0.15f;
                        mist.Spawn(p, new Vector3(f.nx * (1 + Random.value * 2) + (Random.value - 0.5f) * 2, 1.5f + Random.value * 3, f.nz * (1 + Random.value * 2) + (Random.value - 0.5f) * 2),
                            0.8f + Random.value * 0.8f, 0.6f, 3.2f, foamCol, 0.5f, 1, 1.8f, 3.5f, 0, 0.05f);
                    }
                    if (f.drips.Count > 0)
                    {
                        dripAcc += dt * 14 * near;
                        while (dripAcc >= 1)
                        {
                            dripAcc -= 1;
                            var d0 = f.drips[Random.Range(0, f.drips.Count)];
                            mist.Spawn(d0 + new Vector3((Random.value - 0.5f) * 1.5f, 0, (Random.value - 0.5f) * 1.5f), new Vector3(0, -1 - Random.value, 0),
                                1.1f, 0.12f, 0.18f, foamCol, 0.55f, 0, 0.1f, 9.8f, 0, 0);
                        }
                    }
                }
                // wisps tearing off the sheet, thickest where it crosses the road
                if (Random.value < dt * 14 * (0.2f + near))
                {
                    float phi = Mathf.Clamp01(f.phiRoad + (Random.value - 0.4f) * 0.7f);
                    mist.Spawn(OnSheet(f, phi, 0.7f), new Vector3(f.nx * 3 + (Random.value - 0.5f) * 2, -2 - Random.value * 4, f.nz * 3 + (Random.value - 0.5f) * 2),
                        1.6f + Random.value, 1.2f, 5.5f, mistCol, 0.32f, 0.25f, 1.2f, 1.5f, 0, 0.15f);
                }
            }
        }

        void EmitRapids(float dt, Vector3 cam)
        {
            route.RoadCoords(cam.x, cam.z, out _, out float cs);
            if (cs < 0) return;
            rapidAcc += dt * (Application.isMobilePlatform ? 10 : 20);
            while (rapidAcc >= 1)
            {
                rapidAcc -= 1;
                float s = Mathf.Clamp(cs + (Random.value - 0.35f) * 150, riverFrom, riverTo);
                float rap = RapidsAt(s);
                if (rap < 0.4f) continue;
                var fr = route.Frame(s);
                float dd = riverD + (Random.value - 0.5f) * riverW * 0.8f;
                mist.Spawn(new Vector3(fr.x + fr.rx * dd, LevelAt(s) + 0.15f, fr.z + fr.rz * dd),
                    new Vector3(fr.tx * 2 + (Random.value - 0.5f) * 2, 1 + Random.value * 2.5f * rap, fr.tz * 2 + (Random.value - 0.5f) * 2),
                    0.9f + Random.value * 1.2f, 0.4f, 2.6f + rap * 2, foamCol, 0.3f * rap, 1, 1.6f, 1.6f, 0, 0.12f);
            }
        }

        /// how far inside a falls curtain a point is, 0..1
        public float ThroughFalls(Vector3 p)
        {
            float m = 0;
            foreach (var f in falls)
            {
                float dx = p.x - f.cx, dz = p.z - f.cz;
                float u = dx * f.ax + dz * f.az, nn = dx * f.nx + dz * f.nz;
                float phi = Mathf.Clamp01((f.lipY - p.y) / f.drop);
                float nc = f.arc * Mathf.Sqrt(phi), th = f.thick + 1.4f * phi;
                float a = 1 - MathX.Smoothstep(th * 0.3f, th, Mathf.Abs(nn - nc));
                if (a <= 0) continue;
                float hw = f.halfW * (1 + 0.18f * phi);
                float b = 1 - MathX.Smoothstep(hw * 0.75f, hw * 1.05f, Mathf.Abs(u - f.Lat(phi)));
                if (b <= 0) continue;
                float v = MathX.Smoothstep(f.botY - 2.5f, f.botY + 0.5f, p.y) * (1 - MathX.Smoothstep(f.lipY - 0.2f, f.lipY + 2.2f, p.y));
                m = Mathf.Max(m, a * b * v);
            }
            return m;
        }

        // ------------------------------------------------------------------ frame
        public void Update(float dt, Camera camera, Vector3 coachPos)
        {
            var cam = camera.transform.position;
            foreach (var c in chunks)
            {
                float d = Mathf.Sqrt((cam.x - c.c.x) * (cam.x - c.c.x) + (cam.z - c.c.z) * (cam.z - c.c.z)) - c.r;
                c.go.SetActive(d < 760);
            }
            if (falls.Count > 0)
            {
                EmitFalls(dt, cam);
                float tc = ThroughFalls(coachPos);
                if (tc > 0.02f)
                {
                    coachAcc += dt * 90 * tc;
                    while (coachAcc >= 1)
                    {
                        coachAcc -= 1;
                        mist.Spawn(coachPos + new Vector3((Random.value - 0.5f) * 4, Random.value * 3, (Random.value - 0.5f) * 4),
                            new Vector3((Random.value - 0.5f) * 9, 1 + Random.value * 6, (Random.value - 0.5f) * 9), 0.7f + Random.value * 0.7f, 0.5f, 3, foamCol, 0.6f, 1, 2.2f, 4, 0, 0.05f);
                    }
                }
                if (tc > 0.3f && !coachIn)
                {
                    coachIn = true;
                    splash.time = Random.value; splash.volume = 0.9f; splash.Play();
                    Sound.I?.Play("horse_neigh", 0.6f, 1.05f);
                    onBurst?.Invoke(1);
                }
                else if (tc < 0.02f) coachIn = false;
                Burst(dt, cam);
                Roar(cam);
            }
            if (splash.isPlaying) { splash.volume = Mathf.Max(0, splash.volume - dt * 0.8f); if (splash.volume <= 0) splash.Stop(); }
            EmitRapids(dt, cam);
            mist.Update(dt, camera);
        }

        // drizzle from the spray on the approach, soaked going through, then water running off the lens
        void Burst(float dt, Vector3 cam)
        {
            float inCam = ThroughFalls(cam), spray = 0;
            foreach (var f in falls)
            {
                var p = f.At(f.phiRoad);
                float dd = Mathf.Sqrt((cam.x - p.x) * (cam.x - p.x) + (cam.z - p.z) * (cam.z - p.z));
                spray = Mathf.Max(spray, 0.22f * (1 - MathX.Smoothstep(4, 22, dd)));
            }
            float want = Mathf.Max(inCam, spray);
            if (want > wet) wet = Mathf.Min(1, wet + dt * 6 * (want - wet) + dt * 0.5f * want);
            else wet = Mathf.Max(want, wet - dt * 0.2f);
            sheet = inCam;
            lensVeil.color = new Color(0.86f, 0.92f, 0.94f, sheet * 0.6f);
            lensDirt.color = new Color(0.9f, 0.96f, 1, wet * 0.75f);
            lensStreaks.color = new Color(1, 1, 1, wet * 0.55f);
            var r = lensStreaks.uvRect; r.y += Time.unscaledDeltaTime * (0.15f + wet * 0.5f); lensStreaks.uvRect = r;
            lensCanvas.enabled = wet > 0.005f || sheet > 0.005f;
        }

        void Roar(Vector3 cam)
        {
            float dist = float.MaxValue;
            foreach (var f in falls) dist = Mathf.Min(dist, Vector3.Distance(cam, f.At(f.phiRoad)));
            float near = Mathf.Pow(Mathf.Clamp01(1 - dist / 380), 2);
            float inside = ThroughFalls(cam);
            float vol = Mathf.Min(1, near * 0.8f + inside * 0.5f) * (Sound.I ? Sound.I.sfxVolume * Sound.I.master : 1);
            roar.volume = MathX.Damp(roar.volume, vol, 8, Time.unscaledDeltaTime);
            roarLp.cutoffFrequency = MathX.Damp(roarLp.cutoffFrequency, 300 + 2400 * near + 7000 * inside, 10, Time.unscaledDeltaTime);
        }

        /// three seconds of looping stereo noise, mostly brown with a little hiss
        static AudioClip NoiseClip()
        {
            int rate = 22050, n = rate * 3;
            var data = new float[n * 2];
            var rng = new System.Random(7);
            for (int ch = 0; ch < 2; ch++)
            {
                float last = 0;
                for (int i = 0; i < n; i++)
                {
                    float w = (float)rng.NextDouble() * 2 - 1;
                    last = (last + 0.02f * w) / 1.02f;
                    data[i * 2 + ch] = last * 3.2f + w * 0.09f;
                }
                const int X = 2048;
                for (int i = 0; i < X; i++) { float t = i / (float)X; data[(n - X + i) * 2 + ch] = data[(n - X + i) * 2 + ch] * (1 - t) + data[i * 2 + ch] * t; }
            }
            var clip = AudioClip.Create("falls_noise", n, 2, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// water running down the lens: thin vertical threads with beads
        static Texture2D StreakTex()
        {
            const int W = 128, H = 256;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[W * H];
            var nz = new Simplex2D(91);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float th = nz.Noise(u * 22, v * 1.4f) * 0.5f + 0.5f;
                    float bead = nz.Noise(u * 22, v * 18) * 0.5f + 0.5f;
                    float a = Mathf.Pow(MathX.Smoothstep(0.72f, 0.95f, th), 1.5f) * 0.6f + MathX.Smoothstep(0.82f, 0.97f, bead * th) * 0.8f;
                    px[y * W + x] = new Color(0.92f, 0.97f, 1f, Mathf.Clamp01(a));
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        public void Dispose()
        {
            mist.Dispose();
            if (root) Object.Destroy(root);
            foreach (var o in owned) if (o) Object.Destroy(o);
        }
    }
}
