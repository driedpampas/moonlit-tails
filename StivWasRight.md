# Moonlit Tails — Unity project audit

Read-only review of `Unity/Assets/MoonlitTails` and its scene/prefab dependencies. No files were modified.

**Scope:** 41 custom scripts under `MoonlitTails/Scripts` (6,282 lines), plus `RoomBuilder` and `Assets/Editor` tooling. Roughly 69 MB of imported `Samples`.

---

## 1. Executive verdict

- **The project is code-generated, not scene-authored.** `MoonlitBar_VR.unity` serializes only **three** custom components: `HandGrab` ×2 (scene lines 1123 and 1248) and `BarLayout` ×1 (line 2047). Every gameplay component is added at runtime by `DrinkGame.Start()`.
- **`HandGrab` is not required.** The XR rig already has a `NearFarInteractor` whose select input is wired to `ReleaseThresholdButtonReader` — XRI's pinch-select reader. Removing `HandGrab` restores default pinch grab and XRI throwing. The game remains fully playable.
- **`HandGrab` currently makes controller-only play worse.** It forces the interactor's select input to `ManualValue`; if the XR Hands subsystem is not running, selection is permanently disabled.
- **`PokeButton` duplicates built-in XRI poke.** The rig already contains `Poke Interactor`s; `XRSimpleInteractable` + `XRPokeFilter` does the same job with hover/filter support and no custom per-frame distance checks.
- **`BarLayout` + `BarTuning` are scene-authoring automation, not gameplay.** The asset is all defaults (1/0), and the only meaningful runtime effects are `Time.fixedDeltaTime = 1/90` and `throwVelocityScale = 1.1`. The rest is already baked into the saved scene. It also actively fights manual editing.
- **About 49% of custom C# is editor/demo tooling** (3,074 of 6,282 lines), and another ~530 lines are runtime scene construction. The actual game logic is a minority of the codebase.
- **Core gameplay should stay:** `Scripts/Drinks/*`, `DrinkContainer`, `PourSpout`, `IngredientSource`, `Garnish`, `StirTool`, `IceScoop`, `OrderBook`, `ServeStation`, `CoasterSnap`, `ResetButton`, `PropRespawn`, `SpringBoneSystem`, `SpringBoneCollider`, `BlinkController`.

---

## 2. What actually ships

| Item | Reality |
|---|---|
| Build scene | Only `Assets/MoonlitTails/Scenes/MoonlitBar_VR.unity` (`EditorBuildSettings.asset` line 9). |
| Scene-serialized custom components | `HandGrab` left, `HandGrab` right, `BarLayout`. |
| Prefab content | `MoonlitBar.prefab` (628 nested prop instances), 3 character prefabs, XR Origin Hands rig, XR Interaction Manager, XR Interaction Simulator, lighting. |
| Runtime construction | `DrinkGame.Boot()` finds `GameObject.Find("MoonlitBar")`, creates a `Drink Game` object, and `Start()` adds every gameplay component. |
| Prop prefabs | 43 prefabs contain `PropRespawn`; all have `space: {fileID: 0}`. `MoonlitBar.prefab` overrides `space` per instance. |
| Character prefabs | Use `SpringBoneSystem`, `SpringBoneCollider`, `BlinkController`. No engine equivalent; keep. |
| Tested rules | `Tests/DrinkRules.Tests` compiles `Scripts/Drinks/*.cs` directly. Keep those files. |

The important consequence: **opening the scene shows almost none of the game.** Pressing Play is what assembles it. That directly conflicts with the requirement that a human can edit the scene manually.

---

## 3. Redundant custom scripts

Editor scripts do not affect APK size; they cost maintenance time and can overwrite manual work.

### Runtime scripts

| Script (lines) | What it really does | Verdict |
|---|---|---|
| `HandGrab.cs` (210) | Custom whole-hand/pinch grab and hand-velocity throwing. | **Optional polish.** Default pinch grab works. Delete for robustness; keep only if whole-hand grab is a design pillar, and then fix it. |
| `BarLayout.cs` (133) | `[ExecuteAlways]` transform/scale applier plus global hand/physics settings. | **Scene automation.** Delete after baking `fixedDeltaTime` and `throwVelocityScale`. |
| `BarTuning.cs` (116) | Asset backing `BarLayout`/`HandGrab`. | Delete with them; keep only as a live-tuning tool. |
| `PokeButton.cs` + `HandTips` (119) | Custom fingertip button and mouse fallback. | **Redundant** vs `XRPokeInteractor` + `XRSimpleInteractable`. Delete after converting panels. |
| `DrinkGame.cs` (280) | Runtime bootstrap, name-based prop discovery, runtime rule signs/menu text. | **Split.** Keep game orchestration; delete `Boot`, `Plan`, `WriteMenuBoard`, `PutRulesOnBackWall`. |
| `DrinkContainer.cs` (455) | Drink state and procedural liquid visuals. | Keep; refactor `Refresh`/`RebuildLiquid`. |
| `GameVisuals.cs` (119) | Materials, primitives, text, bounds helpers. | Keep; fix unbounded cache and `DestroyImmediate`. |
| `ServeStation.cs` (121) | Serve spot and runtime-built panel. | Keep; remove runtime `AddComponent`s and add null guards. |
| `OrderBook.cs` (159) | Customers, orders, tickets, speech. | Keep; guard against empty glass list. |
| `PropRespawn.cs` (98) | Reset props to home. | Keep; fix null `space`. |
| `SpringBoneSystem`, `SpringBoneCollider`, `BlinkController` | Character secondary motion. | Keep. |
| `Scripts/Drinks/*` | Pure drink rules. | Keep; externally tested. |

### Editor/demo scripts

| Script (lines) | Role | Verdict |
|---|---|---|
| `MoonlitSetup.cs` (602) | Project config and full scene rebuild. | **Delete `BuildScene`**; optionally keep `ConfigureProject` as a one-time setup. |
| `BarSplitAssembler.cs` (360) | Blender piece assembly and prefab refresh. | Keep only if the art pipeline is active. |
| `DemoDirector.cs` (675) | Reflection-based demo choreography. | Delete if no demo videos. |
| `DemoVideoRecorder.cs` (99) | ffmpeg recorder. | Delete with `DemoDirector`. |
| `MoonlitCapture.cs` (239) | Screenshots and reports, including a `HandGrab` reflection report. | Delete if not used; it blocks deleting `HandGrab` cleanly. |
| `DrinkPlaySmokeTest.cs` (340) | Headless play test. | **Keep** (CI value). |
| `BarPieceCheck.cs` (177) | Validator tied to `BarLayout` names. | Keep only while name-based setup remains. |
| `DrinkSetupCheck.cs` (56) | Validator. | Same. |
| `LinuxPlayModeXR.cs` (90) | Linux editor XR bypass. | Keep if developing on Linux. |
| `VRCharacterSetup.cs` (274) | Character prefab builder. | Delete after prefabs are committed. |
| `RoomBuilder/BuildBarRoom.cs` (146) | Unused ProBuilder blockout. | **Delete.** |
| `Editor/InstallProBuilder.cs` (16) | Installs ProBuilder; now a no-op. | **Delete.** |
| `Assets/Scenes/BarRoom.unity` (716 KB) | Unused scene, not in build settings. | **Delete.** |
| `com.unity.probuilder` | Only used by `BuildBarRoom`. | Remove with it. |

---

## 4. `HandGrab` vs default OpenXR/XRI interaction

### What the default rig already provides

The scene uses the sample prefab `Samples/XR Interaction Toolkit/3.7.0/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab`:

- Two `NearFarInteractor`s. Their serialized select input is an object reference to `ReleaseThresholdButtonReader`.
- `ReleaseThresholdButtonReader` is documented as the pinch reader for hand interaction (`ReleaseThresholdButtonReader.cs:5–9`): it keeps pinch true until the fingers open past a release threshold.
- Two `Poke Interactor`s with `m_RequirePokeFilter: 1`.
- `PinchPointFollow`, `HandVisualizer`, `XRHandsConfigurationManager`, etc.

So the default setup already has **pinch grab, hover, poke, and XRI throwing**.

### What `HandGrab` changes

- `TakeOverInteractor()` (`HandGrab.cs:122–131`) switches `selectInput` to `ManualValue`, forces `StateChange`, sets `attachTransform = grabPoint`, and changes the caster origin/radius.
- `Update()` (`:58–108`) computes palm point, thumb/index pinch, and average finger curl, then calls `SetHolding`.
- `SetHolding()` (`:133–143`) writes `manualPerformed` / `manualValue` directly.
- `OnReleased()` + `FixedUpdate()` (`:110–151`) apply a custom hand-velocity throw.

No gameplay script reads `HandGrab.holding`, `grabPoint`, or `HandVelocity`; the dependency is only the two scene components and two editor scripts.

### Does the game run well without it?

**Functionally, yes.**

- Grab, pour, shake, stir, ice, garnish, serve, and reset all rely on `XRGrabInteractable` and the gameplay scripts, not on `HandGrab`.
- Default pinch select still grabs bottles and glasses. Pouring logic uses the prop's own up axis (`PourSpout.UpWorld`), so it is unaffected by the attach point.
- `CoasterSnap` waits a fixed update (`CoasterSnap.cs:55–61`), so it still snaps after XRI's LateUpdate throw.
- The XR Interaction Simulator and controllers work normally because the default select reader is restored.

**Trade-offs:**

- You lose whole-hand grab; large bottles require a pinch. That is less comfortable, but not broken.
- You lose the custom throw window/strength. Set `throwVelocityScale` on prop prefabs; it is currently only applied at runtime by `BarLayout` (`BarLayout.cs:119`).
- Objects are held at the pinch point instead of a palm point. No game logic depends on that.

**Bugs that removing it fixes:**

- `HandGrab.cs:73` throws an NRE when `origin` is null:
  `origin && origin.CameraFloorOffsetObject ? ... : origin.transform`.
- With no XR Hands subsystem (controllers, or a simulator that does not feed hand joints), `TakeOverInteractor()` still forces `ManualValue`, then `Update()` returns early (`:61–64`). Selection is dead. This is the strongest reason to remove it or guard it.
- It rewrites interactor configuration every frame (`:122–131`), making inspector tuning misleading.

**Verdict:** optional comfort feature, not a dependency. Remove it unless whole-hand grabbing is a core design requirement. If kept, fix the null case, only take over when `hands != null && hands.running`, and consider implementing it as an XRI interactor instead of hijacking `selectInput`.

---

## 5. The scene-building automation problem

This is the part that most directly violates the manual-editing requirement.

### It overwrites manual work

- `MoonlitSetup.BuildScene()` creates an **empty scene** (`MoonlitSetup.cs:224`), builds everything, saves over `MoonlitBar_VR.unity` (`:278`), and rewrites build settings (`:279`). Running “2. Rebuild VR Scene” destroys any manual edits.
- `BarSplitAssembler` opens the same scene (`:154`), deletes/recreates bar pieces (`:217`), and saves it (`:264`).
- `BarLayout` is `[ExecuteAlways]` (`BarLayout.cs:14`). In edit mode, `Update()` calls `Apply()` every editor frame (`:68–76`), which repositions the player and all characters, and rescales props from `baseScale`. A human moving a character or resizing a prop sees it snap back.

### It hides the game at runtime

- `DrinkGame.Boot()` runs after every scene load (`DrinkGame.cs:68–74`), finds `MoonlitBar` by name, and creates the game object.
- `Start()` (`:78–109`) adds `IngredientSource`, `DrinkContainer`, `Garnish`, `StirTool`, `IceScoop`, `OrderBook`, `ServeStation`, `ResetButton`, menu text, and the rules sign.
- `Plan()` (`:123–167`) discovers props by exact name prefixes (`PROP_`, `MK_`, `ENV_`) and only logs warnings when something is missing. The warning at `:137` literally says “isn't grabbable (rebuild the scene?)” — the code expects the automation, not manual authoring.
- `WriteMenuBoard()` (`:174–241`) and `PutRulesOnBackWall()` (`:251–278`) build TMP text, quads, and frames at runtime, including geometric face detection (`TryFace`, `:190–212`).

### It duplicates components if a human authors the scene

`DrinkGame` does not check for existing components:

- `DrinkGame.cs:85–91` adds `IngredientSource`, `DrinkContainer`, `Garnish`, `StirTool`, `IceScoop` unconditionally.
- `:99–102` adds `OrderBook` and `ServeStation` unconditionally.
- `DrinkContainer.Init()` adds `PourSpout` unconditionally (`DrinkContainer.cs:75`).
- `ServeStation.Init()` adds `CoasterSnap` (`ServeStation.cs:29`) and adds `PokeButton` to the bell (`:65`).

Result: manual placement gives two components, double pouring, double serving, or duplicated labels.

### The `Init` contract makes manual placement ineffective

Most components do nothing or NRE until `Init()` is called:

| Component | Without `Init()` |
|---|---|
| `OrderBook` | `customers` empty; no orders; `Serve()` says nobody is waiting. |
| `ServeStation` | `Update()` NREs on `title.text` (`:77`). |
| `IngredientSource` | `Ingredient` null; cannot pour; no `PourSpout`. |
| `DrinkContainer` | no `grab`, `home`, `liquidRoot`, or `Label`; most behavior is absent. |
| `Garnish` | no event subscription; `colliders` null if `AttachTo` is called. |
| `StirTool` | zeroed bounds; stirring silently fails. |
| `IceScoop` | `self` null; `Update()` returns. |
| `PourSpout` | `owner` null; `Update()` returns. |

This is the opposite of a manually editable project.

**Recommendation:** keep one real scene with serialized references, make setup idempotent (`if (!TryGetComponent(...)) AddComponent(...)` or require the component), and move construction into an editor-only validation tool that never saves the scene.

---

## 6. Missing null checks

| Location | Trigger | Impact | Fix |
|---|---|---|---|
| `HandGrab.cs:73` | `origin` is null. | NRE every frame. | Guard `origin`; fall back to `transform.parent`. |
| `PropRespawn.cs:27, 35–36, 53, 88–91` | `space` is null. All 43 prop prefabs serialize `space: 0`; only `MoonlitBar.prefab` overrides it. | NRE in `HomePosition`, `RememberHome`, `Update`, `SendHome` if a prop is instantiated outside the bar or placed manually. | `if (!space) space = transform.parent;` and guard every use; fail with a clear log. |
| `ServeStation.cs:77–78, 82–85, 109–116` | `Init()` not called, or called partially. | NRE on `title`, `info`, `bars`, `orders`. | Initialize in `Awake` from serialized refs, or guard and log. |
| `OrderBook.cs:71` | No grabbable `PROP_Glass_*` props found. | `OrderGenerator.Generate` throws `ArgumentException("No glasses")` (`OrderGenerator.cs:36`) every time an order is due. | Check `glasses.Count > 0` once and log/disable order generation. |
| `GameVisuals.cs:25–26` | Both URP Unlit and Sprites/Default shaders are stripped. | `new Material(null)` — invalid material/error. | Log and return a safe fallback; keep the shader in a `Resources` folder or reference it in a serialized field. |
| `DrinkGame.cs:85–91, 99–103` | A human adds gameplay components manually. | Duplicate `PourSpout`, `DrinkContainer`, `OrderBook`, `ServeStation`, etc. | `TryGetComponent` before `AddComponent`; preferably use serialized refs. |
| `DrinkContainer.cs:75` | `PourSpout` already exists. | Two spouts, double pours. | `if (!TryGetComponent<PourSpout>(out _)) AddComponent<PourSpout>()`. |
| `ServeStation.cs:29, 65` | `CoasterSnap`/`PokeButton` already exist. | Duplicate listeners/buttons. | Same idempotent pattern. |
| `PokeButton.cs:31, 41` | No `XROrigin` found. | Falls back to raw joint pose in the wrong space; buttons never trigger. | Log once and disable, or require an origin reference. |
| `Garnish.cs:52, 64` | `Init()` not called. | `colliders` null; NRE when attaching/detaching. | Initialize in `Awake`; guard the array. |
| `IceScoop.cs:55` | `SetLoaded(true)` before `self` is assigned. | NRE on `self.bounds`. | Guard `self`; only call after `Init`. |
| `DemoDirector.cs:250` | No `MoonlitBar` in the scene. | NRE in editor demo. | Guard; editor-only. |

---

## 7. Memory, GC, and performance

There is **no classic managed leak in the gameplay loop** — `DrinkContainer.All` and `PropRespawn.All` remove entries in `OnDisable`. The issues are an unbounded static cache and repeated allocation/churn.

| Location | Issue | Impact | Fix |
|---|---|---|---|
| `GameVisuals.cs:11, 22–31` | Static `Dictionary<Color, Material>` never cleared. `Flat()` creates a new `Material` for every exact color; `LiquidColor()` produces arbitrary drink colors. | Materials survive scene loads and accumulate for the whole process; each unique material breaks batching and costs memory. | Quantize liquid colors to a small palette, use `MaterialPropertyBlock`, or clear/destroy the cache on scene unload. |
| `DrinkContainer.cs:406–453` | `RebuildLiquid()` destroys and recreates every liquid GameObject on every `Refresh()`. | `Destroy` is deferred; two refreshes in one frame (`PourInto` `:337` plus `Clear()` `:362`) leave duplicate geometry visible until end of frame. Continuous GC churn while mixing. | Reuse pooled cylinders and update scale/color; add a dirty flag and rebuild once per frame. |
| `IceScoop.cs:31` | `GameVisuals.WorldBounds(iceWell)` every frame while not loaded; `WorldBounds` calls `GetComponentsInChildren<Renderer>()` (`GameVisuals.cs:94`). | New array allocation every frame. | Cache the ice well bounds in `Init()`; the well does not move. |
| `ServeStation.cs:77–85` + `StatBars.cs:76` | Per-frame interpolated strings and TMP text writes; `StatBars.Show` calls `GameVisuals.Signed()` for three values every frame. | Several small string allocations per frame, plus TMP mesh updates. | Update only when the glass/state changes; cache formatted strings. |
| `GameVisuals.cs:39` | `Object.DestroyImmediate(col)` at runtime. | Synchronous destruction can hitch when many primitives are created; unsafe during physics callbacks. | Create primitives without colliders, or use `Destroy` (deferred). |
| `PourSpout.cs:97` | `GameVisuals.Flat(owner.StreamColor)` every frame while pouring. | Arbitrary colors add material-cache entries and lookups per frame. | Cache the stream material per drink/color, or use a single material with a property block. |
| `DrinkContainer.cs:382` | `new List<string>` on every `Refresh()`. | Minor allocation on each pour/mix. | Use a reusable list or `StringBuilder`. |
| `StirTool.cs:39`, `ServeStation.cs:74`, `IceScoop.cs:37` | Full linear scans over `DrinkContainer.All` every frame. | Small N today (~30 containers), but avoidable. | Registry with spatial buckets if the count grows; otherwise fine. |
| `FloatingLabel.LateUpdate` | One per prop/glass, every frame: position, camera check, rotation, distance. | Dozens of labels cost CPU/TMP updates. | Keep `showWithin` culling; disable labels that are far away. |
| `HandGrab.cs:38, 157` | `List.RemoveAt(0)` on history. | O(n) shifts; n is tiny (0.3 s at 90 Hz). | Use a ring buffer if it ever matters. |

---

## 8. Overcomplex code and simplification opportunities

| Code | Why it is overcomplex | Simpler approach |
|---|---|---|
| `HandGrab.cs` (210) | Reimplements hand gesture recognition, grab-point selection, hysteresis, history, and throw velocity; hijacks XRI's select reader. | Default pinch; or a small XRI interactor that only adds whole-hand grab. |
| `PokeButton.cs` + `HandTips` (119) | Reimplements poke with distance spheres, manual joint-space conversion, mouse raycast, and material feedback. | `XRSimpleInteractable` + `BoxCollider` + `XRPokeFilter`; use `selectEntered`/`activated`. Keep a 5-line `Press()` helper for tests if needed. |
| `DrinkGame.Plan/TryFace/WriteMenuBoard/PutRulesOnBackWall` (~160 lines) | Name-prefix discovery, geometric face detection, and runtime TMP/quad construction for static UI. | Serialize references in the scene; author the menu board and rules sign as TMP objects. |
| `BarLayout.cs` + `BarTuning.cs` (249) | Live edit-mode transform application, version tracking, runtime re-seating, and global settings. | Bake transforms in the scene; put fixed timestep in Project Settings; set throw scale on prefabs. |
| `DemoDirector.cs` (675) | Reflection into private fields/methods, hard-coded object names, panel button indices. | Delete, or use Timeline/animation clips and public APIs. |
| `MoonlitSetup.cs` (602) | Rebuilds the entire project and scene from code, overwriting manual edits. | Keep a one-time `ConfigureProject`; commit the scene and prefabs. |
| `DrinkContainer.Refresh/RebuildLiquid` | Full visual rebuild on every state change, plus multiple refreshes per operation. | Dirty flag + pooled visual objects; update only what changed. |
| `ResetButton.ModelPlayerToServe` scaling | Hard-coded model distance used to scale runtime UI. | Bake button position/scale in the scene; use world-space prefabs. |
| `BarTuning.Character(id)` (`:88–93`) | Mutates the ScriptableObject at runtime, adding entries when missing. | Prefill character entries or return a default without mutating the asset. |
| Editor validators | `BarPieceCheck`, `DrinkSetupCheck`, `BarSplitAssembler`, `MoonlitSetup` duplicate naming/validation logic. | One shared validation utility; one non-destructive scene builder at most. |

---

## 9. Recommended removal order

Before deleting a runtime script, remove its **editor references**, or the project will not compile:

- `HandGrab` is referenced by `MoonlitSetup.cs:514` and `MoonlitCapture.cs:203–215` (reflection).
- `BarLayout` is referenced by `MoonlitSetup`, `BarSplitAssembler`, `BarPieceCheck`, `DrinkSetupCheck`, and `MoonlitCapture`.
- `BarTuning` is referenced by `MoonlitSetup`, `BarSplitAssembler`, `BarLayout`, `HandGrab`, and `MoonlitCapture`.
- `PokeButton` is referenced by `ServeStation`, `ResetButton`, `DemoDirector`, and `DrinkPlaySmokeTest`.

### Phase A — dead weight, no runtime effect

1. Delete `RoomBuilder/BuildBarRoom.cs`, `Assets/Editor/InstallProBuilder.cs`, `Assets/Scenes/BarRoom.unity`, and remove `com.unity.probuilder` from the manifest.
2. Delete `DemoDirector.cs`, `DemoVideoRecorder.cs`, and `MoonlitCapture.cs` if the demo-video pipeline is not needed.
3. Delete `VRCharacterSetup.cs` after the three character prefabs are committed.

### Phase B — scene builders

4. Remove `MoonlitSetup.BuildScene` (keep `ConfigureProject` only if it saves time). Do **not** let any tool call `EditorSceneManager.SaveScene` on the playable scene.
5. Remove `BarSplitAssembler` unless the Blender re-export pipeline is active.

### Phase C — `HandGrab`

6. Remove the two `HandGrab` components from `MoonlitBar_VR.unity`.
7. Remove `HandGrabReport()` from `MoonlitCapture`, or delete `MoonlitCapture`.
8. Remove the hand fields from `BarTuning` if `BarLayout` is also going.
9. Set `XRGrabInteractable.throwVelocityScale = 1.1` on prop prefabs if that feel is wanted.

### Phase D — `BarLayout` / `BarTuning`

10. Set **Project Settings → Time → Fixed Timestep** to `0.011111` (90 Hz) if the current `Time.fixedDeltaTime = 1/90` is wanted.
11. Bake `enableFarCasting = false` and `throwVelocityScale` into prefabs.
12. Remove the `BarLayout` component and `BarTuning.asset`, then remove their editor references.
13. If seated characters float/sink, bake the frame-3 hip correction into their prefab transforms.

### Phase E — `PokeButton`

14. Convert the serve panel, bell, and reset button to `XRSimpleInteractable` + `XRPokeFilter` + collider.
15. Keep a small `Press()` method that invokes the same UnityEvent/`selectEntered` path for tests.
16. Delete `PokeButton` and `HandTips`.

### Phase F — `DrinkGame` refactor (do not delete wholesale)

17. Add serialized fields for ingredient sources, glasses, garnishes, tools, tickets, serve spot, player, bell, and menu board.
18. Delete `Boot()`, `Plan()`, `WriteMenuBoard()`, and `PutRulesOnBackWall()`.
19. Make `Start()`/`Awake()` idempotent and fail fast with `Debug.LogError` when references are missing.
20. Keep `OrderBook`, `ServeStation`, and the rule checks.

### Phase G — correctness and GC

21. Fix the null checks in §6.
22. Fix the material cache, liquid rebuild, ice bounds, and per-frame TMP updates in §7.

### Keep

`Scripts/Drinks/*`, `DrinkContainer`, `PourSpout`, `IngredientSource`, `Garnish`, `StirTool`, `IceScoop`, `OrderBook`, `ServeStation`, `CoasterSnap`, `ResetButton`, `PropRespawn`, `FloatingLabel`, `StatBars`, `CustomerScripts`, `SpringBoneSystem`, `SpringBoneCollider`, `BlinkController`, and `DrinkPlaySmokeTest`.

---

## 10. Bottom line

**Without `HandGrab`: yes, the game runs.** Default XRI pinch grab, poke, and throwing cover every interaction the game needs. Removing it also fixes controller-only play and one real NRE.

**Without `DrinkGame`: no, the game does not run**, because `DrinkGame` is currently the game. The right move is not to delete it but to strip its scene-building half and replace name-based discovery with serialized references.

The biggest redundancy is not `HandGrab` — it is the roughly 3,000 lines of editor/demo tooling plus the runtime scene builder that make the project impossible to author by hand. Delete the ProBuilder leftovers and demo pipeline first, then remove `BarLayout`/`BarTuning`, then replace `PokeButton`, and finally make `DrinkGame` a normal scene component. After that, the scene will contain the actual game, and a human can edit it without a script rebuilding it underneath them.
