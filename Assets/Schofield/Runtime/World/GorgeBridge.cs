// Thunder Gorge: the high timber trestle that carries the road across a side ravine — plank deck on three
// stringers, framed bents every 9 m (battered outer posts, sashes, sway and longitudinal X-bracing) standing
// on the ravine floor, guard rails, stone abutments.
// Ported from world/gorgebridge.js.
using UnityEngine;

namespace Schofield
{
    public class GorgeBridge
    {
        readonly GameObject root;
        readonly Material deckMat, timberMat, ironMat;
        readonly Mesh deck, timber, iron;

        public GorgeBridge(Route route, Transform parent)
        {
            root = new GameObject("Trestle");
            root.transform.SetParent(parent, false);
            float s0 = route.bridgeS0, s1 = route.bridgeS1, width = route.bridgeWidth, depth = route.bridgeDepth;
            var D = new MeshKit.Builder(); var T = new MeshKit.Builder(); var I = new MeshKit.Builder();
            Vector3 P(float s, float d, float y = 0) { var f = route.Frame(s); return new Vector3(f.x + f.rx * d, route.RoadHeightAt(s) + y, f.z + f.rz * d); }
            Quaternion Fwd(float s) { var f = route.Frame(s); return Quaternion.LookRotation(f.Fwd); }

            // ---- deck: plank ties across three stringers, a cap on every bent
            for (float s = s0; s < s1; s += 0.62f)
            {
                float at = Mathf.Min(s + 0.31f, s1);
                D.Box(new Vector3(width + 0.6f, 0.14f, 0.5f), P(at, 0, 0.0f), Fwd(at));
            }
            foreach (float d in new[] { -2.6f, 0f, 2.6f })
                for (float s = s0; s < s1; s += 12) T.Beam(P(s, d, -0.42f), P(Mathf.Min(s + 12.2f, s1), d, -0.42f), 0.5f);
            // guard rail: posts every 2.4 m, a top rail and a mid rail, wheel guards along the deck edge
            foreach (float d in new[] { -(width * 0.5f + 0.15f), width * 0.5f + 0.15f })
            {
                for (float s = s0; s <= s1; s += 2.4f) T.Beam(P(s, d, -0.05f), P(s, d, 1.15f), 0.18f);
                for (float s = s0; s < s1; s += 9.6f)
                {
                    float e = Mathf.Min(s + 9.7f, s1);
                    T.Beam(P(s, d, 1.12f), P(e, d, 1.12f), 0.16f);
                    T.Beam(P(s, d, 0.6f), P(e, d, 0.6f), 0.12f);
                    T.Beam(P(s, d * 0.9f, 0.16f), P(e, d * 0.9f, 0.16f), 0.22f);
                }
            }

            // ---- bents: a framed timber tower every 9 m standing on the ravine floor. Two plumb inner posts,
            // two outer posts battered out 1 in 5, a sash across every 6.5 m with X sway-bracing between them.
            const float SPACING = 9, STORY = 6.5f, BATTER = 0.2f;
            var bents = new System.Collections.Generic.List<float>();
            for (float s = s0 + 4; s <= s1 - 4; s += SPACING) bents.Add(s);
            float Ground(float s, float d) { var p = P(s, d); return route.Height(p.x, p.z); }
            float topY(float s) => route.RoadHeightAt(s) - 0.7f;
            Vector3 Post(float s, float side, bool outer, float y)
            {
                float drop = topY(s) - y;
                float d = side * (outer ? 3.2f + drop * BATTER : 1.3f);
                var f = route.Frame(s);
                return new Vector3(f.x + f.rx * d, y, f.z + f.rz * d);
            }
            var levels = new System.Collections.Generic.Dictionary<float, System.Collections.Generic.List<float>>();
            foreach (float s in bents)
            {
                float top = topY(s);
                T.Beam(Post(s, -1, true, top) + Vector3.up * 0.35f, Post(s, 1, true, top) + Vector3.up * 0.35f, 0.5f);   // cap
                var lv = new System.Collections.Generic.List<float> { top };
                float lowest = top;
                foreach (float side in new[] { -1f, 1f })
                    foreach (bool outer in new[] { true, false })
                    {
                        // walk down until this post meets the ground
                        float y = top; Vector3 p0 = Post(s, side, outer, top);
                        for (int it = 0; it < 40; it++) { var q = Post(s, side, outer, y); float gnd = route.Height(q.x, q.z); if (y <= gnd) break; y -= 2.5f; }
                        var bot = Post(s, side, outer, y); bot.y = route.Height(bot.x, bot.z) - 0.6f;
                        T.Beam(bot, p0, outer ? 0.46f : 0.4f);
                        T.Box(new Vector3(1.2f, 0.6f, 1.2f), bot + Vector3.up * 0.3f, Fwd(s));        // footing
                        lowest = Mathf.Min(lowest, bot.y);
                    }
                for (float y = top - STORY; y > lowest + 1.5f; y -= STORY) lv.Add(y);
                levels[s] = lv;
                for (int i = 0; i < lv.Count; i++)
                {
                    var a0 = Post(s, -1, true, lv[i]); var a1 = Post(s, 1, true, lv[i]);
                    if (i > 0) T.Beam(a0, a1, 0.3f);                                              // sash
                    if (i + 1 < lv.Count)
                    {
                        var b0 = Post(s, -1, true, lv[i + 1]); var b1 = Post(s, 1, true, lv[i + 1]);
                        T.Beam(a0, b1, 0.22f); T.Beam(a1, b0, 0.22f);                             // sway X
                    }
                }
            }
            // ---- between bents: girts at every shared level on the outer posts, X-bracing every other bay
            for (int i = 0; i + 1 < bents.Count; i++)
            {
                float a = bents[i], bb = bents[i + 1];
                var la = levels[a]; var lb = levels[bb];
                int n = Mathf.Min(la.Count, lb.Count);
                foreach (float side in new[] { -1f, 1f })
                    for (int k = 1; k < n; k++)
                    {
                        T.Beam(Post(a, side, true, la[k]), Post(bb, side, true, lb[k]), 0.26f);
                        if (i % 2 == 0 && k + 1 < n)
                        {
                            T.Beam(Post(a, side, true, la[k]), Post(bb, side, true, lb[k + 1]), 0.18f);
                            T.Beam(Post(bb, side, true, lb[k]), Post(a, side, true, la[k + 1]), 0.18f);
                        }
                    }
            }
            // ---- abutments: dressed stone where the deck meets each bank
            foreach (float s in new[] { s0, s1 })
            {
                float y = route.RoadHeightAt(s);
                I.Box(new Vector3(width + 3, 6, 3.5f), P(s, 0, -3.3f), Fwd(s));
            }
            deckMat = Assets.Lit(MathX.Hex("#9a8c76"), 0.06f);
            deckMat.SetTexture("_BaseMap", Assets.Tex("wood_planks"));
            timberMat = Assets.Lit(MathX.Hex("#7d6e5c"), 0.06f);
            timberMat.SetTexture("_BaseMap", Assets.Tex("wood_planks"));
            ironMat = Assets.Lit(MathX.Hex("#8f8a80"), 0.05f);              // abutment stone
            ironMat.SetTexture("_BaseMap", Assets.Tex("rock"));
            MeshKit.Show("Deck", deck = D.Build("Bridge deck"), deckMat, root.transform);
            MeshKit.Show("Trestle", timber = T.Build("Trestle"), timberMat, root.transform);
            MeshKit.Show("Abutments", iron = I.Build("Abutments"), ironMat, root.transform);
        }

        public void Dispose()
        {
            if (root) Object.Destroy(root);
            foreach (var o in new Object[] { deckMat, timberMat, ironMat, deck, timber, iron }) if (o) Object.Destroy(o);
        }
    }
}
