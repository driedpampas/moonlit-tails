# Moonlit Tails – script audit (branch devtools-separation, 2026-11-09)

Scope: the 44 project-owned C# files (6,486 lines) under Unity/Assets, excluding package Samples and TextMesh Pro.
No project files were modified.

## 1. Scripts used by AI agents to build or check the scene: USELESS (3,013 lines, 46% of the code)

| Script | Lines | What it does | Why it's useless here |
|---|---|---|---|
| DevTools/DemoDirector.cs | 675 | Scripted camera + "invisible hand" that plays the game and pipes frames to ffmpeg | Records the demo video. Editor-only |
| DevTools/Editor/DrinkPlaySmokeTest.cs | 329 | Plays the scene headless and makes drinks by moving props | Agent test harness (`-batchmode -executeMethod`, exit codes) |
| DevTools/Editor/MoonlitCapture.cs | 226 | Renders the player's views to PNGs | An agent's "eyes" with no headset |
| DevTools/Editor/DemoVideoRecorder.cs | 86 | Menu/batch wrapper for DemoDirector | Same |
| DevTools/Editor/HeadlessRun.cs | 56 | Shared batch-mode workarounds | Exists only for the three tools above |
| Scripts/Editor/MoonlitSetup.cs | 612 | "Configure Project" + "Rebuild VR Scene": generates MoonlitBar_VR.unity from code | The scene-generator itself. Half of it is dead code: the fallback for `Environment/MoonlitBar.fbx` (ApplyBarMaterials, SetupBarObjects, BuildLights, ConfigureBarImport), and that file no longer exists |
| Scripts/Editor/BarSplitAssembler.cs | 360 | Rebuilt the bar from Blender split pieces into prefabs and saved the scene | One-time migration, already done. The prefabs exist |
| Scripts/Editor/BarPieceCheck.cs | 177 | Validator with `RunBatch` and exit code | Agent self-check. Copies the FixedProps/NoCollider tables from MoonlitSetup |
| Scripts/Editor/DrinkSetupCheck.cs | 56 | Lists which props DrinkGame matched by name | Only needed because of name-based runtime wiring (see 4.1) |
| Scripts/VRCharacters/Editor/VRCharacterSetup.cs | 274 | One-click character import and prefab builder | All 3 character prefabs already exist. Only worth keeping if many new characters on the same rig are coming |
| RoomBuilder/BuildBarRoom.cs (+ asmdef) | 146 | Builds a ProBuilder blockout in Assets/Scenes/BarRoom.unity | Obsolete blockout, not in Build Settings |
| Editor/InstallProBuilder.cs | 16 | Auto-installs ProBuilder on load | Does nothing: ProBuilder is already in manifest.json |

These go with them:
- `Assets/Scenes/BarRoom.unity`, `Assets/Materials/*.mat` (6 blockout materials). Then the `com.unity.probuilder` package: nothing else uses it.
- The "Hands", "Dev options" and most "World" fields of BarTuning (see section 2).
- `PlayModeXR.cs` (103 lines) is borderline. It isn't a scene builder: it lets Play mode run on the **Linux editor**, which has no OpenXR runtime. Keep it if anyone develops on Linux. Delete `SkipNextPlay()` once DevTools is gone.

Order dependency: DrinkSetupCheck, BarPieceCheck, BarSplitAssembler and HeadlessRun all use `MoonlitSetup.ScenePath/Root/BarPrefab/AddPropComponents`. Delete the whole group together, or the editor assembly won't compile.

## 2. Custom scripts that duplicate the stock XRI/OpenXR rig

The scene already uses the stock **"XR Origin Hands (XR Rig)"** from the XRI 3.7 Hands Interaction Demo. That rig has a NearFarInteractor and a **Poke Interactor** on each hand. OpenXR has HandTracking, MetaHandTrackingAim and **HandInteractionProfile** enabled for both Android and Standalone.

### HandGrab.cs (210 lines): redundant
- **What the stock setup already does:** the XRI Default Input Actions `Select` / `Select Value` already bind **`pinchTouched`/`pinchValue` and `graspFirm`/`graspValue`** for both `<HandInteraction>` and `<XRHandDevice>`. Grab-with-fist and grab-with-pinch both come built in.
- **Throwing:** every prop prefab already has `XRGrabInteractable` with `throwOnDetach=1`, `throwSmoothingDuration=0.25`, `throwVelocityScale=1.1`, `throwAngularVelocityScale=1` and `useDynamicAttach=1`.
- **How it fights the stock setup:**
  - It forces the select input to ManualValue.
  - It overrides `attachTransform`, which partly cancels dynamic attach.
  - It re-sets the cast origin and radius **every frame**.
  - It overwrites XRI's throw velocity in the next FixedUpdate, so the prop gets two throw passes. BarLayout writes `throwVelocityScale`, and HandGrab then throws that result away. CoasterSnap waits a FixedUpdate just to work around HandGrab.
- **What you lose without it:** custom curl thresholds with hysteresis, a palm-centred hold point, and "peak speed in the last 0.12 s" throws. These are feel tweaks, not features the game needs.
- **Verdict:** the game runs on the default rig. Remove the 2 HandGrab components from the hands, then playtest the grasp release in the headset once. If the release feels sticky, tune `ReleaseThresholdButtonReader` on the stock "Select Input" object, not with code.
- **Dead once it's removed:** 10 BarTuning fields (grabWithWholeHand, grabCurl, releaseCurl, allowPinchGrab, pinchDistance, pinchReleaseDistance, grabReach, palmGrabOffset, throwSpin, throwWindow).

### PokeButton.cs + HandTips (119 lines): redundant
- It reimplements fingertip buttons by reading XRHandSubsystem joints by hand, plus a mouse-raycast fallback.
- The stock rig already has a Poke Interactor. `XRSimpleInteractable` + `XRPokeFilter` (+ `XRPokeFollowAffordance` for the press animation) does this with real press depth, and the XR Interaction Simulator drives it with the mouse.
- **Verdict:** replace it with prefab buttons that use those components.

### CoasterSnap.cs (114 lines): replaceable
- "Release a glass near the coaster → it stands upright in the centre and stays until picked up" is exactly what `XRSocketInteractor` does.
- `ServeStation.FindGlass()` then becomes `socket.firstInteractableSelected`.

### BarLayout.cs (133) + BarTuning.cs (116): not needed for the build, and they block manual editing
- BarLayout is `[ExecuteAlways]` and calls `Apply()` on **every edit-mode Update**. A human who moves the XR Origin or a character in the Scene view, or scales a prop, gets snapped straight back. This is the biggest obstacle to "edited manually by a human".
- What's left in it is all inspector-native:
  - room and prop scale → set it on the transform/prefab
  - character placement → place the characters by hand
  - `physicsRate` → Project Settings > Time > Fixed Timestep
  - `allowDistanceGrab` → `NearFarInteractor.enableFarCasting` checkbox
  - `playerBodyPushesProps` → the CharacterController (locomotion is off anyway)
- **Hacks to drop:** re-seating characters on play frame 3 by measuring hips, and `BarTuning.Character(id)` adding entries to the asset as a side effect of a getter.

### Keep: no engine equivalent
- **Drinks/\*:** pure rules, tested by `Tests/DrinkRules.Tests`.
- **Gameplay:** DrinkContainer, IngredientSource, PourSpout, StirTool, IceScoop, Garnish, OrderBook, ServeStation, CustomerScripts, StatBars, FloatingLabel, GameVisuals.
- **PropRespawn:** XRI has no out-of-bounds respawn.
- **BlinkController, SpringBoneSystem/Collider:** Unity has no built-in spring bones. They're configured on all 3 prefabs (7–9 chains, 11 colliders each). The wind code is unused: windStrength is 0 and there's no WindZone.

## 3. Build risk
- **The XR Interaction Simulator prefab is placed in MoonlitBar_VR.unity, so it ships in the Quest APK.** Safer: remove it from the scene and enable it under Project Settings > XR Interaction Toolkit > "Use XR Interaction Simulator in scenes" + "Instantiate in Editor Only".
- **`GameVisuals.Flat` uses `Shader.Find("Universal Render Pipeline/Unlit")` at runtime.** It only works in builds because some material happens to reference that shader (the sky). It falls back to `Sprites/Default`. If both are missing, `new Material(null)` throws. Use a serialized material instead.

## 4. Unnecessarily complex code

### 4.1 The whole drink game wires itself at runtime by object name
- `DrinkGame` is attached to nothing. `[RuntimeInitializeOnLoadMethod]` → `GameObject.Find("MoonlitBar")` → name-prefix tables → `AddComponent` on ~40 props (`Init(...)` instead of serialized fields).
- That's why no gameplay component appears in any scene or prefab (0 GUID references for DrinkContainer, DrinkGame, PourSpout, etc.).
- **Consequences for human editing:**
  - Nothing is visible or tunable in the inspector.
  - Renaming a prop silently drops it from the game.
  - The mapping is misleading: `Bottle_Rum` → Whiskey, `Bottle_Gin` → Espresso, `Fridge_Milk` → Dark Caramel, `Bottle_MelonLiqueur` → Grapefruit Acid.
- **Simpler:**
  - Put IngredientSource (with an ingredient dropdown) and DrinkContainer (kind/variant/length) on the prop prefabs.
  - Place one DrinkGame object in the scene with explicit references.
  - This removes `Plan()`/`SetupPlan`, the name tables and DrinkSetupCheck.

### 4.2 Procedural UI that should be prefabs
- These are built from primitives in code:
  - the serve panel (ServeStation.Init)
  - the reset button (ResetButton)
  - two rules signs (`WriteMenuBoard`, `PutRulesOnBackWall`, plus about 40 lines of `TryFace` bounds math)
  - the StatBars
- As prefabs, a designer can move them, and the geometry math disappears.
- `PutRulesOnBackWall` also **silently hides the bar's logo pieces** at runtime, which is surprising for whoever edits the bar.

### 4.3 Smaller items
- Two separate hand-subsystem lookups (HandGrab.FindHands, HandTips). Both go away with the stock rig.
- PropRespawn polls the R key in all 43 props' Update, with a frame guard to dedupe. One listener is enough, or drop it: it's debug-only.
- Unused: `Ingredients.ById`, `PokeButton.SetCaption`, `BlinkController.ForceClosed/ReleaseForce`, SpringBoneSystem wind.
- `StirTool.center` is declared below the method that uses it.
- The bell has a PokeButton **and** a grab listener that both call `Serve()`. One gesture can serve twice (a double "wrong drink" message).
- IceScoop held full over a glass that already has ice flashes "Already has ice" **every frame**.

## 5. Missing null checks

| Location | Failure |
|---|---|
| HandGrab.cs:73 | `origin ? … : origin.transform`. If `origin` is null this throws every frame. Awake explicitly allows a null origin |
| BarLayout.cs:58-64 → 101 | LateUpdate calls `PlaceCharacter` without checking `tuning`, so it throws on the 3rd play frame if tuning is unassigned. Update guards it, LateUpdate doesn't |
| PropRespawn.cs:27, 53, 88 | `space = transform.parent`. A prop at scene root throws in Update every frame |
| OrderBook.cs:71 → OrderGenerator.cs:36 | If no glasses matched (renamed), `Generate` throws `ArgumentException` every frame for every customer. DrinkGame only logs warnings and keeps going |
| DrinkContainer.cs:238, 267-276, 346; ServeStation.cs:106, 117; IceScoop.cs:41 | `Label.Flash(...)` with no check. `Label` exists only if `Init()` ran. `Refresh()` checks it, the others don't. This breaks as soon as a human adds these components in the inspector |
| ServeStation.Update, CoasterSnap.InReach/OnCoaster, FloatingLabel.SetTitle/Flash, IceScoop.SetLoaded | Same pattern: fields set only by `Init/Create`, so a component placed by hand throws every frame |
| PourSpout.cs:80 | `target.Label?.Flash`. `?.` bypasses Unity's destroyed-object check, so a destroyed label throws MissingReferenceException. Use `if (target.Label)` |
| SpringBoneSystem.cs:163-170 | `n.bone` isn't checked. A bone destroyed or renamed after Build throws |
| GameVisuals.cs:25 | `Shader.Find(...) ?? Shader.Find(...)` → `new Material(null)` if both are missing (see 3) |

## 6. Memory leaks and GC pressure (matters on Quest)
- **Real leaks: only in DevTools** (moot if deleted).
  - `DemoDirector`: the RenderTexture is only `Release()`d, never `Destroy()`ed, and the `frame` Texture2D is never destroyed.
  - The two `Process` objects are never disposed.
  - `ffmpeg.WaitForExit()` runs before reading redirected stderr, so it can deadlock if ffmpeg writes a lot.
- **HandGrab:** `grabPoint` GameObject is created in Awake and never destroyed (one orphan per re-created HandGrab).
- **Allocations every frame:**
  - `ServeStation.Update` rebuilds 2–3 interpolated strings + `StatBars.Show` (an `int[]` + 3 strings), and `FinalStats` → `Layering.Resolve` allocates arrays. All unconditional, even when nothing changed. Update only when the glass or its state changes.
  - `IceScoop.Update` calls `GameVisuals.WorldBounds(iceWell)` → `GetComponentsInChildren<Renderer>()`, a new array every frame for a static object. Cache it once.
- **Churn:**
  - `DrinkContainer.RebuildLiquid` destroys and re-creates the liquid GameObjects on every pour/adjust/garnish. Pre-create up to 5 and scale/toggle them.
  - `ReceiveLayer` runs `Layering.Resolve` three times per pour.
- **Listeners never removed:**
  - CoasterSnap adds 2 lambdas per glass.
  - ServeStation adds a bell lambda.
  - Harmless while everything shares one scene's lifetime, but they can't be unsubscribed. Garnish handles this correctly in OnDestroy, so copy that pattern.
- **Fine as is:**
  - `GameVisuals` static material cache: bounded (integer tastes) and never freed. Acceptable, since domain reload is on (`EnterPlayModeOptions: 0`).
  - PropRespawn/DrinkContainer static `All` lists clean up in OnDisable.
  - FloatingLabel destroys itself when its target dies.

## Suggested order
1. Delete section 1 as a group (+ BarRoom scene, Assets/Materials, ProBuilder).
2. Remove HandGrab from the rig, playtest grasp and throw on Quest.
3. Remove `[ExecuteAlways]` from BarLayout (or remove BarLayout and place things by hand).
4. Move the simulator out of the scene.
5. Convert DrinkGame to serialized components on the prefabs, and PokeButton/CoasterSnap to XRI Poke/Socket.
6. Then fix the remaining null checks and per-frame allocations.
