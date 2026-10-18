// Thunder Gorge: the high timber trestle that carries the road across a side cut — plank deck, bents
// every 14 m standing on the cut's floor, cross-bracing all the way down, iron at the ends.
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

            for (float s = s0; s < s1; s += 1.25f)
            {
                float at = Mathf.Min(s + 0.625f, s1);
                D.Box(new Vector3(width, 0.18f, Mathf.Min(1.3f, s1 - s)), P(at, 0, 0.02f), Fwd(at));
            }
            var bents = new System.Collections.Generic.List<float>();
            for (float s = s0; s <= s1; s += 14) bents.Add(s);
            if (bents[bents.Count - 1] < s1 - 1) bents.Add(s1);
            foreach (float s in bents)
            {
                foreach (float d in new[] { -3.45f, 3.45f })
                {
                    var top = P(s, d, -0.48f);
                    var bse = top; bse.y = route.Height(bse.x, bse.z) - 0.35f;
                    T.Beam(bse, top, 0.72f);
                    T.Beam(P(s, d, -0.2f), P(s, d, 1.35f), 0.28f);
                }
                T.Beam(P(s, -3.8f, -0.48f), P(s, 3.8f, -0.48f), 0.62f);
            }
            for (int i = 0; i < bents.Count - 1; i++)
            {
                float a = bents[i], bb = bents[i + 1];
                foreach (float d in new[] { -3.45f, 3.45f })
                {
                    T.Beam(P(a, d, 1.35f), P(bb, d, 1.35f), 0.3f);
                    T.Beam(P(a, d, -1.1f), P(bb, d, -1.1f), 0.32f);
                    for (float level = -2.2f; level > -depth + 2; level -= 6)
                    {
                        T.Beam(P(a, d, level), P(bb, d, level + 5.8f), 0.3f);
                        T.Beam(P(a, d, level + 5.8f), P(bb, d, level), 0.3f);
                    }
                }
            }
            foreach (float s in new[] { s0, s1 })
            {
                foreach (float d in new[] { -3.8f, 3.8f }) I.Beam(P(s, d, -0.2f), P(s, d, 1.4f), 0.12f);
                I.Beam(P(s, -3.8f, -0.2f), P(s, 3.8f, -0.2f), 0.12f);
            }
            deckMat = Assets.Lit(MathX.Hex("#8a8070"), 0.08f);
            deckMat.SetTexture("_BaseMap", Assets.Tex("wood_planks"));
            timberMat = Assets.Lit(MathX.Hex("#5a5650"), 0.1f);
            timberMat.SetTexture("_BaseMap", Assets.Tex("wood_planks"));
            ironMat = Assets.Lit(MathX.Hex("#282e30"), 0.35f, 0.55f);
            MeshKit.Show("Deck", deck = D.Build("Bridge deck"), deckMat, root.transform);
            MeshKit.Show("Trestle", timber = T.Build("Trestle"), timberMat, root.transform);
            MeshKit.Show("Ironwork", iron = I.Build("Ironwork"), ironMat, root.transform);
        }

        public void Dispose()
        {
            if (root) Object.Destroy(root);
            foreach (var o in new Object[] { deckMat, timberMat, ironMat, deck, timber, iron }) if (o) Object.Destroy(o);
        }
    }
}
