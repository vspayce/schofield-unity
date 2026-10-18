// Building merged meshes in code: an icosphere to make boulders from, a unit box for timbers, and a
// builder that appends transformed copies into one mesh (one draw call), as three.js's
// mergeGeometries did for the web game.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Schofield
{
    public static class MeshKit
    {
        static readonly Dictionary<int, (Vector3[] v, int[] t)> ico = new Dictionary<int, (Vector3[], int[])>();

        /// unit icosphere with shared vertices (so it can be displaced without cracking)
        public static (Vector3[] verts, int[] tris) Icosphere(int detail)
        {
            if (ico.TryGetValue(detail, out var c)) return c;
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                                     3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int d = 0; d < detail; d++)
            {
                var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(k, out int m)) return m;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    return mid[k] = v.Count - 1;
                }
                var nf = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], cc = f[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, cc), ca = Mid(cc, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, cc, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            // (front = the side cross(b - a, c - a) points to, in Unity as in three.js: these face outward)
            return ico[detail] = (v.ToArray(), f.ToArray());
        }

        static readonly Vector3[] boxV =
        {
            // +x, -x, +y, -y, +z, -z faces (4 verts each, outward)
            new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(.5f,.5f,.5f), new Vector3(.5f,-.5f,.5f),
            new Vector3(-.5f,-.5f,.5f), new Vector3(-.5f,.5f,.5f), new Vector3(-.5f,.5f,-.5f), new Vector3(-.5f,-.5f,-.5f),
            new Vector3(-.5f,.5f,-.5f), new Vector3(-.5f,.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f), new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,-.5f,.5f),
            new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f), new Vector3(-.5f,-.5f,.5f),
            new Vector3(-.5f,-.5f,-.5f), new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(.5f,-.5f,-.5f),
        };

        public class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Color> c = new List<Color>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            /// a box of the given size, placed by a TRS
            public void Box(Vector3 size, Vector3 pos, Quaternion rot, Color? col = null)
            {
                var m = Matrix4x4.TRS(pos, rot, size);
                int b0 = v.Count;
                for (int fi = 0; fi < 6; fi++)
                {
                    var nn = rot * (fi == 0 ? Vector3.right : fi == 1 ? Vector3.left : fi == 2 ? Vector3.up : fi == 3 ? Vector3.down : fi == 4 ? Vector3.forward : Vector3.back);
                    for (int k = 0; k < 4; k++)
                    {
                        var p = boxV[fi * 4 + k];
                        v.Add(m.MultiplyPoint3x4(p)); n.Add(nn); c.Add(col ?? Color.white);
                        // planks run along the long axis
                        uv.Add(new Vector2((k == 1 || k == 2 ? 1 : 0) * Mathf.Max(size.x, size.z) / 2, (k >= 2 ? 1 : 0) * size.y / 2));
                    }
                    int q = b0 + fi * 4;
                    t.AddRange(new[] { q, q + 1, q + 2, q, q + 2, q + 3 });
                }
            }

            /// a beam of square section between two points
            public void Beam(Vector3 a, Vector3 b, float thick, Color? col = null)
            {
                var d = b - a; float L = d.magnitude;
                if (L < 0.01f) return;
                Box(new Vector3(thick, L, thick), (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / L), col);
            }

            /// a capped cylinder (or cone, r0 != r1) from a to b, smooth sides
            public void Cylinder(Vector3 a, Vector3 b, float r0, float r1, int seg = 14, Color? col = null)
            {
                var ax = b - a; float L = ax.magnitude; if (L < 1e-4f) return;
                var q = Quaternion.FromToRotation(Vector3.up, ax / L);
                var cc = col ?? Color.white;
                int s0 = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float an = i / (float)seg * Mathf.PI * 2;
                    var dir = new Vector3(Mathf.Cos(an), 0, Mathf.Sin(an));
                    var nn = q * new Vector3(dir.x, (r0 - r1) / L, dir.z).normalized;
                    v.Add(a + q * (dir * r0)); n.Add(nn); c.Add(cc); uv.Add(new Vector2(i / (float)seg * 2, 0));
                    v.Add(a + q * (dir * r1 + Vector3.up * L)); n.Add(nn); c.Add(cc); uv.Add(new Vector2(i / (float)seg * 2, L / 2));
                }
                for (int i = 0; i < seg; i++) { int k = s0 + i * 2; t.AddRange(new[] { k, k + 1, k + 2, k + 2, k + 1, k + 3 }); }
                foreach (var (pt, r, up) in new[] { (a, r0, false), (b, r1, true) })
                {
                    if (r <= 0) continue;
                    var nn = q * (up ? Vector3.up : Vector3.down);
                    int ci = v.Count; v.Add(pt); n.Add(nn); c.Add(cc); uv.Add(Vector2.zero);
                    for (int i = 0; i <= seg; i++)
                    {
                        float an = i / (float)seg * Mathf.PI * 2;
                        v.Add(pt + q * (new Vector3(Mathf.Cos(an), 0, Mathf.Sin(an)) * r)); n.Add(nn); c.Add(cc); uv.Add(Vector2.zero);
                    }
                    for (int i = 0; i < seg; i++) { if (up) t.AddRange(new[] { ci, ci + 2 + i, ci + 1 + i }); else t.AddRange(new[] { ci, ci + 1 + i, ci + 2 + i }); }
                }
            }

            /// flat-shaded triangles (each face its own vertices)
            public void Flat(Vector3[] verts, int[] tris, Color col, System.Func<Vector3, Vector3, Vector2> uvOf = null)
            {
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], cc = verts[tris[i + 2]];
                    var nn = Vector3.Cross(b - a, cc - a).normalized;
                    int b0 = v.Count;
                    foreach (var p in new[] { a, b, cc }) { v.Add(p); n.Add(nn); c.Add(col); uv.Add(uvOf != null ? uvOf(p, nn) : Vector2.zero); }
                    t.Add(b0); t.Add(b0 + 1); t.Add(b0 + 2);
                }
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        /// a renderer for a built mesh, with shadows
        public static GameObject Show(string name, Mesh mesh, Material mat, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return go;
        }
    }
}
