// What the coach is carrying. The same road three ways: the cargo sets the pay, how hard the run is,
// and the extra marauder bands that come for it. Ported from game/missions.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class MissionDef
    {
        public string id, name, cargo, blurb, livery;
        public float pay, risk;
        public float[] bands;
        public float countMult = 1, fireMult = 1, dmgMult = 1, riderAcc = 1, rifleAcc = 1;
    }

    public static class Missions
    {
        public static readonly Dictionary<string, MissionDef> All = new Dictionary<string, MissionDef>
        {
            { "mail", new MissionDef { id = "mail", name = "Mail Contract", cargo = "Overland mail and a light strongbox", pay = 1, livery = "concord", bands = new float[0], risk = 0.25f,
                blurb = "Letters, parcels and a modest box. The road knows it, and mostly leaves you be." } },
            { "bank", new MissionDef { id = "bank", name = "Bank Transfer", cargo = "Sealed bullion for the Territorial Bank", pay = 2.1f, livery = "treasure", bands = new[] { 0.55f }, risk = 0.6f,
                countMult = 1.35f, fireMult = 0.88f, dmgMult = 1.1f, riderAcc = 1.1f, rifleAcc = 1.12f,
                blurb = "An armoured Abbott-Downing with shuttered ports. Word of a bullion run travels ahead of you." } },
            { "prisoner", new MissionDef { id = "prisoner", name = "Prisoner Transfer", cargo = "A man due to hang, and friends who disagree", pay = 3.4f, livery = "prison", bands = new[] { 0.32f, 0.58f, 0.78f }, risk = 1,
                countMult = 1.7f, fireMult = 0.76f, dmgMult = 1.2f, riderAcc = 1.2f, rifleAcc = 1.25f,
                blurb = "A barred wagon and a hanging at the end of it. His people will come for him the whole way." } },
        };
        public static readonly string[] Order = { "mail", "bank", "prisoner" };

        /// fold a mission's multipliers into a route's base difficulty
        public static Routes.Diff Apply(Routes.Diff b, MissionDef m) => new Routes.Diff
        {
            riderAcc = Mathf.Min(0.85f, b.riderAcc * m.riderAcc),
            rifleAcc = Mathf.Min(0.9f, b.rifleAcc * m.rifleAcc),
            fireMult = b.fireMult * m.fireMult,
            dmgMult = b.dmgMult * m.dmgMult,
            countMult = b.countMult * m.countMult,
        };
    }
}
