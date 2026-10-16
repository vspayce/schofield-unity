// Seeded RNG + 2D simplex noise and helpers (ported from the web game's core/noise.js), so every
// route generates the same terrain every time.
using UnityEngine;

namespace Schofield
{
    /// mulberry32: a tiny deterministic generator (same sequence as the web game for a given seed)
    public class Rng
    {
        uint a;
        public Rng(int seed) { a = (uint)seed; }
        public float Next()
        {
            unchecked
            {
                a += 0x6d2b79f5;
                uint t = a;
                t = (t ^ (t >> 15)) * (1 | t);
                t = (t + ((t ^ (t >> 7)) * (61 | t))) ^ t;
                return ((t ^ (t >> 14)) >> 0) / 4294967296f;
            }
        }
        public float Range(float lo, float hi) => lo + (hi - lo) * Next();
        public int Int(int n) => Mathf.Min(n - 1, (int)(Next() * n));
    }

    public class Simplex2D
    {
        static readonly float F2 = 0.5f * (Mathf.Sqrt(3f) - 1f);
        static readonly float G2 = (3f - Mathf.Sqrt(3f)) / 6f;
        static readonly int[,] Grad = { { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 }, { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
        readonly byte[] perm = new byte[512];

        public Simplex2D(int seed)
        {
            var rnd = new Rng(seed);
            var p = new byte[256];
            for (int i = 0; i < 256; i++) p[i] = (byte)i;
            for (int i = 255; i > 0; i--)
            {
                int j = Mathf.FloorToInt(rnd.Next() * (i + 1));
                (p[i], p[j]) = (p[j], p[i]);
            }
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
        }

        /// ~[-1, 1]
        public float Noise(float x, float y)
        {
            float s = (x + y) * F2;
            int i = Mathf.FloorToInt(x + s), j = Mathf.FloorToInt(y + s);
            float t = (i + j) * G2;
            float x0 = x - (i - t), y0 = y - (j - t);
            int i1 = x0 > y0 ? 1 : 0, j1 = x0 > y0 ? 0 : 1;
            float x1 = x0 - i1 + G2, y1 = y0 - j1 + G2;
            float x2 = x0 - 1 + 2 * G2, y2 = y0 - 1 + 2 * G2;
            int ii = i & 255, jj = j & 255;
            float n = 0;
            float t0 = 0.5f - x0 * x0 - y0 * y0;
            if (t0 > 0) { int g = perm[ii + perm[jj]] & 7; t0 *= t0; n += t0 * t0 * (Grad[g, 0] * x0 + Grad[g, 1] * y0); }
            float t1 = 0.5f - x1 * x1 - y1 * y1;
            if (t1 > 0) { int g = perm[ii + i1 + perm[jj + j1]] & 7; t1 *= t1; n += t1 * t1 * (Grad[g, 0] * x1 + Grad[g, 1] * y1); }
            float t2 = 0.5f - x2 * x2 - y2 * y2;
            if (t2 > 0) { int g = perm[ii + 1 + perm[jj + 1]] & 7; t2 *= t2; n += t2 * t2 * (Grad[g, 0] * x2 + Grad[g, 1] * y2); }
            return 70f * n;
        }

        public float Fbm(float x, float y, int oct = 5, float lac = 2f, float gain = 0.5f)
        {
            float a = 1, f = 1, s = 0, n = 0;
            for (int i = 0; i < oct; i++) { s += a * Noise(x * f, y * f); n += a; a *= gain; f *= lac; }
            return s / n;
        }

        public float Ridged(float x, float y, int oct = 5, float lac = 2.1f, float gain = 0.5f)
        {
            float a = 1, f = 1, s = 0, n = 0, w = 1;
            for (int i = 0; i < oct; i++)
            {
                float v = 1 - Mathf.Abs(Noise(x * f, y * f));
                v *= v; v *= w; w = Mathf.Min(1, v * 2);
                s += a * v; n += a; a *= gain; f *= lac;
            }
            return s / n;
        }
    }

    public static class MathX
    {
        public static float Smoothstep(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
        public static float Damp(float a, float b, float lambda, float dt) => Mathf.Lerp(a, b, 1 - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt) => Vector3.Lerp(a, b, 1 - Mathf.Exp(-lambda * dt));
        public static float WrapAngle(float a)
        {
            while (a > Mathf.PI) a -= Mathf.PI * 2;
            while (a < -Mathf.PI) a += Mathf.PI * 2;
            return a;
        }
        public static float Rand => Random.value;
        public static float Rand11 => Random.value * 2f - 1f;
        public static T Pick<T>(T[] a) => a[Random.Range(0, a.Length)];
        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        /// heading (radians) of a horizontal direction: 0 = +Z, positive turns toward +X
        public static float Heading(Vector3 fwd) => Mathf.Atan2(fwd.x, fwd.z);
        public static Vector3 FromHeading(float h) => new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h));
        /// yaw/pitch/roll in radians, applied roll then pitch then yaw (the web game's 'YXZ' order)
        public static Quaternion YXZ(float pitch, float yaw, float roll) =>
            Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
    }
}
