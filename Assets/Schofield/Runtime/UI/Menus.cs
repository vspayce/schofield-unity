// Title, pause, results and failure screens (a slice of ui/menus.js: the gunsmith, loadout, missions
// and hired guns come in later phases). Mouse, touch and controller all work: the first button is
// selected so a pad can navigate.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Schofield
{
    public static class Save
    {
        public static int Purse { get => PlayerPrefs.GetInt("schofield.purse", 0); set => PlayerPrefs.SetInt("schofield.purse", value); }
        public static void Earn(int v) { Purse += v; PlayerPrefs.Save(); }
        public static void Record(string route, int stars, int total)
        {
            if (stars > PlayerPrefs.GetInt("schofield.stars." + route, 0)) PlayerPrefs.SetInt("schofield.stars." + route, stars);
            if (total > PlayerPrefs.GetInt("schofield.best." + route, 0)) PlayerPrefs.SetInt("schofield.best." + route, total);
            PlayerPrefs.Save();
        }
        public static int Stars(string route) => PlayerPrefs.GetInt("schofield.stars." + route, 0);
    }

    public struct Results { public int kills, headshots, accuracy, coach, bounty, reward, total, stars; }

    public class Menus
    {
        readonly Game g;
        readonly Canvas canvas;
        RectTransform panel;

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
            if (dim) UIKit.Img("Dim", panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000, 4000), null, new Color(0.05f, 0.03f, 0.02f, 0.62f));
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

        public void Title()
        {
            var p = NewPanel(false, true);
            var L = new Vector2(0, 0.5f);
            var logo = UIKit.Img("Logo", p, new Vector2(0, 1), new Vector2(260, -150), new Vector2(420, 210), Assets.Sprite("logo"));
            if (!Assets.Sprite("logo")) UIKit.Txt("Logo", p, new Vector2(0, 1), new Vector2(260, -150), new Vector2(460, 120), "SCHOFIELD", 72, UIKit.Paper, TextAnchor.MiddleCenter, true);
            int total = 0; foreach (var r in Routes.All) total += Save.Stars(r.id);
            UIKit.Txt("Tag", p, L, new Vector2(260, 30), new Vector2(460, 30), "Shotgun messenger, Wells & Co. Overland", 20, UIKit.Gold, TextAnchor.MiddleCenter, true);
            UIKit.Txt("Stars", p, L, new Vector2(260, -10), new Vector2(400, 30), $"★ {total} of {Routes.All.Length * 3}", 22, UIKit.Paper, TextAnchor.MiddleCenter, true);
            var ride = UIKit.Btn("Ride", p, L, new Vector2(260, -80), new Vector2(300, 64), "RIDE OUT", () => RouteSelect(), 34);
            UIKit.Txt("Purse", p, L, new Vector2(260, -190), new Vector2(300, 26), "Purse: $" + Save.Purse, 18, UIKit.Paper, TextAnchor.MiddleCenter, true);
            Select(ride);
        }

        /// every route is open from the start (as in the web game)
        public void RouteSelect()
        {
            var p = NewPanel(true);
            var C = new Vector2(0.5f, 0.5f);
            UIKit.Txt("T", p, C, new Vector2(0, 262), new Vector2(800, 60), "Choose your route", 44, UIKit.Paper, TextAnchor.MiddleCenter, true);
            Button first = null;
            for (int i = 0; i < Routes.All.Length; i++)
            {
                var def = Routes.All[i]; int idx = i;
                float y = 180 - i * 86;
                var b = UIKit.Btn("Route " + def.id, p, C, new Vector2(0, y), new Vector2(780, 76), "", () => { Close(); g.StartRide(idx); });
                var bt = b.transform;
                var A = new Vector2(0.5f, 0.5f);
                var n = UIKit.Txt("Name", bt, A, new Vector2(-130, 13), new Vector2(480, 34), def.name, 28, UIKit.Paper, TextAnchor.MiddleLeft, true);
                var l = UIKit.Txt("Leg", bt, A, new Vector2(-130, -18), new Vector2(480, 24), $"{def.from} to {def.to}", 16, UIKit.Gold, TextAnchor.MiddleLeft, true);
                int st = Save.Stars(def.id);
                UIKit.Txt("Stars", bt, A, new Vector2(270, 13), new Vector2(200, 32), new string('★', st) + new string('☆', 3 - st), 26, UIKit.Gold);
                UIKit.Txt("Reward", bt, A, new Vector2(270, -18), new Vector2(200, 24), "REWARD $" + def.reward, 15, UIKit.Paper, TextAnchor.MiddleCenter, true);
                if (first == null) first = b;
            }
            UIKit.Btn("Back", p, C, new Vector2(0, -262), new Vector2(240, 54), "BACK", () => Title());
            Select(first);
        }

        public void Pause()
        {
            var p = NewPanel(true);
            var C = new Vector2(0.5f, 0.5f);
            UIKit.Txt("T", p, C, new Vector2(0, 140), new Vector2(600, 70), "Paused", 56, UIKit.Paper, TextAnchor.MiddleCenter, true);
            var resume = UIKit.Btn("Resume", p, C, new Vector2(0, 40), new Vector2(320, 60), "RESUME", () => { Close(); g.Resume(); });
            UIKit.Btn("Restart", p, C, new Vector2(0, -32), new Vector2(320, 60), "RIDE AGAIN", () => { Close(); g.StartRide(g.routeIndex); });
            UIKit.Btn("Quit", p, C, new Vector2(0, -104), new Vector2(320, 60), "QUIT TO TITLE", () => { Close(); g.ToAttract(); Title(); });
            // look sensitivity
            var sens = UIKit.Txt("Sens", p, C, new Vector2(0, -170), new Vector2(500, 30), "", 18, UIKit.Paper, TextAnchor.MiddleCenter, true);
            void ShowSens() => sens.text = $"Aim speed  {g.input.sens:0.0}   ( − / + )";
            ShowSens();
            UIKit.Btn("Slower", p, C, new Vector2(-200, -210), new Vector2(70, 44), "−", () => { g.input.sens = Mathf.Max(0.3f, g.input.sens - 0.1f); g.input.Save(); ShowSens(); });
            UIKit.Btn("Faster", p, C, new Vector2(200, -210), new Vector2(70, 44), "+", () => { g.input.sens = Mathf.Min(3f, g.input.sens + 0.1f); g.input.Save(); ShowSens(); });
            Select(resume);
        }

        public void ShowResults(Results r)
        {
            var p = NewPanel(true);
            var C = new Vector2(0.5f, 0.5f);
            var def = Routes.All[g.routeIndex];
            UIKit.Txt("T", p, C, new Vector2(0, 200), new Vector2(800, 70), def.to, 54, UIKit.Paper, TextAnchor.MiddleCenter, true);
            UIKit.Txt("Stars", p, C, new Vector2(0, 140), new Vector2(300, 50), new string('★', r.stars) + new string('☆', 3 - r.stars), 44, UIKit.Gold);
            string body = $"Outlaws down   {r.kills}\nHeadshots   {r.headshots}\nAccuracy   {r.accuracy}%\nCoach   {r.coach}%\n\nBounty   ${r.bounty}\nMail contract   ${r.reward}\n<b>Total   ${r.total}</b>";
            var t = UIKit.Txt("Body", p, C, new Vector2(0, 0), new Vector2(500, 220), body, 22, UIKit.Paper, TextAnchor.MiddleCenter, true);
            t.supportRichText = true;
            bool next = g.routeIndex + 1 < Routes.All.Length;
            var again = UIKit.Btn("Again", p, C, new Vector2(next ? -310 : -170, -170), new Vector2(290, 60), "RIDE AGAIN", () => { Close(); g.StartRide(g.routeIndex); });
            UIKit.Btn("Routes", p, C, new Vector2(next ? 0 : 170, -170), new Vector2(290, 60), "ROUTES", () => { Close(); g.ToAttract(); RouteSelect(); });
            if (next)
            {
                var nb = UIKit.Btn("Next", p, C, new Vector2(310, -170), new Vector2(290, 60), "NEXT ROUTE", () => { Close(); g.StartRide(g.routeIndex + 1); });
                Select(nb); return;
            }
            Select(again);
        }

        public void Failed(string reason, int kept)
        {
            var p = NewPanel(true);
            var C = new Vector2(0.5f, 0.5f);
            UIKit.Txt("T", p, C, new Vector2(0, 150), new Vector2(800, 70), "The road takes another", 46, UIKit.Blood, TextAnchor.MiddleCenter, true);
            UIKit.Txt("Why", p, C, new Vector2(0, 80), new Vector2(800, 40), reason, 26, UIKit.Paper, TextAnchor.MiddleCenter, true);
            UIKit.Txt("Kept", p, C, new Vector2(0, 30), new Vector2(800, 30), kept > 0 ? $"You keep the ${kept} in bounties." : "", 20, UIKit.Gold, TextAnchor.MiddleCenter, true);
            var again = UIKit.Btn("Again", p, C, new Vector2(-170, -60), new Vector2(300, 60), "RIDE AGAIN", () => { Close(); g.StartRide(g.routeIndex); });
            UIKit.Btn("Routes", p, C, new Vector2(170, -60), new Vector2(300, 60), "ROUTES", () => { Close(); g.ToAttract(); RouteSelect(); });
            Select(again);
        }
    }
}
