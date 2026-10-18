// Instanced props + grass. Placement is deterministic per terrain tile; each prop kind is drawn with
// GPU instancing per mesh part, and the instance lists are rebuilt only when the set of visible tiles
// changes (tile-level frustum cull). Ported from world/scatter.js.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Schofield
{
    public class Scatter
    {
        class Part { public Mesh mesh; public int sub; public Material mat; public Matrix4x4 local; public bool ident; }
        class Kind { public List<Part> parts = new List<Part>(); public float radius = 1; }
        class TileData { public TerrainTile tile; public Dictionary<string, List<Matrix4x4>> kinds; public List<Vector4> grass; }

        readonly Route route;
        readonly float density;
        readonly Dictionary<string, Kind> kinds = new Dictionary<string, Kind>();
        readonly Dictionary<string, TileData> byTile = new Dictionary<string, TileData>();
        readonly Dictionary<string, List<(string kind, float x, float z, float rot, float s)>> globals = new Dictionary<string, List<(string, float, float, float, float)>>();
        readonly Dictionary<string, List<Matrix4x4>> lists = new Dictionary<string, List<Matrix4x4>>();
        readonly HashSet<string> farOnly = new HashSet<string>();
        string sig = ""; bool dirty;
        readonly Plane[] planes = new Plane[6];

        // grass
        readonly bool grassOn; Mesh grassMesh; Material grassMat;
        readonly List<Matrix4x4> grassList = new List<Matrix4x4>();
        string grassKey = ""; int grassCap;

        public Scatter(Route route, float density, float grassAmount)
        {
            this.route = route; this.density = density;
            foreach (var r in route.def.scatter) if (r.far || r.poles) farOnly.Add(r.kind);
            PlaceAlongRoad();
            grassOn = grassAmount > 0 && route.def.grass != null && route.def.grass.density > 0;
            grassCap = Mathf.FloorToInt(26000 * grassAmount);
            if (grassOn) InitGrass();
        }

        // ---------------------------------------------------------------- catalogue
        Kind GetKind(string name)
        {
            if (kinds.TryGetValue(name, out var k)) return k;
            k = new Kind();
            var props = Assets.Model("props");
            var root = props ? Assets.Find(props.transform, name) : null;
            if (root)
            {
                var inv = root.localToWorldMatrix.inverse;
                bool rockish = name.StartsWith("Rock") || name.StartsWith("Boulder") || name.StartsWith("Cliff");
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (!mr || !mf.sharedMesh) continue;
                    var local = inv * mf.transform.localToWorldMatrix;
                    var mats = mr.sharedMaterials;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    {
                        var src = mats[Mathf.Min(s, mats.Length - 1)];
                        var m = new Material(src) { enableInstancing = true };
                        if (rockish) { var c = Assets.GetColor(m); var t = route.def.terrain.propRock; Assets.SetColor(m, new Color(c.r * t.r, c.g * t.g, c.b * t.b, c.a)); }
                        k.parts.Add(new Part { mesh = mf.sharedMesh, sub = s, mat = m, local = local, ident = local == Matrix4x4.identity });
                    }
                    var b = mf.sharedMesh.bounds;
                    k.radius = Mathf.Max(k.radius, b.extents.magnitude + b.center.magnitude);
                }
            }
            if (k.parts.Count == 0)
            {
                // stand-in: a box, so the game still runs without props.glb
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var mesh = cube.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(cube);
                var m = Assets.Lit(new Color(0.45f, 0.4f, 0.33f)); m.enableInstancing = true;
                k.parts.Add(new Part { mesh = mesh, sub = 0, mat = m, local = Matrix4x4.TRS(new Vector3(0, 0.5f, 0), Quaternion.identity, Vector3.one), ident = false });
            }
            kinds[name] = k;
            return k;
        }

        // ------------------------------------------------------------- placement
        void Push(string kind, float x, float z, float rot, float s = 1)
        {
            string key = Mathf.FloorToInt(x / Terrain.Tile) + "," + Mathf.FloorToInt(z / Terrain.Tile);
            if (!globals.TryGetValue(key, out var l)) globals[key] = l = new List<(string, float, float, float, float)>();
            l.Add((kind, x, z, rot, s));
        }

        void PlaceAlongRoad()
        {
            var r = route; var rnd = new Rng(r.def.seed + 99);
            bool poles = false, fence = false;
            foreach (var q in r.def.scatter) { poles |= q.poles; fence |= q.fence; }
            if (poles)
                for (float s = 30; s < r.len; s += 42)
                {
                    if (r.InTown(s) || r.OnBridge(s - 12) || r.OnBridge(s + 12) || (r.hasFalls && Mathf.Abs(s - r.fallsS) < 30)) continue;
                    var f = r.Frame(s);
                    float ps = r.def.gorge != null ? -r.riverSide : 1;     // on the ledge, the wall side
                    Push("TelegraphPole", f.x + f.rx * 9.5f * ps, f.z + f.rz * 9.5f * ps, Mathf.Atan2(f.tx, f.tz) + Mathf.PI / 2 + (rnd.Next() - 0.5f) * 0.1f);
                }
            if (fence)
            {
                float s = 60;
                while (s < r.len - 60)
                {
                    float run = 60 + rnd.Next() * 140; float side = rnd.Next() < 0.5f ? -1 : 1; float off = 8 + rnd.Next() * 5;
                    if (r.def.gorge != null) { side = -r.riverSide; off = 7.4f; }
                    for (float t = s; t < Mathf.Min(r.len - 60, s + run); t += 3)
                    {
                        if (r.InTown(t) || t < 130 || t > r.len - 150 || r.OnBridge(t - 6) || r.OnBridge(t + 6)) continue;
                        var f = r.Frame(t + 1.5f);
                        Push("Fence_Rail", f.x + f.rx * off * side, f.z + f.rz * off * side, Mathf.Atan2(f.tx, f.tz) + Mathf.PI / 2 + (rnd.Next() - 0.5f) * 0.08f);
                    }
                    s += run + 80 + rnd.Next() * 250;
                }
            }
        }

        /// called by Terrain when a tile is created/removed; `extra` is what Compute made for it
        public void OnTile(TerrainTile tile, bool created, object extra)
        {
            if (!created) { byTile.Remove(tile.key); dirty = true; return; }
            var td = extra as TileData ?? (TileData)Compute(tile.i, tile.j, tile.x0, tile.z0);
            td.tile = tile;
            byTile[tile.key] = td;
            dirty = true;
        }

        /// placement for one tile: pure, so the terrain runs it on a worker thread
        public object Compute(int ti, int tj, float x0, float z0)
        {
            var tile = new TerrainTile { i = ti, j = tj, x0 = x0, z0 = z0, key = ti + "," + tj };
            var r = route;
            int seed = (tile.i * 73856093) ^ (tile.j * 19349663) ^ r.def.seed;
            var rnd = new Rng(seed);
            var outK = new Dictionary<string, List<Matrix4x4>>();
            float area = Terrain.Tile * Terrain.Tile / 10000f;
            var town = r.TownRange;
            foreach (var rule in r.def.scatter)
            {
                if (rule.density <= 0) continue;
                int n = Mathf.RoundToInt(rule.density * area * density * (0.8f + rnd.Next() * 0.4f));
                var arr = new List<Matrix4x4>();
                for (int k = 0; k < n * 2 && arr.Count < n; k++)
                {
                    float x = tile.x0 + rnd.Next() * Terrain.Tile, z = tile.z0 + rnd.Next() * Terrain.Tile;
                    if (rule.cluster > 0)
                    {
                        float c = r.noise.Noise(x / 90 + 33, z / 90 - 11) * 0.5f + 0.5f;
                        if (rnd.Next() > MathX.Smoothstep(rule.cluster, rule.cluster + 0.35f, c)) continue;
                    }
                    r.RoadCoords(x, z, out float d, out float s);
                    if (s >= 0 && Mathf.Abs(d) < rule.minRoad) continue;
                    bool inTown = r.InTown(s) || (r.HasGhostTown && s > town.x - 30 && s < town.y + 30) || s < 130 || s > r.len - 150;
                    if (s >= 0 && inTown && Mathf.Abs(d) < (rule.far ? 32 : 40)) continue;
                    float h = r.Height(x, z);
                    if (r.RailDist(x, z) < (rule.far ? 7 : 6)) continue;          // nothing planted on the railroad
                    if (r.def.gorge != null && s >= 0 && (h < r.RiverBedAt(s) + 3.6f || (r.OnBridge(s) && Mathf.Abs(d) < r.bridgeCutHalf))) continue;   // not in the river or the trestle's cut
                    if (!float.IsNaN(rule.minH) && h < rule.minH) continue;
                    if (!float.IsNaN(rule.maxH) && h > rule.maxH) continue;
                    float hx = r.Height(x + 1.5f, z) - h, hz = r.Height(x, z + 1.5f) - h;
                    float slope = Mathf.Sqrt(hx * hx + hz * hz) / 1.5f;
                    if (!float.IsNaN(rule.minSlope) ? slope < rule.minSlope : slope > 0.7f) continue;
                    float sc = rule.scale.x + rnd.Next() * (rule.scale.y - rule.scale.x);
                    var rot = MathX.YXZ((rnd.Next() - 0.5f) * 0.08f, rnd.Next() * Mathf.PI * 2, (rnd.Next() - 0.5f) * 0.08f);
                    arr.Add(Matrix4x4.TRS(new Vector3(x, h - 0.08f * sc, z), rot, new Vector3(sc, sc * (0.9f + rnd.Next() * 0.2f), sc)));
                }
                if (arr.Count > 0) outK[rule.kind] = arr;
            }
            if (globals.TryGetValue(tile.key, out var gl))
                foreach (var g in gl)
                {
                    if (r.RailDist(g.x, g.z) < 5) continue;
                    float h = r.Height(g.x, g.z);
                    if (!outK.TryGetValue(g.kind, out var l)) outK[g.kind] = l = new List<Matrix4x4>();
                    l.Add(Matrix4x4.TRS(new Vector3(g.x, h - 0.1f, g.z), Quaternion.Euler(0, g.rot * Mathf.Rad2Deg, 0), Vector3.one * g.s));
                }
            return new TileData { kinds = outK, grass = grassOn ? GrassFor(tile, rnd) : null };
        }

        // ------------------------------------------------------------------ draw
        public void Update(Camera cam, Vector3 focus)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            var vis = new List<(string key, int band)>();
            var sb = new System.Text.StringBuilder();
            foreach (var kv in byTile)
            {
                var t = kv.Value.tile;
                var b = new Bounds(new Vector3(t.x0 + Terrain.Tile / 2, 200, t.z0 + Terrain.Tile / 2), new Vector3(Terrain.Tile + 24, 800, Terrain.Tile + 24));
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                float d = Vector2.Distance(new Vector2(t.x0 + Terrain.Tile / 2, t.z0 + Terrain.Tile / 2), new Vector2(focus.x, focus.z));
                int band = d > 300 ? 2 : d > 170 ? 1 : 0;
                vis.Add((kv.Key, band));
                sb.Append(kv.Key).Append(band).Append('|');
            }
            var s = sb.ToString();
            if (s != sig || dirty) { sig = s; dirty = false; Rebuild(vis); }
            foreach (var kv in lists) Draw(kv.Key, kv.Value);
            if (grassOn) UpdateGrass(cam);
        }

        void Rebuild(List<(string key, int band)> vis)
        {
            foreach (var l in lists.Values) l.Clear();
            foreach (var (key, band) in vis)
            {
                var e = byTile[key];
                foreach (var kv in e.kinds)
                {
                    if (band == 2 && !farOnly.Contains(kv.Key)) continue;
                    if (!lists.TryGetValue(kv.Key, out var l)) lists[kv.Key] = l = new List<Matrix4x4>();
                    l.AddRange(kv.Value);
                }
            }
        }

        readonly List<Matrix4x4> tmp = new List<Matrix4x4>(1024);
        void Draw(string kind, List<Matrix4x4> mats)
        {
            if (mats.Count == 0) return;
            var K = GetKind(kind);
            foreach (var p in K.parts)
            {
                var rp = new RenderParams(p.mat) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000) };
                for (int start = 0; start < mats.Count; start += 1023)
                {
                    int count = Mathf.Min(1023, mats.Count - start);
                    if (p.ident) Graphics.RenderMeshInstanced(rp, p.mesh, p.sub, mats, count, start);
                    else
                    {
                        tmp.Clear();
                        for (int i = 0; i < count; i++) tmp.Add(mats[start + i] * p.local);
                        Graphics.RenderMeshInstanced(rp, p.mesh, p.sub, tmp, count, 0);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ grass
        void InitGrass()
        {
            var G = route.def.grass; var T = route.def.terrain;
            // three crossed cards per clump
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            foreach (float a in new[] { 0f, Mathf.PI / 2, Mathf.PI / 4 })
            {
                float cx = Mathf.Cos(a) * 0.45f, cz = Mathf.Sin(a) * 0.45f;
                int b = v.Count;
                v.Add(new Vector3(-cx, 0, -cz)); v.Add(new Vector3(cx, 0, cz)); v.Add(new Vector3(-cx, 1, -cz)); v.Add(new Vector3(cx, 1, cz));
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
                tri.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            grassMesh = new Mesh { name = "grass" };
            grassMesh.SetVertices(v); grassMesh.SetUVs(0, uv); grassMesh.SetTriangles(tri, 0);
            grassMesh.normals = v.ConvertAll(_ => Vector3.up).ToArray();
            grassMesh.RecalculateBounds();
            grassMat = new Material(Assets.Shader("Grass")) { enableInstancing = true };
            grassMat.SetTexture("_MainTex", Assets.Tex("grass_blade"));
            grassMat.SetColor("_Color", G.color);
            grassMat.SetTexture("_Macro", Assets.Tex("cloud_noise"));
            grassMat.SetColor("_Tint1", T.tint1); grassMat.SetColor("_Tint2", T.tint2);
            grassMat.SetVector("_GreenMix", T.greenMix);
            grassMat.SetFloat("_Wind", route.def.wind);
        }

        List<Vector4> GrassFor(TerrainTile tile, Rng rnd)
        {
            var r = route; var G = r.def.grass;
            int n = Mathf.FloorToInt(2600 * G.density * (grassCap / 26000f));
            var arr = new List<Vector4>(n);
            for (int k = 0; k < n * 1.6f && arr.Count < n; k++)
            {
                float x = tile.x0 + rnd.Next() * Terrain.Tile, z = tile.z0 + rnd.Next() * Terrain.Tile;
                float patch = r.noise.Noise(x / 14, z / 14) * 0.5f + 0.5f;
                if (rnd.Next() > patch * 1.3f) continue;
                r.RoadCoords(x, z, out float d, out float s);
                if (s >= 0 && Mathf.Abs(d) < Route.RoadHalf + 0.6f + rnd.Next() * 1.5f) continue;
                float h = r.Height(x, z);
                float hx = r.Height(x + 1, z) - h, hz = r.Height(x, z + 1) - h;
                if (Mathf.Sqrt(hx * hx + hz * hz) > 0.5f) continue;
                if (h > r.def.terrain.snowLine - 5) continue;
                if (r.def.gorge != null && s >= 0 && h < r.RiverBedAt(s) + 3.2f) continue;
                if (r.RailDist(x, z) < 3.4f + rnd.Next() * 1.2f) continue;   // ballast, not grass
                arr.Add(new Vector4(x, h, z, (0.6f + rnd.Next() * 0.8f) * G.height * 1.6f));
            }
            return arr;
        }

        void UpdateGrass(Camera cam)
        {
            var cp = cam.transform.position;
            grassMat.SetVector("_Focus", cp);
            string key = Mathf.RoundToInt(cp.x / 12) + "," + Mathf.RoundToInt(cp.z / 12) + "," + byTile.Count;
            if (key != grassKey)
            {
                grassKey = key;
                grassList.Clear();
                const float R2 = 64 * 64;
                foreach (var e in byTile.Values)
                {
                    if (e.grass == null) continue;
                    var t = e.tile;
                    if (cp.x < t.x0 - 64 || cp.x > t.x0 + Terrain.Tile + 64 || cp.z < t.z0 - 64 || cp.z > t.z0 + Terrain.Tile + 64) continue;
                    foreach (var g in e.grass)
                    {
                        if (grassList.Count >= grassCap) break;
                        float dx = g.x - cp.x, dz = g.z - cp.z;
                        if (dx * dx + dz * dz > R2) continue;
                        float rot = (g.x * 12.9898f + g.z * 78.233f) % 6.283f;
                        grassList.Add(Matrix4x4.TRS(new Vector3(g.x, g.y - 0.05f, g.z), Quaternion.Euler(0, rot * Mathf.Rad2Deg, 0), Vector3.one * g.w));
                    }
                }
            }
            if (grassList.Count == 0) return;
            var rp = new RenderParams(grassMat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, worldBounds = new Bounds(cp, Vector3.one * 300) };
            for (int start = 0; start < grassList.Count; start += 1023)
                Graphics.RenderMeshInstanced(rp, grassMesh, 0, grassList, Mathf.Min(1023, grassList.Count - start), start);
        }

        public void Dispose()
        {
            foreach (var k in kinds.Values) foreach (var p in k.parts) Object.Destroy(p.mat);
            if (grassMat) Object.Destroy(grassMat);
            if (grassMesh) Object.Destroy(grassMesh);
        }
    }
}
