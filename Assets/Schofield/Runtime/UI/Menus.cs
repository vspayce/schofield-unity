// Title, route select, job, loadout, gunsmith, guns for hire, pause, settings, how-to, results and
// failure screens. Ported from ui/menus.js. Mouse, touch and controller all work: with a controller
// the first button is selected so a pad can navigate.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Schofield
{
    public struct Results { public int kills, headshots, accuracy, coach, bounty, reward, total, stars, hireFee, escortKills, escortBounty; public string mission; }

    public class Menus
    {
        readonly Game g;
        readonly Canvas canvas;
        RectTransform panel;
        int routeIndex;
        readonly List<string> hireSel = new List<string>();
        Results lastResults;
        static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        static readonly Color CardCol = new Color(0.12f, 0.08f, 0.05f, 0.9f), CardSel = new Color(0.3f, 0.21f, 0.1f, 0.95f);

        public Menus(Game game)
        {
            g = game;
            canvas = UIKit.Canvas("Menus", 20);
            Object.DontDestroyOnLoad(canvas.gameObject);
            UIKit.EnsureEventSystem();
        }

        public bool Open => panel != null;

        public void Close()
        {
            if (panel) Object.Destroy(panel.gameObject);
            panel = null;
        }

        RectTransform NewPanel(bool dim, bool left = false)
        {
            Close();
            panel = UIKit.Fill("Panel", canvas.transform);
            if (dim) UIKit.Img("Dim", panel, C, Vector2.zero, new Vector2(4000, 4000), null, new Color(0.05f, 0.03f, 0.02f, 0.72f));
            else if (left)
            {
                var shade = UIKit.Img("Shade", panel, new Vector2(0, 0.5f), Vector2.zero, new Vector2(820, 4000), UIKit.FadeRight(), new Color(0.05f, 0.03f, 0.02f, 0.75f));
                shade.rectTransform.pivot = new Vector2(0, 0.5f); shade.preserveAspect = false;
            }
            return UIKit.Safe(panel);
        }

        // a touch-only phone has nothing to navigate with, so no button is left lit up
        void Select(Button b)
        {
            bool touchOnly = Application.isMobilePlatform && Application.platform != RuntimePlatform.tvOS && UnityEngine.InputSystem.Gamepad.current == null;
            if (b && !touchOnly) EventSystem.current?.SetSelectedGameObject(b.gameObject);
        }

        /// Highlight the panel's first button (a controller needs something selected to move from).
        public void SelectFirst() { if (panel) Select(panel.GetComponentInChildren<Button>()); }

        static string StarStr(int n) => new string('★', n) + new string('☆', 3 - n);

        // ------------------------------------------------------------- helpers
        static Image Card(Transform p, Vector2 pos, Vector2 size, bool sel = false)
        {
            var im = UIKit.Img("Card", p, C, pos, size, null, sel ? CardSel : CardCol);
            var ol = im.gameObject.AddComponent<Outline>(); ol.effectColor = sel ? UIKit.Gold : new Color(0.79f, 0.63f, 0.29f, 0.45f); ol.effectDistance = new Vector2(2, -2);
            return im;
        }

        static Text T(Transform p, Vector2 pos, Vector2 size, string s, int fs, Color col, TextAnchor a = TextAnchor.MiddleCenter, bool title = true)
            => UIKit.Txt("T", p, C, pos, size, s, fs, col, a, title);

        /// labelled 0..1 bars
        static void Bars(Transform p, Vector2 pos, float width, string[] names, float[] vals, float rowH = 18)
        {
            for (int i = 0; i < names.Length; i++)
            {
                float y = pos.y - i * rowH;
                T(p, new Vector2(pos.x - width / 2 + 45, y), new Vector2(90, rowH), names[i], 13, UIKit.Paper, TextAnchor.MiddleLeft);
                float bw = width - 100;
                var bg = UIKit.Img("bg", p, C, new Vector2(pos.x + 50 - bw / 2 + bw / 2, y), new Vector2(bw, 7), null, UIKit.Dim);
                var f = UIKit.Img("f", bg.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(bw * Mathf.Clamp01(vals[i]), 7), null, UIKit.Gold);
                f.rectTransform.pivot = new Vector2(0, 0.5f);
            }
        }

        static void GunPic(Transform p, Vector2 pos, Vector2 size, WeaponDef W)
        {
            var sp = Assets.Sprite("weapon_" + W.id);
            if (sp) UIKit.Img("Pic", p, C, pos, size, sp);
            else T(p, pos, size, W.year.ToString(), 40, UIKit.Gold);
        }

        // --------------------------------------------------------------- title
        public void Title()
        {
            var p = NewPanel(false, true);
            var L = new Vector2(0, 0.5f);
            UIKit.Img("Logo", p, new Vector2(0, 1), new Vector2(260, -150), new Vector2(420, 210), Assets.Sprite("logo"));
            if (!Assets.Sprite("logo")) UIKit.Txt("Logo", p, new Vector2(0, 1), new Vector2(260, -150), new Vector2(460, 120), "SCHOFIELD", 72, UIKit.Paper, TextAnchor.MiddleCenter, true);
            int total = 0; foreach (var r in Routes.All) total += Save.Stars(r.id);
            UIKit.Txt("Tag", p, L, new Vector2(260, 30), new Vector2(460, 50), "Ride shotgun on the Overland Mail. Keep the strongbox. Keep your scalp.", 18, UIKit.Gold, TextAnchor.MiddleCenter, true);
            UIKit.Txt("Stars", p, L, new Vector2(260, -14), new Vector2(400, 30), $"★ {total} of {Routes.All.Length * 3}     Purse ${Save.Purse}", 20, UIKit.Paper, TextAnchor.MiddleCenter, true);
            var ride = UIKit.Btn("Ride", p, L, new Vector2(260, -80), new Vector2(300, 64), "RIDE OUT", () => RouteSelect(), 34);
            UIKit.Btn("How", p, L, new Vector2(185, -158), new Vector2(145, 48), "HOW TO", () => How(), 20);
            UIKit.Btn("Settings", p, L, new Vector2(335, -158), new Vector2(145, 48), "SETTINGS", () => Settings(false), 20);
            Select(ride);
        }

        /// every route is open from the start (as in the web game)
        public void RouteSelect()
        {
            var p = NewPanel(true);
            T(p, new Vector2(0, 262), new Vector2(800, 60), "Choose your route", 44, UIKit.Paper);
            Button first = null;
            for (int i = 0; i < Routes.All.Length; i++)
            {
                var def = Routes.All[i]; int idx = i;
                float y = 180 - i * 86;
                var b = UIKit.Btn("Route " + def.id, p, C, new Vector2(0, y), new Vector2(780, 76), "", () => Jobs(idx));
                var bt = b.transform;
                T(bt, new Vector2(-130, 13), new Vector2(480, 34), def.name, 28, UIKit.Paper, TextAnchor.MiddleLeft);
                T(bt, new Vector2(-130, -18), new Vector2(480, 24), $"{def.from} to {def.to}", 16, UIKit.Gold, TextAnchor.MiddleLeft);
                T(bt, new Vector2(270, 13), new Vector2(200, 32), StarStr(Save.Stars(def.id)), 26, UIKit.Gold, TextAnchor.MiddleCenter, false);
                T(bt, new Vector2(270, -18), new Vector2(200, 24), "REWARD $" + def.reward, 15, UIKit.Paper);
                if (first == null) first = b;
            }
            UIKit.Btn("Back", p, C, new Vector2(0, -262), new Vector2(240, 54), "BACK", () => Title());
            Select(first);
        }

        // ---------------------------------------------------------------- jobs
        /// pick what you're carrying: it sets the pay and the opposition
        public void Jobs(int i)
        {
            routeIndex = i;
            var R = Routes.All[i];
            var p = NewPanel(true);
            T(p, new Vector2(0, 268), new Vector2(800, 56), R.name, 44, UIKit.Paper);
            T(p, new Vector2(0, 226), new Vector2(800, 28), "What are you hauling? The road pays for the risk.", 18, UIKit.Gold, TextAnchor.MiddleCenter, false);
            Button first = null;
            for (int k = 0; k < Missions.Order.Length; k++)
            {
                var M = Missions.All[Missions.Order[k]]; string id = M.id;
                bool sel = Save.Mission == id;
                var b = UIKit.Btn("Job " + id, p, C, new Vector2((k - 1) * 390, 10), new Vector2(370, 340), "", () => { Save.Mission = id; Loadout(routeIndex); });
                if (sel) b.GetComponent<Image>().color = CardSel;
                var bt = b.transform;
                T(bt, new Vector2(0, 135), new Vector2(340, 36), M.name, 28, UIKit.Paper);
                T(bt, new Vector2(0, 100), new Vector2(340, 30), "$" + Mathf.RoundToInt(R.reward * M.pay), 26, UIKit.Gold);
                T(bt, new Vector2(0, 62), new Vector2(330, 40), M.cargo, 15, UIKit.Gold, TextAnchor.MiddleCenter, false);
                T(bt, new Vector2(0, -20), new Vector2(330, 110), M.blurb, 16, UIKit.Paper, TextAnchor.UpperCenter, false);
                int pips = Mathf.RoundToInt(M.risk * 4);
                T(bt, new Vector2(0, -135), new Vector2(330, 30), "risk  <color=#e0492f>" + new string('◆', pips) + "</color><color=#5a4a3a>" + new string('◆', 4 - pips) + "</color>", 20, UIKit.Paper, TextAnchor.MiddleCenter, false).supportRichText = true;
                if (first == null || sel) first = b;
            }
            UIKit.Btn("Back", p, C, new Vector2(0, -250), new Vector2(240, 54), "BACK", () => RouteSelect());
            Select(first);
        }

        // ------------------------------------------------------------- loadout
        List<string> Hired()
        {
            var ids = new List<string>();
            foreach (var id in Hires.Order) if (hireSel.Contains(id)) ids.Add(id);
            while (ids.Count > 0 && Hires.Fee(ids) > Save.Purse) { hireSel.Remove(ids[ids.Count - 1]); ids.RemoveAt(ids.Count - 1); }
            return ids;
        }

        public void Loadout(int i)
        {
            routeIndex = i;
            var p = NewPanel(true);
            T(p, new Vector2(0, 272), new Vector2(800, 52), Routes.All[i].name, 40, UIKit.Paper);
            T(p, new Vector2(0, 234), new Vector2(900, 26), "Tap the gun to start with in hand. Swap any time on the coach.", 17, UIKit.Gold, TextAnchor.MiddleCenter, false);
            Button first = null;
            foreach (var (slot, label, all, x) in new[] { ("side", "Sidearm", Weapons.Sidearms, -215f), ("long", "Long gun", Weapons.LongGuns, 215f) })
            {
                var W = Weapons.All[Save.Equipped(slot)];
                bool inHand = Save.Start == slot;
                string sl = slot;
                var b = UIKit.Btn("Gun " + slot, p, C, new Vector2(x, 40), new Vector2(400, 330), "", () => { Save.Start = sl; Loadout(routeIndex); });
                if (inHand) b.GetComponent<Image>().color = CardSel;
                var bt = b.transform;
                T(bt, new Vector2(0, 145), new Vector2(380, 26), label.ToUpper() + (inHand ? "  ·  IN HAND" : ""), 16, inHand ? UIKit.Gold : UIKit.Paper);
                GunPic(bt, new Vector2(0, 70), new Vector2(330, 110), W);
                T(bt, new Vector2(0, -2), new Vector2(380, 32), W.name, 25, UIKit.Paper);
                Bars(bt, new Vector2(0, -40), 320, Weapons.StatNames, W.stats);
                var owned = new List<string>(); foreach (var id in all) if (Save.Owns(id)) owned.Add(id);
                if (owned.Count > 1)
                {
                    int k = owned.IndexOf(W.id);
                    UIKit.Btn("Prev", bt, C, new Vector2(-150, -135), new Vector2(60, 40), "◀", () => { Save.Equip(owned[(k - 1 + owned.Count) % owned.Count]); Loadout(routeIndex); }, 22);
                    T(bt, new Vector2(0, -135), new Vector2(160, 30), $"{k + 1} of {owned.Count}", 16, UIKit.Paper);
                    UIKit.Btn("Next", bt, C, new Vector2(150, -135), new Vector2(60, 40), "▶", () => { Save.Equip(owned[(k + 1) % owned.Count]); Loadout(routeIndex); }, 22);
                }
                if (first == null) first = b;
            }
            int hp = Save.CoachHp;
            T(p, new Vector2(hp < 100 ? -110 : 0, -152), new Vector2(420, 30), $"Coach condition  {hp}%" + (hp < 100 ? "" : "  ·  sound"), 19, hp < 55 ? UIKit.Blood : UIKit.Paper);
            if (hp < 100)
                UIKit.Btn("Repair", p, C, new Vector2(170, -152), new Vector2(200, 40), $"REPAIR ${Save.RepairCost()}", () =>
                {
                    if (Save.Repair() == "poor") g.hud.Feed("Not enough for the livery", false); else Sound.I.Play("ui_click", 0.7f, 0.8f);
                    Loadout(routeIndex);
                }, 18);
            var hired = Hired(); int fee = Hires.Fee(hired);
            float by = -232;
            UIKit.Btn("Job", p, C, new Vector2(-450, by), new Vector2(250, 58), Missions.All[Save.Mission].name.ToUpper() + " ▸", () => Jobs(routeIndex), 19);
            UIKit.Btn("Shop", p, C, new Vector2(-180, by), new Vector2(260, 58), $"GUNSMITH · ${Save.Purse}", () => Gunsmith(false, "side"), 19);
            UIKit.Btn("Hire", p, C, new Vector2(90, by), new Vector2(250, 58), hired.Count > 0 ? $"HIRED · ${fee}" : "HIRE GUNS", () => HireGuns(), 19);
            var go = UIKit.Btn("Go", p, C, new Vector2(390, by), new Vector2(320, 64), fee > 0 ? $"ALL ABOARD · −${fee}" : "ALL ABOARD", () =>
            {
                var ids = Hired(); int f = Hires.Fee(ids);
                Save.Spend(f);
                hireSel.Clear();                         // one ride only
                Close(); g.StartRide(routeIndex, ids.ToArray(), f);
            }, 26);
            Select(go);
        }

        // ------------------------------------------------------------ gunsmith
        bool shopFromResults;
        public void Gunsmith(bool fromResults, string tab)
        {
            shopFromResults = fromResults;
            var p = NewPanel(true);
            T(p, new Vector2(0, 280), new Vector2(800, 50), "Gunsmith", 40, UIKit.Paper);
            T(p, new Vector2(0, 244), new Vector2(600, 26), $"Your purse:  ${Save.Purse}", 19, UIKit.Gold);
            UIKit.Btn("TabSide", p, C, new Vector2(-110, 204), new Vector2(200, 42), tab == "side" ? "▸ SIDEARMS" : "SIDEARMS", () => Gunsmith(shopFromResults, "side"), 18);
            UIKit.Btn("TabLong", p, C, new Vector2(110, 204), new Vector2(200, 42), tab == "long" ? "▸ LONG GUNS" : "LONG GUNS", () => Gunsmith(shopFromResults, "long"), 18);
            var list = tab == "side" ? Weapons.Sidearms : Weapons.LongGuns;
            Button first = null;
            for (int k = 0; k < list.Length; k++)
            {
                var W = Weapons.All[list[k]]; string id = W.id;
                int col = k % 3, row = k / 3;
                var pos = new Vector2((col - 1) * 395, 60 - row * 232);
                bool own = Save.Owns(id), carried = Save.Equipped(W.slot) == id, afford = Save.Purse >= W.price;
                var card = Card(p, pos, new Vector2(380, 222), carried).transform;
                T(card, new Vector2(-30, 92), new Vector2(300, 28), W.name, 20, UIKit.Paper, TextAnchor.MiddleLeft);
                T(card, new Vector2(150, 92), new Vector2(70, 28), W.year.ToString(), 16, UIKit.Gold);
                GunPic(card, new Vector2(-95, 32), new Vector2(170, 70), W);
                Bars(card, new Vector2(90, 58), 180, Weapons.StatNames, W.stats, 15);
                T(card, new Vector2(0, -35), new Vector2(355, 62), W.blurb, 12, UIKit.Paper, TextAnchor.UpperLeft, false);
                Button b;
                if (carried) { b = UIKit.Btn("Carrying", card, C, new Vector2(0, -88), new Vector2(200, 36), "CARRYING", () => { }, 16); b.interactable = false; }
                else if (own) b = UIKit.Btn("Carry", card, C, new Vector2(0, -88), new Vector2(200, 36), "CARRY THIS", () => { Save.Equip(id); Gunsmith(shopFromResults, tab); }, 16);
                else
                {
                    b = UIKit.Btn("Buy", card, C, new Vector2(0, -88), new Vector2(200, 36), $"BUY ${W.price}", () =>
                    {
                        if (Save.Buy(id)) Sound.I.Play("schofield_cock", 0.8f);
                        Gunsmith(shopFromResults, tab);
                    }, 16);
                    if (!afford) { b.interactable = false; b.GetComponentInChildren<Text>().color = new Color(1, 1, 1, 0.35f); }
                }
                if (first == null && b.interactable) first = b;
            }
            var done = UIKit.Btn("Done", p, C, new Vector2(0, -272), new Vector2(240, 50), "DONE", () => { if (shopFromResults) ShowResults(lastResults); else Loadout(routeIndex); });
            Select(first ?? done);
        }

        // ------------------------------------------------------------- hire
        /// Guns for hire: toggles for the next ride only, paid when you climb aboard
        public void HireGuns()
        {
            var ids = Hired(); int fee = Hires.Fee(ids);
            var p = NewPanel(true);
            T(p, new Vector2(0, 280), new Vector2(800, 50), "Guns for Hire", 40, UIKit.Paper);
            T(p, new Vector2(0, 244), new Vector2(900, 26), $"Your purse: ${Save.Purse}" + (fee > 0 ? $"   ·   this ride: −${fee}   ·   left: ${Save.Purse - fee}" : ""), 18, UIKit.Gold);
            T(p, new Vector2(0, 208), new Vector2(1000, 40), "Paid when you climb aboard, and gone whatever happens. They ride one trip only. Outlaws they drop pay you half the bounty; they keep the rest.", 14, UIKit.Paper, TextAnchor.MiddleCenter, false);
            Button first = null;
            for (int k = 0; k < Hires.Order.Length; k++)
            {
                var H = Hires.All[Hires.Order[k]]; string id = H.id;
                bool on = ids.Contains(id), afford = on || Save.Purse >= fee + H.price;
                var card = Card(p, new Vector2((k - 1) * 395, -15), new Vector2(380, 370), on).transform;
                T(card, new Vector2(0, 160), new Vector2(360, 32), H.name, 24, UIKit.Paper);
                T(card, new Vector2(0, 128), new Vector2(360, 24), $"${H.price} a ride", 17, UIKit.Gold);
                var pic = Weapons.All.TryGetValue(H.pic, out var PW) ? PW : null;
                if (pic != null) GunPic(card, new Vector2(0, 75), new Vector2(240, 70), pic);
                T(card, new Vector2(0, -5), new Vector2(350, 100), H.blurb, 13, UIKit.Paper, TextAnchor.UpperCenter, false);
                Bars(card, new Vector2(0, -80), 300, HireDef.StatNames, H.stats);
                var b = UIKit.Btn("Hire", card, C, new Vector2(0, -150), new Vector2(220, 40), on ? "HIRED · LET GO" : $"HIRE ${H.price}", () =>
                {
                    if (hireSel.Contains(id)) hireSel.Remove(id); else { hireSel.Add(id); Sound.I.Play("schofield_cock", 0.7f); }
                    HireGuns();
                }, 16);
                if (!afford) { b.interactable = false; b.GetComponentInChildren<Text>().color = new Color(1, 1, 1, 0.35f); }
                if (first == null && b.interactable) first = b;
            }
            var done = UIKit.Btn("Done", p, C, new Vector2(0, -262), new Vector2(240, 50), "DONE", () => Loadout(routeIndex));
            Select(first ?? done);
        }

        // ------------------------------------------------------------- pause
        public void Pause()
        {
            var p = NewPanel(true);
            T(p, new Vector2(0, 140), new Vector2(600, 70), "Paused", 56, UIKit.Paper);
            var resume = UIKit.Btn("Resume", p, C, new Vector2(0, 40), new Vector2(320, 60), "RESUME", () => { Close(); g.Resume(); });
            UIKit.Btn("Settings", p, C, new Vector2(0, -32), new Vector2(320, 60), "SETTINGS", () => Settings(true));
            UIKit.Btn("Restart", p, C, new Vector2(0, -104), new Vector2(320, 60), "RESTART RUN", () => { Save.CoachHp = 100; Close(); g.StartRide(g.routeIndex); });
            UIKit.Btn("Quit", p, C, new Vector2(0, -176), new Vector2(320, 60), "QUIT TO TITLE", () => { Close(); g.ToAttract(); Title(); });
            Select(resume);
        }

        // ----------------------------------------------------------- settings
        public void Settings(bool fromPause)
        {
            var p = NewPanel(true);
            T(p, new Vector2(0, 262), new Vector2(600, 60), "Settings", 48, UIKit.Paper);
            var snd = Sound.I;
            Button first = null;
            void Row(int k, string label, System.Func<string> value, System.Action dec, System.Action inc)
            {
                float y = 185 - k * 62;
                T(p, new Vector2(-170, y), new Vector2(300, 40), label, 22, UIKit.Paper, TextAnchor.MiddleLeft);
                var v = T(p, new Vector2(140, y), new Vector2(160, 40), value(), 22, UIKit.Gold);
                var a = UIKit.Btn("Dec", p, C, new Vector2(30, y), new Vector2(56, 44), "−", () => { dec(); v.text = value(); }, 24);
                UIKit.Btn("Inc", p, C, new Vector2(250, y), new Vector2(56, 44), "+", () => { inc(); v.text = value(); }, 24);
                if (first == null) first = a;
            }
            Row(0, "Aim speed", () => g.input.sens.ToString("0.0"), () => { g.input.sens = Mathf.Max(0.3f, g.input.sens - 0.1f); g.input.Save(); }, () => { g.input.sens = Mathf.Min(3f, g.input.sens + 0.1f); g.input.Save(); });
            Row(1, "Invert look", () => g.input.invertY ? "ON" : "OFF", () => { g.input.invertY = !g.input.invertY; g.input.Save(); }, () => { g.input.invertY = !g.input.invertY; g.input.Save(); });
            Row(2, "Music", () => Mathf.RoundToInt(snd.musicVolume * 100) + "%", () => snd.SetVolumes(Mathf.Max(0, snd.musicVolume - 0.1f), snd.sfxVolume), () => snd.SetVolumes(Mathf.Min(1, snd.musicVolume + 0.1f), snd.sfxVolume));
            Row(3, "Effects", () => Mathf.RoundToInt(snd.sfxVolume * 100) + "%", () => snd.SetVolumes(snd.musicVolume, Mathf.Max(0, snd.sfxVolume - 0.1f)), () => snd.SetVolumes(snd.musicVolume, Mathf.Min(1, snd.sfxVolume + 0.1f)));
            Row(4, "Graphics", () => Post.Quality == 1 ? "HIGH" : "FAST", () => { Post.Quality = 1 - Post.Quality; g.post.ApplyQuality(); }, () => { Post.Quality = 1 - Post.Quality; g.post.ApplyQuality(); });
            Row(5, "God mode", () => g.godMode ? "ON" : "OFF", () => g.godMode = !g.godMode, () => g.godMode = !g.godMode);
            UIKit.Btn("Done", p, C, new Vector2(fromPause ? 0 : -140, -250), new Vector2(240, 54), "DONE", () => { if (fromPause) Pause(); else Title(); });
            if (!fromPause) UIKit.Btn("Wipe", p, C, new Vector2(140, -250), new Vector2(240, 54), "START OVER…", () => ConfirmWipe(), 20);
            Select(first);
        }

        // wiping the save can't be undone, so it asks first
        void ConfirmWipe()
        {
            var p = NewPanel(true);
            T(p, new Vector2(0, 120), new Vector2(700, 60), "Start over?", 46, UIKit.Paper);
            T(p, new Vector2(0, 30), new Vector2(760, 100), $"Your purse (${Save.Purse}), every gun you've bought, your stars and the coach's wear all go. Your settings stay. This can't be undone.", 19, UIKit.Paper, TextAnchor.MiddleCenter, false);
            var keep = UIKit.Btn("Keep", p, C, new Vector2(-170, -80), new Vector2(300, 58), "KEEP MY PROGRESS", () => Settings(false), 20);
            UIKit.Btn("Wipe", p, C, new Vector2(170, -80), new Vector2(300, 58), "START OVER", () => { Save.Wipe(); Title(); }, 20);
            Select(keep);
        }

        // ---------------------------------------------------------------- how
        public void How()
        {
            var p = NewPanel(true);
            bool touch = g.input.touch;
            T(p, new Vector2(0, 275), new Vector2(800, 50), "How to Ride Shotgun", 38, UIKit.Paper);
            bool pad = !touch && (g.input.pad || GameInput.TV);
            string s = (touch
                ? "<b>Aim:</b> drag anywhere on the right. Dragging from FIRE aims while you shoot.\n<b>Fire / Reload / Swap:</b> buttons at bottom right. Swap between your sidearm and long gun any time.\n<b>Whip / Rein:</b> buttons at left. Hold Rein to stop; whip to pull away. The team tires if you whip too long.\n"
                : pad
                ? "<b>Aim:</b> left stick.  <b>Fire:</b> right trigger (hold for the repeaters).  <b>Reload:</b> X.  <b>Swap guns:</b> Y.\n<b>Whip:</b> right stick up.  <b>Rein in:</b> right stick down; hold it to stop. The team tires if you whip too long.\n"
                : "<b>Aim:</b> mouse.  <b>Fire:</b> click.  <b>Reload:</b> R.  <b>Swap:</b> Q.  <b>Whip:</b> W / Shift.  Hold <b>Rein:</b> S to stop.\n")
                + $"<b>Scope:</b> {(touch ? "the scope button" : pad ? "left trigger" : "F")}, long guns only. Magnifies for the men on the ridges.\n"
                + $"<b>Dead Eye:</b> {(touch ? "the eye button" : pad ? "left shoulder (LB)" : "E or right click")}. Time slows. Sweep across outlaws to mark them, then fire to drop every one.\n"
                + $"<b>Dynamite:</b> {(touch ? "the dynamite button" : pad ? "right shoulder (RB)" : "G")}. Three sticks per run; throw into a group to blast riders from their horses.\n"
                + (pad ? "<b>Pause:</b> the Menu button.\n\n" : "\n")
                + "<b>Your health</b> heals itself after four seconds without being hit. Shoot a buffalo, bear or goat and the meat restores more.\n"
                + "<b>The coach</b> does not heal on its own; at zero the run is over. It's patched +10 each time you clear a wave. Riders who reach the team hurt it badly, and so does running into anything on the road.\n\n"
                + "<b>Riflemen</b> on the ridges flash a glint before they shoot and hit far harder than riders. Drop them first.\n"
                + "<b>Guns for hire</b> from the loadout ride one trip, paid up front; what they drop pays you half the bounty.";
            var t = T(p, new Vector2(0, 10), new Vector2(1080, 460), s, 16, UIKit.Paper, TextAnchor.UpperLeft, false);
            t.supportRichText = true;
            Select(UIKit.Btn("Ok", p, C, new Vector2(0, -268), new Vector2(240, 50), "GOT IT", () => Title()));
        }

        // ------------------------------------------------------------ results
        public void ShowResults(Results r)
        {
            lastResults = r;
            var p = NewPanel(true);
            var def = Routes.All[g.routeIndex];
            T(p, new Vector2(0, 262), new Vector2(800, 28), ((r.mission ?? "Mail Contract") + " — arrived at").ToUpper(), 16, UIKit.Gold);
            T(p, new Vector2(0, 222), new Vector2(800, 60), def.to, 50, UIKit.Paper);
            T(p, new Vector2(0, 170), new Vector2(300, 46), StarStr(r.stars), 40, UIKit.Gold, TextAnchor.MiddleCenter, false);
            var lines = new List<(string, string)>
            {
                ("Outlaws dropped", r.kills.ToString()), ("Headshots", r.headshots.ToString()), ("Accuracy", r.accuracy + "%"), ("Coach condition", r.coach + "%"),
                ("Bounties", "$" + r.bounty), ((r.mission ?? "Contract") + " payout", "$" + r.reward),
            };
            if (r.hireFee > 0) { lines.Add(($"of which hired guns' {r.escortKills} kills, at half", "$" + r.escortBounty)); lines.Add(("Guns for hire, paid at departure", "−$" + r.hireFee)); }
            for (int i = 0; i < lines.Count; i++)
            {
                float y = 120 - i * 28;
                T(p, new Vector2(-120, y), new Vector2(420, 28), lines[i].Item1, 19, UIKit.Paper, TextAnchor.MiddleLeft);
                T(p, new Vector2(170, y), new Vector2(160, 28), lines[i].Item2, 19, UIKit.Paper, TextAnchor.MiddleRight);
            }
            float ty = 120 - lines.Count * 28 - 14;
            T(p, new Vector2(0, ty), new Vector2(600, 36), $"Total ${r.total}" + (r.hireFee > 0 ? $"   ·   net of hire ${r.total - r.hireFee}" : ""), 28, UIKit.Gold);
            T(p, new Vector2(0, ty - 30), new Vector2(600, 26), $"Purse ${Save.Purse}", 18, UIKit.Paper);
            bool next = g.routeIndex + 1 < Routes.All.Length;
            float by = -250, w = 250;
            UIKit.Btn("Shop", p, C, new Vector2(next ? -405 : -270, by), new Vector2(w, 56), "GUNSMITH", () => Gunsmith(true, "side"), 20);
            UIKit.Btn("Routes", p, C, new Vector2(next ? -135 : 0, by), new Vector2(w, 56), "ROUTES", () => { Close(); g.ToAttract(); RouteSelect(); }, 20);
            var again = UIKit.Btn("Again", p, C, new Vector2(next ? 135 : 270, by), new Vector2(w, 56), "RIDE AGAIN", () => { Save.CoachHp = 100; Close(); g.StartRide(g.routeIndex); }, 20);
            if (next)
            {
                var nb = UIKit.Btn("Next", p, C, new Vector2(405, by), new Vector2(w, 56), "NEXT ROUTE", () => { Close(); g.StartRide(g.routeIndex + 1); }, 20);
                Select(nb); return;
            }
            Select(again);
        }

        public void Failed(string reason, int kept, int fee = 0)
        {
            var p = NewPanel(true);
            T(p, new Vector2(0, 150), new Vector2(800, 70), "Dead & Buried", 52, UIKit.Blood);
            T(p, new Vector2(0, 85), new Vector2(800, 40), reason, 24, UIKit.Paper, TextAnchor.MiddleCenter, false);
            T(p, new Vector2(0, 40), new Vector2(800, 30), kept > 0 ? $"You keep the ${kept} in bounties. Purse ${Save.Purse}" : "", 19, UIKit.Gold);
            T(p, new Vector2(0, 10), new Vector2(800, 30), fee > 0 ? $"The ${fee} you paid your hired guns is gone." : "", 17, UIKit.Paper);
            var again = UIKit.Btn("Again", p, C, new Vector2(-170, -70), new Vector2(300, 60), "TRY AGAIN", () => { Save.CoachHp = 100; Close(); g.StartRide(g.routeIndex); });
            UIKit.Btn("Routes", p, C, new Vector2(170, -70), new Vector2(300, 60), "ROUTES", () => { Close(); g.ToAttract(); RouteSelect(); });
            Select(again);
        }
    }
}
