// Progress kept on the device (PlayerPrefs): stars and best totals per route, the purse, the guns you
// own and carry, which one starts in hand, the job, and the coach's wear between runs.
// Ported from the save object in ui/menus.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public static class Save
    {
        const string P = "schofield.";
        static void Flush() => PlayerPrefs.Save();

        public static int Purse { get => PlayerPrefs.GetInt(P + "purse", 0); set { PlayerPrefs.SetInt(P + "purse", Mathf.Max(0, value)); Flush(); } }
        public static void Earn(int v) { Purse += Mathf.Max(0, v); }
        public static void Spend(int v) { Purse -= Mathf.Max(0, v); }

        public static void Record(string route, int stars, int total)
        {
            if (stars > PlayerPrefs.GetInt(P + "stars." + route, 0)) PlayerPrefs.SetInt(P + "stars." + route, stars);
            if (total > PlayerPrefs.GetInt(P + "best." + route, 0)) PlayerPrefs.SetInt(P + "best." + route, total);
            Flush();
        }
        public static int Stars(string route) => PlayerPrefs.GetInt(P + "stars." + route, 0);
        public static int Best(string route) => PlayerPrefs.GetInt(P + "best." + route, 0);

        // ------------------------------------------------------------------ guns
        static HashSet<string> Owned()
        {
            var h = new HashSet<string>(Weapons.Starters);
            foreach (var id in PlayerPrefs.GetString(P + "owned", "").Split(',')) if (id.Length > 0) h.Add(id);
            return h;
        }
        public static bool Owns(string id) => Owned().Contains(id);
        public static bool Buy(string id)
        {
            if (!Weapons.All.TryGetValue(id, out var W) || Owns(id) || Purse < W.price) return false;
            Purse -= W.price;
            var o = Owned(); o.Add(id);
            PlayerPrefs.SetString(P + "owned", string.Join(",", o));
            Equip(id);
            return true;
        }
        /// the gun carried in a slot ("side" | "long")
        public static string Equipped(string slot)
        {
            var id = PlayerPrefs.GetString(P + "equip." + slot, "");
            return Weapons.All.TryGetValue(id, out var W) && W.slot == slot && Owns(id) ? id : slot == "side" ? "schofield" : "shotgun";
        }
        public static void Equip(string id) { PlayerPrefs.SetString(P + "equip." + Weapons.All[id].slot, id); Flush(); }
        /// which of the two is in hand when the ride starts
        public static string Start { get => PlayerPrefs.GetString(P + "start", "side") == "long" ? "long" : "side"; set { PlayerPrefs.SetString(P + "start", value); Flush(); } }

        // ------------------------------------------------------------------- job
        public static string Mission { get { var m = PlayerPrefs.GetString(P + "mission", "mail"); return Missions.All.ContainsKey(m) ? m : "mail"; } set { PlayerPrefs.SetString(P + "mission", value); Flush(); } }

        // ----------------------------------------------------------------- coach
        // Damage carries between runs. The driver patches what he can for nothing between towns; anything
        // better costs money at the livery.
        public const int FreePatch = 18, RepairRate = 5;
        public static int CoachHp { get => PlayerPrefs.GetInt(P + "coachHp", 100); set { PlayerPrefs.SetInt(P + "coachHp", Mathf.Clamp(value, 0, 100)); Flush(); } }
        public static void WearCoach(float endHp) => CoachHp = Mathf.Min(100, Mathf.Max(0, Mathf.RoundToInt(endHp)) + FreePatch);
        public static int RepairCost() => Mathf.Max(0, (100 - CoachHp) * RepairRate);
        /// "full" | "poor" | points repaired
        public static string Repair()
        {
            int cost = RepairCost();
            if (cost <= 0) return "full";
            int afford = Mathf.Min(cost, Purse);
            if (afford < RepairRate) return "poor";
            int points = afford / RepairRate;
            Purse -= points * RepairRate;
            CoachHp += points;
            return points.ToString();
        }

        /// start over: purse, guns, stars, coach wear (settings stay)
        public static void Wipe()
        {
            foreach (var k in new[] { "purse", "owned", "equip.side", "equip.long", "start", "mission", "coachHp" }) PlayerPrefs.DeleteKey(P + k);
            foreach (var r in Routes.All) { PlayerPrefs.DeleteKey(P + "stars." + r.id); PlayerPrefs.DeleteKey(P + "best." + r.id); }
            Flush();
        }
    }
}
