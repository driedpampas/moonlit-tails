# DevTools: automation, safe to ignore

These scripts run the game without a headset: for desk checks, the drink smoke test and the weekly demo video.
**You don't need them to work on the game.** Nothing in the game depends on them, and none of them runs unless you start it.

| Script | Menu: Tools > Moonlit Tails > Automation > | What it does |
|---|---|---|
| `Editor/MoonlitCapture.cs` | Capture Player Views | Plays the scene, saves screenshots from the player's eye and a physics report to `Unity/Captures/`. |
| `Editor/DrinkPlaySmokeTest.cs` | Play Smoke Test | Plays the scene and makes drinks by moving the props. Exit code 0 = passed. See `Docs/DRINK_GAME.md`. |
| `Editor/DemoVideoRecorder.cs` + `DemoDirector.cs` | Record Demo Video | Films the weekly progress video into `Unity/Captures/Demo/` (needs ffmpeg). |
| `Editor/HeadlessRun.cs` | | What the three share: opening the scene, batch-mode workarounds, exiting. |

What they promise:
- **They never save a scene or an asset.** Everything they move happens in Play mode and goes back when Play stops.
- **They don't throw away your work.** If another scene with unsaved changes is open, they ask you to save it first. If the bar scene is already open, they play it as it is.
- **The XR settings don't change.** XR startup is turned off for that one Play session in memory (`Scripts/Editor/PlayModeXR.cs`), so the Quest build and the XR asset in git are untouched.
- Their output (`Unity/Captures/`) is ignored by git.

`DemoDirector.cs` sits outside `Editor/` only because Unity can't add a component from an Editor folder. It is wrapped in `#if UNITY_EDITOR`, so it's never in a build.
