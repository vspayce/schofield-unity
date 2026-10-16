// Shot resolution for both sides (hit-scan against the enemies' hit spheres, the ground and the
// town's buildings). Ported from game/combat.js.
using UnityEngine;

namespace Schofield
{
    public struct ShotResult { public bool hit, killed, head; }

    public class Combat
    {
        public static int Bounty(string type) => type == "rifleman" ? 40 : type == "gunman" ? 30 : 25;
        const int HeadBonus = 25, HorseBonus = 10;
        readonly Game g;
        public Combat(Game game) { g = game; }

        /// ray from the camera (dir already includes spread)
        public ShotResult PlayerShot(Vector3 origin, Vector3 dir, WeaponDef W, Vector3 muzzle, bool guaranteed)
        {
            var hit = g.enemies.RayHit(origin, dir, W.range);
            float occT = g.terrain.Raycast(origin, dir, Mathf.Min(W.range, hit.HasValue ? hit.Value.t : W.range));
            string occKind = "ground";
            var ray = new Ray(origin, dir);
            foreach (var box in g.towns.solids)
                if (box.IntersectRay(ray, out float bt) && bt > 1.5f && bt < occT) { occT = bt; occKind = "wood"; }
            if (hit.HasValue && (guaranteed || hit.Value.t < occT))
            {
                var h = hit.Value; var e = h.enemy;
                float fall = 1 - MathX.Smoothstep(W.falloff0, W.falloff1, h.t) * (1 - W.floor);
                float dmg = W.damage * fall * (h.part == "head" ? W.headMult : 1);
                if (guaranteed && h.part != "horse") dmg = 999;   // Dead Eye shots are lethal
                bool killed = e.Damage(dmg, h.part, dir);
                Rattle(origin, dir, h.t);
                g.fx.Tracer(muzzle, h.point);
                g.fx.Blood(h.point, dir);
                Sound.I.Play("hit_flesh_" + (1 + Random.Range(0, 2)), 1.2f, 1, h.point);
                if (killed) OnKill(e, h.part);
                return new ShotResult { hit = true, killed = killed, head = h.part == "head" };
            }
            Rattle(origin, dir, Mathf.Min(occT, 260));
            var end = origin + dir * Mathf.Min(occT, 250);
            g.fx.Tracer(muzzle, end);
            if (occT < W.range)
            {
                if (occKind == "wood") { g.fx.Splinters(end, dir); Sound.I.Play("hit_wood_" + (1 + Random.Range(0, 2)), 0.9f, 1, end); }
                else
                {
                    g.fx.Dust(end, 5, 0.35f, null, 1.2f);
                    if (Random.value < 0.3f) Sound.I.Play("ricochet_" + (1 + Random.Range(0, 3)), 0.6f, 1, end);
                }
            }
            return default;
        }

        /// a hit or near-miss (within 1.5 m) on a glinting rifleman spoils his shot
        void Rattle(Vector3 origin, Vector3 dir, float maxT)
        {
            foreach (var e in g.enemies.list)
            {
                if (!e.alive || !(e.glintT > 0)) continue;
                var c = e.spheres[0].c;
                float t = Vector3.Dot(c - origin, dir);
                if (t < 0 || t > maxT + 2) continue;
                if (Vector3.Distance(origin + dir * t, c) < 1.5f) { e.glintT = 0; e.fireT += 1.6f; g.hud.Feed("Rattled him!", false); }
            }
        }

        void OnKill(Enemy e, string part)
        {
            var p = g.player;
            p.stats.kills++;
            bool head = part == "head";
            if (head) p.stats.headshots++;
            int bounty = Bounty(e.type);
            string label = e.type == "rifleman" ? "Rifleman" : e.type == "gunman" ? "Gunman" : "Outlaw";
            if (part == "horse") { label = "Unhorsed"; bounty = HorseBonus + 10; }
            if (head) bounty += HeadBonus;
            g.AddBounty(bounty, head ? $"HEADSHOT +${bounty}" : $"{label} +${bounty}", head);
            p.AddDeadeye(head ? 0.16f : 0.1f);
        }

        /// an enemy fires at the player or the coach
        public void EnemyShot(Enemy enemy, Vector3 muzzle, float dist, float acc, float dmgPlayer, float dmgCoach, bool rifle = false)
        {
            bool atPlayer = Random.value < 0.6f;
            var target = atPlayer ? g.player.HeadPos + new Vector3(0, -0.35f, 0) : g.coach.TargetPoint();
            float chance = acc * (1 - MathX.Smoothstep(10, rifle ? 220 : 48, dist) * 0.7f);
            if (g.coach.speed > g.coach.cruise + 3) chance *= 0.7f;
            bool hits = Random.value < chance;
            var dir = (target - muzzle).normalized;
            g.fx.Muzzle(muzzle, dir, rifle);
            if (rifle && dist > 60) Sound.I.Play("rifle_shot_far", 1, 1, muzzle, dist / 340, false, 900);
            else Sound.I.Play("schofield_shot2", 0.9f, 0.85f + Random.value * 0.1f, muzzle, 0, false, 600);
            if (hits)
            {
                g.fx.Tracer(muzzle, target);
                if (atPlayer) g.player.Damage(dmgPlayer * g.diff.dmgMult, muzzle);
                else
                {
                    g.DamageCoach(dmgCoach * g.diff.dmgMult);
                    g.fx.Splinters(target, dir);
                    Sound.I.Play("hit_wood_" + (1 + Random.Range(0, 2)), 1, 1, target);
                    g.OnCoachDamage();
                }
            }
            else
            {
                var miss = target + new Vector3((Random.value - 0.5f) * 3, Random.value * 1.6f, (Random.value - 0.5f) * 3);
                g.fx.Tracer(muzzle, miss + (miss - muzzle).normalized * 60);
                if (atPlayer || Random.value < 0.5f) Sound.I.Play("bullet_whiz_" + (1 + Random.Range(0, 3)), 0.8f, 1, null, rifle ? 0 : 0.02f);
                if (Random.value < 0.35f) Sound.I.Play("ricochet_" + (1 + Random.Range(0, 3)), 0.5f, 1, miss);
            }
        }
    }
}
