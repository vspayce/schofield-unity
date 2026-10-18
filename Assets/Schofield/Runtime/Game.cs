// SCHOFIELD — boot + game state machine (attract → ride → results). The whole world is built in code
// from the route definition, as in the web game's main.js. Drop this on a GameObject in an empty scene
// (Schofield ▸ Build Game Scene does that) and press Play.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Schofield
{
    public class Game : MonoBehaviour
    {
        const float CoachDamageMult = 0.7f;
        public Camera cam;
        public Transform world;
        public GameInput input;
        public Hud hud; public Menus menus; public Post post;
        public Route route; public Terrain terrain; public Scatter scatter; public Sky sky; public Towns towns;
        public GorgeBridge bridge; public RockArches arches; public Water water;
        public Escorts escorts; public MissionDef mission = Missions.All["mail"];
        public Dynamite dynamite; public Billboards boards; public Wildlife wildlife;
        public Railroad rail; public Crossing crossing;
        string[] hires = new string[0]; public int hireFee;
        public Coach coach; public Player player; public Enemies enemies; public Combat combat; public FX fx;
        public Routes.Diff diff;
        public int routeIndex;
        public string mode = "boot";
        public float time, timeScale = 1, targetScale = 1, shake, hitStopT;
        public bool over, arrived, paused, godMode;
        public int bounty;
        Person attractGuard;
        float whipCd;

        // development builds keep a log file the Mac can fetch from the device (Documents/diag.txt)
        static System.IO.StreamWriter diagFile;
        static void DiagLog(string msg, string stack, LogType type)
        {
            try { diagFile?.WriteLine($"{Time.realtimeSinceStartup:0.00} {type} {msg}" + (type == LogType.Exception ? "\n" + stack : "")); diagFile?.Flush(); } catch { }
        }

        void Awake()
        {
            if (Debug.isDebugBuild && diagFile == null)
            {
                try { diagFile = new System.IO.StreamWriter(System.IO.Path.Combine(Application.persistentDataPath, "diag.txt"), false); Application.logMessageReceived += DiagLog; }
                catch { }
            }
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            cam = Camera.main;
            if (!cam) { var go = new GameObject("Main Camera") { tag = "MainCamera" }; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 5000; cam.fieldOfView = 60;
            cam.clearFlags = CameraClearFlags.Skybox;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            if (!cam.GetComponent<AudioListener>()) cam.gameObject.AddComponent<AudioListener>();
            Sound.Create(cam.transform);
            input = new GameInput();
            hud = new Hud(input);
            menus = new Menus(this);
            post = new Post(transform);
        }

        void Start()
        {
            BuildWorld(Random.Range(0, 1), "attract");
            menus.Title();
            Sound.I.Loop("music_menu", 1, true);
        }

        // ------------------------------------------------------------- world
        void DisposeWorld()
        {
            if (route == null) return;
            player?.Dispose(); player = null;
            enemies?.Dispose(); coach?.Dispose(); fx?.Dispose(); scatter?.Dispose(); towns?.Dispose(); terrain?.Dispose(); sky?.Dispose();
            escorts?.Dispose(); escorts = null; crossing?.Dispose(); crossing = null; rail?.Dispose(); rail = null; dynamite?.Dispose(); dynamite = null; boards?.Dispose(); boards = null; wildlife?.Dispose(); wildlife = null;
            bridge?.Dispose(); bridge = null; arches?.Dispose(); arches = null; water?.Dispose(); water = null;
            if (attractGuard != null) { attractGuard.anim.Dispose(); attractGuard = null; }
            if (world) Destroy(world.gameObject);
            route = null;
        }

        // testing on a Mac: -shot <file.png> takes a picture of the title scene after a few seconds and quits
        float shotT; int shotStage, shotFrames;
        void DebugShot()
        {
            if (shotStage > 1) return;
            var args = System.Environment.GetCommandLineArgs();
            int k = System.Array.IndexOf(args, "-shot");
            if (k < 0 || k + 1 >= args.Length) { shotStage = 2; return; }
            shotT += Time.unscaledDeltaTime;
            if (shotStage == 0 && shotT > 9)
            {
                ScreenCapture.CaptureScreenshot(args[k + 1]); shotStage = 1;
                var mod = UnityEngine.EventSystems.EventSystem.current ? UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() : null;
                Debug.Log($"[shot] ui module={(mod ? "yes" : "no")} actions={(mod && mod.actionsAsset ? mod.actionsAsset.name : "NONE")} submit={(mod && mod.submit != null && mod.submit.action != null && mod.submit.action.enabled)} point={(mod && mod.point != null && mod.point.action != null && mod.point.action.enabled)} click={(mod && mod.leftClick != null && mod.leftClick.action != null && mod.leftClick.action.enabled)} selected={UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.name}");
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    var n = r.transform.root.name;
                    if (n == "World" && !r.transform.IsChildOf(world)) continue;
                    var m = r.sharedMaterial;
                    Debug.Log($"[shot] {r.transform.root.name}/{r.name} {r.GetType().Name} en={r.enabled} vis={r.isVisible} b={r.bounds.center}/{r.bounds.size} mat={(m ? m.name : "null")} sh={(m && m.shader ? m.shader.name + (m.shader.isSupported ? "" : " UNSUPPORTED") : "null")} q={(m ? m.renderQueue : 0)} cam={cam.transform.position}");
                }
            }
            else if (shotStage == 1 && shotFrames < 8) { shotFrames++; ShotProbe("update"); ScreenCapture.CaptureScreenshot(args[k + 1].Replace(".png", "_" + shotFrames + ".png")); }   // consecutive frames, to see flicker
            else if (shotStage == 1 && shotT > 11) { shotStage = 2; Application.Quit(); }
        }

        // development builds: where a tap lands, and what the menus see under it
        void UiDiag()
        {
            var ts = UnityEngine.InputSystem.Touchscreen.current; var ms = UnityEngine.InputSystem.Mouse.current;
            Vector2? at = null;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) at = ts.primaryTouch.position.ReadValue();
            else if (ms != null && ms.leftButton.wasPressedThisFrame) at = ms.position.ReadValue();
            var g = UnityEngine.InputSystem.Gamepad.current;
            if (g != null && g.buttonSouth.wasPressedThisFrame) Debug.Log($"[diag] pad A on {g.name}");
            if (at == null) return;
            var es = UnityEngine.EventSystems.EventSystem.current;
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            if (es) es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = at.Value }, hits);
            var mod = es ? es.currentInputModule : null;
            Debug.Log($"[diag] press at {at.Value} screen {Screen.width}x{Screen.height} module={(mod ? mod.GetType().Name : "none")} menus={menus.Open} hits={string.Join(", ", hits.ConvertAll(h => h.gameObject.name + "@" + h.module?.GetType().Name))}");
        }

        void ShotProbe(string when)
        {
            var h0 = coach.TeamRoot(0);
            var rends = coach.Root.GetComponentsInChildren<Renderer>();
            string first = rends.Length > 0 ? rends[0].bounds.center.ToString("F1") : "-";
            var hr = h0 ? h0.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            string bones = "";
            if (hr) { bones = $"skin={hr.name} rootBone={(hr.rootBone ? hr.rootBone.name : "-")} rb={(hr.rootBone ? (hr.rootBone.position - h0.position).ToString("F2") : "-")} b5={(hr.bones.Length > 5 && hr.bones[5] ? (hr.bones[5].position - h0.position).ToString("F2") : "-")} anim={(h0.GetComponentInChildren<Animation>() ? "legacy" + (h0.GetComponentInChildren<Animation>().enabled ? "" : "(off)") + " clips=" + h0.GetComponentInChildren<Animation>().GetClipCount() : "mecanim")} lossy={hr.rootBone?.lossyScale:F2} par={hr.rootBone?.parent?.localPosition:F2}"; }
            Debug.Log($"[probe] {rends[0].name} {bones} f{Time.frameCount} {when} s={coach.s:F2} pos={coach.pos:F1} root={coach.Root.transform.position:F1} rend0={first} horse0={(h0 ? h0.position.ToString("F1") : "-")} cam={cam.transform.position:F1}");
        }

        void BuildWorld(int i, string m)
        {
            DisposeWorld();
            routeIndex = i;
            var def = Routes.All[i];
            mission = Missions.All[Save.Mission];
            diff = Missions.Apply(Routes.DiffFor(i), mission);
            world = new GameObject("World").transform;
            route = new Route(def);
            sky = new Sky(route, world);
            post.SetRoute(def.light);
            // the railroad first: it grades the land along its line, which the terrain and scatter have to see
            rail = def.railroad != null ? new Railroad(route, world, def.railroad) : null;
            bool phone = Application.isMobilePlatform || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-phone") >= 0;
            terrain = new Terrain(route, world, phone ? 560 : 700);
            scatter = new Scatter(route, phone ? 0.8f : 1f, phone ? 0.6f : 1f);
            terrain.onTile = scatter.OnTile;
            terrain.work = scatter.Compute;
            towns = new Towns(route, world);
            if (rail != null) { towns.solids.AddRange(rail.solids); rail.onJolt = k => { shake += 0.16f * k; Sound.I.Play("hit_wood_1", 0.5f * k, 0.7f); }; }
            bridge = route.hasBridge ? new GorgeBridge(route, world) : null;
            arches = def.arches != null ? new RockArches(route, world, def.arches) : null;
            // the gorge river and the falls the road runs under; punching through the curtain rocks the coach
            water = def.gorge != null ? new Water(route, world) : null;
            if (water != null) water.onBurst = k => shake += 0.4f * k;
            boards = new Billboards(route, world, def.billboards);
            coach = new Coach(route, world);
            fx = new FX(route, world);
            combat = new Combat(this);
            enemies = new Enemies(this);
            wildlife = m == "ride" ? new Wildlife(this) : null;
            mode = m;
            over = arrived = false;
            bounty = 0; hud.SetBounty(0);
            coach.s = m == "attract" ? 20 : 8;
            coach.speed = m == "attract" ? 12 : 0;
            coach.Update(0.016f, default, false);
            if (m == "ride")
            {
                player = new Player(this, Save.Equipped("side"), Save.Equipped("long"), Save.Start == "long");
                coach.hp = Save.CoachHp;
                enemies.PlanFor(player, mission);
                escorts = new Escorts(this, hires);
                dynamite = new Dynamite(this);
                crossing = rail != null && rail.hasTrain ? new Crossing(this, rail, def.railroad) : null;
                hud.SetRoute(def);
            }
            else
            {
                attractGuard = Characters.CreateRider("player");
                attractGuard.root.transform.SetParent(coach.seatGuard ? coach.seatGuard : coach.body, false);
                attractGuard.root.transform.localRotation = Quaternion.Euler(0, -0.5f * Mathf.Rad2Deg, 0);
                var gun = Characters.CreateWeapon("CoachGun");
                Characters.Hold(gun, attractGuard.hand);
                attractGuard.Play(attractGuard.Has("SeatAim") ? "SeatAim" : "Ride", 0);
            }
            terrain.Prime(coach.pos.x, coach.pos.z);
            cam.transform.position = coach.pos + new Vector3(0, 4, -8);
            cam.transform.LookAt(coach.pos);
        }

        public void ToAttract()
        {
            paused = false; Sound.I.Pause(false);
            input.SetEnabled(false);
            hud.Show(false);
            Sound.I.StopAll(0.4f);
            SetTimeScale(1); timeScale = 1;
            BuildWorld(0, "attract");
            Sound.I.Loop("music_menu", 1, true);
        }

        /// hires: escort ids for this ride only (already paid for by the menu)
        public void StartRide(int i, string[] hireIds = null, int fee = 0)
        {
            hires = hireIds ?? new string[0]; hireFee = hireIds == null ? 0 : fee;
            paused = false; Sound.I.Pause(false);
            Sound.I.StopAll(0.3f);
            BuildWorld(i, "ride");
            hud.Show(true);
            input.SetEnabled(true);
            input.Lock();
            SetTimeScale(1); timeScale = 1;
            time = 0;
            var def = Routes.All[i];
            hud.Banner(def.name, $"{mission.name} — {def.from} to {def.to}", 3.5f);
            Sound.I.Play("whip_crack", 0.9f);
            Sound.I.Play("horse_neigh", 0.5f, 1, null, 0.3f);
            Sound.I.Loop("horse_gallop_loop", 0.5f);
            Sound.I.Loop("coach_rumble_loop", 0.45f);
            Sound.I.Loop("wind_loop", 0.25f * def.wind);
            Sound.I.Loop("music_ride_loop", 0.8f, true);
        }

        public void HitStop(float t) => hitStopT = Mathf.Max(hitStopT, t);

        public void SetTimeScale(float s)
        {
            targetScale = s;
            Sound.I.SetRate(s < 1 ? 0.6f : 1);
            post.deadeyeTarget = s < 1 && player != null && player.deadeyeOn ? 1 : 0;
        }

        public void AddBounty(int v, string label, bool hs) { bounty += v; hud.SetBounty(bounty); hud.Feed(label, hs); }
        public void DamageCoach(float amount) { if (!godMode) coach.hp -= amount * CoachDamageMult; }
        public void OnCoachDamage() { if (coach.hp <= 0 && !over) Fail("The coach was overrun."); }

        public void Pause()
        {
            if (mode != "ride" || over || paused) return;
            paused = true;
            input.SetEnabled(false);
            Sound.I.Pause(true);
            Time.timeScale = 0;
            menus.Pause();
        }

        public void Resume()
        {
            paused = false;
            Sound.I.Pause(false);
            input.SetEnabled(true);
            input.Lock();
        }

        void OnApplicationPause(bool p) { if (p) Pause(); }
        void OnApplicationFocus(bool f) { if (!f && !Application.isEditor) Pause(); }

        float laterT; System.Action later;
        void Later(float secs, System.Action fn) { laterT = secs; later = fn; }

        public void Fail(string reason)
        {
            if (over) return;
            over = true;
            input.SetEnabled(false);
            SetTimeScale(0.25f);
            Sound.I.StopLoop("music_ride_loop", 0.5f);
            Sound.I.Play("sting_death", 1, 1, null, 0, true);
            coach.stopping = true;
            int kept = bounty;
            Save.Earn(kept);
            Save.WearCoach(coach.hp);       // you limp home with whatever is left
            int fee = hireFee;
            Later(2.2f, () => { SetTimeScale(1); hud.Show(false); menus.Failed(reason, kept, fee); });
        }

        void Arrive()
        {
            if (over) return;
            over = arrived = true;
            coach.stopping = true;
            input.SetEnabled(false);
            if (player.deadeyeOn) player.EndDeadeye();
            SetTimeScale(1);
            enemies.ArriveAll();
            Sound.I.Play("bell_town", 0.8f);
            Sound.I.StopLoop("music_ride_loop", 1.2f);
            Sound.I.Play(Assets.Clip("music_results") ? "music_results" : "sting_victory", 1, 1, null, 0.6f, true);
            var def = Routes.All[routeIndex];
            hud.Banner(def.to, "You made it", 3);
            var st = player.stats;
            int accuracy = st.shots > 0 ? Mathf.RoundToInt(st.hits * 100f / st.shots) : 0;
            int coachPct = Mathf.Max(0, Mathf.RoundToInt(coach.hp));
            int stars = 1;
            if (coachPct >= 50 && accuracy >= 35) stars = 2;
            if (coachPct >= 75 && accuracy >= 50 && player.hp > 30) stars = 3;
            int reward = Mathf.RoundToInt(def.reward * mission.pay * (0.5f + coachPct / 200f));
            var r = new Results { kills = st.kills, headshots = st.headshots, accuracy = accuracy, coach = coachPct, bounty = bounty, reward = reward, total = bounty + reward, stars = stars,
                mission = mission.name, hireFee = hireFee, escortKills = escorts != null ? escorts.kills : 0, escortBounty = escorts != null ? escorts.bounty : 0 };
            Save.Record(def.id, stars, r.total);
            Save.Earn(r.total);
            Save.WearCoach(coach.hp);
            Later(3.5f, () => { hud.Show(false); menus.ShowResults(r); });
        }

        // ------------------------------------------------------------- loop
        void Update()
        {
            float rdt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            input.Poll(rdt);
            if (paused) { Time.timeScale = 0; input.Consume(); return; }
            if (later != null) { laterT -= rdt; if (laterT <= 0) { var f = later; later = null; f(); } }
            timeScale = MathX.Damp(timeScale, targetScale, 8, rdt);
            float k = timeScale;
            if (hitStopT > 0) { hitStopT -= rdt; k *= 0.12f; }
            Time.timeScale = Mathf.Max(0.01f, k);
            float dt = rdt * k;
            time += dt;
            DebugShot();
            if (Debug.isDebugBuild) UiDiag();
            // menus: a controller's A (or Enter) presses the highlighted button even if the event system missed it,
            // and the highlight is never lost
            if (menus.Open && UnityEngine.EventSystems.EventSystem.current)
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                var sel = es.currentSelectedGameObject;
                // only a controller or keyboard needs a highlighted button; on a phone, re-selecting after every tap
                // flashed the first button on and off
                bool navigates = UnityEngine.InputSystem.Gamepad.current != null || !input.touch;
                if ((sel == null || !sel.activeInHierarchy) && navigates) menus.SelectFirst();
                else if (GameInput.Confirm() && sel.TryGetComponent<UnityEngine.UI.Button>(out var btn) && btn.interactable) btn.onClick.Invoke();
            }
            if (route == null) return;
            var inp = input.Consume();
            if (inp.pause && mode == "ride") { Pause(); return; }
            coach.Update(dt, inp, mode == "ride" && !over);
            if (mode == "ride")
            {
                player.Update(dt, rdt, over ? default : inp);
                enemies.Update(dt);
                escorts?.Update(dt);
                wildlife?.Update(dt);
                crossing?.Update(dt);
                dynamite?.Update(dt);
                if (!over && coach.s >= route.len - 40) Arrive();
                float sp = coach.speed / 15;
                Sound.I.LoopRate("horse_gallop_loop", Mathf.Clamp(0.92f + (sp - 0.53f) * 0.21f, 0.92f, 1.13f));
                Sound.I.LoopVolume("horse_gallop_loop", Mathf.Min(0.6f, sp * 0.5f), 0.2f);
                Sound.I.LoopVolume("coach_rumble_loop", Mathf.Min(0.6f, sp * 0.45f), 0.2f);
                if (inp.whip && coach.stamina > 0.05f && whipCd <= 0) { Sound.I.Play("whip_crack", 0.7f); whipCd = 1.2f; }
                whipCd = Mathf.Max(0, whipCd - rdt);
            }
            else attractGuard?.Update(dt);
            rail?.Update(dt, mode == "ride" ? coach : null);
            towns.Update(dt, coach);
            fx.Trail(coach.pos - coach.fwd * 1.6f, coach.speed > 4 ? coach.speed * 0.9f : 0, dt, 1.0f);
            fx.Trail(coach.pos + coach.fwd * (coach.teamFront * 0.55f), coach.speed > 4 ? coach.speed * 0.5f : 0, dt, 0.8f);
            shake = MathX.Damp(shake, 0, 6, rdt);
            terrain.Update(coach.pos.x, coach.pos.z, 1);
        }

        void LateUpdate()
        {
            if (route == null) return;
            float rdt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            coach.LateUpdate();
            if (shotStage == 1 && shotFrames < 8) ShotProbe("late");
            if (mode == "ride" && player != null)
            {
                enemies.LateUpdate();
                escorts?.LateUpdate();
                rail?.LateUpdate();
                player.LateUpdate(paused ? 0 : rdt);
                hud.Update(this);
            }
            else AttractCam(rdt);
            scatter.Update(cam, coach.pos);
            sky.Update(cam);
            fx.Update(paused ? 0 : Time.deltaTime, cam);
            water?.Update(paused ? 0 : rdt, cam, coach.pos);
            post.scopeTarget = mode == "ride" && player != null && player.HasGlass ? player.scopeT : 0;
            post.Update(rdt, mode == "ride" && !paused ? (coach.speed - coach.cruise - 2) / 9 : 0);
        }

        // Title cinematic: wide landscape shots between close ones, the coach on the right of frame
        float at; int shot = -1; float side = 1; Vector3 plant;
        void AttractCam(float rdt)
        {
            var c = coach; var R = route;
            at += rdt;
            if (c.s > R.len - 200) c.s = 20;
            const float LEN = 9; const int SHOTS = 6;
            int k = Mathf.FloorToInt(at / LEN), sh = k % SHOTS;
            float t = at % LEN / LEN;
            var right = c.right; var fwd = c.fwd; var up = Vector3.up;
            var look = c.pos + new Vector3(0, 1.8f, 0);
            bool cut = shot != k;
            float fov = 60;
            if (cut) side = Random.value < 0.5f ? -1 : 1;
            Vector3 target;
            switch (sh)
            {
                case 0: target = c.pos + right * side * (55 - t * 10) + fwd * (-30 + t * 25) + up * (32 - t * 6); look += fwd * 40; fov = 50; break;
                case 1: target = c.pos + right * 7 + fwd * (6 - t * 12) + up * 1.4f; look += right * 2.6f; break;
                case 2:
                    if (cut) { plant = R.WorldAt(Mathf.Min(R.len - 10, c.s + 150), side * 14); plant.y += 2.5f + Random.value * 3; }
                    target = plant; look.y += 0.4f; fov = 26 - t * 6; break;
                case 3: target = c.pos + fwd * (-10 - t * 8) - right * 3 * side + up * (3 + t * 16); look += fwd * (12 + t * 40); break;
                case 4: { float a = side * (0.6f + t * 1.3f); target = c.pos + fwd * Mathf.Cos(a) * 26 + right * Mathf.Sin(a) * 26 + up * 9; look += fwd * 4; fov = 55; break; }
                default: target = c.pos + fwd * (14 + c.teamFront - t * 2) + right * (-4 + t * 3) + up * 1.2f; look += fwd * 3; break;
            }
            float gh = R.Height(target.x, target.z) + 0.8f;
            target.y = Mathf.Max(target.y, gh);
            if (cut) { shot = k; cam.transform.position = target; cam.fieldOfView = fov; }
            else { cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1 - Mathf.Exp(-6 * rdt)); cam.fieldOfView = MathX.Damp(cam.fieldOfView, fov, 4, rdt); }
            cam.transform.LookAt(look);
        }
    }
}
