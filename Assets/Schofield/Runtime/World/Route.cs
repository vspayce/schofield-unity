// A route: a winding road spline sampled every metre, plus the terrain height function that carves
// the road into the land. A distance field around the road gives, for any world XZ, the nearest road
// station `s` and signed lateral `d` (right-positive). Ported from world/route.js.
using UnityEngine;

namespace Schofield
{
    public struct RoadFrame
    {
        public float x, z, tx, tz, rx, rz, curv;
        public Vector3 Fwd => new Vector3(tx, 0, tz);
        public Vector3 Right => new Vector3(rx, 0, rz);
    }

    public class Route
    {
        public const float RoadHalf = 4.2f;
        const float Cell = 2f;

        public readonly RouteDef def;
        public readonly string biome;
        public readonly float len;
        public readonly Simplex2D noise, noise2;
        readonly Rng rand;

        public int samples;
        public float ds, total;
        float[] px, pz, tx, tz, curv, roadH;
        float fx0, fz0, fieldR; int fW, fH;
        float[] fd, fs;
        float townS0 = -1, townS1 = -1;

        // Thunder Gorge
        public readonly float riverSide = 1, gorgeDepth = 26;
        public readonly bool hasBridge, hasFalls;
        public readonly float bridgeS0, bridgeS1, bridgeWidth, bridgeCutHalf, bridgeDepth;
        public readonly float fallsS, fallsDrop, fallsWidth;

        public Route(RouteDef def)
        {
            this.def = def; biome = def.biome; len = def.length;
            noise = new Simplex2D(def.seed);
            noise2 = new Simplex2D(def.seed * 7 + 3);
            rand = new Rng(def.seed);
            if (def.ghostTownStart >= 0) { townS0 = def.ghostTownStart * len; townS1 = townS0 + def.ghostTownLength; }
            if (biome == "gorge" && def.gorge != null)
            {
                var gd = def.gorge;
                riverSide = gd.side; gorgeDepth = gd.depth;
                if (gd.fallsAt >= 0) { hasFalls = true; fallsS = len * gd.fallsAt; fallsDrop = gd.fallsDrop; fallsWidth = gd.fallsWidth; }
                if (gd.bridgeAt >= 0)
                {
                    hasBridge = true;
                    bridgeS0 = len * gd.bridgeAt - gd.bridgeLength * 0.5f; bridgeS1 = len * gd.bridgeAt + gd.bridgeLength * 0.5f;
                    bridgeWidth = gd.bridgeWidth; bridgeCutHalf = gd.bridgeCutHalf; bridgeDepth = gd.bridgeDepth;
                }
            }
            BuildPath();
            BuildRoadHeights();
            BuildField();
        }

        // ------------------------------------------------------------------ path
        void BuildPath()
        {
            var pts = new System.Collections.Generic.List<Vector3>();
            float x = 0, z = 0, h = Mathf.PI; // start travelling toward -Z
            const float step = 60;
            int n = Mathf.CeilToInt((len + 400) / step) + 2;
            for (int i = 0; i < n; i++)
            {
                pts.Add(new Vector3(x, 0, z));
                float t = i * step;
                bool inTown = townS0 >= 0 && t > townS0 - 120 && t < townS1 + 60;
                bool straight = i < 3 || t > len - 60 || inTown;
                if (!straight) h += (noise.Noise(i * 0.23f, 5.1f) * 0.9f + (rand.Next() - 0.5f) * 0.35f) * def.winding;
                x += Mathf.Sin(h) * step; z += Mathf.Cos(h) * step;
            }
            // dense centripetal Catmull-Rom, then resample at ~1 m arc length
            var dense = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = i == 0 ? 2 * pts[0] - pts[1] : pts[i - 1];
                Vector3 p1 = pts[i], p2 = pts[i + 1];
                Vector3 p3 = i + 2 < pts.Count ? pts[i + 2] : 2 * pts[i + 1] - pts[i];
                for (int k = 0; k < 40; k++) dense.Add(CentripetalCR(p0, p1, p2, p3, k / 40f));
            }
            dense.Add(pts[pts.Count - 1]);
            var cum = new float[dense.Count];
            for (int i = 1; i < dense.Count; i++) cum[i] = cum[i - 1] + Vector3.Distance(dense[i - 1], dense[i]);
            total = cum[dense.Count - 1];
            int N = Mathf.FloorToInt(total);
            samples = N; ds = total / N;
            px = new float[N + 1]; pz = new float[N + 1]; tx = new float[N + 1]; tz = new float[N + 1];
            int j = 0;
            for (int i = 0; i <= N; i++)
            {
                float target = i * ds;
                while (j < dense.Count - 2 && cum[j + 1] < target) j++;
                float seg = cum[j + 1] - cum[j];
                float u = seg > 1e-5f ? (target - cum[j]) / seg : 0;
                var p = Vector3.Lerp(dense[j], dense[j + 1], Mathf.Clamp01(u));
                px[i] = p.x; pz[i] = p.z;
            }
            for (int i = 0; i <= N; i++)
            {
                int a = Mathf.Max(0, i - 2), b = Mathf.Min(N, i + 2);
                float dx = px[b] - px[a], dz = pz[b] - pz[a];
                float l = Mathf.Sqrt(dx * dx + dz * dz); if (l < 1e-6f) l = 1;
                tx[i] = dx / l; tz[i] = dz / l;
            }
            // signed curvature for coach body roll (same numbers as the web game: the world here is
            // its mirror image, and Unity's rotation matrices match three.js's, so roll stays outward)
            curv = new float[N + 1];
            for (int i = 0; i <= N; i++)
            {
                int a = Mathf.Max(0, i - 6), b = Mathf.Min(N, i + 6);
                float c = tx[a] * tz[b] - tz[a] * tx[b];
                curv[i] = c / Mathf.Max(1e-3f, (b - a) * ds);
            }
        }

        static Vector3 CentripetalCR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float d01 = Mathf.Pow(Mathf.Max(1e-4f, (p1 - p0).sqrMagnitude), 0.25f);
            float d12 = Mathf.Pow(Mathf.Max(1e-4f, (p2 - p1).sqrMagnitude), 0.25f);
            float d23 = Mathf.Pow(Mathf.Max(1e-4f, (p3 - p2).sqrMagnitude), 0.25f);
            // tangents (Barry-Goldman, non-uniform), as three.js's CatmullRomCurve3
            Vector3 t1 = (p1 - p0) / d01 - (p2 - p0) / (d01 + d12) + (p2 - p1) / d12;
            Vector3 t2 = (p2 - p1) / d12 - (p3 - p1) / (d12 + d23) + (p3 - p2) / d23;
            t1 *= d12; t2 *= d12;
            float tt = t * t, ttt = tt * t;
            return (2 * ttt - 3 * tt + 1) * p1 + (ttt - 2 * tt + t) * t1 + (-2 * ttt + 3 * tt) * p2 + (ttt - tt) * t2;
        }

        // ------------------------------------------------------------ natural land
        public float Natural(float x, float z)
        {
            var n = noise; var n2 = noise2;
            switch (biome)
            {
                case "plains":
                {
                    float big = n.Fbm(x / 700, z / 700, 4) * 26;
                    float mid = n2.Fbm(x / 160, z / 160, 4) * 6;
                    float hills = Mathf.Pow(n.Fbm(x / 260 + 11, z / 260, 4) * 0.5f + 0.5f, 2.2f) * 30;
                    return big + mid + hills;
                }
                case "mountain":
                {
                    float r = n.Ridged(x / 650, z / 650, 5) * 170;
                    float m = n2.Fbm(x / 220, z / 220, 4) * 38;
                    float d = n.Fbm(x / 60, z / 60, 3) * 4;
                    return r + m + d - 40;
                }
                case "desert":
                {
                    float big = n.Fbm(x / 800, z / 800, 3) * 18;
                    float mid = n2.Fbm(x / 180, z / 180, 3) * 4;
                    float mesa = MathX.Smoothstep(0.35f, 0.42f, n2.Fbm(x / 520 + 3, z / 520 - 9, 3)) * 55;
                    mesa = Mathf.Floor(mesa / 11) * 11 + (mesa % 11) * 0.15f;
                    return big + mid + mesa;
                }
                case "gorge":
                {
                    // high forested shoulders falling into a cut river valley
                    float big = n.Fbm(x / 520, z / 520, 4) * 70;
                    float ridge = n2.Ridged(x / 300 + 5, z / 300, 4) * 48;
                    float mid = n2.Fbm(x / 120, z / 120, 3) * 9;
                    return big + ridge + mid + 20;
                }
                case "canyon":
                {
                    float plat = 62 + n.Fbm(x / 400, z / 400, 4) * 22;
                    float terr = n2.Fbm(x / 90, z / 90, 3) * 10;
                    plat = Mathf.Floor(plat / 9) * 9 + (plat % 9) * 0.25f;
                    return plat + terr;
                }
            }
            return 0;
        }

        void BuildRoadHeights()
        {
            int N = samples;
            var raw = new float[N + 1];
            for (int i = 0; i <= N; i++) raw[i] = biome == "canyon" ? 0 : Natural(px[i], pz[i]);
            int win = biome == "mountain" ? 70 : 110;
            float[] a = raw, b = new float[N + 1];
            for (int pass = 0; pass < 3; pass++)
            {
                float acc = 0; int cnt = 0;
                for (int i = -win; i <= N + win; i++)
                {
                    int add = i + win, rem = i - win - 1;
                    if (add >= 0 && add <= N) { acc += a[add]; cnt++; }
                    if (rem >= 0 && rem <= N) { acc -= a[rem]; cnt--; }
                    if (i >= 0 && i <= N) b[i] = acc / cnt;
                }
                (a, b) = (b, a);
            }
            float maxG = biome == "mountain" ? 0.09f : 0.05f;
            for (int i = 1; i <= N; i++) a[i] = Mathf.Clamp(a[i], a[i - 1] - maxG * ds, a[i - 1] + maxG * ds);
            for (int i = N - 1; i >= 0; i--) a[i] = Mathf.Clamp(a[i], a[i + 1] - maxG * ds, a[i + 1] + maxG * ds);
            roadH = a;
        }

        // ------------------------------------------------------ road distance field
        void BuildField()
        {
            float R = biome == "canyon" ? 110 : biome == "mountain" ? 70 : 60;
            fieldR = R;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i <= samples; i++)
            {
                minX = Mathf.Min(minX, px[i]); maxX = Mathf.Max(maxX, px[i]);
                minZ = Mathf.Min(minZ, pz[i]); maxZ = Mathf.Max(maxZ, pz[i]);
            }
            minX -= R + 4; minZ -= R + 4; maxX += R + 4; maxZ += R + 4;
            int W = Mathf.CeilToInt((maxX - minX) / Cell) + 1, H = Mathf.CeilToInt((maxZ - minZ) / Cell) + 1;
            fx0 = minX; fz0 = minZ; fW = W; fH = H;
            var dist2 = new float[W * H]; fd = new float[W * H]; fs = new float[W * H];
            for (int k = 0; k < W * H; k++) { dist2[k] = R * R; fd[k] = R; fs[k] = -1; }
            int rc = Mathf.CeilToInt(R / Cell);
            for (int i = 0; i <= samples; i += 2)
            {
                float x = px[i], z = pz[i], ftx = tx[i], ftz = tz[i];
                float rx = ftz, rz = -ftx;
                int cx = Mathf.RoundToInt((x - minX) / Cell), cz = Mathf.RoundToInt((z - minZ) / Cell);
                for (int gz = Mathf.Max(0, cz - rc); gz <= Mathf.Min(H - 1, cz + rc); gz++)
                {
                    float wz = minZ + gz * Cell - z;
                    for (int gx = Mathf.Max(0, cx - rc); gx <= Mathf.Min(W - 1, cx + rc); gx++)
                    {
                        float wx = minX + gx * Cell - x;
                        float d2 = wx * wx + wz * wz;
                        int k = gz * W + gx;
                        if (d2 < dist2[k])
                        {
                            dist2[k] = d2;
                            fd[k] = wx * rx + wz * rz;
                            fs[k] = i * ds + wx * ftx + wz * ftz;
                        }
                    }
                }
            }
        }

        /// nearest road station s and signed lateral d (s = -1 far from the road)
        public void RoadCoords(float x, float z, out float d, out float s)
        {
            float gx = (x - fx0) / Cell, gz = (z - fz0) / Cell;
            if (gx < 0 || gz < 0 || gx >= fW - 1 || gz >= fH - 1) { d = fieldR; s = -1; return; }
            int ix = (int)gx, iz = (int)gz; float fx = gx - ix, fz = gz - iz;
            int k = iz * fW + ix;
            float s00 = fs[k], s10 = fs[k + 1], s01 = fs[k + fW], s11 = fs[k + fW + 1];
            float smin = Mathf.Min(Mathf.Min(s00, s10), Mathf.Min(s01, s11)), smax = Mathf.Max(Mathf.Max(s00, s10), Mathf.Max(s01, s11));
            if (smin < 0 || smax - smin > 12)
            {
                int kk = (fz < 0.5f ? iz : iz + 1) * fW + (fx < 0.5f ? ix : ix + 1);
                d = fd[kk]; s = fs[kk];
                if (s < 0) d = fieldR;
                return;
            }
            d = Mathf.Lerp(Mathf.Lerp(fd[k], fd[k + 1], fx), Mathf.Lerp(fd[k + fW], fd[k + fW + 1], fx), fz);
            s = Mathf.Lerp(Mathf.Lerp(s00, s10, fx), Mathf.Lerp(s01, s11, fx), fz);
        }

        public float RoadHeightAt(float s)
        {
            float f = Mathf.Clamp(s / ds, 0, samples);
            int i = Mathf.Min(samples - 1, (int)f); float t = f - i;
            return Mathf.Lerp(roadH[i], roadH[i + 1], t);
        }

        public float Height(float x, float z)
        {
            RoadCoords(x, z, out float d, out float s);
            float nat = Natural(x, z);
            if (s < 0) return nat;
            float ad = Mathf.Abs(d);
            float rh = RoadHeightAt(s);
            float crown = (ad < RoadHalf ? -0.012f * ad * ad : 0)
                - (ad > RoadHalf ? 0.25f * MathX.Smoothstep(RoadHalf, RoadHalf + 1.5f, ad) * (1 - MathX.Smoothstep(RoadHalf + 2, RoadHalf + 5, ad)) : 0);
            float blendW = def.terrain.blend;
            float townFlat = 0;
            if (s < 140) townFlat = 1 - MathX.Smoothstep(90, 140, s);
            if (s > len - 150) townFlat = Mathf.Max(townFlat, MathX.Smoothstep(len - 150, len - 90, s));
            if (townS0 >= 0 && s > townS0 - 60 && s < townS1 + 60)
                townFlat = Mathf.Max(townFlat, MathX.Smoothstep(townS0 - 60, townS0, s) * (1 - MathX.Smoothstep(townS1, townS1 + 60, s)));
            if (biome == "gorge") return GorgeHeight(x, z, d, s, rh, crown, nat);
            if (biome == "canyon")
            {
                float floorW = 16 + 6 * noise.Noise(s / 120, 1.7f);
                float gully = noise2.Fbm(x / 14, z / 14, 3) * 7 + Mathf.Abs(noise.Noise(x / 40 + 7, z / 40)) * 6;
                float w0 = MathX.Smoothstep(floorW, floorW + 28, ad + noise.Noise(x / 25, z / 25) * 4 + gully * 0.6f);
                w0 = w0 * w0 * (3 - 2 * w0);
                float flatW = 1 - townFlat * (1 - MathX.Smoothstep(30, 60, ad));
                float talus = MathX.Smoothstep(floorW - 4, floorW + 6, ad) * (1 - w0) * 3;
                return Mathf.Lerp(rh + crown + talus, nat + gully * 0.4f, w0 * flatW);
            }
            float inner = RoadHalf + 2 + townFlat * 40;
            float w = MathX.Smoothstep(inner, inner + blendW + townFlat * 30, ad + noise.Noise(x / 40, z / 40) * 3);
            return Mathf.Lerp(rh + crown, nat, w);
        }

        // A shelf blasted into the valley side: road level out to the shoulder, then the ground drops away hard
        // on the river side and climbs on the other. Buttresses, gullies and bedding planes break up the faces.
        float GorgeHeight(float x, float z, float d, float s, float rh, float crown, float nat)
        {
            float sideD = d * riverSide;               // > 0 = the river side
            float shelf = RoadHalf + 3.4f;
            float butt = noise.Fbm(x / 58 + 4, z / 58, 3) * 9;
            float gully = Mathf.Pow(Mathf.Abs(noise.Noise(x / 21 + 9, z / 21)), 0.7f) * 7;
            float fine = noise2.Fbm(x / 9, z / 9, 3) * 2.2f;
            float grain = butt + gully + fine;
            float y;
            if (sideD > 0)
            {
                float t = MathX.Smoothstep(shelf, shelf + 26, sideD + grain * 0.5f);
                float bed = RiverBedAt(s) - 1.2f + noise.Fbm(x / 30, z / 30, 2) * 1.4f;
                y = Mathf.Lerp(rh + crown, bed, t * t * (3 - 2 * t));
                if (t > 0.05f && t < 0.97f)
                {
                    const float step = 3.2f;
                    float q = Mathf.Floor(y / step) * step + (y % step) * 0.34f;
                    y = Mathf.Lerp(y, q, 0.55f);
                }
            }
            else
            {
                float t = MathX.Smoothstep(shelf, shelf + 34, -sideD + grain * 0.7f);
                y = Mathf.Lerp(rh + crown, Mathf.Max(nat, rh + 12) + grain * 0.8f, t * t * (3 - 2 * t));
            }
            if (hasBridge && s >= bridgeS0 && s <= bridgeS1)
            {
                float along = MathX.Smoothstep(0, 18, s - bridgeS0) * MathX.Smoothstep(0, 18, bridgeS1 - s);
                float across = 1 - MathX.Smoothstep(bridgeWidth * 0.5f + 1, bridgeCutHalf, Mathf.Abs(d));
                float floor = RoadHeightAt(s) - bridgeDepth + noise2.Fbm(x / 18, z / 18, 2) * 0.9f;
                y = Mathf.Lerp(y, floor, along * across);
            }
            return y;
        }

        /// bed height under the river at station s: a steady fall down the valley, the road ledge above it
        public float RiverBedAt(float s) => RoadHeightAt(s) - gorgeDepth;

        public bool OnBridge(float s) => hasBridge && s >= bridgeS0 && s <= bridgeS1;

        /// what a wheel or a hoof stands on: the trestle deck where there is one, else the ground
        public float Surface(float x, float z)
        {
            if (hasBridge)
            {
                RoadCoords(x, z, out float d, out float s);
                if (s >= bridgeS0 && s <= bridgeS1 && Mathf.Abs(d) <= bridgeWidth * 0.5f) return RoadHeightAt(s);
            }
            return Height(x, z);
        }

        // ----------------------------------------------------------- road frames
        public RoadFrame Frame(float s)
        {
            float f = Mathf.Clamp(s / ds, 0, samples);
            int i = Mathf.Min(samples - 1, (int)f); float t = f - i;
            var o = new RoadFrame
            {
                x = Mathf.Lerp(px[i], px[i + 1], t),
                z = Mathf.Lerp(pz[i], pz[i + 1], t),
            };
            float ftx = Mathf.Lerp(tx[i], tx[i + 1], t), ftz = Mathf.Lerp(tz[i], tz[i + 1], t);
            float l = Mathf.Sqrt(ftx * ftx + ftz * ftz); if (l < 1e-6f) l = 1;
            o.tx = ftx / l; o.tz = ftz / l;
            o.rx = o.tz; o.rz = -o.tx;      // right of the direction of travel (Unity is left-handed)
            o.curv = Mathf.Lerp(curv[i], curv[i + 1], t);
            return o;
        }

        /// world position of road station s with lateral offset d (right-positive), on the ground
        public Vector3 WorldAt(float s, float d)
        {
            var f = Frame(s);
            float x = f.x + f.rx * d, z = f.z + f.rz * d;
            float y = OnBridge(s) && Mathf.Abs(d) <= bridgeWidth * 0.5f ? RoadHeightAt(s) : Height(x, z);
            return new Vector3(x, y, z);
        }

        public bool InTown(float s) => townS0 >= 0 && s > townS0 && s < townS1;
        public bool HasGhostTown => townS0 >= 0;
        public Vector2 TownRange => new Vector2(townS0, townS1);
        public float RailDist(float x, float z) => float.PositiveInfinity;   // (railroad: next phase)
    }
}
