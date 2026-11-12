# Refactor baseline

Recorded before any refactor change (see [REFACTOR.md](REFACTOR.md)), on `devtools-separation` after merging `main` (2446f86), 2026-11-09.

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

Measured with `RuntimeBaseline.RunBatch` on the Linux editor in batch mode, 72 fps cap. Each state is a 3-second window of about 200 profiled frames. The numbers are **only our own scripts** (Profiler samples from `MoonlitTails.Runtime` and `VRCharacters.Runtime`). Times are desktop CPU, so expect them to be several times higher on a Quest.

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

## After the refactor (Phase 7, 9 Nov 2026)

Same measurement, on the same machine, after Phases 1–7. Each cell is the range over two runs. The "characters in view" column is an estimate: the measured "nobody in view" time plus the solver's earlier 1 ms.

| State | GC per frame: start | before Phase 7 | after | Time per frame: start | after, characters in view | after, nobody in view |
|---|---|---|---|---|---|---|
| Idle | 1221 B | 627–878 B | 458–713 B | 1.33 ms | about 1.25 ms | 0.22–0.26 ms |
| Glass on the coaster | 1822 B | 1190–1225 B | 495–505 B | 1.40 ms | about 1.25 ms | 0.23–0.27 ms |
| Pouring | 1546 B | 947–950 B | 508–516 B | 1.45 ms | about 1.3 ms | 0.30–0.32 ms |

- **No script allocates every frame any more.** What's left (about 500 B/frame on average) is text built when something happens: an order arrives, a customer speaks, a pour lands. All of it shows under `DrinkGame`, because it now redraws the changed containers once per frame.
- `ServeStation`: 446 B/frame with a glass on the coaster, now about 3 B. `Pourer`: 500 B/frame while pouring, now about 29 B.
- **`SpringBoneSystem` still costs about 1 ms on desktop while a character is in view.** It's skipped only while no camera sees the character. In batch mode nothing is drawn to the Game view, so these runs measure the "nobody in view" case; the "in view" column is the earlier figure, since that code path didn't change. The next steps from the plan (a fixed 30–45 Hz step, fewer collider checks, a Burst job) need a profile on a Quest Development build first.
- The whole-frame figure (editor included) jumps between 1 KB and over 1 MB per frame from run to run, with and without these changes, so it isn't used here.
