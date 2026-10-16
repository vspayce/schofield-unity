// Streaming terrain tiles around the coach, drawn with the splat shader (road / two ground layers /
// tri-planar rock on steep slopes / snow above the snow line). Ported from world/terrain.js.
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Schofield
{
    public class TerrainTile
    {
        public string key; public int i, j, lod; public GameObject go; public Mesh mesh;
        public float x0, z0; public const float Size = Terrain.Tile;
    }

    public class Terrain
    {
        public const float Tile = 128;
        readonly Route route;
        readonly Material mat;
        readonly Transform root;
        readonly Vector3 sunDir;
        public readonly Dictionary<string, TerrainTile> tiles = new Dictionary<string, TerrainTile>();
        public float radius;
        public System.Action<TerrainTile, bool, object> onTile;

        public Terrain(Route route, Transform parent, float drawDist)
        {
            this.route = route;
            radius = drawDist;
            root = new GameObject("Terrain").transform; root.SetParent(parent, false);
            mat = MakeMaterial(route);
            sunDir = Sky.SunDirection(route.def.light);
        }

        public static Material MakeMaterial(Route route)
        {
            var T = route.def.terrain;
            var m = new Material(Assets.Shader("Terrain")) { name = "Terrain_" + route.def.id };
            m.SetTexture("_G1", Assets.Tex(T.ground1)); m.SetTexture("_G2", Assets.Tex(T.ground2));
            m.SetTexture("_Road", Assets.Tex(T.road)); m.SetTexture("_Rock", Assets.Tex(T.rock));
            m.SetTexture("_Snow", Assets.Tex(T.snow ?? T.ground1)); m.SetTexture("_Macro", Assets.Tex("cloud_noise"));
            m.SetTexture("_G1N", Assets.Tex(T.ground1 + "_n", true)); m.SetTexture("_G2N", Assets.Tex(T.ground2 + "_n", true));
            m.SetTexture("_RoadN", Assets.Tex(T.road + "_n", true)); m.SetTexture("_RockN", Assets.Tex(T.rock + "_n", true));
            m.SetColor("_Tint1", T.tint1); m.SetColor("_Tint2", T.tint2); m.SetColor("_RockTint", T.rockTint); m.SetColor("_RoadTint", T.roadTint);
            m.SetFloat("_SnowLine", T.snowLine); m.SetFloat("_RoadHalf", Route.RoadHalf);
            m.SetFloat("_RockSlope", T.rockSlope >= 0 ? T.rockSlope : route.biome == "canyon" ? 0.22f : 0.3f);
            m.SetVector("_GreenMix", T.greenMix);
            return m;
        }

        int LodFor(float dist) => dist < 190 ? 64 : dist < 380 ? 32 : 16;

        // Heights, normals, road coordinates and sun visibility are pure functions of the route, so
        // tiles are computed on worker threads and only turned into meshes here on the main thread.
        class Job { public string key; public int i, j, lod; public float d; public Task<Data> task; }
        class Data { public Vector3[] pos, nor; public Vector2[] road, vis; public int[] idx; public object extra; }
        readonly Dictionary<string, Job> jobs = new Dictionary<string, Job>();
        /// extra per-tile work for the worker (the scatter's placement): (i, j, x0, z0) -> data for onTile
        public System.Func<int, int, float, float, object> work;
        const int MaxJobs = 3;

        /// Called every frame with the focus point (coach). Returns how many tiles are still wanted.
        public int Update(float fx, float fz, int budget = 2)
        {
            float R = radius;
            var want = new Dictionary<string, (int i, int j, int lod, float d)>();
            int i0 = Mathf.FloorToInt((fx - R) / Tile), i1 = Mathf.FloorToInt((fx + R) / Tile);
            int j0 = Mathf.FloorToInt((fz - R) / Tile), j1 = Mathf.FloorToInt((fz + R) / Tile);
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    float cx = (i + 0.5f) * Tile, cz = (j + 0.5f) * Tile;
                    float d = Mathf.Max(0, Mathf.Sqrt((cx - fx) * (cx - fx) + (cz - fz) * (cz - fz)) - Tile * 0.7f);
                    if (d > R) continue;
                    want[i + "," + j] = (i, j, LodFor(d), d);
                }
            var drop = new List<string>();
            foreach (var kv in tiles) if (!want.ContainsKey(kv.Key)) drop.Add(kv.Key);
            foreach (var k in drop) Remove(k);
            // finished jobs become meshes (a few per frame)
            var done = new List<string>();
            foreach (var kv in jobs) if (kv.Value.task.IsCompleted) done.Add(kv.Key);
            int made = 0;
            foreach (var k in done)
            {
                var jb = jobs[k]; jobs.Remove(k);
                if (jb.task.IsFaulted) { Debug.LogException(jb.task.Exception); continue; }
                if (!want.TryGetValue(k, out var w) || w.lod != jb.lod) continue;   // moved on meanwhile
                if (made++ >= budget + 1) { jobs[k] = jb; continue; }
                Finish(jb, jb.task.Result);
            }
            var todo = new List<(string k, int i, int j, int lod, float d)>();
            foreach (var kv in want)
                if ((!tiles.TryGetValue(kv.Key, out var t) || t.lod != kv.Value.lod) && !jobs.ContainsKey(kv.Key))
                    todo.Add((kv.Key, kv.Value.i, kv.Value.j, kv.Value.lod, kv.Value.d));
            todo.Sort((a, b) => a.d.CompareTo(b.d));
            for (int n = 0; n < todo.Count && jobs.Count < MaxJobs; n++)
            {
                var q = todo[n];
                bool fresh = !tiles.ContainsKey(q.k);
                var x0 = q.i * Tile; var z0 = q.j * Tile;
                var w2 = fresh ? work : null;
                jobs[q.k] = new Job { key = q.k, i = q.i, j = q.j, lod = q.lod, d = q.d, task = Task.Run(() => Compute(q.i, q.j, q.lod, w2)) };
            }
            return todo.Count + jobs.Count;
        }

        /// build everything needed near a point before the ride starts (loading), using every core
        public void Prime(float fx, float fz, float primeRadius = 300)
        {
            float keep = radius; radius = primeRadius;
            var want = new List<(string k, int i, int j, int lod)>();
            int i0 = Mathf.FloorToInt((fx - radius) / Tile), i1 = Mathf.FloorToInt((fx + radius) / Tile);
            int j0 = Mathf.FloorToInt((fz - radius) / Tile), j1 = Mathf.FloorToInt((fz + radius) / Tile);
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    float cx = (i + 0.5f) * Tile, cz = (j + 0.5f) * Tile;
                    float d = Mathf.Max(0, Mathf.Sqrt((cx - fx) * (cx - fx) + (cz - fz) * (cz - fz)) - Tile * 0.7f);
                    if (d > radius || tiles.ContainsKey(i + "," + j)) continue;
                    want.Add((i + "," + j, i, j, LodFor(d)));
                }
            var results = new Data[want.Count];
            var w = work;
            Parallel.For(0, want.Count, n => results[n] = Compute(want[n].i, want[n].j, want[n].lod, w));
            for (int n = 0; n < want.Count; n++) Finish(new Job { key = want[n].k, i = want[n].i, j = want[n].j, lod = want[n].lod }, results[n]);
            radius = keep;
        }

        void Remove(string k)
        {
            var t = tiles[k];
            Object.Destroy(t.go); Object.Destroy(t.mesh);
            tiles.Remove(k);
            onTile?.Invoke(t, false, null);
        }

        Data Compute(int i, int j, int lod, System.Func<int, int, float, float, object> extra)
        {
            int n = lod; float step = Tile / n, x0 = i * Tile, z0 = j * Tile;
            int S = n + 3;
            var H = new float[S * S];
            for (int b = 0; b < S; b++) for (int a = 0; a < S; a++) H[b * S + a] = route.Height(x0 + (a - 1) * step, z0 + (b - 1) * step);
            int V = n + 1, edge = 4 * n, vcount = V * V + edge;
            var pos = new Vector3[vcount]; var nor = new Vector3[vcount]; var road = new Vector2[vcount]; var vis = new Vector2[vcount];
            int p = 0;
            for (int b = 0; b < V; b++)
                for (int a = 0; a < V; a++, p++)
                {
                    float x = x0 + a * step, z = z0 + b * step;
                    pos[p] = new Vector3(x, H[(b + 1) * S + (a + 1)], z);
                    float hl = H[(b + 1) * S + a], hr = H[(b + 1) * S + a + 2], hd = H[b * S + a + 1], hu = H[(b + 2) * S + a + 1];
                    nor[p] = new Vector3(hl - hr, 2 * step, hd - hu).normalized;
                    route.RoadCoords(x, z, out float d, out float s);
                    road[p] = new Vector2(s < 0 ? 99 : Mathf.Abs(d), Mathf.Max(0, s));
                }
            // sun visibility: march toward the sun on a coarse grid, interpolate to vertices
            int SG = lod >= 32 ? 16 : 8; float sg = Tile / SG;
            var sv = new float[(SG + 1) * (SG + 1)];
            for (int b = 0; b <= SG; b++)
                for (int a = 0; a <= SG; a++)
                {
                    float x = x0 + a * sg, z = z0 + b * sg;
                    float h0 = route.Height(x, z) + 0.5f;
                    float v = 1, t = 3;
                    while (t < 520)
                    {
                        float dd = route.Height(x + sunDir.x * t, z + sunDir.z * t) - (h0 + sunDir.y * t);
                        if (dd > 0) { v = Mathf.Max(0, v - Mathf.Min(1, dd / 6)); if (v <= 0) break; }
                        t *= 1.18f;
                    }
                    sv[b * (SG + 1) + a] = v;
                }
            float kk = (float)n / SG;
            for (int b = 0; b < V; b++)
                for (int a = 0; a < V; a++)
                {
                    float fa = a / kk, fb = b / kk; int ia = Mathf.Min(SG - 1, (int)fa), ib = Mathf.Min(SG - 1, (int)fb);
                    float ta = fa - ia, tb = fb - ib;
                    float r0 = sv[ib * (SG + 1) + ia] * (1 - ta) + sv[ib * (SG + 1) + ia + 1] * ta;
                    float r1 = sv[(ib + 1) * (SG + 1) + ia] * (1 - ta) + sv[(ib + 1) * (SG + 1) + ia + 1] * ta;
                    vis[b * V + a] = new Vector2(0.12f + 0.88f * (r0 * (1 - tb) + r1 * tb), 0);
                }
            var idx = new List<int>(n * n * 6 + edge * 12);
            for (int b = 0; b < n; b++)
                for (int a = 0; a < n; a++)
                {
                    int v0 = b * V + a, v1 = v0 + 1, v2 = v0 + V, v3 = v2 + 1;
                    idx.Add(v0); idx.Add(v2); idx.Add(v1); idx.Add(v1); idx.Add(v2); idx.Add(v3);
                }
            // skirts (both windings: they only hide cracks between LODs)
            var ring = new List<int>();
            for (int a = 0; a < n; a++) ring.Add(a);
            for (int b = 0; b < n; b++) ring.Add(b * V + n);
            for (int a = n; a > 0; a--) ring.Add(n * V + a);
            for (int b = n; b > 0; b--) ring.Add(b * V);
            int skirt0 = V * V;
            for (int r = 0; r < ring.Count; r++)
            {
                int src = ring[r], q = skirt0 + r;
                pos[q] = pos[src] - new Vector3(0, 6, 0); nor[q] = nor[src]; road[q] = road[src]; vis[q] = vis[src];
            }
            for (int r = 0; r < ring.Count; r++)
            {
                int a0 = ring[r], a1 = ring[(r + 1) % ring.Count], s0 = skirt0 + r, s1 = skirt0 + (r + 1) % ring.Count;
                idx.Add(a0); idx.Add(a1); idx.Add(s0); idx.Add(a1); idx.Add(s1); idx.Add(s0);
                idx.Add(a0); idx.Add(s0); idx.Add(a1); idx.Add(a1); idx.Add(s0); idx.Add(s1);
            }
            return new Data { pos = pos, nor = nor, road = road, vis = vis, idx = idx.ToArray(), extra = extra?.Invoke(i, j, x0, z0) };
        }

        void Finish(Job jb, Data dat)
        {
            string k = jb.key;
            tiles.TryGetValue(k, out var old);
            var mesh = new Mesh { name = "tile " + k };
            if (dat.pos.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = dat.pos; mesh.normals = dat.nor; mesh.uv = dat.road; mesh.uv2 = dat.vis;
            mesh.SetTriangles(dat.idx, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Tile " + k);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.receiveShadows = true;
            mr.shadowCastingMode = jb.lod == 64 && route.biome != "plains" ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (old != null) { Object.Destroy(old.go); Object.Destroy(old.mesh); }
            var tile = new TerrainTile { key = k, i = jb.i, j = jb.j, lod = jb.lod, go = go, mesh = mesh, x0 = jb.i * Tile, z0 = jb.j * Tile };
            tiles[k] = tile;
            if (old == null) onTile?.Invoke(tile, true, dat.extra);
        }

        /// Ray march against the height field (for bullets hitting the ground). Infinity on a miss.
        public float Raycast(Vector3 o, Vector3 dir, float maxDist = 400)
        {
            float t = 0, prevT = 0, step = 1.5f;
            while (t < maxDist)
            {
                t += step;
                float x = o.x + dir.x * t, y = o.y + dir.y * t, z = o.z + dir.z * t;
                float diff = y - route.Height(x, z);
                if (diff < 0)
                {
                    float a = prevT, b = t;
                    for (int k = 0; k < 6; k++)
                    {
                        float m = (a + b) / 2;
                        float dm = o.y + dir.y * m - route.Height(o.x + dir.x * m, o.z + dir.z * m);
                        if (dm < 0) b = m; else a = m;
                    }
                    return b;
                }
                prevT = t;
                step = Mathf.Min(8, Mathf.Max(1.5f, diff * 0.5f));
            }
            return float.PositiveInfinity;
        }

        public void Dispose()
        {
            foreach (var t in tiles.Values) { Object.Destroy(t.go); Object.Destroy(t.mesh); }
            tiles.Clear();
            if (root) Object.Destroy(root.gameObject);
            Object.Destroy(mat);
        }
    }
}
