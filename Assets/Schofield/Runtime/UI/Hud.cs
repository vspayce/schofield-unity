// The in-ride HUD, built in code: crosshair and hitmarker, route bar and bounty, health / coach /
// Dead Eye meters, ammo (cylinder, shells or a count), kill feed, banners, edge chevrons for threats
// you can't see, brackets on riflemen who can see you, damage direction marks, the scope, Dead Eye
// marks, and the touch buttons. Ported from ui/hud.js.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Schofield
{
    public class Hud
    {
        readonly Canvas canvas; readonly RectTransform safe, layer;
        readonly GameObject root;
        Image[] cross = new Image[5]; Image ring; RectTransform crossRt;
        readonly Image[] hm = new Image[4]; float hmT, hmKill; bool hmHead;
        Text bounty, from, to, ammoName, ammoCount, banner, bannerSub, coachPct, reloadText;
        Image routeFill, routeCoach, hpFill, coachFill, deFill, hpIcon, reloadFill;
        RectTransform cylRt, shellsRt, feedRt, chevLayer, shooterLayer, dirLayer, markLayer, routeTrack;
        readonly List<Image> chambers = new List<Image>();
        readonly List<Image> shells = new List<Image>();
        readonly List<(Text t, float until)> feed = new List<(Text, float)>();
        readonly List<Image> chevs = new List<Image>(); readonly List<RectTransform> brackets = new List<RectTransform>();
        readonly List<(Image im, float until)> dirs = new List<(Image, float)>();
        readonly List<Text> marks = new List<Text>();
        Image scopeRing, scopeDark, stickBase, stickKnob;
        float bannerUntil, hpFlashUntil, coachFlashUntil; bool hpHeavy; int lastCoach = -1;
        WeaponDef weapon; string ammoMode;
        readonly Dictionary<string, Image> buttons = new Dictionary<string, Image>();
        Text dynCount, dynCountTouch, hint; bool hintPad = true;
        public const string PadHint = "Left stick aim · RT fire · LT aim/scope · X reload · Y swap · RB dynamite · Right stick up whip, down rein · Menu pause";
        const string KeysHint = "Mouse aim · Click fire · F or right click aim/scope · R reload · Q swap · G dynamite · Hold S to stop, W to whip · Esc pause · or use a controller";
        readonly GameInput input;

        public Hud(GameInput input)
        {
            this.input = input;
            canvas = UIKit.Canvas("HUD", 10);
            root = canvas.gameObject;
            Object.DontDestroyOnLoad(root);
            layer = UIKit.Fill("Layer", canvas.transform);
            safe = UIKit.Safe(canvas.transform);
            var C = new Vector2(0.5f, 0.5f);

            // scope (behind everything)
            scopeDark = UIKit.Img("ScopeDark", layer, C, Vector2.zero, new Vector2(4000, 4000), UIKit.Ring, new Color(0, 0, 0, 0)); scopeDark.gameObject.SetActive(false);
            scopeRing = UIKit.Img("Scope", layer, C, Vector2.zero, new Vector2(640, 640), UIKit.Ring, new Color(0.05f, 0.04f, 0.03f, 0.95f)); scopeRing.gameObject.SetActive(false);

            shooterLayer = UIKit.Fill("Shooters", layer);
            markLayer = UIKit.Fill("Marks", layer);
            dirLayer = UIKit.Rect("Dirs", layer, C, Vector2.zero, Vector2.zero);
            chevLayer = UIKit.Fill("Chevrons", layer);

            // crosshair: four ticks and a dot, and the coach gun's pellet ring
            crossRt = UIKit.Rect("Crosshair", layer, C, Vector2.zero, new Vector2(60, 60));
            var ticks = new (Vector2 p, Vector2 s)[] { (new Vector2(0, 14), new Vector2(3, 10)), (new Vector2(0, -14), new Vector2(3, 10)), (new Vector2(14, 0), new Vector2(10, 3)), (new Vector2(-14, 0), new Vector2(10, 3)), (Vector2.zero, new Vector2(4, 4)) };
            for (int i = 0; i < 5; i++) cross[i] = UIKit.Img("t", crossRt, C, ticks[i].p, ticks[i].s, null, UIKit.Paper);
            ring = UIKit.Img("Ring", crossRt, C, Vector2.zero, new Vector2(80, 80), UIKit.Ring, new Color(1, 0.95f, 0.85f, 0.5f));
            for (int i = 0; i < 4; i++)
            {
                hm[i] = UIKit.Img("hm", layer, C, new Vector2((i % 2 == 0 ? 1 : -1) * 12, (i < 2 ? 1 : -1) * 12), new Vector2(3, 14), null, Color.white);
                hm[i].rectTransform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0) == (i < 2) ? 45 : -45);
                hm[i].enabled = false;
            }
            reloadText = UIKit.Txt("Reload", layer, C, new Vector2(0, -52), new Vector2(200, 24), "RELOADING", 16, UIKit.Paper, TextAnchor.MiddleCenter, true);
            UIKit.Img("ReloadBg", reloadText.transform, C, new Vector2(0, -16), new Vector2(90, 4), null, UIKit.Dim);
            reloadFill = UIKit.Img("ReloadFill", reloadText.transform, new Vector2(0.5f, 0.5f), new Vector2(-45, -16), new Vector2(0, 4), null, UIKit.Gold);
            reloadFill.rectTransform.pivot = new Vector2(0, 0.5f);
            reloadText.gameObject.SetActive(false);

            // top: route bar and bounty
            var top = new Vector2(0.5f, 1);
            from = UIKit.Txt("From", safe, top, new Vector2(-300, -28), new Vector2(180, 30), "", 18, UIKit.Paper, TextAnchor.MiddleRight, true);
            to = UIKit.Txt("To", safe, top, new Vector2(300, -28), new Vector2(180, 30), "", 18, UIKit.Paper, TextAnchor.MiddleLeft, true);
            routeTrack = UIKit.Img("Track", safe, top, new Vector2(0, -28), new Vector2(400, 6), null, UIKit.Dim).rectTransform;
            routeFill = UIKit.Img("Fill", routeTrack, new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 6), null, UIKit.Gold);
            routeFill.rectTransform.pivot = new Vector2(0, 0.5f);
            routeCoach = UIKit.Img("Coach", routeTrack, new Vector2(0, 0.5f), new Vector2(0, 10), new Vector2(34, 22), Assets.Sprite("coach_icon"));
            UIKit.Txt("BountyLbl", safe, new Vector2(1, 1), new Vector2(-150, -24), new Vector2(120, 24), "BOUNTY", 14, UIKit.Gold, TextAnchor.MiddleRight, true);
            bounty = UIKit.Txt("Bounty", safe, new Vector2(1, 1), new Vector2(-30, -24), new Vector2(120, 34), "$0", 28, UIKit.Paper, TextAnchor.MiddleRight, true);
            bounty.rectTransform.pivot = new Vector2(1, 0.5f);

            // left: meters
            Image Meter(string name, string icon, float y, Color fill, out Image ic)
            {
                ic = UIKit.Img(name + "Icon", safe, new Vector2(0, 1), new Vector2(40, y), new Vector2(30, 30), Assets.Sprite(icon));
                var bg = UIKit.Img(name + "Bar", safe, new Vector2(0, 1), new Vector2(64, y), new Vector2(150, 10), null, UIKit.Dim);
                bg.rectTransform.pivot = new Vector2(0, 0.5f);
                var f = UIKit.Img("Fill", bg.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(150, 10), null, fill);
                f.rectTransform.pivot = new Vector2(0, 0.5f);
                return f;
            }
            float my = input.touch ? -120 : -40;      // on a phone the pause button sits top left, so the meters go under it
            hpFill = Meter("Health", "health_heart", my, MathX.Hex("#c8402c"), out hpIcon);
            coachFill = Meter("Coach", "coach_icon", my - 38, MathX.Hex("#c9a04a"), out _);
            coachPct = UIKit.Txt("Pct", safe, new Vector2(0, 1), new Vector2(250, my - 38), new Vector2(60, 24), "100%", 16, UIKit.Paper, TextAnchor.MiddleLeft, true);
            if (Player.DeadEyeEnabled) deFill = Meter("DeadEye", "deadeye_icon", my - 76, MathX.Hex("#e8c26a"), out _);

            // ammo, top right under the bounty (where the web game has it, clear of the touch buttons)
            var br = new Vector2(1, 1);
            ammoName = UIKit.Txt("AmmoName", safe, br, new Vector2(-90, -186), new Vector2(240, 24), "", 16, UIKit.Paper, TextAnchor.MiddleRight, true);
            ammoName.rectTransform.pivot = new Vector2(1, 0.5f); ammoName.rectTransform.anchoredPosition = new Vector2(-40, -186);
            cylRt = UIKit.Rect("Cylinder", safe, br, new Vector2(-90, -120), new Vector2(96, 96));
            UIKit.Img("CylArt", cylRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96), Assets.Sprite("revolver_cylinder"));
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2 + Mathf.PI / 2;
                chambers.Add(UIKit.Img("ch", cylRt, new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(a) * 28, Mathf.Sin(a) * 28), new Vector2(20, 20), UIKit.Disc, MathX.Hex("#d9b25a")));
            }
            shellsRt = UIKit.Rect("Shells", safe, br, new Vector2(-110, -120), new Vector2(200, 60));
            ammoCount = UIKit.Txt("Count", safe, br, new Vector2(-40, -120), new Vector2(200, 60), "", 42, UIKit.Paper, TextAnchor.MiddleRight, true);
            ammoCount.rectTransform.pivot = new Vector2(1, 0.5f);

            // kill feed just under the crosshair, centred (as in the web game)
            feedRt = UIKit.Rect("Feed", layer, new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(500, 200));
            banner = UIKit.Txt("Banner", layer, new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(1000, 60), "", 46, UIKit.Paper, TextAnchor.MiddleCenter, true);
            bannerSub = UIKit.Txt("Sub", banner.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(1000, 30), "", 20, UIKit.Gold, TextAnchor.MiddleCenter, true);

            if (!input.touch) dynCount = UIKit.Txt("Dynamite", safe, new Vector2(1, 1), new Vector2(-40, -214), new Vector2(240, 24), "", 15, UIKit.Paper, TextAnchor.MiddleRight, true);
            if (dynCount) dynCount.rectTransform.pivot = new Vector2(1, 0.5f);
            if (input.touch) TouchButtons();
            else
                hint = UIKit.Txt("Hint", safe, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(1240, 22), "", 13, new Color(1, 1, 1, 0.55f));
            Show(false);
        }

        void TouchButtons()
        {
            Image B(string role, string sprite, string label, Vector2 anchor, Vector2 pos, float size)
            {
                var im = UIKit.Img("btn " + role, safe, anchor, pos, new Vector2(size, size), sprite != null ? Assets.Sprite(sprite) : UIKit.Disc, sprite != null ? Color.white : new Color(0.16f, 0.1f, 0.06f, 0.8f));
                UIKit.Txt("l", im.transform, new Vector2(0.5f, 0), new Vector2(0, -10), new Vector2(size + 40, 20), label, 14, UIKit.Paper, TextAnchor.MiddleCenter, true);
                input.buttons[role] = im.rectTransform;
                buttons[role] = im;
                return im;
            }
            // the web game's touch layout (style.css), centres in canvas units (1 css px ≈ 1.85 on a phone):
            // shooting on the right thumb, Dead Eye / scope and the team on the left
            var R = new Vector2(1, 0); var L = new Vector2(0, 0);
            B("fire", "btn_fire", "FIRE", R, new Vector2(-152, 155), 170);
            B("reload", "btn_reload", "RELOAD", R, new Vector2(-322, 104), 100);
            B("swap", "btn_swap", "SWAP", R, new Vector2(-137, 344), 96);
            if (Player.DeadEyeEnabled) B("deadeye", "btn_deadeye", "DEAD EYE", L, new Vector2(120, 287), 130);
            // scope: a proper visible button where Dead Eye was — dark disc, brass ring, sight glyph
            var sc = B("scope", null, "SCOPE", L, new Vector2(120, 287), 120);
            var rim = UIKit.Img("rim", sc.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120), UIKit.Ring, UIKit.Gold); rim.raycastTarget = false;
            var glyph = UIKit.Img("ring", sc.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54), UIKit.Ring, UIKit.Paper); glyph.raycastTarget = false;
            UIKit.Img("h", sc.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66, 3), null, UIKit.Paper).raycastTarget = false;
            UIKit.Img("v", sc.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 66), null, UIKit.Paper).raycastTarget = false;
            // the floating aim stick (left half of the screen): base + knob, shown while a thumb is on it
            stickBase = UIKit.Img("StickBase", layer, new Vector2(0, 0), Vector2.zero, new Vector2(220, 220), UIKit.Ring, new Color(1, 1, 1, 0.35f));
            stickKnob = UIKit.Img("StickKnob", layer, new Vector2(0, 0), Vector2.zero, new Vector2(90, 90), UIKit.Disc, new Color(1, 1, 1, 0.45f));
            stickBase.raycastTarget = stickKnob.raycastTarget = false;
            stickBase.gameObject.SetActive(false); stickKnob.gameObject.SetActive(false);
            var dyn = B("dynamite", null, "DYNAMITE", R, new Vector2(-302, 272), 104);
            dynCountTouch = UIKit.Txt("n", dyn.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 104), "3", 34, UIKit.Paper, TextAnchor.MiddleCenter, true);
            B("whip", "btn_whip", "WHIP", L, new Vector2(266, 111), 96);
            B("brake", "btn_brake", "REIN", L, new Vector2(118, 104), 96);
            var pause = UIKit.Img("btn pause", safe, new Vector2(0, 1), new Vector2(60, -52), new Vector2(76, 76), UIKit.Disc, new Color(0, 0, 0, 0.45f));
            UIKit.Txt("l", pause.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76, 76), "II", 26, UIKit.Paper, TextAnchor.MiddleCenter, true);
            input.buttons["pause"] = pause.rectTransform;
        }

        public void Show(bool v) => root.SetActive(v);

        public void SetRoute(RouteDef def) { from.text = def.from; to.text = def.to; }

        public void SetWeapon(WeaponDef W, int n)
        {
            weapon = W;
            ammoName.text = W.name;
            ammoMode = W.kind == "revolver" && W.mag == 6 ? "cyl" : W.mag <= 2 ? "shells" : "count";
            cylRt.gameObject.SetActive(ammoMode == "cyl");
            shellsRt.gameObject.SetActive(ammoMode == "shells");
            ammoCount.gameObject.SetActive(ammoMode == "count");
            ring.enabled = W.kind == "shotgun";
            if (ammoMode == "shells")
            {
                foreach (var s in shells) Object.Destroy(s.gameObject);
                shells.Clear();
                for (int i = 0; i < W.mag; i++)
                    shells.Add(UIKit.Img("sh", shellsRt, new Vector2(0.5f, 0.5f), new Vector2((i - (W.mag - 1) / 2f) * 34, 0), new Vector2(28, 56), Assets.Sprite("shell_full")));
            }
            SetAmmo(W, n);
        }

        public void SetAmmo(WeaponDef W, int n)
        {
            if (ammoMode == "cyl")
            {
                for (int i = 0; i < chambers.Count; i++) chambers[i].color = i < n ? MathX.Hex("#d9b25a") : new Color(0.08f, 0.06f, 0.05f, 0.9f);
                cylRt.localRotation = Quaternion.Euler(0, 0, -(6 - n) * 60);
            }
            else if (ammoMode == "shells")
            {
                for (int i = 0; i < shells.Count; i++) shells[i].sprite = Assets.Sprite(i < n ? "shell_full" : "shell_empty") ?? shells[i].sprite;
            }
            else
            {
                ammoCount.text = n + "<size=20>/" + W.mag + "</size>";
                ammoCount.color = n <= Mathf.CeilToInt(W.mag / 5f) ? UIKit.Blood : UIKit.Paper;
                ammoCount.supportRichText = true;
            }
        }

        public void SetBounty(int v) => bounty.text = "$" + v;

        public void SetDynamite(int n)
        {
            if (dynCount) dynCount.text = n > 0 ? $"DYNAMITE  {n}   ({(input.pad ? "RB" : "G")})" : "";
            if (dynCountTouch) dynCountTouch.text = n.ToString();
            if (buttons.TryGetValue("dynamite", out var b)) b.color = n > 0 ? new Color(0.42f, 0.16f, 0.1f, 0.85f) : new Color(0.16f, 0.1f, 0.06f, 0.3f);
        }

        public void Feed(string text, bool hs)
        {
            foreach (var f in feed) f.t.rectTransform.anchoredPosition += new Vector2(0, 30);
            var t = UIKit.Txt("feed", feedRt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 28), text, hs ? 24 : 20, hs ? MathX.Hex("#ffd36a") : UIKit.Paper, TextAnchor.MiddleCenter, true);
            feed.Add((t, Time.unscaledTime + 1.4f));
            while (feed.Count > 4) { Object.Destroy(feed[0].t.gameObject); feed.RemoveAt(0); }
        }

        public void Banner(string title, string sub = "", float secs = 1.4f)
        {
            banner.text = title; bannerSub.text = sub;
            banner.gameObject.SetActive(true);
            bannerUntil = Time.unscaledTime + secs;
        }

        public void SetScope(bool on, bool glass = true)
        {
            scopeRing.gameObject.SetActive(on && glass);
            if (buttons.TryGetValue("scope", out var b)) b.color = on ? UIKit.Gold : new Color(0.16f, 0.1f, 0.06f, 0.8f);
        }

        public void CrosshairEnemy(bool on) { foreach (var c in cross) c.color = on ? UIKit.Blood : UIKit.Paper; }

        public void Hitmarker(bool hit, bool kill, bool head)
        {
            if (!hit) return;
            hmT = 0.18f; hmKill = kill ? 1 : 0; hmHead = head;
        }

        public void NudgeReload() { reloadText.transform.localScale = Vector3.one * 1.3f; }

        public void PulseHealth(bool heavy) { hpFlashUntil = Time.unscaledTime + (heavy ? 0.62f : 0.38f); hpHeavy = heavy; }

        public void DamageFrom(Vector3 from, Vector3 camPos, Vector3 aimDir)
        {
            var dv = from - camPos; dv.y = 0; dv.Normalize();
            var f = aimDir; f.y = 0; f.Normalize();
            // + = to the right
            float ang = Mathf.Atan2(f.z * dv.x - f.x * dv.z, f.x * dv.x + f.z * dv.z);
            var im = UIKit.Img("dir", dirLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 14), null, new Color(0.9f, 0.15f, 0.1f, 0.85f));
            float r = 200;
            im.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * r;
            im.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang * Mathf.Rad2Deg);
            dirs.Add((im, Time.unscaledTime + 1.1f));
        }

        Vector2 ToCanvas(Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out var lp);
            return lp;
        }

        public void Update(Game game)
        {
            if (hint && (hint.text.Length == 0 || hintPad != input.pad)) { hintPad = input.pad; hint.text = input.pad ? PadHint : KeysHint; game.dynamite?.Refresh(); }
            float now = Time.unscaledTime;
            var p = game.player; var c = game.coach; var cam = game.cam;
            void Bar(Image f, float v) { f.rectTransform.sizeDelta = new Vector2(150 * Mathf.Clamp01(v), 10); }
            Bar(hpFill, p.hp / 100); Bar(coachFill, c.hp / 100); if (deFill) Bar(deFill, p.deadeye);
            hpFill.color = p.hp < 30 ? UIKit.Blood : MathX.Hex("#c8402c");
            float pulse = now < hpFlashUntil ? (hpHeavy ? 1.5f : 1.25f) : 1;
            hpIcon.rectTransform.localScale = Vector3.one * pulse;
            if (deFill) deFill.color = p.deadeyeOn ? Color.white : p.deadeye >= 0.2f ? MathX.Hex("#e8c26a") : new Color(0.6f, 0.5f, 0.3f);
            int cp = Mathf.Max(0, Mathf.RoundToInt(c.hp));
            if (cp != lastCoach) { if (lastCoach >= 0 && cp < lastCoach) coachFlashUntil = now + 0.4f; lastCoach = cp; coachPct.text = cp + "%"; }
            coachPct.color = now < coachFlashUntil ? UIKit.Blood : UIKit.Paper;
            if (buttons.TryGetValue("deadeye", out var de)) de.color = p.deadeye >= 0.2f || p.deadeyeOn ? Color.white : new Color(1, 1, 1, 0.4f);
            if (buttons.TryGetValue("scope", out var sb) && !p.scoped) sb.color = new Color(0.16f, 0.1f, 0.06f, 0.8f);
            if (stickBase)
            {
                bool on = input.stickOrigin.HasValue;
                stickBase.gameObject.SetActive(on); stickKnob.gameObject.SetActive(on);
                if (on)
                {
                    float s = ((RectTransform)canvas.transform).rect.height / Screen.height;   // screen px -> canvas units
                    stickBase.rectTransform.anchoredPosition = input.stickOrigin.Value * s;
                    stickKnob.rectTransform.anchoredPosition = (input.stickOrigin.Value + input.stickOffset) * s;
                }
            }
            if (buttons.TryGetValue("whip", out var wb)) wb.color = new Color(1, 1, 1, 0.4f + c.stamina * 0.5f);
            foreach (var kv in buttons) kv.Value.rectTransform.localScale = Vector3.one * (input.held.Contains(kv.Key) ? 0.92f : 1);

            // the coach gun's pattern ring sized to the real pellet cone
            if (p.weapon.kind == "shotgun")
            {
                float H = ((RectTransform)canvas.transform).rect.height;
                float r = p.weapon.spread * (H / 2) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                ring.rectTransform.sizeDelta = Vector2.one * r * 2;
            }
            float prog = Mathf.Clamp01(c.s / game.route.len);
            routeFill.rectTransform.sizeDelta = new Vector2(400 * prog, 6);
            routeCoach.rectTransform.anchoredPosition = new Vector2(400 * prog, 10);

            // hitmarker
            if (hmT > 0)
            {
                hmT -= Time.unscaledDeltaTime;
                var col = hmHead ? MathX.Hex("#ffd36a") : hmKill > 0 ? UIKit.Blood : Color.white;
                foreach (var h in hm) { h.enabled = hmT > 0; h.color = col; }
            }
            // reload ring
            bool rl = p.reloading > 0;
            reloadText.gameObject.SetActive(rl);
            if (rl) reloadFill.rectTransform.sizeDelta = new Vector2(90 * (1 - p.reloading / Mathf.Max(0.01f, p.weapon.reload)), 4);
            reloadText.transform.localScale = Vector3.Lerp(reloadText.transform.localScale, Vector3.one, Time.unscaledDeltaTime * 8);

            // feed, banner, damage dirs
            for (int i = feed.Count - 1; i >= 0; i--) if (now > feed[i].until) { Object.Destroy(feed[i].t.gameObject); feed.RemoveAt(i); }
            if (banner.gameObject.activeSelf && now > bannerUntil) banner.gameObject.SetActive(false);
            for (int i = dirs.Count - 1; i >= 0; i--)
            {
                var (im, until) = dirs[i];
                if (now > until) { Object.Destroy(im.gameObject); dirs.RemoveAt(i); continue; }
                var col = im.color; col.a = Mathf.Clamp01((until - now) / 0.5f) * 0.85f; im.color = col;
            }

            // hit flash + low health through the post volume
            float since = game.time - p.lastHit;
            game.post.hitWeight = Mathf.Max(0, 1 - p.hp / 100) * 0.7f + Mathf.Max(0, 1 - since / 0.5f) * 0.8f;

            Brackets(game); Chevrons(game); Marks(game);
        }

        // a small down-pointing triangle over every rifleman who can see you, sized to his distance;
        // red before he fires
        void Brackets(Game game)
        {
            var cam = game.cam;
            int n = 0;
            foreach (var e in game.enemies.list)
            {
                if (!e.alive || e.type == "rider" || !e.los) continue;
                float dist = Vector3.Distance(e.spheres[1].c, game.player.camPos);
                if (dist > (e.Rifle ? 320 : 80)) continue;
                var head = e.spheres[0].c + Vector3.up * (0.55f + dist * 0.004f);
                var sp = cam.WorldToScreenPoint(head);
                if (sp.z < 0 || sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;
                float px = Mathf.Lerp(30, 16, Mathf.InverseLerp(20, 260, dist));
                while (brackets.Count <= n)
                {
                    var b = UIKit.Rect("marker", shooterLayer, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 24, new Vector2(0.5f, 0));
                    var tri = UIKit.Img("tri", b, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 24, UIKit.TriDown, Color.white);
                    tri.rectTransform.anchorMin = Vector2.zero; tri.rectTransform.anchorMax = Vector2.one; tri.rectTransform.sizeDelta = Vector2.zero;
                    var sh = tri.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.55f); sh.effectDistance = new Vector2(1.5f, -1.5f);
                    UIKit.Txt("d", b, new Vector2(0.5f, 1), new Vector2(0, 10), new Vector2(120, 18), "", 13, UIKit.Paper, TextAnchor.MiddleCenter, true);
                    brackets.Add(b);
                }
                var br = brackets[n++];
                br.gameObject.SetActive(true);
                br.anchoredPosition = ToCanvas(sp);
                br.sizeDelta = new Vector2(px, px * 0.85f);
                var ec = e.glintT > 0 ? UIKit.Blood : new Color(1, 0.9f, 0.7f, 0.9f);
                br.GetComponentInChildren<Image>().color = ec;
                var label = br.GetComponentInChildren<Text>();
                label.text = dist > 40 ? Mathf.RoundToInt(dist) + "m" : "";
            }
            for (int i = n; i < brackets.Count; i++) brackets[i].gameObject.SetActive(false);
        }

        // edge arrows for threats you can't see (behind you, off screen)
        void Chevrons(Game game)
        {
            var cam = game.cam;
            var size = layer.rect.size;
            var list = new List<(float a, int cls)>();
            foreach (var e in game.enemies.list)
            {
                if (!e.alive) continue;
                float dist = Vector3.Distance(e.spheres[1].c, game.player.camPos);
                bool threat = e.type == "rider" ? e.IsRidingRider && dist < 70 : e.los && dist < (e.Rifle ? 200 : 70);
                if (!threat) continue;
                var vp = cam.WorldToViewportPoint(e.spheres[1].c);
                bool behind = vp.z < 0;
                float x = (vp.x - 0.5f) * 2, y = (vp.y - 0.5f) * 2;
                bool on = !behind && Mathf.Abs(x) < 0.92f && Mathf.Abs(y) < 0.88f;
                if (on) continue;
                if (behind) { x = -x; y = -y; if (Mathf.Abs(y) < 0.3f) y = -0.9f; }
                float a = Mathf.Atan2(y * size.y, x * size.x);
                int cls = e.glintT > 0 ? 2 : e.type == "rider" ? 0 : 1;
                int near = list.FindIndex(q => Mathf.Abs(MathX.WrapAngle(q.a - a)) < 0.25f);
                if (near >= 0) { if (cls > list[near].cls) list[near] = (list[near].a, cls); continue; }
                list.Add((a, cls));
            }
            while (chevs.Count < list.Count)
            {
                var im = UIKit.Img("chev", chevLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26), null, Color.white);
                im.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                chevs.Add(im);
            }
            float rx = size.x / 2 - 46, ry = size.y / 2 - 40;
            for (int i = 0; i < chevs.Count; i++)
            {
                bool act = i < list.Count; chevs[i].gameObject.SetActive(act);
                if (!act) continue;
                var (a, cls) = list[i];
                chevs[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                chevs[i].rectTransform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 45);
                chevs[i].color = cls == 2 ? UIKit.Blood : cls == 1 ? MathX.Hex("#ffb347") : new Color(1, 0.92f, 0.75f, 0.9f);
            }
        }

        void Marks(Game game)
        {
            var p = game.player;
            int want = p.deadeyeOn ? p.marks.Count : 0;
            while (marks.Count < want) marks.Add(UIKit.Txt("mark", markLayer, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30), "✕", 28, MathX.Hex("#ff3b2f")));
            for (int i = 0; i < marks.Count; i++)
            {
                bool act = i < want;
                if (act)
                {
                    var sp = game.cam.WorldToScreenPoint(p.marks[i].enemy.HitPoint(p.marks[i].part));
                    act = sp.z > 0;
                    if (act) marks[i].rectTransform.anchoredPosition = ToCanvas(sp);
                }
                marks[i].gameObject.SetActive(act);
            }
        }
    }
}
