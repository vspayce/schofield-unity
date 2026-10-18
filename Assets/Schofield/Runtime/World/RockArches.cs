// Devil's Gulch: natural stone arches spanning the canyon over the road, each a curve of lumpy
// boulders merged into one mesh. Ported from world/rockarches.js.
using UnityEngine;

namespace Schofield
{
    public class RockArches
    {
        readonly GameObject root;
        readonly Material mat;
        readonly Mesh mesh;

        public RockArches(Route route, Transform parent, ArchDef[] arches)
        {
            root = new GameObject("Rock Arches");
            root.transform.SetParent(parent, false);
            var (iv, it) = MeshKit.Icosphere(1);
            var noise = new Simplex2D(route.def.seed + 293);
            var rnd = new Rng(route.def.seed + 617);
            var b = new MeshKit.Builder();
            var verts = new Vector3[iv.Length];
            foreach (var arch in arches)
            {
                float s = route.len * arch.at;
                var f = route.Frame(s);
                float halfSpan = arch.span * 0.5f;
                int stones = Mathf.CeilToInt(arch.span / 3.2f);
                for (int i = 0; i <= stones; i++)
                {
                    float t = i / (float)stones * 2 - 1;
                    float d = t * halfSpan;
                    float y = route.RoadHeightAt(s) + arch.baseH + arch.rise * (1 - t * t);
                    float radius = 3.2f + Mathf.Sqrt(Mathf.Max(0, 1 - t * t)) * 1.1f;
                    foreach (float along in new[] { -2.4f, 2.4f })
                    {
                        float jitter = noise.Noise(i * 0.31f, along * 0.2f) * 0.7f;
                        var center = new Vector3(f.x + f.rx * (d + jitter) + f.tx * along, y + noise.Noise(i * 0.47f, along + 4) * 0.65f, f.z + f.rz * (d + jitter) + f.tz * along);
                        var rot = Quaternion.LookRotation(f.Right) * Quaternion.Euler(rnd.Next() * 0.45f * Mathf.Rad2Deg, rnd.Next() * 6.28f * Mathf.Rad2Deg, rnd.Next() * 0.35f * Mathf.Rad2Deg);
                        var scl = new Vector3(radius * (0.85f + rnd.Next() * 0.35f), radius * (0.75f + rnd.Next() * 0.3f), radius * (0.8f + rnd.Next() * 0.35f));
                        var m = Matrix4x4.TRS(center, rot, scl);
                        for (int k = 0; k < iv.Length; k++) verts[k] = m.MultiplyPoint3x4(iv[k]);
                        b.Flat(verts, it, Color.white, TriplanarUV);
                    }
                }
            }
            mesh = b.Build("Devils Gulch rock arches");
            mat = Assets.Lit(MathX.Hex("#c08a70"), 0.06f);
            mat.SetTexture("_BaseMap", Assets.Tex("red_rock"));
            MeshKit.Show("Arches", mesh, mat, root.transform);
        }

        /// UVs by the dominant normal axis, 5 m to a tile
        public static Vector2 TriplanarUV(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay > ax && ay > az) return new Vector2(p.x / 5, p.z / 5);
            if (ax > az) return new Vector2(p.z / 5, p.y / 5);
            return new Vector2(p.x / 5, p.y / 5);
        }

        public void Dispose() { if (root) Object.Destroy(root); Object.Destroy(mat); Object.Destroy(mesh); }
    }
}
