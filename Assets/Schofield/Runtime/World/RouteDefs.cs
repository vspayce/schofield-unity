// Route (level) definitions, ported from the web game's world/routes.js. Everything visual and
// gameplay-related that varies between the runs lives here.
using UnityEngine;

namespace Schofield
{
    public class LightDef
    {
        public float sunElev, sunAzim, sunIntensity, hemiIntensity, env = 0.5f, fogDensity, exposure, clouds;
        public Color sunColor, hemiSky, hemiGround, zenith, horizon, sunGlow, ground, fog;
        public Vector3 tintShadow = new Vector3(0.9f, 0.95f, 1.05f);
        public float gradeSat = 1, gradeContrast = 1;
        public Vector3 gradeGain = Vector3.one, gradeLift = Vector3.zero;
    }

    public class TerrainDef
    {
        public string ground1, ground2, road, rock, snow;
        public Color tint1, tint2, rockTint, roadTint, propRock;
        public float snowLine = 9999, blend = 30, rockSlope = -1;
        public Vector2 greenMix = new Vector2(0.42f, 0.68f);
    }

    public class ScatterRule
    {
        public string kind;
        public float density, minRoad = 6, cluster, minH = float.NaN, maxH = float.NaN, minSlope = float.NaN;
        public Vector2 scale = Vector2.one;
        public bool far, fence, poles;
        public ScatterRule(string kind, float density) { this.kind = kind; this.density = density; }
    }

    public class GrassDef { public float density, height; public Color color; }

    public class WaveDef
    {
        public float at; public string type; public int count; public string from = "behind";
        public bool done; public string kind; public float lead;
        public WaveDef(float at, string type, int count, string from = "behind") { this.at = at; this.type = type; this.count = count; this.from = from; }
    }

    public class ArchDef { public float at, span = 92, baseH, rise; public ArchDef(float at, float span, float baseH, float rise) { this.at = at; this.span = span; this.baseH = baseH; this.rise = rise; } }

    /// Thunder Gorge: which hand the river is on, the ledge height over the water, the timber trestle across a
    /// side cut, and the falls the road runs under
    public class GorgeDef
    {
        public float side = 1, depth = 26;
        public float bridgeAt = -1, bridgeLength = 300, bridgeWidth = 8.4f, bridgeCutHalf = 46, bridgeDepth = 76;
        public float fallsAt = -1, fallsDrop = 34, fallsWidth = 24;
    }

    public class RouteDef
    {
        public string id, name, from, to, blurb, biome, clock;
        public int seed; public float length, reward, wind, winding;
        public LightDef light; public TerrainDef terrain; public ScatterRule[] scatter; public GrassDef grass;
        public WaveDef[] waves;
        public float ghostTownStart = -1, ghostTownLength;
        public ArchDef[] arches;
        public int billboards = 3;
        public WildDef wildlife;
        public RailDef railroad;
        public GorgeDef gorge;
    }

    public static class Routes
    {
        static Color C(string h) => MathX.Hex(h);
        static ScatterRule S(string k, float d, float minRoad, float s0, float s1, float cluster = 0, bool far = false)
            => new ScatterRule(k, d) { minRoad = minRoad, scale = new Vector2(s0, s1), cluster = cluster, far = far };

        public static readonly RouteDef[] All =
        {
            new RouteDef
            {
                id = "plains", clock = "5:40", name = "Dry Creek Plains", from = "Mercy Springs", to = "Dry Creek",
                blurb = "Open grassland at golden hour. Riders will try to run you down.",
                seed = 1871, length = 2600, biome = "plains", reward = 400, wind = 0.9f, winding = 0.35f,
                wildlife = new WildDef(95, "buffalo", "goat", "goat", "bear", "hawk"),
                railroad = new RailDef { at = 0.62f, workers = 7, train = true, cars = 3, gunmen = 4 },
                light = new LightDef
                {
                    sunElev = 13, sunAzim = 230, sunColor = C("#ffc98a"), sunIntensity = 4.2f,
                    hemiSky = C("#7f9fd0"), hemiGround = C("#5a3f24"), hemiIntensity = 0.7f, env = 0.3f, tintShadow = new Vector3(0.8f, 0.9f, 1.12f),
                    zenith = C("#3f6ea8"), horizon = C("#f2c18c"), sunGlow = C("#ffb36b"), ground = C("#8a6a45"),
                    fog = C("#d8b48c"), fogDensity = 0.0017f, exposure = 0.95f, clouds = 0.45f,
                    gradeSat = 1.08f, gradeContrast = 1.15f, gradeGain = new Vector3(1.04f, 1.0f, 0.92f), gradeLift = new Vector3(0.008f, 0.004f, 0f),
                },
                terrain = new TerrainDef
                {
                    ground1 = "dry_grass", ground2 = "green_grass", road = "dirt_road", rock = "rock", snow = null,
                    tint1 = C("#ffffff"), tint2 = C("#fff6c8"), rockTint = C("#c8b8a0"), roadTint = C("#fff4e6"), propRock = C("#d8c8b0"),
                    snowLine = 9999, blend = 34, rockSlope = 0.2f, greenMix = new Vector2(0.5f, 0.75f),
                },
                scatter = new[]
                {
                    S("Sagebrush", 22, 7, 0.8f, 1.5f, 0.1f),
                    S("Rock_A", 1.6f, 8, 0.6f, 1.8f),
                    S("Rock_B", 1.2f, 8, 0.6f, 1.6f),
                    S("Boulder_Big", 0.25f, 25, 0.8f, 1.6f, 0, true),
                    S("Pine_A", 0.7f, 25, 0.9f, 1.4f, 0.45f, true),
                    S("DeadTree", 0.25f, 10, 0.9f, 1.3f, 0, true),
                    new ScatterRule("Fence_Rail", 0) { fence = true },
                    new ScatterRule("TelegraphPole", 0) { poles = true },
                    S("CowSkull", 0.1f, 5, 1, 1),
                },
                grass = new GrassDef { density = 3.5f, color = C("#c9a860"), height = 0.42f },
                waves = new[]
                {
                    new WaveDef(0.08f, "riders", 2, "behind"),
                    new WaveDef(0.2f, "riders", 3, "flank"),
                    new WaveDef(0.28f, "herd", 16) { kind = "buffalo", lead = 320 },
                    new WaveDef(0.34f, "ridge", 2),
                    new WaveDef(0.42f, "riders", 4, "behind"),
                    new WaveDef(0.58f, "riders", 3, "ahead"),
                    new WaveDef(0.66f, "ridge", 3),
                    new WaveDef(0.76f, "riders", 5, "behind"),
                },
            },
            new RouteDef
            {
                id = "pass", clock = "10:20", name = "Widow's Pass", from = "Dry Creek", to = "Silver Notch",
                blurb = "A winding mountain road. Riflemen on every ridge; mind the drop.",
                seed = 1874, length = 2800, biome = "mountain", reward = 650, wind = 1.2f, winding = 1.0f, billboards = 2,
                wildlife = new WildDef(120, "goat", "goat", "goat", "bear", "hawk"),
                light = new LightDef
                {
                    sunElev = 24, sunAzim = 150, sunColor = C("#ffe2c0"), sunIntensity = 3.0f,
                    hemiSky = C("#9fb4d2"), hemiGround = C("#4d4a40"), hemiIntensity = 0.85f, env = 0.35f, tintShadow = new Vector3(0.8f, 0.9f, 1.12f),
                    zenith = C("#4a6a92"), horizon = C("#b9c3cc"), sunGlow = C("#c9bba0"), ground = C("#5a5850"),
                    fog = C("#8e9cae"), fogDensity = 0.0019f, exposure = 1.0f, clouds = 0.75f,
                    gradeSat = 0.95f, gradeContrast = 1.14f, gradeGain = new Vector3(0.98f, 1.0f, 1.04f), gradeLift = new Vector3(0f, 0.004f, 0.012f),
                },
                terrain = new TerrainDef
                {
                    ground1 = "green_grass", ground2 = "dry_grass", road = "dirt_road", rock = "rock", snow = "snow",
                    tint1 = C("#b9c29a"), tint2 = C("#c9b48a"), rockTint = C("#b0aca6"), roadTint = C("#d8d0c4"), propRock = C("#b8b4ae"),
                    snowLine = 122, blend = 20, rockSlope = 0.2f,
                },
                scatter = new[]
                {
                    new ScatterRule("Pine_A", 40) { minRoad = 7, cluster = 0.25f, scale = new Vector2(0.9f, 1.5f), far = true, maxH = 118 },
                    new ScatterRule("Pine_B", 26) { minRoad = 7, cluster = 0.3f, scale = new Vector2(0.9f, 1.4f), far = true, maxH = 118 },
                    new ScatterRule("Pine_Snow", 20) { minRoad = 7, cluster = 0.2f, scale = new Vector2(0.9f, 1.4f), far = true, minH = 105, maxH = 170 },
                    S("Rock_A", 3, 6, 0.6f, 2.2f), S("Rock_C", 2, 6, 0.8f, 2.0f),
                    S("Boulder_Big", 0.8f, 12, 0.8f, 2.0f, 0, true),
                    new ScatterRule("CliffChunk", 0.5f) { minRoad = 14, scale = new Vector2(1, 2), far = true, minSlope = 0.3f },
                    new ScatterRule("Sagebrush", 6) { minRoad = 6, scale = new Vector2(0.6f, 1.1f), maxH = 110 },
                    S("DeadTree", 0.3f, 8, 0.9f, 1.2f, 0, true),
                },
                grass = new GrassDef { density = 0.8f, color = C("#9aa060"), height = 0.45f },
                waves = new[]
                {
                    new WaveDef(0.07f, "ridge", 2), new WaveDef(0.16f, "riders", 2, "behind"), new WaveDef(0.27f, "ridge", 3),
                    new WaveDef(0.38f, "riders", 3, "ahead"), new WaveDef(0.5f, "ridge", 3), new WaveDef(0.6f, "riders", 4, "behind"),
                    new WaveDef(0.72f, "ridge", 4), new WaveDef(0.82f, "riders", 4, "flank"),
                },
            },
            new RouteDef
            {
                id = "perdition", clock = "7:25", name = "Perdition", from = "Silver Notch", to = "Red Mesa",
                blurb = "Straight through a ghost town at dusk. Gunmen in every window.",
                seed = 1877, length = 2400, biome = "desert", reward = 900, wind = 1.0f, winding = 0.25f,
                wildlife = new WildDef(130, "goat", "goat", "hawk"),
                railroad = new RailDef { at = 0.34f, workers = 5, train = true, cars = 3, gunmen = 5, speed = 12 },
                ghostTownStart = 0.38f, ghostTownLength = 420,
                light = new LightDef
                {
                    sunElev = 5, sunAzim = 260, sunColor = C("#ff9a55"), sunIntensity = 3.0f,
                    hemiSky = C("#7d7fa8"), hemiGround = C("#6a4630"), hemiIntensity = 1.0f, tintShadow = new Vector3(0.8f, 0.9f, 1.12f),
                    zenith = C("#2e3f6e"), horizon = C("#f08a55"), sunGlow = C("#ff7a3a"), ground = C("#6a4a32"),
                    fog = C("#c98a66"), fogDensity = 0.0021f, exposure = 1.1f, clouds = 0.55f,
                    gradeSat = 1.12f, gradeContrast = 1.1f, gradeGain = new Vector3(1.06f, 0.97f, 0.9f), gradeLift = new Vector3(0.02f, 0.006f, 0.012f),
                },
                terrain = new TerrainDef
                {
                    ground1 = "sand", ground2 = "dry_grass", road = "dirt_road", rock = "red_rock", snow = null,
                    tint1 = C("#f0d8b8"), tint2 = C("#c8a070"), rockTint = C("#e0b090"), roadTint = C("#f0c8a0"), propRock = C("#e8b090"),
                    snowLine = 9999, blend = 30,
                },
                scatter = new[]
                {
                    S("Saguaro", 1.4f, 8, 0.8f, 1.4f, 0, true), S("Joshua", 0.8f, 8, 0.8f, 1.3f, 0, true),
                    S("Sagebrush", 12, 6, 0.6f, 1.2f), S("Rock_B", 1.5f, 7, 0.6f, 1.8f),
                    S("Boulder_Big", 0.4f, 20, 0.8f, 1.8f, 0, true), S("Tumbleweed", 0.4f, 3, 0.8f, 1.2f),
                    S("CowSkull", 0.15f, 4, 1, 1), S("GraveCross", 0.08f, 6, 1, 1),
                    new ScatterRule("TelegraphPole", 0) { poles = true },
                },
                grass = new GrassDef { density = 0.35f, color = C("#b09060"), height = 0.4f },
                waves = new[]
                {
                    new WaveDef(0.1f, "riders", 3, "behind"), new WaveDef(0.24f, "ridge", 2), new WaveDef(0.38f, "town", 10),
                    new WaveDef(0.44f, "riders", 3, "ahead"), new WaveDef(0.66f, "riders", 4, "behind"), new WaveDef(0.8f, "ridge", 3),
                },
            },
            new RouteDef
            {
                id = "gulch", clock = "5:55", name = "Devil's Gulch", from = "Red Mesa", to = "Fort Providence",
                blurb = "A red canyon at sundown. Every gun in the territory wants your strongbox.",
                seed = 1879, length = 3000, biome = "canyon", reward = 1400, wind = 1.1f, winding = 0.6f,
                wildlife = new WildDef(115, "bear", "goat", "goat", "hawk"),
                arches = new[] { new ArchDef(0.24f, 96, 30, 25), new ArchDef(0.57f, 88, 33, 27), new ArchDef(0.82f, 102, 29, 26) },
                light = new LightDef
                {
                    sunElev = 16, sunAzim = 200, sunColor = C("#ffb070"), sunIntensity = 4.2f,
                    hemiSky = C("#8aa2d6"), hemiGround = C("#8a4028"), hemiIntensity = 1.05f, env = 0.5f, tintShadow = new Vector3(0.82f, 0.9f, 1.1f),
                    zenith = C("#335c96"), horizon = C("#f5b07a"), sunGlow = C("#ff9848"), ground = C("#7a4028"),
                    fog = C("#c48a6a"), fogDensity = 0.0022f, exposure = 1.22f, clouds = 0.3f,
                    gradeSat = 1.02f, gradeContrast = 1.13f, gradeGain = new Vector3(1.05f, 0.98f, 0.9f), gradeLift = new Vector3(0.018f, 0.006f, 0f),
                },
                terrain = new TerrainDef
                {
                    ground1 = "sand", ground2 = "gravel", road = "dirt_road", rock = "red_rock", snow = null,
                    tint1 = C("#f2c9a0"), tint2 = C("#d0a080"), rockTint = C("#ffffff"), roadTint = C("#f2b88c"), propRock = C("#ffb08a"),
                    snowLine = 9999, blend = 34,
                },
                scatter = new[]
                {
                    S("Rock_A", 3, 6, 0.6f, 2.2f), S("Rock_C", 2, 6, 0.8f, 2.0f), S("Boulder_Big", 1.0f, 10, 0.8f, 2.2f, 0, true),
                    S("Sagebrush", 8, 6, 0.6f, 1.1f), S("Saguaro", 0.5f, 10, 0.8f, 1.3f, 0, true), S("DeadTree", 0.3f, 8, 0.8f, 1.2f, 0, true),
                    S("Wagon_Wreck", 0.04f, 6, 1, 1), S("CowSkull", 0.1f, 4, 1, 1),
                },
                grass = new GrassDef { density = 0.3f, color = C("#b89060"), height = 0.4f },
                waves = new[]
                {
                    new WaveDef(0.06f, "riders", 3, "behind"), new WaveDef(0.16f, "ridge", 3), new WaveDef(0.26f, "riders", 4, "flank"),
                    new WaveDef(0.38f, "ridge", 4), new WaveDef(0.48f, "riders", 5, "behind"), new WaveDef(0.6f, "ridge", 4),
                    new WaveDef(0.7f, "riders", 4, "ahead"), new WaveDef(0.8f, "riders", 6, "behind"), new WaveDef(0.86f, "ridge", 4),
                },
            },
            new RouteDef
            {
                id = "gorge", clock = "8:15", name = "Thunder Gorge", from = "Fort Providence", to = "Cascade",
                blurb = "Cross a high timber trestle over the gorge before the road runs beneath the falls.",
                seed = 1883, length = 2700, biome = "gorge", reward = 1800, wind = 1.1f, winding = 0.75f, billboards = 2,
                wildlife = new WildDef(110, "goat", "goat", "bear", "hawk"),
                gorge = new GorgeDef { side = 1, depth = 27, bridgeAt = 0.22f, bridgeLength = 300, bridgeWidth = 8.4f, bridgeCutHalf = 46, bridgeDepth = 76, fallsAt = 0.54f, fallsDrop = 34, fallsWidth = 24 },
                light = new LightDef
                {
                    sunElev = 19, sunAzim = 118, sunColor = C("#ffe6c4"), sunIntensity = 3.4f,
                    hemiSky = C("#93b4d6"), hemiGround = C("#3f4a3a"), hemiIntensity = 0.95f, env = 0.4f, tintShadow = new Vector3(0.78f, 0.9f, 1.15f),
                    zenith = C("#3d6b9c"), horizon = C("#c9d7dc"), sunGlow = C("#ffd9a6"), ground = C("#4e5648"),
                    fog = C("#a9bcc4"), fogDensity = 0.0021f, exposure = 1.0f, clouds = 0.6f,
                    gradeSat = 1.1f, gradeContrast = 1.12f, gradeGain = new Vector3(0.98f, 1.02f, 1.04f), gradeLift = new Vector3(0.006f, 0.01f, 0.014f),
                },
                terrain = new TerrainDef
                {
                    ground1 = "green_grass", ground2 = "dry_grass", road = "gravel", rock = "rock", snow = null,
                    tint1 = C("#d8e8c8"), tint2 = C("#fff2d0"), rockTint = C("#b8bcb4"), roadTint = C("#f0ece4"), propRock = C("#c4c8c0"),
                    snowLine = 9999, blend = 20, rockSlope = 0.16f, greenMix = new Vector2(0.25f, 0.55f),
                },
                scatter = new[]
                {
                    S("Pine_A", 5.5f, 9, 0.9f, 1.6f, 0.5f), S("Pine_B", 4.2f, 9, 0.9f, 1.5f, 0.5f),
                    S("Rock_A", 3.0f, 7, 0.6f, 1.9f), S("Rock_C", 2.2f, 7, 0.6f, 1.7f), S("Boulder_Big", 0.8f, 12, 0.9f, 1.8f),
                    S("DeadTree", 0.4f, 10, 0.9f, 1.3f), S("Sagebrush", 6, 6, 0.7f, 1.2f, 0.2f),
                    new ScatterRule("Fence_Rail", 0) { fence = true },
                    new ScatterRule("TelegraphPole", 0) { poles = true },
                },
                grass = new GrassDef { density = 2.6f, color = C("#8fa864"), height = 0.36f },
                waves = new[]
                {
                    new WaveDef(0.1f, "riders", 3, "behind"), new WaveDef(0.22f, "ridge", 3), new WaveDef(0.33f, "riders", 4, "flank"),
                    new WaveDef(0.44f, "riders", 4, "behind"), new WaveDef(0.62f, "ridge", 3), new WaveDef(0.72f, "riders", 5, "ahead"),
                    new WaveDef(0.85f, "riders", 5, "behind"),
                },
            },
        };

        // difficulty per route (the web game's DIFF table in main.js)
        public struct Diff { public float riderAcc, rifleAcc, fireMult, dmgMult, countMult; }
        static readonly Diff[] Table =
        {
            new Diff { riderAcc = 0.3f, rifleAcc = 0.4f, fireMult = 1.15f, dmgMult = 0.85f, countMult = 1 },
            new Diff { riderAcc = 0.34f, rifleAcc = 0.45f, fireMult = 1.05f, dmgMult = 0.95f, countMult = 1 },
            new Diff { riderAcc = 0.37f, rifleAcc = 0.48f, fireMult = 1.0f, dmgMult = 1.0f, countMult = 1.1f },
            new Diff { riderAcc = 0.4f, rifleAcc = 0.5f, fireMult = 0.92f, dmgMult = 1.05f, countMult = 1.2f },
        };
        public static Diff DiffFor(int i) => Table[Mathf.Clamp(i, 0, Table.Length - 1)];
    }
}
