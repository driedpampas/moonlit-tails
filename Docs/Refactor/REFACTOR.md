# The November 2026 refactor: what changed and why

Branch `devtools-separation`, 9 Nov 2026. The step-by-step plan and the two audits it came from are in the git history (`465c3e5` to `2e82443`); this page is the short version.

## Why

- **The scene couldn't be edited by hand.** The game's objects were built by scripts, at edit time and at runtime, so hand edits were overwritten or had nothing to hold on to. The scene had only three of our components in it.
- **Custom scripts did what the XR Interaction Toolkit already does:** hand grabbing, poke buttons, layout.
- **Too many scripts that were really functions** (a colleague's note about the coaster snap).
- **Automation got in teammates' way:** it saved settings and scenes, and could wipe scene edits.

## What changed

| Before | Now |
|---|---|
| Scene and asset builder menus (Rebuild VR Scene, Refresh Bar Pieces, ...) | Deleted. The scene and the prefabs are edited by hand |
| `BarLayout` + `BarTuning` moved and scaled things every editor frame | Deleted. Positions are the ones in the scene; the physics step is in Project Settings |
| `HandGrab` (custom fist and pinch grab) | Deleted. The stock XRI hand rig grabs, pinches and throws |
| `PokeButton` (custom fingertip buttons) | Stock XRI poke: *XR Simple Interactable* + *XR Poke Filter* + the Starter Assets follow affordance, on a **Poke** interaction layer |
| The drink game built its panel, buttons, signs and components at runtime | A `Drink Game` object in the scene, a `ServePanel` prefab, a `RulesSign` prefab, and the drink components on the prop prefabs |
| `CustomerScripts` (lines in code) | One *Customer Profile* asset per customer in `Data/Customers/` |
| `CoasterSnap`, `PourSpout`, `OrderBook`, `ResetButton`, `StatBars` as components | Folded into their owners: Serve Station, a `Pourer` base class, Drink Game, a plain `StatBarsView` class |
| 62 props each with their own `Update` | Drink Game checks them once per frame |
| Automation mixed in with the game | `DevTools/`, in its own editor-only assemblies. The game never references it. See [its README](../../Unity/Assets/MoonlitTails/DevTools/README.md) |
| The XR Interaction Simulator shipped in builds | Tagged `EditorOnly` in the scene |
| Labels, liquid, ice and the pour stream were built in code | Four small prefabs in `Resources/Moonlit` that the game copies |
| HDR, camera post-processing and a forced extra buffer, for a bloom volume with an empty profile | All off, and the volume removed |
| The Quest ran the "Very Low" quality level (1 bone per vertex, half-size textures) | One level, *Standard* |
| No foveated rendering; physics at 90 Hz whatever the headset's rate | A `Headset Display` object in the scene sets both |
| Ingredients had an enum id and a string id | The enum only |

Also: a clear error message where a reference can be missing (and no checks for parts that always exist), no script allocates every frame any more, and the demo recorder frees what it creates. Numbers are in [BASELINE.md](BASELINE.md).

## Rules to keep

- **The scene is the source of truth.** No script builds or moves scene objects, in the editor or at runtime, if a scene object or a prefab can do it.
- **Stock XRI first.** Write a custom interaction script only when the toolkit can't do it.
- **One script per job.** A small behaviour that belongs to one thing is a method in that thing's script. A new component is worth it only when it goes on many different objects or is a different job.
- **Automation lives in DevTools**, never saves a scene or an asset, and uses only the game's public API. `Tools/check_devtools_isolation.sh` proves the game compiles without it.

## Checks

- `Tools > Moonlit Tails > Automation > Check Scene`: no problems.
- `Play Smoke Test`: 42 checks pass.
- `cd Tests/DrinkRules.Tests && dotnet run`.
- `Tools/check_devtools_isolation.sh`.

## Still to check in a headset

None of this can be tested from a desk:

| Area | Checks |
|---|---|
| Grab | Fist and pinch on a bottle, a glass and a garnish. Release by opening the hand. Throwing feels right |
| Serve panel | + / − and Serve by fingertip poke and by a controller's tip. Pinching near the panel presses nothing |
| Bell | Poke and pinch each serve once |
| Reset | The dome is pressed from above (within 45° of straight down). The glass on the coaster goes home and stays there |
| Scene | Characters seated right, each ticket by its customer, the rules sign readable from behind the bar |
| Spring bones | Look away from a character and back: the tail and ears restart from their pose without a jump |
| Performance | 72/90 Hz without GC spikes, on a Development build with the Profiler |
| Headset Display | The log line `Headset display: … Hz, foveation …` appears. The edges of the view don't look blocky (lower *Foveation* if they do). Held props move smoothly |
| Characters | Joints and hair deform better than before (4 bones per vertex) |

## Not done

- **Quest APK build** after the refactor (`Automation > Build Android APK`), which also tests the `EditorOnly` tag.
- **Spring bones** still cost about 1 ms a frame on desktop for three characters in view. Profile on a Quest Development build before going further (fixed 30–45 Hz step, fewer collider checks, a Burst job).
- **Baked lighting.** The bar has 20 realtime point and spot lights plus the moonlight, nothing is marked static, and the renderer is Forward+ because of it. This is the biggest cost left on a Quest. Someone needs to mark the fixed pieces static in the Editor, set the lights to Baked, bake, and check the look. The models already have lightmap UVs. After that the renderer can go to Forward.
- **Held props trail the hand.** All 42 grabbable props use *Velocity Tracking* with continuous collision detection. Try *Instantaneous* on one prop in a headset and compare.
- **GPU skinning** is off, and the toon outline draws each character twice. Test both on a Development build before changing them.
- **Colliders.** The bar's fixed pieces use 178 mesh colliders. Box colliders would be cheaper for walls, counters and shelves.
- **Prop shape is measured, not authored.** The up axis, mouth and radius of a glass or bottle come from its mesh bounds at startup. A "Mouth" child in each prefab would replace that, but the measurement works for every prop today.
- **Customer writing** in `Data/Customers/` is placeholder: replace it with the characters in [Docs/Characters](../Characters/).
