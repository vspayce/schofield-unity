# Schofield — Unity

The native port of [Schofield](https://github.com/vspayce/Schofield), the stagecoach shooter, for
iPhone/iPad, Apple TV and the Mac. Phase 1 is **Dry Creek Plains**, playable end to end: the coach and
its six-horse team on the winding road, the Schofield and the messenger shotgun, scope, Dead Eye,
riders and ridge riflemen, the HUD, and touch, mouse/keyboard and controller input.

## Opening it

1. Unity Hub ▸ **Add ▸ Add project from disk** ▸ `~/Schofield-Unity`, with **Unity 6000.6.4f1** (the
   same editor as The Black Bird) and its iOS / tvOS / Mac build modules.
2. On first open, `ProjectSetup.cs` installs glTFast (and its meshopt decoder) for the web game's GLBs,
   plus URP, the Input System and MCP for Unity. Accept the Input System's prompt to enable the new
   input backend (the editor restarts).
3. Once it has compiled, the scene builds itself (`Assets/Schofield/Scenes/Main.unity`). Open it and
   press **Play**. (Menu **Schofield ▸ Build Game Scene** rebuilds it any time.)

## Menu: Schofield

| Item | What it does |
|---|---|
| Build Game Scene | URP asset + the one scene (camera + `Game`); everything else is built at runtime |
| Configure Player Settings | Landscape-only, linear colour, bundle id `com.vspayce.schofield` |
| Apple ▸ Build Mac App | `Builds/macOS/Schofield.app`, to play at once |
| Apple ▸ Build Xcode Project for Apple TV / iPhone and iPad | `Builds/tvOS`, `Builds/iOS` — then `Tools/apple/testflight.sh tvOS` (or `iOS`) archives and uploads to TestFlight |

## How it's put together

- The art is the web game's own: `Assets/Schofield/Resources/{Models,Textures,Audio,UI}` are copies of
  `Schofield/public/assets` (GLBs imported by glTFast, legacy Animation clips for code-driven blending).
- `Runtime/World` — `Route` (spline, road-carved height field, road distance field), `Terrain`
  (streamed tiles built on worker threads, `Shaders/Terrain.shader` splat), `Scatter` (GPU-instanced
  props and grass), `Sky`, `Towns`.
- `Runtime/Game` — `Coach`, `Player`, `Enemies` (riders, riflemen), `Combat`, `FX`, `Characters`, `Anim`.
- `Runtime/UI` — HUD and menus built in code (uGUI). `Runtime/Core` — input, sound, post, assets.
- The world uses the web game's numbers unchanged, so it is a mirror image of the browser version.

Next phases: the other routes, the railroad and train, wildlife and the buffalo herd, townsfolk, the
gunsmith, missions and hired guns, dynamite.
