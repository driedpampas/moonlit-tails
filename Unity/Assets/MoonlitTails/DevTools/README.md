# DevTools: automation, safe to ignore

These scripts run the game without a headset: for desk checks, the drink smoke test and the weekly demo video.
**You don't need them to work on the game.** Nothing in the game depends on them, and none of them runs unless you start it.

| Script | Menu: Tools > Moonlit Tails > Automation > | What it does |
|---|---|---|
| `Editor/MoonlitCapture.cs` | Capture Player Views | Plays the scene, saves screenshots from the player's eye and a physics report to `Unity/Captures/`. |
| `Editor/DrinkPlaySmokeTest.cs` | Play Smoke Test | Plays the scene and makes drinks by moving the props. Exit code 0 = passed. See `Docs/DRINK_GAME.md`. |
| `Editor/DemoVideoRecorder.cs` + `DemoDirector.cs` | Record Demo Video | Films the weekly progress video into `Unity/Captures/Demo/` (needs ffmpeg). |
| `Editor/SceneCheck.cs` | Check Scene | Checks the bar pieces (prefabs, materials, colliders, grabbing) and lists which props the drink game uses. Changes nothing. Exit code 0 = no problems. |
| `Editor/AndroidBuild.cs` | Build Android APK | Builds the Quest APK from the scenes in Build Settings into `Unity/Captures/Build/`. Optional `-apkPath`, `-jdk`, `-sdk`, `-ndk` on the command line (tool paths only go into your editor preferences). Exit code 0 = built. |
| `Editor/HeadlessRun.cs` | | What they share: opening the scene, batch-mode workarounds, exiting. |

What they promise:
- **They never save a scene or an asset.** Everything they move happens in Play mode and goes back when Play stops.
- **They don't throw away your work.** If another scene with unsaved changes is open, they ask you to save it first. If the bar scene is already open, they play it as it is.
- **The XR settings don't change.** XR startup is turned off for that one Play session in memory (`Scripts/Editor/PlayModeXR.cs`), so the Quest build and the XR asset in git are untouched.
- Their output (`Unity/Captures/`) is ignored by git. **The demo recorder overwrites `Captures/Demo/`**, so copy last week's files out first if you still need them.

## How they're kept apart from the game

| Assembly | Folder | Builds into the APK? |
|---|---|---|
| `MoonlitTails.DevTools` | `DevTools/` (DemoDirector) | No: `defineConstraints: UNITY_EDITOR`. It isn't Editor-only because Unity can't add a component from an Editor-only assembly |
| `MoonlitTails.DevTools.Editor` | `DevTools/Editor/` | No: Editor only |

The rules:
- **The game never references DevTools.** The game's assemblies (`MoonlitTails.Runtime`, `MoonlitTails.Drinks`, `VRCharacters.Runtime`, `MoonlitTails.Editor`) don't list DevTools as a reference, so they can't use it. DevTools may use the game's public API.
- **No reflection.** DevTools only calls public members. If a tool needs something, make it public on the game side, with a comment saying the tool uses it.
- **Never save** a scene or an asset.
- **Proof:** `Tools/check_devtools_isolation.sh` (repo root) deletes DevTools in a copy-on-write copy of the project and compiles it. Exit code 0 means the game builds without DevTools.
