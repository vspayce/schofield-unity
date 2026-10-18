// Dynamite: three sticks a run, thrown along the aim with a lit fuse; whatever is within 20 m when it
// goes off is blown out of the saddle. Ported from game/dynamite.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class Dynamite
    {
        readonly Game g;
        public int stock = 3;
        class Bomb { public GameObject root; public Vector3 vel; public float age, trail; }
        readonly List<Bomb> active = new List<Bomb>();
        readonly Material body, fuse, ember;

        public Dynamite(Game game)
        {
            g = game;
            body = Assets.Lit(MathX.Hex("#8e241c"), 0.38f); fuse = Assets.Lit(MathX.Hex("#30241c"), 0);
            ember = new Material(Assets.Urp(false)); ember.SetColor(Assets.BaseColor, new Color(1, 0.64f, 0.23f));
            g.hud.SetDynamite(stock);
        }

        public bool Throw()
        {
            if (stock <= 0 || g.over) { if (stock <= 0) g.hud.Banner("No dynamite left", "", 1.2f); return false; }
            var root = new GameObject("Dynamite");
            root.transform.SetParent(g.world, false);
            GameObject Part(PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
            {
                var o = GameObject.CreatePrimitive(t); Object.Destroy(o.GetComponent<Collider>());
                o.transform.SetParent(root.transform, false); o.transform.localPosition = pos; o.transform.localScale = scale;
                o.GetComponent<Renderer>().sharedMaterial = m; return o;
            }
            Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.17f, 0.31f, 0.17f), body);
            Part(PrimitiveType.Cylinder, new Vector3(0, 0.37f, 0), new Vector3(0.036f, 0.08f, 0.036f), fuse);
            Part(PrimitiveType.Sphere, new Vector3(0, 0.47f, 0), Vector3.one * 0.08f, ember);
            var p = g.player;
            root.transform.position = p.model.hand ? p.model.hand.position : p.HeadPos;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, p.aimDir);
            active.Add(new Bomb { root = root, vel = p.aimDir * 21 + g.coach.fwd * 8 + new Vector3(0, 6.5f, 0) });
            stock--;
            g.hud.SetDynamite(stock);
            Sound.I.Play("ui_click", 0.55f, 0.72f);
            return true;
        }

        public void Update(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var b = active[i];
                b.age += dt;
                b.vel.y -= 13 * dt;
                var t = b.root.transform;
                t.position += b.vel * dt;
                t.rotation = Quaternion.FromToRotation(Vector3.up, b.vel.normalized) * Quaternion.Euler(0, b.age * 9 * Mathf.Rad2Deg, 0);
                b.trail -= dt;
                if (b.trail <= 0)
                {
                    b.trail = 0.055f;
                    var e = t.TransformPoint(0, 0.47f, 0);
                    g.fx.add.Spawn(e, life: 0.1f, size: 0.12f, size1: 0.08f, color: new Color(1, 0.44f, 0.12f), fadeIn: 0);
                    g.fx.smoke.Spawn(e, new Vector3(0, 0.6f, 0), life: 0.3f, size: 0.08f, size1: 0.22f, color: new Color(0.85f, 0.83f, 0.8f), alpha: 0.45f, drag: 2, grav: -0.3f);
                }
                float ground = g.route.Surface(t.position.x, t.position.z) + 0.15f;
                if (b.age >= 1.55f || (b.age > 0.16f && t.position.y <= ground))
                {
                    var impact = t.position; impact.y = Mathf.Max(ground, impact.y);
                    Object.Destroy(b.root);
                    active.RemoveAt(i);
                    Explode(impact);
                }
            }
        }

        void Explode(Vector3 at)
        {
            const float radius = 20;
            g.fx.Explosion(at, radius);
            g.shake += 1.8f;
            g.HitStop(0.12f);
            Sound.I.Play("shotgun_shot", 1, 0.58f, at, 0, false, 900);
            Sound.I.Play("hit_wood_1", 0.8f, 0.6f, at, 0.04f, false, 700);
            int riders = 0, animals = 0;
            foreach (var e in g.enemies.list.ToArray())
            {
                if (!e.alive) continue;
                var ep = e is Rider r ? r.pos : e.spheres[1].c;
                if (Vector3.Distance(ep, at) > radius) continue;
                var away = ep - at; away = away.sqrMagnitude < 0.001f ? Vector3.forward : away.normalized;
                if (e.Damage(999, e.type == "rider" ? "horse" : "body", away)) { g.combat.OnKill(e, e.type == "rider" ? "horse" : "body"); riders++; }
            }
            if (g.wildlife != null) animals = g.wildlife.Blast(at, radius);
            var parts = new List<string>();
            if (riders > 0) parts.Add($"{riders} {(riders == 1 ? "outlaw" : "outlaws")} down");
            if (animals > 0) parts.Add($"{animals} {(animals == 1 ? "animal" : "animals")} down");
            g.hud.Banner("DYNAMITE!", parts.Count > 0 ? string.Join(", ", parts) : "No one caught in the blast", 1.7f);
        }

        public void Dispose()
        {
            foreach (var b in active) if (b.root) Object.Destroy(b.root);
            active.Clear();
            Object.Destroy(body); Object.Destroy(fuse); Object.Destroy(ember);
        }
    }
}
