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

Also: null checks with clear error messages where a reference can be missing, no script allocates every frame any more, and the demo recorder frees what it creates. Numbers are in [BASELINE.md](BASELINE.md).

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

## Not done

- **Quest APK build** after the refactor (`Automation > Build Android APK`), which also tests the `EditorOnly` tag.
- **Spring bones** still cost about 1 ms a frame on desktop for three characters in view. Profile on a Quest Development build before going further (fixed 30–45 Hz step, fewer collider checks, a Burst job).
- **Customer writing** in `Data/Customers/` is placeholder: replace it with the characters in [Docs/Characters](../Characters/).
