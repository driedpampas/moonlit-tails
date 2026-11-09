# Refactor baseline

Recorded before any refactor change (FIX_PLAN.md Phase 0), on `devtools-separation` after merging `main` (2446f86), 2026-11-09.

| Check | Result |
|---|---|
| Drink rules (`Tests/DrinkRules.Tests`, `MSBuildEnableWorkloadResolver=false dotnet run`) | 73 passed, 0 failed |
| Batch compile | 0 errors, 0 `warning CS` |
| Smoke test (`DrinkPlaySmokeTest.RunBatch`) | 37 passed, 0 failed, exit 0 |
| Game setup log line | `DrinkGame ready: 20 ingredient props, 15 glasses, 18 garnishes, 3 customers.` |
| Tracked files changed by the batch run | None (the TMP font upgrade was already committed in 9dbb157) |

HandGrab feel values in `BarTuning.asset`, kept for the headset comparison once HandGrab is removed (Phase 3):

| grabCurl | releaseCurl | pinchDistance | pinchReleaseDistance | grabReach | palmGrabOffset | throwStrength | throwWindow |
|---|---|---|---|---|---|---|---|
| 0.5 | 0.35 | 0.02 | 0.035 | 0.07 | 0.03 | 1.1 | 0.12 |

## Runtime baseline (`Automation > Record Runtime Baseline`)

Measured with `RuntimeBaseline.RunBatch` on the Linux editor in batch mode, 72 fps cap. Each state is a 3-second window of about 200 profiled frames. The numbers are **only our own scripts** (Profiler samples from `MoonlitTails.Runtime` and `VRCharacters.Runtime`). Times are desktop CPU, so expect them to be several times higher on a Quest. Full data and the layout dump: [`runtime-baseline.json`](runtime-baseline.json).

| State | Our scripts: GC per frame | Our scripts: time per frame |
|---|---|---|
| Idle | 1221 B | 1.33 ms |
| Glass on the coaster | 1822 B | 1.40 ms |
| Pouring | 1546 B | 1.45 ms |

Numbers vary a little between runs (an earlier run gave 1478 / 1552 / 1546 B), mostly because customers' orders and speech lines land inside some windows and not others. Compare before/after with several runs.

Where it comes from (bytes/frame; idle / coaster / pouring):

| Script | GC | Time | Notes |
|---|---|---|---|
| `PokeButton` | 432 / 432 / 432 | ~0.02 ms | Every frame, from the 9 buttons. Goes away with XRI poke (Phase 4) |
| `ServeStation` | 280 / 562 / 563 | 0.03 ms | Rebuilds its panel strings every frame (Phase 7) |
| `PourSpout` | 0 / 0 / 503 | 0.05–0.09 ms | While pouring (Phase 7) |
| `IceScoop` | 48 / 48 / 48 | ~0.02 ms | `GetComponentsInChildren` on the ice well every frame (Phase 4) |
| `OrderBook` | 461 / 781 / 0 | ~0.02 ms | Not per frame: new orders and speech lines land inside these windows |
| **`SpringBoneSystem`** | 0 | **0.98–1.05 ms** | **No garbage, but the most expensive script by far.** It's the tail, ear and coat physics on the 3 characters. Neither audit flagged it. Added to Phase 7 |
| `FloatingLabel` | 0 | ~0.10 ms | 44 labels |
| `PropRespawn` | 0 | ~0.09 ms | 62 instances, each with its own Update (Phase 4 consolidation) |
| Others (`DrinkContainer`, `StirTool`, `BlinkController`, `CoasterSnap`, `BarLayout`) | 0 | < 0.015 ms each | |

The whole frame (including the editor and TMP text-mesh rebuilds outside our script samples) allocated roughly 1.6 KB idle, 3.1 KB with a glass on the coaster and 56–96 KB while pouring. The whole-frame figure is approximate: it's sampled once per editor update. The jump while pouring probably comes from TMP rebuilding the labels whose text changes on each pour. It isn't attributed to a script here, so check it on a Quest Development build in Phase 7.

## Runtime layout (for the Phase 4 Editor work)

`runtime-baseline.json` → `layout` has the full hierarchy of everything built at runtime, with world and local position, rotation and scale of every object, plus text, font size, alignment, rect size and colour for each TMP text, and mesh and colour for each primitive:
- `Serve Panel` (34 objects, at (0.28, 1.246, −0.83))
- `Reset Button` (5, at (1.05, 1.02, −0.42))
- `How To Play Sign` (4, the back-wall sign) and `How To Play` (3, the chalkboard text)

It also has the characters after BarLayout re-seats them (Kintsu standing at (0, 0.5, −1.42); Character03 seated at (−1, 0.991, −1.48), yaw 30°; Character02 seated at (3.52, 0.937, −3.5), yaw 270°), the XR Origin at (0, 0, 0.25), and all 44 floating labels with their heights.
