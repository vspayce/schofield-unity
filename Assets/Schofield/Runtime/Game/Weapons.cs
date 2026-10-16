// The guns (from game/weapons.js). You carry two: a sidearm and a long gun, and SWAP flips between
// them. The Schofield and the messenger shotgun are yours from the start; the rest are sold by the
// gunsmith (next phase).
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class WeaponDef
    {
        public string id, slot, kind, name, model; public int year, price, mag, pellets = 1;
        public Color? finish; public float interval, reload, spread, damage, headMult, range, falloff0, falloff1, floor, kick, zoom = 1, pitch = 1;
        public bool auto, knock; public string[] snd; public string reloadSnd; public string blurb;
    }

    public static class Weapons
    {
        static WeaponDef Rev(string id, string name, int year, int price, Color? finish, int mag, float interval, float reload, float spread, float damage, float range, float f0, float f1, float floor, float kick, string[] snd, float pitch, string blurb)
            => new WeaponDef { id = id, slot = "side", kind = "revolver", name = name, year = year, price = price, model = "Schofield", finish = finish, mag = mag, interval = interval, reload = reload, auto = true, spread = spread, damage = damage, headMult = 3, range = range, falloff0 = f0, falloff1 = f1, floor = floor, kick = kick, snd = snd, pitch = pitch, reloadSnd = "schofield_reload", blurb = blurb };

        public static readonly Dictionary<string, WeaponDef> All = new Dictionary<string, WeaponDef>
        {
            { "schofield", Rev("schofield", "Schofield Revolver", 1875, 0, null, 6, 0.26f, 1.6f, 0.0045f, 60, 260, 90, 260, 0.5f, 0.028f, new[] { "schofield_shot", "schofield_shot2" }, 1,
                "Smith & Wesson top-break .45. Six quick, true shots and a fast reload. Jesse James carried one.") },
            { "paterson", Rev("paterson", "Colt Paterson", 1836, 150, new Color(0.55f, 0.55f, 0.6f), 5, 0.2f, 2.6f, 0.0055f, 58, 230, 70, 230, 0.45f, 0.024f, new[] { "schofield_shot2" }, 1.12f,
                "Colt's first revolver. Five shots fired fast, but slow to reload.") },
            { "navy", Rev("navy", "Colt Navy", 1851, 300, new Color(1.25f, 1.05f, 0.7f), 6, 0.25f, 2.1f, 0.0025f, 64, 300, 110, 300, 0.55f, 0.024f, new[] { "schofield_shot" }, 1.05f,
                "A brass-framed .36 that points like your finger. Wild Bill Hickok wore a pair of them.") },
            { "peacemaker", Rev("peacemaker", "Colt Peacemaker", 1873, 800, new Color(0.45f, 0.47f, 0.55f), 6, 0.3f, 2.0f, 0.0035f, 90, 300, 110, 300, 0.55f, 0.036f, new[] { "schofield_shot" }, 0.88f,
                "The Single Action Army in .45 Colt, the frontier lawman's gun. One shot drops most men.") },
            { "shotgun", new WeaponDef { id = "shotgun", slot = "long", kind = "shotgun", name = "Messenger Shotgun", year = 1870, price = 0, model = "CoachGun", mag = 2, interval = 0.32f, reload = 1.8f,
                pellets = 9, spread = 0.06f, damage = 20, headMult = 1.5f, range = 70, falloff0 = 14, falloff1 = 55, floor = 0.15f, kick = 0.075f, zoom = 1.6f, knock = true,
                snd = new[] { "shotgun_shot" }, reloadSnd = "shotgun_reload", blurb = "The Wells Fargo guard's double-barrel 10 gauge. Knocks a man clean out of the saddle." } },
            { "winchester", new WeaponDef { id = "winchester", slot = "long", kind = "rifle", name = "Winchester 1873", year = 1873, price = 650, model = "Winchester", mag = 12, interval = 0.36f, reload = 3.0f,
                spread = 0.0025f, damage = 78, headMult = 2.5f, range = 360, falloff0 = 150, falloff1 = 360, floor = 0.55f, kick = 0.04f, zoom = 4,
                snd = new[] { "schofield_shot" }, pitch = 0.8f, reloadSnd = "shotgun_reload", blurb = "\"The gun that won the West.\" Twelve rounds of .44-40 through the lever." } },
            { "sharps", new WeaponDef { id = "sharps", slot = "long", kind = "rifle", name = "Sharps \"Old Reliable\"", year = 1874, price = 1000, model = "Sharps", mag = 1, interval = 0.5f, reload = 1.5f,
                spread = 0.0007f, damage = 260, headMult = 2, range = 700, falloff0 = 400, falloff1 = 700, floor = 0.7f, kick = 0.09f, zoom = 8, knock = true,
                snd = new[] { "shotgun_shot" }, pitch = 0.75f, reloadSnd = "schofield_cock", blurb = "The buffalo hunter's .50-90." } },
        };
    }
}
