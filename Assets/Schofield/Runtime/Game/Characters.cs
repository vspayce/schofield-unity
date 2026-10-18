// Horses, people and guns from the GLBs: instancing, the outfits ("looks") made of mesh toggles and
// colour multipliers, named bones for aiming, and the gun-on-hand rig. Ported from game/characters.js.
using System.Collections.Generic;
using UnityEngine;

namespace Schofield
{
    public class Horse
    {
        public GameObject root; public Anim anim; public Transform mount;
        public void Play(string n, float fade = 0.25f, bool once = false, float phase = -1) => anim.Play(n, fade, once, 1, phase);
        public bool Has(string n) => anim.Has(n);
        public void SetSpeed(float s) => anim.SetSpeed(s);
        public void Update(float dt) => anim.Update(dt);
    }

    public class Person
    {
        public GameObject root; public Anim anim; public string variant;
        public Transform hand, handL, chest, spine, head;
        public void Play(string n, float fade = 0.25f, bool once = false) => anim.Play(n, fade, once);
        public bool Has(string n) => anim.Has(n);
        public void Update(float dt) => anim.Update(dt);
        public void Kick() => anim.Kick();
    }

    public class Gun
    {
        public GameObject root; public Transform muzzle, barrels;
        public void Spin(float a) { if (barrels) barrels.localRotation *= Quaternion.Euler(0, 0, a * Mathf.Rad2Deg); }
    }

    public static class Characters
    {
        static readonly Dictionary<string, string[]> BoneAliases = new Dictionary<string, string[]>
        {
            { "spine", new[] { "spine", "Spine", "spine.001" } },
            { "chest", new[] { "chest", "Chest", "spine.003" } },
            { "head", new[] { "head", "Head" } },
            { "handR", new[] { "hand.R", "handR", "hand_R", "Hand.R" } },
            { "handL", new[] { "hand.L", "handL", "hand_L", "Hand.L" } },
        };

        class Look { public string[] show; public Dictionary<string, Color> color = new Dictionary<string, Color>(); public string[] pick; }
        static Look L(string[] show, params (string, float, float, float)[] cols)
        {
            var l = new Look { show = show };
            foreach (var (n, r, g, b) in cols) l.color[n] = new Color(r, g, b);
            return l;
        }
        static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            { "townsman", L(new[] { "Body", "Hat_Bowler", "Duster" }) },
            { "drifter", L(new[] { "Body", "Hat_Wide", "Duster" }) },
            { "driver", L(new[] { "Body_Driver", "Coat_Driver", "Hat_Driver", "Moustache_Walrus", "Neckerchief_Driver", "Watch_Driver" }) },
            { "player", L(new[] { "Body_Man", "Coat_Guard", "Hat_Guard", "Moustache_Guard" }) },
            { "outlaw", L(new[] { "Body_Man", "Hat_Wide", "Bandana_Man", "Coat_Guard" }, ("Coat_Guard", 0.36f, 0.31f, 0.27f), ("Hat_Wide", 0.7f, 0.66f, 0.62f)) },
            { "sugarloaf", L(new[] { "Body_Man", "Hat_Sugarloaf", "Bandana_Man", "Jacket" }, ("Bandana_Man", 0.45f, 1.6f, 3.4f), ("Jacket", 0.92f, 0.76f, 0.55f)) },
            { "vaquero", L(new[] { "Body_Man", "Hat_Sombrero", "Serape", "Moustache_Vaquero" }, ("Body_Man", 0.92f, 0.86f, 0.8f)) },
            { "reb", L(new[] { "Body_Man", "Hat_Slouch", "Jacket", "Gauntlets" }, ("Hat_Slouch", 1.0f, 0.85f, 0.62f), ("Jacket", 0.8f, 0.82f, 0.88f)) },
            { "mountain", L(new[] { "Body_Man", "Coat_Buffalo", "Beard_Long", "Hat_Wide" }, ("Hat_Wide", 0.55f, 0.5f, 0.46f)) },
            { "pearl", L(new[] { "Body_Female", "Hat_Slouch" }, ("Hat_Slouch", 1.1f, 0.96f, 0.8f)) },
            { "bart", L(new[] { "Body_Man", "Coat_Guard", "Hat_Bowler", "Mask_Sack" }, ("Body_Man", 0.5f, 0.5f, 0.55f), ("Coat_Guard", 1.04f, 1.04f, 1.02f)) },
            { "lawman", L(new[] { "Body_Man", "Coat_Guard", "Hat_Lawman" }, ("Coat_Guard", 0.22f, 0.21f, 0.21f)) },
            { "hickok", L(new[] { "Body_Hickok", "Coat_Guard", "Hat_Hickok", "Colts_Hickok" }, ("Coat_Guard", 0.09f, 0.087f, 0.087f)) },
            { "bandit", L(new[] { "Body_Man", "Hat_Wide", "Bandana_Man", "Coat_Guard" }, ("Coat_Guard", 0.36f, 0.31f, 0.27f)) },
            { "bandit2", L(new[] { "Body_Man", "Hat_Bowler", "Bandana_Man", "Serape" }) },
            { "bandit3", L(new[] { "Body_Man", "Hat_Wide", "Serape" }) },
            { "gunman", L(new[] { "Body_Man", "Hat_Wide", "Coat_Guard", "Bandana_Man" }, ("Coat_Guard", 0.5f, 0.44f, 0.38f)) },
            { "woman_slate", L(new[] { "Body_Woman", "Dress", "Bonnet" }, ("Dress", 0.55f, 0.6f, 0.72f), ("Bonnet", 0.9f, 0.9f, 0.95f)) },
            { "woman_plum", L(new[] { "Body_Woman", "Dress", "Hat_Lady" }, ("Dress", 0.72f, 0.4f, 0.45f), ("Hat_Lady", 0.55f, 0.35f, 0.4f)) },
            { "woman_calico", L(new[] { "Body_Woman", "Dress", "Bonnet" }, ("Dress", 0.95f, 0.82f, 0.62f), ("Bonnet", 1.0f, 0.95f, 0.85f)) },
        };
        public static readonly string[] RiderLooks = { "outlaw", "sugarloaf", "vaquero", "reb", "mountain", "pearl", "outlaw", "reb" };
        public static readonly string[] RiderLooksMountain = { "mountain", "mountain", "outlaw", "reb", "sugarloaf", "pearl" };
        public static readonly string[] GunmanLooks = { "outlaw", "reb", "mountain", "sugarloaf", "pearl", "outlaw" };

        static readonly Dictionary<GameObject, string> modelOf = new Dictionary<GameObject, string>();
        /// which GLB a spawned character came from (for its Mecanim clips)
        public static string ModelOf(GameObject root) => root && modelOf.TryGetValue(root, out var m) ? m : null;

        static GameObject Spawn(GameObject prefab, string name)
        {
            var go = Object.Instantiate(prefab);
            var model = Assets.ModelName(prefab); if (model != null) modelOf[go] = model;
            go.name = name;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = true; }   // skinned bounds are unreliable
            }
            return go;
        }

        // ------------------------------------------------------------------- horse
        /// `breed` picks horse_<breed>.glb (the coach team), else the plain horse the riders use
        public static Horse CreateHorse(Color coat, bool saddle = true, bool harness = false, string breed = null)
        {
            var prefab = (breed != null ? Assets.Model("horse_" + breed) : null) ?? Assets.Model("horse");
            var h = new Horse();
            if (!prefab)
            {
                h.root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(h.root.GetComponent<Collider>());
                h.root.transform.localScale = new Vector3(0.8f, 1.2f, 2.2f);
                h.anim = new Anim(h.root);
                h.mount = new GameObject("Mount").transform; h.mount.SetParent(h.root.transform, false); h.mount.localPosition = new Vector3(0, 1.45f, 0);
                return h;
            }
            h.root = Spawn(prefab, "Horse");
            if (breed == null && coat != Color.white)
                foreach (var r in h.root.GetComponentsInChildren<Renderer>(true))
                {
                    var n = Assets.MeshName(r).ToLowerInvariant() + " " + r.sharedMaterial?.name?.ToLowerInvariant();
                    if (n.Contains("coat") || n.Contains("body") || n.Contains("horse")) Assets.Tint(r, coat);
                }
            var sd = Assets.Find(h.root.transform, "Saddle"); if (sd) sd.gameObject.SetActive(saddle);
            var hn = Assets.Find(h.root.transform, "Harness"); if (hn) hn.gameObject.SetActive(harness);
            h.mount = Assets.Find(h.root.transform, "Mount");
            if (!h.mount) { h.mount = new GameObject("Mount").transform; h.mount.SetParent(h.root.transform, false); h.mount.localPosition = new Vector3(0, 1.45f, 0); }
            h.anim = new Anim(h.root);
            h.Play(h.Has("Gallop") ? "Gallop" : "Idle", 0);
            return h;
        }

        // ------------------------------------------------------------------- human
        public static bool HasLook(string variant) => Looks.ContainsKey(variant);

        public static Person CreateRider(string variant, Color? tint = null)
        {
            var prefab = Assets.Model("rider");
            var p = new Person { variant = variant };
            if (!prefab)
            {
                p.root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(p.root.GetComponent<Collider>());
                p.anim = new Anim(p.root);
                p.hand = p.chest = p.head = p.root.transform;
                return p;
            }
            if (!Looks.TryGetValue(variant, out var look)) look = Looks["townsman"];
            p.root = Spawn(prefab, "Person " + variant);
            foreach (var r in p.root.GetComponentsInChildren<Renderer>(true))
            {
                var n = Assets.MeshName(r);
                bool show = System.Array.IndexOf(look.show, n) >= 0;
                r.enabled = show;
                if (!show) continue;
                Color mul = Color.white;
                if (look.color.TryGetValue(n, out var c)) mul *= c;
                if (tint.HasValue)
                {
                    var mn = (r.sharedMaterial ? r.sharedMaterial.name : "").ToLowerInvariant() + " " + n.ToLowerInvariant();
                    if (mn.Contains("coat") || mn.Contains("body")) mul *= tint.Value;
                }
                if (mul != Color.white) Assets.Tint(r, mul);
            }
            var t = p.root.transform;
            Transform B(string k) { foreach (var n in BoneAliases[k]) { var b = Assets.Find(t, n); if (b) return b; } return null; }
            p.spine = B("spine"); p.chest = B("chest"); p.head = B("head"); p.handL = B("handL");
            p.hand = B("handR") ?? t;
            p.anim = new Anim(p.root);
            return p;
        }

        // ----------------------------------------------------------------- weapons
        /// finish: RGB multiplier over the gun, so one revolver mesh passes for blued, nickel or brass
        public static Gun CreateWeapon(string name, Color? finish = null)
        {
            var w = Assets.Model("weapons");
            var src = w ? Assets.Find(w.transform, name) : null;
            var g = new Gun();
            if (src)
            {
                g.root = Object.Instantiate(src.gameObject);
                g.root.name = name;
                foreach (var r in g.root.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (finish.HasValue) Assets.Tint(r, finish.Value);
                }
                g.muzzle = Assets.Find(g.root.transform, "Muzzle_" + name) ?? g.root.transform;
                g.barrels = Assets.Find(g.root.transform, name + "_Barrels");
                return g;
            }
            if (name == "Gatling") return Gatling();
            g.root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.root.GetComponent<Collider>());
            float len = name == "Schofield" ? 0.3f : 0.9f;
            g.root.transform.localScale = new Vector3(0.04f, 0.06f, len);
            g.muzzle = new GameObject("Muzzle").transform; g.muzzle.SetParent(g.root.transform, false); g.muzzle.localPosition = new Vector3(0, 0, 0.5f);
            return g;
        }

        // Six-barrel Gatling built from primitives (no GLB), held at the grip like the other guns (+Z = muzzle)
        static Gun Gatling()
        {
            var g = new Gun { root = new GameObject("Gatling") };
            Material brass = Assets.Lit(MathX.Hex("#b08a3e"), 0.6f, 0.85f), steel = Assets.Lit(MathX.Hex("#2b2c30"), 0.55f, 0.8f), wood = Assets.Lit(MathX.Hex("#5a3219"), 0.3f);
            GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, bool alongZ)
            {
                var o = GameObject.CreatePrimitive(t);
                Object.Destroy(o.GetComponent<Collider>());
                o.transform.SetParent(parent, false);
                o.transform.localPosition = pos; o.transform.localScale = scale;
                if (alongZ) o.transform.localRotation = Quaternion.Euler(90, 0, 0);
                o.GetComponent<Renderer>().sharedMaterial = m;
                return o;
            }
            var r = g.root.transform;
            // (a Unity cylinder is 2 units tall along Y, radius 0.5)
            Part(PrimitiveType.Cylinder, r, new Vector3(0, 0.08f, 0.1f), new Vector3(0.18f, 0.15f, 0.18f), brass, true);
            var barrels = new GameObject("Gatling_Barrels").transform; barrels.SetParent(r, false); barrels.localPosition = new Vector3(0, 0.08f, 0.25f);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2;
                Part(PrimitiveType.Cylinder, barrels, new Vector3(Mathf.Cos(a) * 0.05f, Mathf.Sin(a) * 0.05f, 0.37f), new Vector3(0.028f, 0.375f, 0.028f), steel, true);
            }
            foreach (float z in new[] { 0.2f, 0.5f, 0.72f }) Part(PrimitiveType.Cylinder, barrels, new Vector3(0, 0, z), new Vector3(0.14f, 0.015f, 0.14f), brass, true);
            Part(PrimitiveType.Cube, r, new Vector3(0, 0.2f, 0.08f), new Vector3(0.05f, 0.14f, 0.08f), brass, false);
            Part(PrimitiveType.Cube, r, new Vector3(0.11f, 0.08f, 0.02f), new Vector3(0.02f, 0.02f, 0.12f), steel, false);
            Part(PrimitiveType.Cube, r, new Vector3(0, 0, -0.02f), new Vector3(0.04f, 0.12f, 0.06f), wood, false);
            g.barrels = barrels;
            g.muzzle = new GameObject("Muzzle_Gatling").transform; g.muzzle.SetParent(r, false); g.muzzle.localPosition = new Vector3(0, 0.08f, 1.0f);
            return g;
        }

        public static void Hold(Gun g, Transform hand)
        {
            g.root.transform.SetParent(hand, false);
            g.root.transform.localPosition = Vector3.zero; g.root.transform.localRotation = Quaternion.identity; g.root.transform.localScale = Vector3.one;
        }

        /// Turn `bone` (usually the chest) so the hand's +Z (the gun barrel) points along worldDir.
        /// Call after the animation has been applied (LateUpdate). weight < 1 aims partially.
        public static void AimBone(Transform bone, Transform hand, Vector3 worldDir, float weight = 1)
        {
            if (!bone || !hand || worldDir.sqrMagnitude < 1e-6f) return;
            var r = Quaternion.FromToRotation(hand.forward, worldDir.normalized);
            if (weight < 1) r = Quaternion.Slerp(Quaternion.identity, r, weight);
            bone.rotation = r * bone.rotation;
        }
    }
}
