# Moonlit Tails: fix plan (v7.1)

Final independent review: **9.0/10**, no blocking defects. v7.1 also fixes the reviewer's non-blocking items:
- `StatBarsView.root` / `IsBuilt`
- `IsGlass` filter on glass variants
- the Core.Runtime ref
- the full list of expected SceneCheck warnings
- the 4.3 duplicate removed
- `HandGrabReport` call site, usings, smoke-test ordering

**Changes since v6 (review: 8.8/10):**
- DrinkGame's ServeStation property is now `Station`, and the moved order check is `TryServe` (no CS0102).
- `Unity.XR.CoreUtils` added to the DevTools.Editor refs.
- MoonlitCapture `:170` returns a Rigidbody; every `layout.*` replaced in 4.1.
- Reset handle wording; the `Tickets.Length` bounds check.
- Expected SceneCheck warnings between 4.1 and 4.3.

**Changes since v5 (review: 8.5/10):**
- DrinkGame name constants renamed in 4.1 (no CS0102 clash with the new properties).
- SceneCheck stops using `Plan`/`GlassLengths` in 4.1.
- `ServeStation.Bell` and `DrinkGame.ResetButtonObject` added for the demo shots.
- CustomerScripts→CustomerProfile fallback spelled out, and the 3 assets made by hand in 4.3.
- `internal set` on SeatState.
- FloatingLabel, the `Greet` call site, the `Find` helpers and MoonlitCapture leftovers listed.
- DevTools runtime asmdef refs; Phase 6 checks in `Start`.

**Changes since v4 (review: 8/10):**
- DevTools gets a writable seat API (`SetOrder/Say/ShowTicket` by seat index, stable public `SeatState` instances, `ServedCount`, `MenuBoardText`), and the change list covers every line.
- `MoonlitSetup.Root` (`BarPieceCheck:79`) is re-homed in Phase 1.
- Every cross-object setup runs in `Init`/`Start`, never `Awake` (IceScoop bounds lazy, ServeStation `started` guard).
- Liquid rebuild moved into one DrinkGame `LateUpdate`.
- §5.1 item 6 aligned with Phase 0 step 6; TMP note updated (upgrade already in `9dbb157`).

**Changes since v3 (review: 7.5/10):**
- Idempotent setup contract and `IngredientId.None`, so the 4.1 transition never runs setup on default data.
- `protected virtual` Unity messages on the `Pourer` base.
- Controller Poke Interactors overridden to the Poke layer.
- Complete, line-referenced DevTools change list, plus a public `SeatView`.
- Per-instance callback counts (62 PropRespawn instances, net ≈ −148).
- DrinkGame is never disabled, so the respawn tick survives missing refs.
- Batch-run asset changes fixed at their cause (Phase 0, step 6).
- No duplicate ice-well ref; corrected registry and asmdef notes.

Inputs: [`SCRIPT_AUDIT.md`](SCRIPT_AUDIT.md) (Claude), a second audit written with DeepSeek (not in the repo), and a colleague's note: *"why is coaster snapping a separate script instead of a function on the glass or the coaster? Too many scripts that could be functions."*

Repo: branch `devtools-separation` @ b9bb04f, Unity 6000.3.25f1 (`~/Unity/Hub/Editor/6000.3.25f1`), XRI 3.7.0, XR Hands 1.10.0, OpenXR 1.19.0.

**Changes since v2:**
- **Consolidation rule (§2.1):** scripts that are really functions are folded into their owner:
  - CoasterSnap → ServeStation
  - PourSpout → a `Pourer` base class
  - OrderBook + ResetButton + the respawn tick → DrinkGame
  - StatBars → a plain serializable class
  - the two validators → one
  - no new `Customer` component
- Cross-object hookups are serialized refs subscribed in code. UnityEvents are used only inside one prefab.
- The `StatBars.prefab` and `ResetButton.prefab` assets are dropped: they're unique objects.

**Changes since v1:** phase order fixed so every phase compiles and plays; strangler transition in Phase 4; simulator stripped with `EditorOnly`; every asset-building tool deleted; Editor work by hand only; Poke-only interaction layer; pinned enum values; isolation test in a worktree; corrected grep, counts and profiling method; build side-effects covered.

## 0. Goal and definition of done

**Goal:** the shipped game is a normal Unity project that a human edits in the Editor:
- Every gameplay object is visible and configured in the scene or a prefab.
- No committed script builds or rewrites scenes or prefabs.
- Each MonoBehaviour exists for a reason (§2.1).
- Headless tools (smoke test, capture, demo video, APK build) stay, in their own assemblies that the game never references and that can be deleted without breaking the game.

| # | Check | How it's verified |
|---|---|---|
| D1 | No runtime code adds gameplay components, finds objects by name, or boots itself | The `git grep` in §5.1, item 3 prints nothing |
| D2 | Opening `MoonlitBar_VR.unity` in edit mode shows the serve panel, reset button, rules sign, chalkboard text, and a gameplay component on every gameplay prop | Human inspection; `Check Scene` (one read-only DevTools validator) reports 0 problems |
| D3 | Objects moved by hand stay put | No `[ExecuteAlways]` in `Scripts/`; drag test on a character, the XR Origin and a prop |
| D4 | The game compiles and plays with `DevTools/` deleted | `Tools/check_devtools_isolation.sh` (git worktree, §5.1) |
| D5 | Drink rules correct | `dotnet run --project Tests/DrinkRules.Tests`: all pass, including the new tests (pinned IngredientId values; `TryGenerate` with no glasses) |
| D6 | Headless end-to-end flow | `DrinkPlaySmokeTest.RunBatch` exit 0 |
| D7 | Works on Quest | Headset checklist §5.3, signed off by a human |
| D8 | Our scripts allocate nothing per frame in steady state | Development APK on Quest + Profiler, or the Editor Profiler hierarchy filtered to our scripts' samples (excludes Editor/simulator noise). `GC.Alloc` = 0 B in our samples, idle and with a glass on the coaster. Before/after numbers in the PR |
| D9 | APK has no simulator and no DevTools code | Build with `DevTools/Editor/AndroidBuild.cs`. The APK's `ScriptingAssemblies.json` lists no `MoonlitTails.DevTools*`. `adb logcat` shows no `XRInteractionSimulator` |
| D10 | Docs match reality | Each phase updates its docs. A final grep finds no "Rebuild VR Scene", `BarTuning`, `HandGrab`, `PokeButton`, `CoasterSnap`, `PourSpout`, "Assemble Split Bar" outside `Docs/AI_Conversation_Log` |
| D11 | Fewer scripts and per-frame callbacks | Runtime gameplay scripts go from 20 files to 12 (§2.1). Unity callbacks per frame + physics step in the bar drop by about 150 net (§2.1 counts). Counted in the PR as callback methods × **instances**, not prefabs |

## 1. Where the reviews disagree (verified against the repo)

| Claim | Verdict | Evidence |
|---|---|---|
| DeepSeek: default rig grabs with pinch only | **Wrong** | The rig's two "Select Input" `ReleaseThresholdButtonReader`s (`XR Origin Hands (XR Rig).prefab:759-760, 1159-1160`) read `XRI Left/Right Interaction/Select` + `Select Value` (fileIDs resolved in the imported artifact). Those bind `pinchTouched`/`pinchValue` **and** `graspFirm`/`graspValue` on `<HandInteraction>` and `<XRHandDevice>`. HandInteractionProfile is on for Android + Standalone. **Caveat:** the reader fires at value ≥ 1.0 and holds while > 0.9 (`ReleaseThresholdButtonReader.cs:91`). If Quest's `graspValue` doesn't reach 1.0, lower the thresholds (Phase 3) |
| DeepSeek: throw scale only applied at runtime | **Wrong** | All 43 grabbable prop prefabs serialize `m_ThrowVelocityScale: 1.1`, `m_ThrowSmoothingDuration: 0.25`, `m_ThrowOnDetach: 1`, `m_UseDynamicAttach: 1` |
| DeepSeek: HandGrab breaks controller-only play | **Partly** | Controllers have their own interactors. True part: the hand interactor is forced to `ManualValue`, so hand select is dead when the hand subsystem isn't running |
| DeepSeek: BarLayout only affects timestep and throw scale | **Incomplete** | It also re-seats seated characters on play frame 3, sets `CharacterController.detectCollisions = false` (the CharacterController is in the nested Starter `XR Origin (XR Rig)`), and is the only source of the customer list (`DrinkGame.cs:95-96`). `enableFarCasting` is already 0 in the scene (`MoonlitBar_VR.unity:962,1022,1030,1042`). Fixed timestep is 0.02 s; BarLayout forces 1/90 at runtime |
| DeepSeek: `MoonlitBar.prefab` has 628 nested instances | **Wrong** | 200 `PrefabInstance` entries, 94 GameObjects |
| DeepSeek: material cache unbounded | **Overstated, fix anyway** | Finite (integer tastes) but never freed, and built on `Shader.Find` |
| Claude: delete DemoDirector/Capture/SmokeTest | **Superseded** | The user allows keeping headless tools, separated; the weekly video depends on them |
| Colleague: separate scripts "add headers to memory for nothing" | **Right about the scripts, the reason is different** | Each component costs well under 1 KB (native object + managed wrapper). Removing about 100 instances saves under 100 KB, which is irrelevant on Quest. What it does fix: <ul><li>hidden dependencies (ServeStation silently adds CoasterSnap; DrinkContainer and IngredientSource add PourSpout)</li><li>about 150 per-frame/physics callbacks</li><li>files a reader must open to follow one feature</li></ul> |

## 2. Target structure

```
Unity/Assets/MoonlitTails/
  Scripts/
    Drinks/                MoonlitTails.Drinks.asmdef     (exists; pure C#; Tests/DrinkRules.Tests)
    Runtime/               MoonlitTails.Runtime.asmdef    (new) refs: MoonlitTails.Drinks, Unity.XR.Interaction.Toolkit,
                                                           Unity.XR.CoreUtils, Unity.InputSystem, Unity.TextMeshPro
                                                           (+ Unity.XR.Hands until PokeButton/HandTips go in 4.5)
    VRCharacters/Runtime/  VRCharacters.Runtime.asmdef    (new) SpringBoneSystem, SpringBoneCollider, BlinkController
    Editor/                MoonlitTails.Editor.asmdef     (new, Editor only) PlayModeXR.cs; refs Unity.XR.Management(.Editor)
  Data/Customers/          Kintsu.asset, Character02.asset, Character03.asset   (CustomerProfile, Phase 4)
  Prefabs/UI/              ServePanel.prefab, RulesSign.prefab   (Phase 4, built by hand)
  DevTools/                MoonlitTails.DevTools.asmdef   (all platforms, defineConstraints ["UNITY_EDITOR"]) → DemoDirector
    Editor/                MoonlitTails.DevTools.Editor.asmdef (Editor only) → MoonlitCapture, DrinkPlaySmokeTest,
                             DemoVideoRecorder, HeadlessRun, AndroidBuild, SceneCheck (read-only)
Tools/check_devtools_isolation.sh   (repo root, outside Assets)
```

**Rules:**
- Asmdef references go `DevTools → Runtime/Editor/Drinks` only.
- DevTools uses public API only (no reflection, no name lookups) and never saves a scene or an asset.
- **UnityEvents only inside one prefab.** Any hookup between separate objects is a serialized reference, subscribed in code in `OnEnable`/`OnDisable`.

Moving scripts under asmdefs keeps scene and prefab references (they use the `.meta` GUID; no `[SerializeReference]` in the project). Expected diff noise: about 86 `m_EditorClassIdentifier: Assembly-CSharp::…` strings under Assets change on the next save of each file. Some are in files this plan deletes.

`DevTools/Editor/AndroidBuild.cs` is **untracked work in progress by someone else** (created 2026-11-09 14:55). The plan doesn't edit it. It joins `MoonlitTails.DevTools.Editor` when its owner commits it, and Phase 8 uses it.

### 2.1 Consolidation: when something is a script and when it's a function

**Rule:** a MonoBehaviour exists only if it is
- (a) data a human sets on one specific object in the Inspector, or
- (b) behaviour that needs that object's own Unity callbacks (physics, select events, its own Update state).

Anything else is a method on the component that owns the responsibility, a plain `[Serializable]` class held by that component, or a static helper.

| Today | v3 | Why |
|---|---|---|
| `CoasterSnap` (separate component, added at runtime by ServeStation) | **Methods on `ServeStation`** (`TrySnap`, `Release`, `InReach`, `OnCoaster`) + one `FixedUpdate` there. Phase 5's optional socket replaces those methods | The colleague's case. The coaster is the owner; one instance instead of a second component. A method on the glass would run 15× for one coaster |
| `PourSpout` + `IPourer` (a second component on 37 props) | **`abstract class Pourer : MonoBehaviour`**; `IngredientSource : Pourer`, `DrinkContainer : Pourer`. The pour logic lives in `Pourer.Update`; DrinkContainer's shake check goes in an override | Removes 37 components and the interface, and DrinkContainer's second Update (−17 callbacks) |
| `OrderBook` (separate component) + `DrinkGame` (bootstrap) | **One `DrinkGame`** scene component: customers and orders, the reset handling, the respawn tick | One owner for "the night at the bar". DrinkGame's own bootstrap code is deleted |
| New `Customer` component (v2) | **Dropped.** `[Serializable] class Seat { CustomerProfile profile; Transform body; Transform ticketSpot; }` list on DrinkGame | It was data only. That also removes the `OrderBook.Customer` name clash. Runtime state goes in `DrinkGame.SeatState` (public read-only properties; mutated only through DrinkGame methods, see 4.1) |
| `ResetButton` (static builder) + `ResetAllProps` (v2) | **Deleted.** DrinkGame has `[SerializeField] XRBaseInteractable resetButton`, subscribed in code, and `public void ResetAllProps()`. It also owns the single keyboard-R listener | No script for a button |
| `PropRespawn` Update + FixedUpdate on **62 instances** (43 prefabs) | `PropRespawn` stays as **data** (home pose, editable) + `SendHome`/`ResetToHome` methods, with **no Unity callbacks** except OnEnable/OnDisable registration. DrinkGame's `Update` checks `PropRespawn.All` once per frame for props that left the bar, and its `FixedUpdate` runs the pending homes. The tick never depends on DrinkGame's other references (§3 Phase 6) | 124 callbacks → 1 extra (FixedUpdate; the Update already exists). Per-prop data stays visible |
| `StatBars` (MonoBehaviour, about 21 instances under labels and the panel) | **`[Serializable] class StatBarsView`** (fills, values, caption, `Show(Taste)`) held by ServeStation (authored inside ServePanel.prefab) and FloatingLabel (built by `GameVisuals.BuildStatBars`) | No Unity callbacks, so it doesn't need to be a component |
| `PokeButton` + `HandTips` | Deleted. XRI `XRSimpleInteractable` + `XRPokeFilter` | Stock component |
| `CustomerScripts` (code text) | `CustomerProfile` ScriptableObject assets | Writers edit the text in the Inspector |
| `BarPieceCheck` + `DrinkSetupCheck` (DevTools) | **One `SceneCheck`** with two sections | Same job (read-only scene validation), and one batch entry point |
| `HandGrab`, `BarLayout`, `BarTuning`, `MoonlitSetup`, `BarSplitAssembler`, `VRCharacterSetup`, `BuildBarRoom`, `InstallProBuilder` | Deleted (Phases 1, 3, 4) | §8 |

**Kept as components, on purpose:**
- **`Garnish`:** 18 props, each with its own select event and attached-glass state.
- **`StirTool`, `IceScoop`:** one per tool. Moving them onto the vessels/glasses would run the check on 2–15 objects instead of 1.
- **`FloatingLabel`:** its own billboarding GameObject.
- **`BlinkController`:** per character.
- **`SpringBoneSystem`.**
- **`SpringBoneCollider`:** a collider is a position on a specific bone that an artist places with the Scene-view gizmo, the standard spring-bone pattern (VRM, Dynamic Bone). Folding the 33 colliders into a list loses that gizmo editing and means re-entering 33 entries by hand. Decision Q8.

**Result:**
- Runtime gameplay scripts go from 20 to 12: DrinkGame, Pourer, IngredientSource, DrinkContainer, Garnish, StirTool, IceScoop, ServeStation, FloatingLabel, PropRespawn, GameVisuals (static), CustomerProfile (asset type). `StatBarsView` lives in `FloatingLabel.cs` and `SeatState` inside `DrinkGame.cs`, so no extra files.
- Per-frame/physics callbacks, counted per instance:
  - removed: PropRespawn −124 (62 × 2), DrinkContainer −17 (15 glasses + 2 vessels: its own Update merges into Pourer's), PokeButton −9, HandGrab −4, BarLayout −2, CoasterSnap −1, OrderBook's Update becoming DrinkGame's ±0
  - added: DrinkGame FixedUpdate +1, DrinkGame LateUpdate for the liquid rebuild (Phase 7) +1, stock `XRPokeFollowAffordance` on 8 buttons ≈ +8
  - net ≈ **−147**

## 3. Phases

Each phase is one PR into `main`. Every commit compiles, and at the end of every phase the game is fully playable and the phase's checks pass.

**Owners:**
- **Code** = `.cs`/`.asmdef`/docs, by Claude.
- **Editor** = Unity Editor work, **by a human, by hand**. No migration or helper scripts.

### Phase 0: preparation (Code + Editor, ~1 h)

1. Announce a **scene freeze** on `MoonlitBar_VR.unity` and `MoonlitBar.prefab` for Phases 3–5. Merge `main` into the branch.
2. **Baseline in `Docs/refactor-baseline.md`:**
   - DrinkRules test count.
   - Smoke test exit code.
   - Today's `DrinkGame ready: …` log line (counts of ingredient props, glasses, garnishes, customers).
   - Profiler `GC.Alloc` and the time of our scripts (idle, glass on coaster, pouring), including the `DrinkContainer.All` scans. Those are optimized only if they're > 0.1 ms/frame.
   - HandGrab feel values from `BarTuning.asset` (grabCurl 0.5, releaseCurl 0.35, pinchDistance 0.02, pinchReleaseDistance 0.035, grabReach 0.07, palmGrabOffset 0.03, throwStrength 1.1, throwWindow 0.12) for the headset comparison.
3. **Record the runtime-built layout.**
   - A temporary DevTools menu, `Automation > Dump Runtime Layout`, plays the scene, waits 10 frames, and writes world pose/scale to `Unity/Captures/layout-baseline.json` for:
     - `Serve Panel` and its buttons
     - `Reset Button`
     - `How To Play Sign` and `How To Play`
     - each `Label *` height above its target
     - each character after the frame-3 re-seat
     - the XR Origin
   - Read-only; removed in Phase 9.
4. Verify in the Editor that the rig's `Locomotion`/`Teleport*` objects are inactive.
5. **Hygiene:**
   - Add `Unity/.utmp/` to `.gitignore`.
   - Ask the owner about the dangling untracked `Unity/Assets/Resources.meta` (folder missing; left by an interrupted build or performance test); expected action: delete it.
6. **Fix the cause of batch-run asset changes** (requirement 5: automation must not change tracked files).
   - On a clean tree, run each DevTools entry point once (Capture, Smoke Test, Demo Video, Check Scene, AndroidBuild), each followed by `git status --porcelain` and `git diff`. Then fix per file:
     - **`LiberationSans SDF.asset`:** a Static atlas (`m_AtlasPopulationMode: 0` = `Static` in `TMP_FontAsset.cs:22`). A serialization upgrade was already committed by a teammate (`9dbb157`). If a fresh run still changes it, read the diff:
       - If the change is in the **dynamic fallback** asset it references (mode 1) adding glyphs: give the labels a static font asset containing every character they use (ASCII + `·` + `±`; the scripts use ASCII `-`).
       - If it's a format field: commit it once.
     - **`QualitySettings.asset`:** find the tool and the property that change (the bar-split notes blame MoonlitCapture, but the DevTools code has no direct `QualitySettings` write, so the diff decides: likely a URP asset or a quality level set during Play). That tool records the original value and restores it in a `finally` / on `EnteredEditMode`.
     - **`preloadedAssets` / `Assets/Resources/PerformanceTestRun*.json`:** left by an interrupted build. AndroidBuild (owner's file, coordinated with them) snapshots `PlayerSettings.GetPreloadedAssets()` before building and restores it in a `finally`.
   - Each DevTools entry point ends with `HeadlessRun.WarnIfTrackedFilesChanged()`: it runs `git status --porcelain -uno` (if git is on PATH) and logs a warning that lists any tracked file changed during the run. It's a guard, not a fix, and doesn't fail the run.
   - Until a file's cause is fixed, the old "revert before commit" rule applies to that file.

### Phase 1: delete every script that builds scenes or assets (Code, ~1.5 h)

| Delete | Notes |
|---|---|
| `Assets/RoomBuilder/` (BuildBarRoom.cs + asmdef) | Blockout builder |
| `Assets/Editor/InstallProBuilder.cs` (and the empty folder) | No-op installer |
| `Assets/Scenes/BarRoom.unity` (and the empty folder) | Not in Build Settings |
| `Assets/Materials/*.mat` (6) | Only referenced by BarRoom.unity (GUID grep) |
| `com.unity.probuilder` in `Packages/manifest.json` | Only used by the above |
| `Scripts/Editor/MoonlitSetup.cs` | Before deleting, copy into `Docs/LAB_SETUP.md` as a manual checklist: <ul><li>OpenXR features (HandTracking, MetaHandTrackingAim, HandInteractionProfile, OculusTouch/MetaQuestTouchPlus profiles, MetaQuestFeature on Android)</li><li>IL2CPP, ARM64, min SDK 29</li><li>Run In Background</li><li>linear colour space</li></ul> |
| `Scripts/Editor/BarSplitAssembler.cs` (whole file) | Assemble saved the scene; Refresh/Promote rebuild prefabs (Q4) |
| `Scripts/VRCharacters/Editor/VRCharacterSetup.cs` | The 3 prefabs exist (Q4) |
| — | **Moves and merges:** `BarPieceCheck.cs` + `DrinkSetupCheck.cs` → one `DevTools/Editor/SceneCheck.cs` (read-only, menu `Automation > Check Scene`, `SceneCheck.RunBatch`). `HeadlessRun` gets `public const string Root = "Assets/MoonlitTails"` and `ScenePath`, and every `MoonlitSetup.*` use outside the deleted files points there. Complete list (`git grep -n "MoonlitSetup\."` outside MoonlitSetup/BarSplitAssembler): `ScenePath` in BarPieceCheck, DrinkSetupCheck and HeadlessRun; **`Root` in `BarPieceCheck.cs:79`**. Re-run that grep after the change: it must print nothing |

Docs:
- `Docs/BAR_PIECES.md` gets the manual steps:
  - **Re-export with the same name:** reimports in place, same GUID; the prefab and MeshCollider follow.
  - **New piece:** drag the FBX into `Environment/Bar/Prefabs/<group>`, add MeshCollider (props also get convex + Rigidbody + XRGrabInteractable + PropRespawn, with the values copied from an existing prop prefab via the component menu's Copy/Paste Component Values), then add it to `MoonlitBar.prefab`.
  - **Remade model:** instance → Prefab → Replace.
- README loses "Rebuild VR Scene".

**Checks:** batch compile, D5, D6, `SceneCheck` batch 0 problems.

### Phase 2: assemblies and DevTools isolation (Code, ~2 h + one worktree compile)

1. Add the 5 asmdefs from §2.
   - DemoDirector loses its `#if UNITY_EDITOR` wrapper (`defineConstraints` handles it; it's never serialized in a scene). `MoonlitTails.DevTools` (runtime) references MoonlitTails.Runtime, .Drinks, Unity.RenderPipelines.Universal.Runtime + Unity.RenderPipelines.Core.Runtime (`GetUniversalAdditionalCameraData`), Unity.TextMeshPro, Unity.XR.Interaction.Toolkit.
   - `MoonlitTails.DevTools.Editor` references: MoonlitTails.Runtime, .Drinks, .Editor, .DevTools, Unity.XR.Interaction.Toolkit (`SwitchOffSimulator` matches the simulator by type name, so no sample asmdef reference), Unity.XR.CoreUtils (`XROrigin`, used by MoonlitCapture from 4.1), Unity.XR.Management, Unity.RenderPipelines.Universal.Runtime + Unity.RenderPipelines.Core.Runtime (`GetUniversalAdditionalCameraData` returns a type implementing Core's `IAdditionalData`), Unity.InputSystem, Unity.TextMeshPro. Confirm the final list with a batch compile.
2. **Remove reflection:**
   - `FloatingLabel.ViewCamera { get; set; }` replaces reflection on `cam` (DemoDirector:59).
   - `OrderBook.ShowTicket`/`Say` become public (DemoDirector:434,445), and DemoDirector calls them directly. In 4.1 they move into DrinkGame as `ShowTicket(int seat)` / `Say(int seat, …)`, and DemoDirector is updated in the same commit.
   - Delete `MoonlitCapture.HandGrabReport()` (:197-215) and its call (:66).
3. `DevTools/README.md`: asmdef layout and the rules from §2.
4. **`Tools/check_devtools_isolation.sh`:**
   1. `git worktree add --detach /tmp/mt-iso HEAD`
   2. `cp -a --reflink=auto Unity/Library /tmp/mt-iso/Unity/` (no full reimport)
   3. delete `DevTools{,.meta}` in the worktree
   4. batch compile there
   5. grep `error CS`
   6. `trap 'git worktree remove --force /tmp/mt-iso' EXIT`

   It never touches the main working copy. Run it at the end of Phases 2, 4 and 9.

   **As implemented (Phase 2):** the script makes a `cp --reflink=always` copy of `Unity/` next to the repo instead of using a git worktree.
   - `/tmp` is a 9.7 GB tmpfs, too small for the 8.6 GB Library.
   - A worktree checkout would duplicate every LFS model on a nearly full disk.
   - The reflink copy costs almost no space, and it refuses to fall back to a full copy.
   - It passed in about 2 minutes.

**Checks:**
- batch compile
- isolation script
- D6
- one DemoVideoRecorder run at 960x540 (`-demoSize`)
- `SceneCheck` 0 missing scripts

### Phase 3: remove HandGrab, bake the timestep and body collisions (Code + Editor, ~2 h + headset)

BarLayout and BarTuning stay until Phase 4, because the customer list still comes from BarLayout.

**Code:**
- Delete `Runtime/HandGrab.cs`. Its only users were MoonlitSetup (Phase 1) and MoonlitCapture's report (Phase 2).
- Delete the "Hands" fields, `throwSpin`, `throwWindow`, `physicsRate`, `playerBodyPushesProps` from BarTuning, and the BarLayout lines that apply them.
- Delete the HandGrab mention in the CoasterSnap comment.

**Editor (`MoonlitBar_VR.unity`):**
1. Remove the 2 HandGrab components (Left/Right Hand → Near-Far Interactor).
2. Project Settings → Time → Fixed Timestep = `0.0111111` (90 Hz).
3. XR Origin → nested Starter rig → `CharacterController`: **disable** it (scene override). That bakes "body doesn't push props"; locomotion is inactive.
4. **Headset**, against the baseline: fist and pinch grab on a bottle, a glass and a garnish; release; throw.
   - If the fist doesn't trigger: the rig's two `Select Input` → `ReleaseThresholdButtonReader` Press/Release Threshold = 0.8/0.6 (scene override), then retest.
   - If the runtime reports no grasp: pinch-only (Q1). No custom grab code.

**Checks:** batch compile, D6, D3 on the XR Origin, headset check.

Docs: `LAB_SETUP.md` (timestep = headset rate; grab thresholds live on the rig's Select Input).

### Phase 4: authored drink game, consolidated, step by step (Code + Editor, ~3 days)

**Strangler pattern:**
1. First, the consolidated components work both from serialized fields **and** from the old runtime path. `DrinkGame.Boot`/`Start` become idempotent.
2. Then the Editor work replaces the runtime pieces one group at a time.
3. Finally, the runtime builders are deleted.

The game is playable after every commit.

**4.1 Code: consolidated, dual-mode components (game behaves as today)**

| Script | Change |
|---|---|
| `Drinks/Ingredient.cs` | `public enum IngredientId { None = 0, Whiskey = 1, Bourbon = 2, Tequila = 3, TonicWater = 10, OrangePeel = 11, Espresso = 12, BlackTea = 13, Syrup = 20, Cola = 21, DarkCaramel = 22, Grenadine = 23, LimeJuice = 30, AppleCider = 31, GrapefruitAcid = 32, SourMix = 33 }` (explicit, because Unity serializes enums as ints; `None` is the default, meaning "not configured"). `Ingredients.Get(IngredientId)` replaces the unused `ById` and returns null for None. Test pins every value and checks the mapping is one-to-one for all values except None |
| **Setup contract (all gameplay components)** | Setup lives in a private `EnsureSetUp()` guarded by `bool setUp`, so it runs at most once. `Awake` calls it **only if the component is configured**: <ul><li>IngredientSource: `ingredient != None`</li><li>DrinkContainer: non-empty `variant`</li><li>Garnish: non-empty `color`</li><li>StirTool, IceScoop: always</li></ul>The transition `Init(...)` shims set the fields and then call `EnsureSetUp()`. That matters because during 4.1 the runtime path does `AddComponent` (which runs `Awake` immediately, with default data, so setup is skipped) and then `Init`, which runs setup once with real data. No exception in `Awake`, no duplicate labels, liquid roots or streams. After 4.5, an unconfigured component logs one error and disables itself. `SceneCheck` flags None, empty variant and empty colour.

**Anything that depends on another object's reference runs in `Start` (or in `Init` during the transition), never in `Awake`:**
- **IceScoop:** the ice-well bounds are computed lazily on first use (and again if `iceWell` changes), never in `EnsureSetUp`, because during 4.1 `iceWell` only arrives via `Init(well)` after `Awake`.
- **ServeStation**, created by `AddComponent` in 4.1 (`DrinkGame.cs:102`):
  - `Awake` only caches its own components.
  - The runtime panel builder, the `serveSpot` health check, glass subscriptions and the bell subscription run in `Init(...)` (transition) or `Start` (authored), whichever comes first, guarded by `bool started`.
  - Its `OnEnable` subscribes the bell only if `bell` is already set. `Init` subscribes it otherwise.
- **DrinkGame:** the seat fallback, the health check (`ordersEnabled`) and registry reads run in `Start`. The `Boot`-created instance gets the same `Start`. |
| `Drinks/OrderGenerator.cs` | `TryGenerate(…, out order)` returns false when there are no glasses. `Generate` stays as a wrapper for the existing tests. New test for the empty case |
| `Pourer` (new abstract base, replaces `PourSpout` + `IPourer`) | Pour fields (`pourAngle` 100, `rearmAngle` 60, `pourSeconds` 0.5, `dumpSeconds` 1.2), the stream LineRenderer, and the tilt/target/dump logic from `PourSpout.Update`, as `protected virtual void Update()`. Abstract members: `HasSomethingToPour`, `StreamColor`, `PourInto`, `Dump`, `CanDump`.

**Unity messages are `protected virtual` on Pourer** (`Awake`, `OnEnable`, `OnDisable`, `Update`), and every derived class uses `protected override` and calls `base.X()`. A private `Awake` in both base and derived would make Unity call only the derived one, silently breaking pours.

`upAxis` and `bounds` move from DrinkContainer into Pourer as `protected` fields, computed in `Pourer.EnsureGeometry()`, which the derived `EnsureSetUp()` calls. `SpoutWorld`, `UpWorld` and `Tilt` move to Pourer as public. `DrinkContainer.All` registration stays in `DrinkContainer.OnEnable/OnDisable` overrides (calling base).

**No existing asset has a PourSpout** (it was runtime-only), so nothing serialized is lost |
| `IngredientSource : Pourer` | `[SerializeField] IngredientId ingredient` (replaces the `string ingredientId`) + label fields (`labelHeight`, `labelSize = 0.22`, `showWithin = 1.2`). Setup in `Awake`; `Init(Ingredient)` sets the field (`Enum.Parse<IngredientId>(ingredient.Id)`; ids equal the enum names, covered by the pinned test) and calls `EnsureSetUp()` (shim, removed in 4.5) |
| `DrinkContainer : Pourer` | `kind`/`variant`/`length` already serialized. Setup in `Awake`; label settings serialized. `Update` override = shake detection + `base.Update()`. Init shim |
| `Garnish`, `StirTool`, `IceScoop` | Setup in `Awake`; Init shims. IceScoop: `[SerializeField] Transform iceWell`, cached bounds, "Already has ice" once per glass. StirTool: `center` field above its use |
| `CustomerProfile` (new ScriptableObject, `[CreateAssetMenu(menuName = "Moonlit Tails/Customer Profile")]`) | `displayName`, `greetings[]`, `story[]`, `thanks[]`, `wrong[]`, `storyDone`. `Fallback()` keeps the default lines. `FromScript(CustomerScripts.Profile)` exists only for the transition (deleted in 4.5). The 3 assets are made by hand in 4.3 |
| `DrinkGame` (absorbs `OrderBook`) | `[SerializeField] List<Seat> seats` (Seat = profile, body, ticketSpot). Refs: `ServeStation serve`, `Transform playerSpot`, `IceScoop iceScoop` (the ice well is read from the scoop; no duplicate ref), `XRBaseInteractable resetButton`, `Transform rulesSign`.

**Name constants:** today's `public const string PlayerSpot/ServeSpot/IceWell/BellProp/...` and `Tickets` (`DrinkGame.cs:56-65`) would clash with the new properties of the same names (CS0102). In 4.1 they're renamed with a `Name` suffix (`PlayerSpotName`, `ServeSpotName`, `IceWellName`, `BellPropName`, `StirCupPropName`, `ShakeMixerPropName`, `SpoonPropName`, `IceScoopPropName`, `MenuBoardName`, `TicketNames`). Only `Plan()` uses them, and they're deleted with it in 4.5.

**Public API for DevTools (no reflection).** Runtime per-seat state is a public sealed class `SeatState`, nested in DrinkGame, with **stable instances** (created once in `Start`), so `IndexOf`/`??` patterns in DevTools keep working.
- Read-only properties on `SeatState`, declared `{ get; internal set; }`: DrinkGame (same assembly) can write them, DevTools (separate assembly) can't. `Profile` (CustomerProfile), `DisplayName`, `Body`, `TicketSpot`, `Bubble` (FloatingLabel), `Ticket` (FloatingLabel), `Order` (DrinkOrder or null), `StoryIndex`, `Missed`.
- Mutations only through DrinkGame methods:
  - `SetOrder(int seat, DrinkOrder order)`: sets the order, clears `Missed`, refreshes the ticket.
  - `ShowTicket(int seat)`.
  - `Say(int seat, string text, Color color, float seconds)`.
- `IReadOnlyList<SeatState> Seats`.
- Other properties: `ServedCount` (was `OrderBook.Served`), `RulesSign` (back-wall sign), `MenuBoardText` (chalkboard text), `ResetButtonObject` (Transform of the reset button; not named `ResetButton`, which would clash with the static `ResetButton` class until 4.5), `PlayerSpot`, `IceScoop`, `Station` (the ServeStation; **not** `Serve`).

**The order check moved from OrderBook** (`OrderBook.Serve(ServedDrink, out string)`, called at `ServeStation.cs:116`) is named `DrinkGame.TryServe(ServedDrink, out string)`. That avoids clashing with `ServeStation.Serve()` and keeps one meaning per name. ServeStation calls `game.TryServe(...)`.

During the transition, `RulesSign`/`MenuBoardText`/`ResetButtonObject` return the runtime-built objects (`How To Play Sign` / `How To Play` / the dome's transform: `ResetButton.Create` returns the dome PokeButton, and DrinkGame keeps it). From 4.4 they return the authored ones, so no demo shot drops out.

**Seat fallback during the transition** (empty `seats`): build seats from BarLayout's characters. Each profile is a runtime `CustomerProfile.FromScript(CustomerScripts.For(id))` made with `ScriptableObject.CreateInstance`, kept in a list and destroyed in `DrinkGame.OnDestroy`. The 3 real `.asset` files are created **by the human in 4.3** (Assets → Create → Moonlit Tails → Customer Profile, via `[CreateAssetMenu]`), with text copied from `CustomerScripts.cs`.

**Health:** a missing `serve`/`seats` ref disables **ordering and serving only** (`ordersEnabled = false` + one error). The component stays enabled, so the respawn tick, keyboard R and reset keep working. All of `OrderBook` moves in: orders via `TryGenerate` (no glasses → one `Debug.LogError`, ordering disabled), tickets, `Say`/`ShowTicket` (public). Glass variants come from `FindObjectsByType<DrinkContainer>` **filtered by `IsGlass`** (otherwise customers could order a "Stir Cup"), colours from `FindObjectsByType<Garnish>`, both in `Start` (after every `Awake`/`OnEnable`). `ResetAllProps()` (calls the static `PropRespawn.ResetAll()`, which stays as the implementation, so the transition-era `ResetButton.cs` (`Pressed += PropRespawn.ResetAll`, line 42) keeps compiling until 4.5), the keyboard-R listener, and the respawn tick (`Update`: lost-prop check over `PropRespawn.All`; `FixedUpdate`: pending homes). Read-only properties for DevTools. **Transition (removed in 4.5):** <ul><li>`Boot` skips if any DrinkGame exists</li><li>`Start` adds a prop's gameplay component only if it has none (`TryGetComponent`)</li><li>if `seats` is empty, fill it from BarLayout + `CustomerScripts`</li><li>create ServeStation only if `FindFirstObjectByType<ServeStation>() == null`</li><li>build the runtime reset button only if `resetButton` is null</li><li>build the rules sign/menu text only if `rulesAuthored` is false</li></ul> |
| `OrderBook.cs` | Deleted (no asset references it; it was only ever added at runtime) |
| `ServeStation` (absorbs `CoasterSnap` and `StatBars`) | Serialized: `TMP_Text title, info`, `StatBarsView bars`, `Transform serveSpot`, `DrinkGame game`, `XRBaseInteractable bell`, `snapRadius 0.13`, `snapHeight 0.3`. Coaster methods `TrySnap`/`Release`/`InReach`/`OnCoaster` + `FixedUpdate`. Glasses subscribed in `Start` via `FindObjectsByType<DrinkContainer>` with named handlers, unsubscribed in `OnDestroy`; null `serveSpot` → error and disabled. The bell is subscribed in code (`OnEnable`/`OnDisable`). Public `Serve()` (0.5 s cooldown, so one gesture = one serve), `AdjustBitter(int)`, `AdjustSweet(int)`, `AdjustSour(int)` (single-int UnityEvent signatures). Public read-only `CoasterGlass` (the DrinkContainer on the coaster, or null), `ServeSpot`, `Bell` (Transform of the bell interactable), `Panel` (root transform of the panel, whether authored or runtime-built). `TrySnap(DrinkContainer)` and `InReach(DrinkContainer)` are public (DevTools use them). The release coroutine (`WaitForFixedUpdate`, then snap) moves over unchanged. **Transition:** if `title` is unassigned, build today's runtime panel (it keeps using `PokeButton` and `GameVisuals.BuildStatBars` until 4.5) |
| `CoasterSnap.cs`, `StatBars.cs`, `PourSpout.cs` | Deleted in 4.1 (merged above). None is referenced by an asset |
| `FloatingLabel` | `ShowBars` (`FloatingLabel.cs:20,55`) switches from `StatBars.Create` to `GameVisuals.BuildStatBars` returning a `StatBarsView`. `StatBarsView` is declared in this file |
| `StatBarsView` (plain `[Serializable]` class) | `GameObject root`, `Transform[3] fills`, `TMP_Text[3] values`, `TMP_Text caption`, `Show(Taste, string)` with skip-if-unchanged, `SetVisible(bool)` (→ `root.SetActive`), `IsBuilt => root != null`.

Unity creates a non-null instance of a serialized plain class, so code checks `IsBuilt`, never `bars != null`. FloatingLabel keeps its field **non-serialized** (built on first `ShowBars`); ServeStation serializes it (authored in ServePanel.prefab). Call sites that move the bars (`FloatingLabel.cs:52-58`, `ServeStation.cs:47`) use `bars.root.transform`.

`GameVisuals.BuildStatBars(parent, width)` builds one for floating labels |
| `PropRespawn` | No Update/FixedUpdate. `ResetToHome()` public; `RequestHomeNextStep()` sets the flag DrinkGame's FixedUpdate consumes. Root-level props use world space (Phase 6) |
| DevTools (same commit as the deletions, so 4.1 compiles) | Complete change list (line numbers as of b9bb04f):
- `PourSpout` → `Pourer` and `.SpoutWorld` → `Pourer.SpoutWorld`: DrinkPlaySmokeTest:115,125; DemoDirector:311,334.
- `station.Snap.Snap/InReach/Glass` → `ServeStation.TrySnap/InReach/CoasterGlass`: DrinkPlaySmokeTest:277-302; DemoDirector:567,598.
- **Customers and orders:**
  - Reads: `game.Orders.Customers` → `game.Seats` (DrinkPlaySmokeTest:159,215,224,260,268).
  - Writes `c.order = …` → `game.SetOrder(i, …)`: DrinkPlaySmokeTest:216, 261 (use the loop index).
  - `game.Orders.Served` → `game.ServedCount`: DrinkPlaySmokeTest:220,222,265,267.
  - `.order/.storyIndex/.missed` → `.Order/.StoryIndex/.Missed`.
- **DemoDirector customer code:**
  - `OrderBook orders` field and lookup (:57, :76) are deleted; use `game`.
  - `SetOrder` (:437-443) → `game.SetOrder(customer, order)`.
  - `Greet(OrderBook.Customer c)` (:447-452) → `Greet(int seat)`, using `game.Seats[seat].Profile.greetings` and `game.Say(seat, …)`.
  - `HeadOf(OrderBook.Customer c)` (:454-466) → `HeadOf(DrinkGame.SeatState c)`, using `c.Body`/`c.Bubble`.
  - `orders.Customers` (:480) → `game.Seats`.
  - Delete the `ShowTicket`/`SayLine` reflection fields (:434, :445). Phase 2 already made the OrderBook methods public; this step retargets them to DrinkGame.
- `transform.Find("Serve Panel")` → `station.Panel` (DemoDirector:615). `GameObject.Find("How To Play")` → `game.MenuBoardText` (DemoDirector:657; that object is the chalkboard text, `DrinkGame.cs:177`).
- **Button presses:**
  - Serve-panel buttons are "pressed" by calling `ServeStation.Serve/AdjustX`, not by child index (DemoDirector:418-419, 618-620).
  - The reset button press → `game.ResetAllProps()` (DrinkPlaySmokeTest:293-299; DemoDirector:643-650).
- **MoonlitCapture: 4.1 replaces every `layout.*` use** (4.3 only re-greps `BarLayout` to confirm nothing is left):
  - `"MK_ServeSpot"` lookup (:130-132) → `FindFirstObjectByType<ServeStation>().ServeSpot`.
  - `layout.environment` (:131) → the `MoonlitBar` root via `ServeSpot.root`.
  - every `layout.xrOrigin` (:91, :174-175, :180-183) → `FindFirstObjectByType<XROrigin>().transform`.
  - `layout.characters` (:64, :116-125) → `game.Seats[i].Body`/`DisplayName`.
  - the `FindFirstObjectByType<BarLayout>()` lines (:90, :171, :181) are deleted, and so are the hip-height report lines (:64-65).
  - add `using MoonlitTails.Gameplay; using Unity.XR.CoreUtils;` and one `var game = FindFirstObjectByType<DrinkGame>()` per method that uses `game.Seats`.
- **Smoke test ordering:** compute `serve` (:136) from `station.ServeSpot` *after* `station` is looked up (:144).
- **Allowed name lookup:** `HeadlessRun.OpenBarScene`'s `GameObject.Find("MoonlitBar")` (`HeadlessRun.cs:20`) stays. It's a DevTools-only "is the bar scene open?" probe, not game wiring. The no-name-lookups rule covers `Scripts/` (D1) and DevTools' access to gameplay objects.
- Name lookups through the old `DrinkGame` name constants → properties:
  - `ServeSpot` → `station.ServeSpot`
  - `PlayerSpot` → `game.PlayerSpot`
  - `IceWell` → `game.IceScoop.IceWell`
  - `BellProp` → `station.Bell` (DemoDirector:423 `Ring()`)
  - `Tickets[i]` → `game.Seats[i].TicketSpot`

  Lines: DemoDirector:77-78,423,522,546,607-609; DrinkPlaySmokeTest:131-136,194. Delete the `Find(string)` helpers (DemoDirector:250; DrinkPlaySmokeTest:131). The bounds check `DrinkGame.Tickets.Length` (DemoDirector:607) → `game.Seats.Count`.
- **Reset:**
  - press → `game.ResetAllProps()` (DrinkPlaySmokeTest:293-299)
  - the DemoDirector shot (:643-650) frames `game.ResetButtonObject` and then calls `game.ResetAllProps()`
- `Greet(customers[i])` call site → `Greet(i)` (DemoDirector:519).
- **SceneCheck** (from `DrinkSetupCheck.cs:32-47`) stops calling `DrinkGame.Plan(...)`, `GlassLengths` and `plan.tickets/menuBoard`. It reports from components and DrinkGame/ServeStation properties instead, **in this same 4.1 commit**, so deleting `Plan` in 4.5 doesn't break it.
- **MoonlitCapture**, besides the items below:
  - `:170` `"Glass_Rocks_01"` → `DrinkContainer.All.First(c => c.IsGlass && c.variant == "Rocks").GetComponent<Rigidbody>()` (`thrown` is a Rigidbody)
  - the hip-height report line (`:65`, `seatedHipHeight`) is deleted
  - the character loop (`:116-125`) uses `Body`/`DisplayName`
- `IceScoop` gets a public read-only `IceWell`.
- Done when: batch compile, then D6 and a 960x540 demo render, both in the 4.1 checks.

`SceneCheck`: <ul><li>lists Pourer/Garnish/StirTool/IceScoop components and seats</li><li>warns on `PROP_Bottle_*`/`PROP_Fridge_*`/`PROP_Glass_*` with no gameplay component</li><li>warns on empty DrinkGame/ServeStation refs, seats without a profile or ticket, and ServePanel buttons with no `selectEntered` listener</li></ul> |

**Awake/Start order:**
- DrinkContainer registers in `DrinkContainer.All` and PropRespawn in `PropRespawn.All`, both in `OnEnable`, right after each object's own `Awake`. Garnishes have no registry; DrinkGame finds them with `FindObjectsByType<Garnish>` in `Start`.
- DrinkGame and ServeStation only *read* in `Start`, which Unity runs after every scene object's `Awake`/`OnEnable`. During the transition, `Boot` (AfterSceneLoad) also runs before any `Start`.
- No Script Execution Order settings are needed.

Checks after 4.1: plays exactly as today; batch compile; D5; D6; a 960x540 demo render.

**4.2 Editor: prop prefabs (by hand; prefab assets, not instances)**

| Prefab asset(s) | Component, values |
|---|---|
| `Bottles/PROP_Bottle_Rum`, `_Vodka`, `_Tequila` | IngredientSource: Whiskey, Bourbon, Tequila |
| `Fridge/PROP_Fridge_Tonic`; `Bottles/PROP_Bottle_TripleSec`, `_Gin`, `_Violet` | TonicWater, OrangePeel, Espresso, BlackTea |
| `Bottles/PROP_Bottle_PeachSyrup`; `Fridge/PROP_Fridge_Cola`, `_Milk`; `Bottles/PROP_Bottle_Grenadine` | Syrup, Cola, DarkCaramel, Grenadine |
| `Fridge/PROP_Fridge_Soda`, `_Juice`; `Bottles/PROP_Bottle_MelonLiqueur`, `_BlueCuracao` | LimeJuice, AppleCider, GrapefruitAcid, SourMix |
| `Glasses/PROP_Glass_Shot` | DrinkContainer: Glass, "Shot", Short |
| `Glasses/PROP_Glass_Rocks`, `_Mug`, `_Coupe` | Glass, variant = Rocks/Mug/Coupe, Medium |
| `Glasses/PROP_Glass_Highball`, `_Martini`, `_Wine` | Glass, variant = Highball/Martini/Wine, Long |
| `Tools/PROP_Blender_Jar` | DrinkContainer: StirCup, "Stir Cup", Long |
| `Tools/PROP_Shaker_Tin` | DrinkContainer: ShakeMixer, "Shake Mixer", Long |
| `Garnish/PROP_Garnish_*` (12 assets) | Garnish, colour = the word after `Garnish_` |
| `Tools/PROP_BarSpoon` | StirTool |
| `Tools/PROP_IceScoop` | IceScoop. Then in `MoonlitBar.prefab`, the scoop instance's `iceWell` = `ENV_IceWell` (one override inside the same prefab) |

That's 38 prefab edits, about 1–1.5 h. The pairs are today's mapping (`DrinkGame.cs:21-52`); renaming is Q3. `Check Scene` must match the baseline log line: 20 ingredient props (15 prefabs; each fridge prefab has 2 instances), 15 glasses, 18 garnishes, plus stir cup, shaker, spoon and scoop.

Checks: play-through (Start adds nothing now), D6.

**Expected SceneCheck warnings until 4.3:**
- Until 4.2 is done: "no gameplay component" on the `PROP_Bottle_*`/`PROP_Fridge_*`/`PROP_Glass_*` props.
- Until 4.3: "no DrinkGame object", "seats empty".
- Until 4.4: unset ServeStation refs (ServeStation is still runtime-created). 4.2 compares only the component counts with the baseline. `SceneCheck` must reach **0 problems from the end of 4.4**. Between 4.1 and 4.3, a non-zero exit with only these warnings is expected.

**4.3 Editor: the Drink Game object, seats, poses, interaction layers**

0. Create the 3 `CustomerProfile` assets in `Data/Customers/` (Assets → Create → Moonlit Tails → Customer Profile), text copied verbatim from `CustomerScripts.cs`: Kintsu; Character03 = "Fox"; Character02 = "Guest".
1. Scene object `Drink Game` with `DrinkGame`.
   - `seats`, in today's ticket order:
     - Kintsu (Kintsu.asset, `PROP_OrderTicket_01`)
     - Character03 (Character03.asset, `_02`)
     - Character02 (Character02.asset, `_03`)
   - `playerSpot` = `MK_PlayerStart`, `iceScoop` = the `PROP_IceScoop` instance.
   - `serve` and `resetButton` are set in 4.4.
   - Characters stay scene roots.
2. Set Character02/03 to the post-re-seat world poses from `layout-baseline.json`; confirm Kintsu and the XR Origin match.
3. **In the same commit as the matching Code change:**
   - Delete the `BarLayout` object and `BarTuning.asset`.
   - Code deletes `BarLayout.cs`, `BarTuning.cs` and every remaining reference: the DrinkGame seat fallback, `DemoDirector.cs:487`, and the BarLayout sections of `SceneCheck` (formerly `BarPieceCheck.cs:131-138`, `DrinkSetupCheck.cs:46-47`).
   - MoonlitCapture's BarLayout uses were already replaced in 4.1; re-run `git grep -n BarLayout -- Unity/Assets` and expect nothing outside the deleted files. BarTuning's remaining values are all neutral (scales 1, environmentScale 1, floor offset 0), so nothing else to bake.
4. `XRI/Settings/Resources/InteractionLayerSettings.asset`: add `Poke` (layer 1) and `Serveable` (layer 2).
   - Current layers: the two **hand** Poke Interactors in the hands rig are on Everything (`XR Origin Hands (XR Rig).prefab:281, 1318`). The **controller** Poke Interactors come from Starter Assets `Poke Interactor.prefab` on Default only (`m_InteractionLayers: 1`, line 82). NearFar interactors are on Default.
   - **Scene overrides:** set both controller Poke Interactors to Default + Poke. Otherwise controllers can't press the panel or reset buttons.
   - Leave NearFar on Default, so pinch/grab can't press Poke-only buttons.

**4.4 Editor: authored UI (by hand, poses from `layout-baseline.json`)**

All buttons: `XRSimpleInteractable` (interaction layer **Poke**) + trigger `BoxCollider` + `XRPokeFilter` + `XRPokeFollowAffordance` (Starter Assets sample script, already in the project).

XRPokeFilter settings:
- Poke Configuration → Use Constant
- **Poke Direction** = the axis the finger travels along (`PokeAxis` doc: "poking in the positive Z-axis direction"):
  - panel buttons face the player on local −Z, so the finger moves along +Z: `Z` (the default)
  - the reset dome faces up: `NegativeY`
- Interaction Depth Offset 0, Enable Poke Angle Threshold on, 45°.

Test each with the simulator's poke. If one never fires, flip the axis sign.

| Object | Kind | Contents and wiring |
|---|---|---|
| `ServePanel.prefab` (instance near `MK_ServeSpot`) | Prefab (complex; keeps scene merges small) | Root: `ServeStation`. TMP title/info, StatBars rows (background, fill, tick, name, value × 3 + caption) assigned to `bars`, 6 ± buttons + Serve. Button `selectEntered` → `ServeStation.AdjustBitter(±1)` etc. and `Serve()`, **all inside the prefab**. Instance field refs (not UnityEvents): `serveSpot` = `MK_ServeSpot`, `game` = Drink Game, `bell`. Then set `DrinkGame.serve` |
| Reset button | **Plain scene object** (unique, no script) | Base, red dome (button), caption "DO NOT PUSH". `DrinkGame.resetButton` = the dome's XRSimpleInteractable (subscribed in code) |
| `RulesSign.prefab` | Prefab (no script) | Frame, slate, TMP rules text (copied from `DrinkGame.RulesText`) |
| Chalkboard text | Scene object (no script) | Slate + TMP placed on `DECO_MenuBoard` |

**Bell** (`Tools/PROP_ServiceBell.prefab`, Q2):
- Remove `PropRespawn` first (it requires a Rigidbody), then `XRGrabInteractable`, then `Rigidbody`.
- Add `XRSimpleInteractable` (layers Default + Poke, so pinch or poke rings it) + `XRPokeFilter` (`NegativeY`).
- In `MoonlitBar.prefab`, **Overrides → Remove unused overrides** clears its stale `space` overrides.

**Logo** (Q6): deactivate `DECO_Medallion`, `DECO_Neon_Glass_Back`, `DECO_Neon_Olive` on the scene's MoonlitBar instance (visible overrides) and place RulesSign there. Set `rulesAuthored` = true.

Commit order:
1. ServePanel
2. reset button
3. rules sign + chalkboard

Play-through after each.

**4.5 Code: delete the transition paths**

Delete:
- `DrinkGame.Boot`, `Plan`/`SetupPlan`, the `*Name` constants, the `Sources`/`GlassLengths` tables, `CustomerProfile.FromScript` and the runtime profile list, `WriteMenuBoard`, `TryFace`, `RulesText`, `PutRulesOnBackWall`, `LogoPieces`, `rulesAuthored`, the runtime reset button builder, the seat fallback
- every `Init(...)` shim
- ServeStation's runtime panel builder
- `ResetButton.cs`, `PokeButton.cs` + `HandTips` (and `Unity.XR.Hands` from the Runtime asmdef if unused), `CustomerScripts.cs`

Add `[RequireComponent(typeof(XRGrabInteractable))]` to Pourer, Garnish, StirTool and IceScoop. That's safe: the prefabs already have one after 4.2, and RequireComponent never adds a duplicate. `GameVisuals.Primitive` disables, then `Destroy`s, the collider (no `DestroyImmediate`).

**Checks for Phase 4:**
- D1, D2, D3 (characters and props too), D11
- batch compile, isolation script, D5, D6, `SceneCheck` 0 problems
- **Simulator play-through:** pour, stir, shake, layer, mixed pour, ice, garnish, ± by poke, serve right and wrong by Serve and by bell (one serve each), reset by poke with a glass on the coaster

Docs:
- `DRINK_GAME.md`: the game is the `Drink Game` object; how to add an ingredient prop or a glass; poke buttons; fixed bell; the consolidation rule.
- `MENU_AND_CHARACTERS.md`: mapping set per prefab; customer text in `Data/Customers`.
- README.

### Phase 5: coaster as an XRI socket (optional spike ≤ 2 h; Code + Editor)

ServeStation's snap methods already work after Phase 4, so skipping this costs nothing.

**Spike:**
- `XRSocketInteractor` on a coaster child: interaction layer **Serveable** only; trigger box 0.26 × 0.3 × 0.26 m; attach transform at the coaster top; `showInteractableHoverMeshes` off; `recycleDelayTime` 0.5 s.
- Glass prefabs get Default + Serveable.
- Sockets ignore dynamic attach (`XRGrabInteractable.cs:2443,2463`), so the glass aligns by its `attachTransform`. If a glass pivot isn't at its base centre, add a `Base` child and assign it as `attachTransform` (hands keep dynamic attach).
- ServeStation reads `socket.firstInteractableSelected`. `DrinkContainer.ServedAndReturned()` → `PropRespawn.ResetToHome()` (cancels the socket's selection before teleporting).

**Pass criteria (all):**
- upright and centred on release
- can be lifted off
- serve and reset send it home and it stays there
- bottles never snap
- works in the simulator and the headset

**Pass:** delete ServeStation's snap methods and fields. **Fail:** revert.

### Phase 6: remaining null-safety (Code, ~0.5 day)

| Location | Fix |
|---|---|
| `PropRespawn` | `Awake` already falls back to `transform.parent` (`:43`). A prop at the **scene root** uses world space (`ToWorld/FromWorld`), with no NRE in `HomePosition`, `RememberHome`, `SendHome` |
| `Pourer` (was `PourSpout.cs:80`) | `target.Label?.Flash` becomes `if (target.Label) …` (Unity null semantics) |
| `Label.Flash` call sites (DrinkContainer, ServeStation, IceScoop) | Guard with `if (Label)` |
| `FloatingLabel` | Guard `title/message/content` |
| `SpringBoneSystem` (`:119, 163-170`) | Skip nodes with a null `bone`; `Build()` skips a null `root` |
| `ServeStation`, `IceScoop` serialized refs | In `Start` (consistent with 4.1: cross-object checks never run in `Awake`): one `Debug.LogError(this)` naming the field, then `enabled = false`. No per-frame NRE spam |
| `DrinkGame` serialized refs | **Never disabled.** A missing `serve`/`seats` sets `ordersEnabled = false` with one error. The respawn tick, keyboard R and reset keep running (they need no refs). A missing `resetButton` only skips that subscription |
| DevTools `DemoDirector`/`DrinkPlaySmokeTest` | Clear error + exit code 2 if there's no DrinkGame |
| Kept public API | `BlinkController.ForceClosed/ReleaseForce` (emote API) and SpringBone wind (authoring option) are kept on purpose |

The empty-glasses path is tested by the DrinkRules `TryGenerate` test (D5). There's no asset-mutating test.

**Checks:** batch compile, D5, D6.

### Phase 7: memory and GC (Code, ~1 day)

| Location | Fix |
|---|---|
| `GameVisuals.Flat` | A hand-made `Resources/Moonlit/FlatUnlit.mat` (URP Unlit), loaded once with `Resources.Load`. That guarantees the shader is in builds and removes `Shader.Find`. Per-colour instances cached by colour quantized to 1/32; `SceneManager.sceneUnloaded` destroys and clears them |
| `DrinkContainer` liquid | Pool made in setup (≤ 5 cylinders + 3 ice cubes), toggled and rescaled. `Refresh()` adds the container to a static `DrinkContainer.Dirty` set. **One `LateUpdate` on DrinkGame** rebuilds every dirty container once per frame, then clears the set. One callback instead of 17 (already counted in D11's −147). Fixes same-frame duplicates |
| `DrinkContainer` layered stats | Cache `Layering.Resolve`, invalidated when the layers change (one resolve per pour, not three) |
| `DrinkContainer.Refresh` | Reuse one `StringBuilder` |
| `ServeStation.Update` | A state key (glass id, method, cold, garnish, stats, adjust left, served count); TMP and `bars.Show` only when it changes |
| `StatBarsView.Show` | Skip when unchanged; `Signed()` uses a cached string table for −20..20 |
| `Pourer` | Stream material set only when the colour changes; early-out when upright and not armed |
| `FloatingLabel.LateUpdate` | Cheap squared-distance check first; skip while hidden |
| HandGrab leak / `RemoveAt(0)`; lambda listeners; per-prop R polling | Gone (Phases 3, 4) |
| `DrinkContainer.All` scans | Only if the baseline shows > 0.1 ms/frame |
| DevTools `DemoDirector` | `Destroy(rt)` after `Release()`; `Destroy(frame)`; `using` on both `Process` objects; `BeginErrorReadLine()` before `WaitForExit()` (fixes the stderr deadlock) |

**Checks:** D8 before/after, D6, one demo video render.

### Phase 8: build hygiene and APK (Code + Editor, ~0.5 day)

1. On the scene's `XR Interaction Simulator` instance, set the root Tag to **`EditorOnly`**. Unity strips EditorOnly GameObjects (and their children) from player builds.
   - No settings change, no reflection, and it stays available for Linux and simulator play.
   - It isn't in DevTools, so D4 still plays.
2. `PlayModeXR` stays (`MoonlitTails.Editor`; Linux Play mode; `SkipNextPlay` used by DevTools).
3. Optional: remove unused samples (`Starter Assets/DemoScene.unity`, the classic `XR Device Simulator` sample) after a GUID grep. Unreferenced samples don't ship.
4. APK via `AndroidBuild.Build` once its owner commits it. Then D9, headset checklist §5.3 on the APK, and the D8 profile on a Development build.

### Phase 9: final docs and cleanup (Code, ~2 h)

- Remove `Dump Runtime Layout` (the baseline lives in the git-ignored `Unity/Captures/`).
- Final D10 grep. `DevTools/README.md`.
- Update the Claude memories:
  - `bar-fbx-split`: manual steps replace Refresh Bar Pieces.
  - `automation-must-not-touch-teammates-work`: asmdef rule, no asset builders, the consolidation rule.
- Final isolation run.

## 4. Editor work: by hand only

All Editor work (Phase 3; 4.2–4.4; 5; 8.1) is done by a human in the Unity Editor following the tables above. No migration or helper script is written, committed or run. The tables are the PR review checklist, and `Check Scene` (read-only) confirms the result.

Estimated Editor time:
- 4.2: about 1–1.5 h
- 4.3: about 1 h
- 4.4: about 4–6 h
- Phase 3/5/8 steps: about 1 h

## 5. Testing

### 5.1 Automated
1. `dotnet run --project Tests/DrinkRules.Tests` (D5).
2. **Batch compile** (interactive Editor closed; watch the log file, not `pgrep`):
   ```
   ~/Unity/Hub/Editor/6000.3.25f1/Editor/Unity -batchmode -nographics -quit -projectPath Unity -logFile /tmp/mt-compile.log
   ```
   Then `! command grep -E 'error CS|Compilation failed' /tmp/mt-compile.log`.
3. **D1:**
   ```
   ! git grep -nE 'AddComponent<(IngredientSource|DrinkContainer|Pourer|PourSpout|Garnish|StirTool|IceScoop|OrderBook|ServeStation|CoasterSnap|PokeButton)>|GameObject\.Find\(|RuntimeInitializeOnLoadMethod' -- Unity/Assets/MoonlitTails/Scripts
   ```
   Uses `git grep` rather than the shell's `grep` wrapper. Sanity check: on today's tree it must print the DrinkGame lines.
4. `Tools/check_devtools_isolation.sh` (D4).
5. `-executeMethod MoonlitTails.EditorTools.DrinkPlaySmokeTest.RunBatch`, exit 0 (D6). `SceneCheck.RunBatch`, exit 0.
6. Check `git status` after automated runs. Batch side effects are fixed at the cause (Phase 0, step 6), and `WarnIfTrackedFilesChanged` names any regression. Revert only files whose cause isn't fixed yet.

### 5.2 Editor
Simulator play-through (Phase 4 list), drag test (D3), Inspector review of every table row.

### 5.3 Headset checklist (Quest 2; Link and APK; human)

| Area | Checks |
|---|---|
| Grab | Fist + pinch on a bottle, a glass and a garnish. Release by opening. Throw feels like the baseline |
| Pour and mix | Stir Cup and Shake Mixer. Stir with the spoon. Shake |
| Glass | Layered and mixed pour. Ice. Garnish on the rim |
| Serve | Coaster snap and lift. ± by poke. **Pinching near the panel presses nothing.** Serve button. Bell by poke and by pinch (one serve each). Right and wrong order |
| Reset | Reset by poke; the glass on the coaster goes home and stays home |
| Scene | Labels readable within range. Rules sign readable from behind the bar. Characters seated right |
| Controllers | Pick up a controller: grab works; the controller's poke tip presses ± / Serve / reset; the grip button doesn't press them |
| Performance | 72/90 Hz without GC spikes (Development build + Profiler) |

## 6. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Quest `graspValue` never reaches 1.0 | Lower thresholds on the rig (Phase 3, step 4); Q1 accepts pinch-only |
| Intermediate state breaks play | Strangler steps; smoke test after each commit |
| Merging PourSpout into a base class changes pour behaviour | The logic moves verbatim. Smoke test pours from every source type; headset pour check |
| Socket fights serve/reset | Phase 5 optional with pass criteria |
| Teammate edits conflict | Scene freeze for Phases 3–5; merge `main` first; one PR per phase |
| Asmdef split misses a reference | Phase 2 alone; batch compile + isolation script |
| UnityEvent wiring lost | UnityEvents only inside ServePanel.prefab; names fixed in 4.1; `SceneCheck` flags empty listeners |
| Enum renumbering | Explicit values + pinned test |
| Weekly demo video breaks | DevTools updated in the same commits as each API change (Phases 2, 3, 4); demo render in the Phase 2 and 7 checks; record from `main` if a deadline falls mid-phase |
| Layout drift | Baseline poses; MoonlitCapture screenshots compared before/after |
| Override noise in `MoonlitBar.prefab` (200 instances) | Edit prefab assets; the only instance overrides are the scoop's `iceWell` and the bell clean-up |
| `m_EditorClassIdentifier` diff noise | Expected, noted in the PR |

## 7. Decisions needed (defaults in bold)

| # | Question | Default |
|---|---|---|
| Q1 | Whole-hand grab must work? | **Yes, via the stock grasp binding + thresholds.** Pinch-only rather than custom code |
| Q2 | Bell fixed or grabbable? | **Fixed** |
| Q3 | Rename props to match ingredients? | **Not now** (visible in the Inspector and on the label) |
| Q4 | Delete BarSplitAssembler (incl. Refresh/Promote) and VRCharacterSetup? | **Delete both**; manual steps in `BAR_PIECES.md` |
| Q5 | Customers: serialized seats or discovered? | **Serialized `seats` on DrinkGame** |
| Q6 | Rules sign replaces the logo? | **Yes**, as visible overrides |
| Q7 | Socket instead of ServeStation snap methods? | **Only if the Phase 5 spike passes** |
| Q8 | Fold the 33 `SpringBoneCollider`s into a list on SpringBoneSystem? | **No.** Per-bone gizmo editing is the right tool for artists. Revisit if the character artist prefers a list |

## 8. Traceability: every finding → where it's fixed

| Finding (C = Claude, DS = DeepSeek, Col = colleague) | Phase |
|---|---|
| Scene/asset builders: MoonlitSetup, BarSplitAssembler, BuildBarRoom, InstallProBuilder, VRCharacterSetup (C, DS) | 1 |
| BarRoom.unity, blockout materials, ProBuilder (C, DS) | 1 |
| Duplicated validator tables/logic (C, DS) | 1 (one `SceneCheck`) |
| Headless tools kept but separated; reflection (user, DS) | 2 |
| MoonlitCapture blocks HandGrab deletion (DS) | 2 |
| HandGrab redundant, NRE `:73`, ManualValue, per-frame interactor rewrite, double throw, grabPoint leak, RemoveAt(0) (C, DS) | 3 |
| Timestep and body collisions set only at runtime (DS) | 3 |
| BarLayout `[ExecuteAlways]`, `BarTuning.Character` mutates the asset, LateUpdate null tuning, frame-3 re-seat, customers from BarLayout (C, DS) | 4.3 |
| **Too many scripts that could be functions; CoasterSnap separate (Col)** | §2.1 + 4.1 (CoasterSnap→ServeStation, PourSpout→Pourer, OrderBook→DrinkGame, StatBars→StatBarsView, ResetButton→DrinkGame, PropRespawn callbacks→DrinkGame, validators→SceneCheck; Q8) |
| Runtime name wiring, Boot, Plan, misleading mapping (C, DS) | 4 (Q3) |
| Duplicate components when authored; Init-only contract (DS) | 4.1 (idempotent) → 4.5 (Awake + RequireComponent; D1) |
| OrderBook throws with no glasses (C, DS) | 4.1 (`TryGenerate` + test) |
| Customer text in code | 4.1 (CustomerProfile) |
| `Ingredients.ById` unused (C) | 4.1 |
| Procedural UI, TryFace, ModelPlayerToServe (C, DS) | 4.4 / 4.5 |
| Logo hidden at runtime (C) | 4.4 / Q6 |
| PokeButton/HandTips vs XRI poke; wrong space without an XROrigin (C, DS) | 4.4 / 4.5 |
| Bell double serve (C) | 4.1 (cooldown + one code subscription) |
| CoasterSnap vs socket; its null checks; lambda listeners (C) | 4.1 (in ServeStation, named handlers, null guards) + 5 |
| R key polled in 43 props (C) | 4.1 (DrinkGame) |
| `DestroyImmediate` at runtime (DS) | 4.5 |
| IceScoop flash spam and per-frame `GetComponentsInChildren`; StirTool field order (C, DS) | 4.1 |
| Garnish `colliders` / IceScoop `self` before Init (DS) | 4.1 (Awake) |
| Null checks: PropRespawn root, `?.`, Label.Flash, FloatingLabel, SpringBone, missing refs, DevTools Find (C, DS) | 6 |
| `Shader.Find`; material cache never freed; Flat per frame (C, DS) | 7 |
| RebuildLiquid churn and duplicates; Resolve ×3; List per Refresh (C, DS) | 7 |
| ServeStation/StatBars per-frame strings (C, DS) | 7 |
| FloatingLabel per-frame cost (DS) | 7 |
| Linear scans (DS) | 0 (measure) → 7 (if > 0.1 ms) |
| DemoDirector RT/Texture/Process leaks, stderr deadlock (C) | 7 |
| Simulator ships in the APK (C) | 8 (EditorOnly) |
| ~69 MB samples (DS) | 8 (optional) |
| PlayModeXR (C, DS) | Kept |
| Build side effects: `.utmp/`, stray `Resources.meta`, `preloadedAssets`, PerformanceTestRun JSON, TMP font and QualitySettings re-saves | 0, step 5 (ignore/remove) + step 6 (fix in the tool that causes it; `WarnIfTrackedFilesChanged` guard) |
| Controllers can't press Poke-layer buttons (review) | 4.3, step 4 (controller Poke Interactor overrides) + §5.3 |
| Docs out of date | Per phase + 9 |

## 9. Effort

| Phases | Time |
|---|---|
| 0–2 | 1 day |
| 3 | Half a day + headset |
| 4 | About 3 days (1.5 Code, 1.5 Editor) |
| 5 | ≤ 2 h, optional |
| 6–9 | About 2 days |

Total: about 7 developer days, about 1.5 days of a human Editor partner, and 2–3 headset sessions. Teammates are blocked only by the Phase 3–5 scene freeze.
