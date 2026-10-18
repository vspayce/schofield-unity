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
            // game before outlaws: whichever is nearer along the ray
            var beast = g.wildlife?.RayHit(origin, dir, W.range);
            if (beast.HasValue && (!hit.HasValue || beast.Value.t < hit.Value.t) && g.terrain.Raycast(origin, dir, beast.Value.t) >= beast.Value.t - 0.5f)
            {
                var bh = beast.Value;
                float bfall = 1 - MathX.Smoothstep(W.falloff0, W.falloff1, bh.t) * (1 - W.floor);
                bool bk = bh.b.Damage(W.damage * bfall, dir);
                g.fx.Tracer(muzzle, bh.point); g.fx.Blood(bh.point, dir);
                Sound.I.Play("hit_flesh_" + (1 + Random.Range(0, 2)), 1.1f, 1, bh.point);
                if (bk) { g.wildlife.OnKill(bh.b); g.HitStop(0.05f); }
                return new ShotResult { hit = true, killed = bk };
            }
            // a roadside board in front of everything else takes the round (and repairs the coach)
            var board = g.boards?.RayHit(origin, dir, hit.HasValue ? hit.Value.t : W.range);
            if (board.HasValue && g.terrain.Raycast(origin, dir, board.Value.t) >= board.Value.t - 0.5f)
            {
                g.fx.Tracer(muzzle, board.Value.point);
                board.Value.board.Strike(g, board.Value.point);
                return default;
            }
            float occT = g.terrain.Raycast(origin, dir, Mathf.Min(W.range, hit.HasValue ? hit.Value.t : W.range));
            string occKind = "ground";
            var th = g.crossing?.train.RayHit(origin, dir, occT);
            if (th.HasValue) { occT = th.Value.t; occKind = th.Value.iron ? "iron" : "wood"; }
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
                else if (occKind == "iron") { g.fx.Sparks(end); Sound.I.Play("ricochet_" + (1 + Random.Range(0, 3)), 0.8f, 1, end); }
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

        public void OnKill(Enemy e, string part)
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

        /// one of the hired guns fires at an enemy
        public bool EscortShot(Escort esc, Vector3 muzzle, Vector3 aim, Enemy e, string part, bool hits)
        {
            var cfg = esc.cfg;
            var dir = (aim - muzzle).normalized;
            g.fx.Muzzle(muzzle, dir, cfg.rifle);
            Sound.I.Play(cfg.snd, 0.85f, cfg.pitch * (0.97f + Random.value * 0.06f), muzzle, 0, false, 600);
            if (!hits)
            {
                var miss = aim + new Vector3((Random.value - 0.5f) * 2.4f, (Random.value - 0.5f) * 1.2f, (Random.value - 0.5f) * 2.4f);
                var md = (miss - muzzle).normalized;
                float t = g.terrain.Raycast(muzzle, md, 120);
                var end = muzzle + md * Mathf.Min(t, 120);
                g.fx.Tracer(muzzle, end);
                if (t < 120) g.fx.Dust(end, 4, 0.3f, null, 1);
                return false;
            }
            float dmg = cfg.dmg * (part == "head" ? cfg.headMult : 1);
            bool killed = e.Damage(dmg, part, dir);
            g.fx.Tracer(muzzle, aim);
            g.fx.Blood(aim, dir);
            Sound.I.Play("hit_flesh_" + (1 + Random.Range(0, 2)), 1, 1, aim);
            if (killed)
            {
                int bounty = Bounty(e.type);
                if (part == "horse") bounty = HorseBonus + 10;
                if (part == "head") bounty += HeadBonus;
                esc.Credit(bounty, part == "head");
            }
            return killed;
        }

        void ShotAtEscort(Escort esc, Vector3 muzzle, float acc, float dmgPlayer, bool rifle)
        {
            var target = esc.chest;
            float dist = Vector3.Distance(muzzle, target);
            float chance = acc * 0.85f * (1 - MathX.Smoothstep(10, rifle ? 220 : 48, dist) * 0.7f);
            var dir = (target - muzzle).normalized;
            g.fx.Muzzle(muzzle, dir, rifle);
            if (rifle && dist > 60) Sound.I.Play("rifle_shot_far", 1, 1, muzzle, dist / 340, false, 900);
            else Sound.I.Play("schofield_shot2", 0.9f, 0.85f + Random.value * 0.1f, muzzle, 0, false, 600);
            if (Random.value < chance) { g.fx.Tracer(muzzle, target); g.fx.Blood(target, dir); esc.Damage(dmgPlayer * 1.4f * g.diff.dmgMult, muzzle); }
            else
            {
                var miss = target + new Vector3((Random.value - 0.5f) * 3, Random.value * 1.4f, (Random.value - 0.5f) * 3);
                g.fx.Tracer(muzzle, miss + (miss - muzzle).normalized * 40);
            }
        }

        /// an enemy fires at the player or the coach (now and then at an escort riding nearer to him)
        public void EnemyShot(Enemy enemy, Vector3 muzzle, float dist, float acc, float dmgPlayer, float dmgCoach, bool rifle = false)
        {
            var esc = g.escorts?.PickTarget(muzzle, rifle);
            if (esc != null) { ShotAtEscort(esc, muzzle, acc, dmgPlayer, rifle); return; }
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
