// Train sounds, synthesised into AudioClips at run time so the railroad needs no audio files: a chime
// steam whistle, the exhaust chuff, the bell and a rail-clatter loop. Registered as train_* so they play
// through Sound like any other clip. Ported from core/synth.js.
using UnityEngine;

namespace Schofield
{
    public static class Synth
    {
        const int SR = 44100;
        static bool made;

        static uint seed;
        static float R() { seed = seed * 1664525u + 1013904223u; return seed / 2147483648f - 1; }

        static void Bandpass(float[] d, float f, float q)
        {
            float w = 2 * Mathf.PI * f / SR, a = Mathf.Sin(w) / (2 * q), c = Mathf.Cos(w);
            float b0 = a, b2 = -a, a0 = 1 + a, a1 = -2 * c, a2 = 1 - a;
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < d.Length; i++) { float x = d[i]; float y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0; x2 = x1; x1 = x; y2 = y1; y1 = y; d[i] = y; }
        }
        static void Lowpass(float[] d, float f) { float k = 1 - Mathf.Exp(-2 * Mathf.PI * f / SR), y = 0; for (int i = 0; i < d.Length; i++) { y += (d[i] - y) * k; d[i] = y; } }
        static void Normalise(float[] d, float peak) { float m = 0; foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v)); if (m > 0) for (int i = 0; i < d.Length; i++) d[i] *= peak / m; }
        static AudioClip Clip(string name, float[] d) { var c = AudioClip.Create(name, d.Length, 1, SR, false); c.SetData(d, 0); return c; }

        // a three-chime steam whistle: a minor triad of pipes rich in odd harmonics, slurring up as the valve opens
        static float[] Whistle(float secs)
        {
            float[] notes = { 311, 370, 466 };
            seed = 7;
            int n = (int)(secs * SR);
            var d = new float[n]; var breath = new float[n];
            for (int i = 0; i < n; i++) breath[i] = R();
            Bandpass(breath, 1400, 0.8f);
            var ph = new float[3];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float att = Mathf.Min(1, t / 0.12f), rel = Mathf.Min(1, (secs - t) / 0.25f);
                float env = att * att * Mathf.Max(0, rel);
                float slur = 0.9f + 0.1f * Mathf.Min(1, t / 0.18f);
                float wob = 1 + 0.004f * Mathf.Sin(t * 2 * Mathf.PI * 5.5f) + 0.002f * Mathf.Sin(t * 2 * Mathf.PI * 1.3f);
                float v = 0;
                for (int k = 0; k < 3; k++)
                {
                    ph[k] += 2 * Mathf.PI * notes[k] * slur * wob / SR;
                    float p = ph[k];
                    v += Mathf.Sin(p) + 0.35f * Mathf.Sin(3 * p) + 0.12f * Mathf.Sin(5 * p) + 0.2f * Mathf.Sin(2 * p);
                }
                d[i] = env * (v * 0.3f + breath[i] * 1.6f * (0.6f + 0.4f * att));
            }
            Normalise(d, 0.85f);
            return d;
        }

        static float[] Chuff()
        {
            seed = 11; int n = (int)(0.34f * SR); var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = R();
            Lowpass(d, 900); Lowpass(d, 1400);
            for (int i = 0; i < n; i++) { float t = i / (float)SR; d[i] *= Mathf.Min(1, t / 0.008f) * Mathf.Exp(-t * 11) * 1.6f + 0.25f * Mathf.Sin(2 * Mathf.PI * 62 * t) * Mathf.Exp(-t * 18); }
            Normalise(d, 0.8f);
            return d;
        }

        static float[] Bell()
        {
            float[,] parts = { { 540, 1, 1.5f }, { 1080, 0.55f, 2.2f }, { 1490, 0.35f, 3.0f }, { 2920, 0.18f, 4.5f }, { 4800, 0.08f, 7 } };
            int n = (int)(1.6f * SR); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, v = 0;
                for (int k = 0; k < 5; k++) v += parts[k, 1] * Mathf.Sin(2 * Mathf.PI * parts[k, 0] * t) * Mathf.Exp(-t * parts[k, 2]);
                d[i] = v * Mathf.Min(1, t / 0.002f);
            }
            Normalise(d, 0.7f);
            return d;
        }

        // rolling stock over jointed rail: a low rumble with the double clack of each truck at a joint
        static float[] Clatter()
        {
            const float secs = 2.4f; seed = 23; int n = (int)(secs * SR);
            var d = new float[n]; var rum = new float[n]; var hiss = new float[n];
            for (int i = 0; i < n; i++) rum[i] = R();
            Lowpass(rum, 120); Lowpass(rum, 160);
            for (int i = 0; i < n; i++) hiss[i] = R();
            Bandpass(hiss, 2600, 0.7f);
            var hits = new System.Collections.Generic.List<float>();
            for (int k = 0; k < 4; k++) { float t0 = 0.05f + k * 0.6f + (k % 2) * 0.03f; hits.Add(t0); hits.Add(t0 + 0.13f); }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, c = 0;
                foreach (var h in hits) { float dt = t - h; if (dt >= 0 && dt < 0.09f) c += Mathf.Sin(2 * Mathf.PI * 190 * dt) * Mathf.Exp(-dt * 60) + 0.6f * Mathf.Sin(2 * Mathf.PI * 820 * dt) * Mathf.Exp(-dt * 90); }
                d[i] = rum[i] * 7 + c * 0.5f + hiss[i] * 0.06f;
            }
            Normalise(d, 0.75f);
            int f = (int)(0.02f * SR);
            for (int i = 0; i < f; i++) { float k = i / (float)f; d[i] *= k; d[n - 1 - i] *= k; }
            return d;
        }

        public static void MakeTrainSounds()
        {
            if (made) return;
            made = true;
            Assets.RegisterClip("train_whistle", Clip("train_whistle", Whistle(2.1f)));
            Assets.RegisterClip("train_whistle_short", Clip("train_whistle_short", Whistle(0.7f)));
            Assets.RegisterClip("train_chuff", Clip("train_chuff", Chuff()));
            Assets.RegisterClip("train_bell", Clip("train_bell", Bell()));
            Assets.RegisterClip("train_clatter", Clip("train_clatter", Clatter()));
        }
    }
}
