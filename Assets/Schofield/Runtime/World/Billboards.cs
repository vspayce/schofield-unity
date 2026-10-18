// Roadside billboards you can shoot for a wagon repair: Greeley's Saloon and Minnie's Haberdashery.
// Put a round through the sign and the driver takes the hint — the coach is patched up on the spot.
// One repair per board, spaced out. Ported from world/billboards.js (the faces are lettered with a
// world-space canvas instead of a drawn texture).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Schofield
{
    public class Billboards
    {
        public class Ad { public string id, top, mid, low; public int repair; public Color paper, ink, accent; }
        static readonly Ad[] Ads =
        {
            new Ad { id = "greeley", repair = 18, top = "GREELEY'S", mid = "SALOON", low = "WHISKEY · BEER · BEDS", paper = MathX.Hex("#e8d7a8"), ink = MathX.Hex("#5a1a12"), accent = MathX.Hex("#8a6a22") },
            new Ad { id = "minnie", repair = 14, top = "MINNIE'S", mid = "HABERDASHERY", low = "HATS · CLOTH · NOTIONS", paper = MathX.Hex("#dfe6dc"), ink = MathX.Hex("#2c4a3a"), accent = MathX.Hex("#8a2f4a") },
        };

        public struct Hit { public Board board; public float t; public Vector3 point; }

        public class Board
        {
            public bool used;
            readonly Ad ad; readonly Vector3 centre; readonly float radius; readonly Material face; readonly CanvasGroup letters;
            public Board(Route route, Transform parent, Ad ad, float s, float side, List<Object> owned)
            {
                this.ad = ad;
                var f = route.Frame(s);
                float off = route.biome == "gorge" ? 9 : 15 + Random.value * 4;
                if (route.biome == "gorge") side = -route.riverSide;
                var root = new GameObject("Billboard " + ad.id).transform; root.SetParent(parent, false);
                var at = new Vector3(f.x + f.rx * off * side, 0, f.z + f.rz * off * side);
                // face the coming coach: aim at the road a good way back, like a real roadside sign
                var toward = route.Frame(Mathf.Max(0, s - 45));
                root.rotation = Quaternion.Euler(0, Mathf.Atan2(toward.x - at.x, toward.z - at.z) * Mathf.Rad2Deg, 0);
                root.position = at;
                const float W = 11, H = 5.5f, bse = 3.2f;
                float ground = float.MinValue;
                var feet = new float[2]; int fi = 0;
                foreach (float px in new[] { -W / 2 + 0.6f, W / 2 - 0.6f }) { var foot = root.TransformPoint(px, 0, 0); feet[fi] = route.Height(foot.x, foot.z); ground = Mathf.Max(ground, feet[fi++]); }
                at.y = ground + 0.15f; root.position = at;
                var post = Assets.Lit(MathX.Hex("#4a3524"), 0.05f); var back = Assets.Lit(MathX.Hex("#6b5336"), 0);
                face = Assets.Lit(ad.paper, 0.08f);
                owned.Add(post); owned.Add(back); owned.Add(face);
                GameObject Box(Vector3 pos, Vector3 size, Material m, Vector3? euler = null)
                {
                    var o = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(o.GetComponent<Collider>());
                    o.transform.SetParent(root, false); o.transform.localPosition = pos; o.transform.localScale = size;
                    if (euler.HasValue) o.transform.localRotation = Quaternion.Euler(euler.Value);
                    o.GetComponent<Renderer>().sharedMaterial = m; return o;
                }
                // each post runs down to its own footing, so a board on a slope never hangs in the air
                fi = 0;
                foreach (float px in new[] { -W / 2 + 0.6f, W / 2 - 0.6f })
                {
                    float sink = at.y - feet[fi++] + 0.6f, len = bse + H + sink;
                    Box(new Vector3(px, bse + H - len / 2, 0), new Vector3(0.34f, len, 0.34f), post);
                }
                foreach (float sx in new[] { -1f, 1f }) Box(new Vector3(sx * (W / 2 - 1.1f), bse * 0.85f, -1.2f), new Vector3(0.22f, 4.4f, 0.22f), post, new Vector3(-0.5f * Mathf.Rad2Deg, 0, 0));
                Box(new Vector3(0, bse + H / 2, 0), new Vector3(W, H, 0.16f), back);
                Box(new Vector3(0, bse + H / 2, 0.085f), new Vector3(W - 0.1f, H - 0.1f, 0.01f), face);
                // lettering: a world-space canvas laid on the face
                var cgo = new GameObject("Letters", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
                var crt = (RectTransform)cgo.transform; crt.SetParent(root, false);
                crt.sizeDelta = new Vector2(512, 256); crt.localScale = Vector3.one * (W / 512f);
                crt.localPosition = new Vector3(0, bse + H / 2, 0.1f); crt.localRotation = Quaternion.Euler(0, 180, 0);
                cgo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                letters = cgo.GetComponent<CanvasGroup>();
                var frame = UIKit.Img("Frame", crt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(486, 230), null, new Color(0, 0, 0, 0));
                var ol = frame.gameObject.AddComponent<Outline>(); ol.effectColor = ad.accent; ol.effectDistance = new Vector2(4, 4);
                Text L(string str, float y, int size, Color c) { var t = UIKit.Txt("t", crt, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(470, size + 16), str, size, c, TextAnchor.MiddleCenter, true); t.resizeTextForBestFit = true; t.resizeTextMaxSize = size; t.resizeTextMinSize = 12; Object.Destroy(t.GetComponent<Shadow>()); return t; }
                L(ad.top, 62, 52, ad.ink); L(ad.mid, -6, 64, ad.ink); L(ad.low, -66, 30, ad.accent);
                centre = root.TransformPoint(0, bse + H / 2, 0.1f);
                radius = Mathf.Max(W, H) * 0.42f;
            }

            public bool HitTest(Vector3 o, Vector3 dir, float range, out float t)
            {
                t = 0;
                if (used) return false;
                var oc = o - centre;
                float b = Vector3.Dot(oc, dir), c = oc.sqrMagnitude - radius * radius, h = b * b - c;
                if (h < 0) return false;
                t = -b - Mathf.Sqrt(h);
                return t > 0 && t <= range;
            }

            public void Strike(Game game, Vector3 point)
            {
                used = true;
                float before = game.coach.hp;
                game.coach.hp = Mathf.Min(100, game.coach.hp + ad.repair);
                int got = Mathf.RoundToInt(game.coach.hp - before);
                Sound.I.Play("hit_wood_1", 1, 0.85f, point);
                Sound.I.Play("bell_town", 0.35f, 1.5f, null, 0.1f);
                game.fx.Splinters(point, Vector3.forward);
                game.hud.Banner(ad.mid, got > 0 ? $"Wagon patched +{got}" : "Wagon already sound", 1.8f);
                Assets.SetColor(face, ad.paper * 0.55f);           // a shot-out board reads as shot out
                letters.alpha = 0.55f;
            }
        }

        readonly GameObject root;
        readonly List<Board> list = new List<Board>();
        readonly List<Object> owned = new List<Object>();

        public Billboards(Route route, Transform parent, int count)
        {
            root = new GameObject("Billboards"); root.transform.SetParent(parent, false);
            var rnd = new Rng(route.def.seed + 41);
            int adOffset = rnd.Next() < 0.5f ? 0 : 1;
            const float first = 0.16f, last = 0.9f;
            for (int i = 0; i < count; i++)
            {
                float t = first + (last - first) * ((i + 0.5f) / count) + (rnd.Next() - 0.5f) * 0.05f;
                float s = route.len * t;
                if (s < 80 || s > route.len - 120) continue;
                if (route.OnBridge(s - 10) || route.OnBridge(s + 10) || route.InTown(s) || (route.hasFalls && Mathf.Abs(s - route.fallsS) < route.fallsWidth * 0.5f + 30)) continue;
                list.Add(new Board(route, root.transform, Ads[(i + adOffset) % Ads.Length], s, rnd.Next() < 0.5f ? -1 : 1, owned));
            }
        }

        /// nearest board along the ray
        public Hit? RayHit(Vector3 o, Vector3 dir, float range)
        {
            Hit? best = null;
            foreach (var b in list) if (b.HitTest(o, dir, best.HasValue ? best.Value.t : range, out float t)) best = new Hit { board = b, t = t, point = o + dir * t };
            return best;
        }

        public void Dispose() { if (root) Object.Destroy(root); foreach (var o in owned) if (o) Object.Destroy(o); }
    }
}
