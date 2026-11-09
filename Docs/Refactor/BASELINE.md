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

Still to record before Phase 4 (it needs Play mode with the Profiler / a layout dump):
- Profiler `GC.Alloc` and time of our scripts: idle, glass on the coaster, pouring.
- Runtime layout poses (`Automation > Dump Runtime Layout`).
