# Moonlit Tails: VR test scene (lab setup)

Scene: `Assets/MoonlitTails/Scenes/MoonlitBar_VR.unity`

The scene contains:
- **The bar.** `Environment/Bar/MoonlitBar.prefab`, built from one prefab per piece. The Blender source is in `Art/Bar/`. See [BAR_PIECES.md](BAR_PIECES.md).
- **Three placeholder customers** (the real characters are in [MENU_AND_CHARACTERS.md](MENU_AND_CHARACTERS.md#characters)):
  - Kintsu stands at the ordering spot in front of you.
  - Character03 sits on the right-hand bar stool.
  - Character02 sits at the left table in the lounge.
- **A hand-tracking rig.** You can pick up every glass, bottle, tool and garnish, put them down, and throw them.

## Running it on the Quest 2 (Link, recommended for the lab)

Link lets you change sizes in the Inspector while someone is wearing the headset.

1. **PC:**
   - Install the Meta Quest Link app.
   - In the app, go to **Settings > General** and turn on **OpenXR Runtime**.
   - In **Settings > Beta**, turn on **Developer Runtime Features**. Hand tracking over Link needs this.
2. **Headset:**
   - In Settings > Movement tracking, turn on **Hand and body tracking**.
   - Connect Link (cable or Air Link).
3. **Unity:**
   - Open the scene and press **Play**.
   - Put the controllers down. The hands show up as soon as the headset sees them.
4. **Recenter:** stand where you want to be behind the bar, and long-press the Meta button to recenter.

**Linux (no headset):** OpenXR can't start in the Linux editor, so on Linux Play skips XR startup and the scene's XR Interaction Simulator drives the rig with mouse and keyboard. The XR setting comes back when you stop Play and is never saved, so the Quest build doesn't change. To turn this off (for example to try a Linux OpenXR runtime), use **Tools > Moonlit Tails > Linux: Skip OpenXR in Play Mode**.

**Hands** (the stock XR Interaction Toolkit hand rig, no custom grab script):
- **Hold something:** close your hand around it (grasp) or pinch it between thumb and index. Opening your hand lets go. It's held where your hand touched it.
- **Throw:** let go while your hand is moving. The XR Grab Interactable on each prop throws it with your hand's speed (Throw Velocity Scale 1.1, smoothed over 0.25 s).
- **Reset:** press **R** on the PC keyboard to send every prop back to where it started. Anything that leaves the room (out of a window, through the floor) comes back by itself.

If grabbing feels off, tune it on the XR rig in the scene, not in code:
- **Grab triggers too late, or a fist doesn't grab:** on `Left Hand` / `Right Hand` > `Select Input` (Release Threshold Button Reader), lower **Press Threshold** (default 1) and **Release Threshold** (default 0.9), e.g. to 0.8 / 0.6.
- **Throws too weak or too strong:** **Throw Velocity Scale** / **Throw Angular Velocity Scale** on the prop prefabs' XR Grab Interactable.
- **Physics rate:** Project Settings > Time > Fixed Timestep is 0.0111111 (90 Hz). Match the headset refresh rate (72/90/120 Hz) so held props move smoothly.
- **Props pass through your body:** the XR Origin's CharacterController is disabled (walking and teleporting are off), so leaning over the counter doesn't knock glasses away. Enable it again if locomotion comes back.

## Moving and resizing things

Everything is placed by hand in the scene, like any Unity object. No script moves anything when you open the scene or press Play.

- **A customer:** select the character (`Kintsu`, `Character03`, `Character02`) and move, turn or scale it. Seated customers are posed on their seats as saved; if you change a seat's height, lower or raise the character to match.
- **Who sits where, and whose ticket is whose:** the **Seats** list on the `Drink Game` object. Each seat has a customer profile, the character model and their ticket on the rail.
- **What a customer says:** their profile asset in `Assets/MoonlitTails/Data/Customers` (placeholder writing for now).
- **The player:** move the `XR Origin Hands (XR Rig)` object. It starts behind the bar at `MK_PlayerStart`, facing the serve spot.
- **The bar or a prop:** edit its prefab in `Environment/Bar` (see [BAR_PIECES.md](BAR_PIECES.md)), so the change doesn't touch the scene.

Props' "home" positions are stored relative to the bar, so a rescaled bar still resets props onto the right counter.

## Project settings (already set; check them by hand if something breaks)

The scene and these settings are edited by hand in the Editor. No script rebuilds them.

- **Rendering:** URP with `Settings/MoonlitURP.asset` (Forward+, MSAA 4x, HDR, shadow distance 12 m), set as the default pipeline and on every quality level. Colour space: Linear.
- **XR Plug-in Management:** OpenXR for PC and Android, *Initialize XR on Startup* on.
- **OpenXR features (PC and Android):** Hand Tracking Subsystem, Meta Hand Tracking Aim, Hand Interaction Profile, Oculus Touch and Meta Quest Touch Plus controller profiles. On Android also **Meta Quest Support**.
- **Player:** *Run In Background* on (Play keeps running when the headset takes focus). Android: IL2CPP, ARM64, minimum API level 29.
- **Samples** (Package Manager): XR Interaction Toolkit *Starter Assets*, *Hands Interaction Demo* and *XR Interaction Simulator*; XR Hands *HandVisualizer*.
- **Sitting animation** (`Animations/Sitting Idle.fbx`): Rig = Humanoid, create the avatar from this model; clip renamed `Sitting_Idle`, Loop Time + Loop Pose, root rotation, height and XZ baked into the pose.

## Checking the scene

**Tools > Moonlit Tails > Automation > Check Scene** checks the bar pieces (see [BAR_PIECES.md](BAR_PIECES.md)) and lists which props the drink game uses (see [DRINK_GAME.md](DRINK_GAME.md)). It changes nothing.

**Automation** (Check Scene, Capture Player Views, Play Smoke Test, Record Demo Video) runs the game without a headset for desk checks, tests and the demo video. You don't need it to work on the game, and it never saves the scene. It lives in `Assets/MoonlitTails/DevTools` (see the README there).

## Building an APK instead of Link

The Android settings are already filled in:
- OpenXR with Meta Quest support and hand tracking
- IL2CPP, ARM64, min SDK 29

To build: **File > Build Profiles > Android > Switch Platform**, then Build And Run with the Quest plugged in.

Or without opening the build window: **Tools > Moonlit Tails > Automation > Build Android APK**, or from a terminal (the APK goes to `Unity/Captures/Build/`):
```
Unity -batchmode -quit -projectPath Unity -executeMethod MoonlitTails.EditorTools.AndroidBuild.Build
```

On the Quest itself, keep an eye on performance:
- **Lights:** the bar uses about 20 realtime lights. Baking the lighting is the next step for a standalone build.
- **Bloom:** turn off the `PostProcess (Bloom)` object if the frame rate drops.

## What the desk check showed (no headset)

These come from `Captures/report.txt`, for a 1.60 m eye height standing in the middle of the pit:

| Thing | Value |
|---|---|
| Work counter top | 1.02 m (customer floor is +0.5 m) |
| Kintsu (standing, ordering) | about 1.6 m away, head about 0.5 m below your eyes |
| Character03 (stool) | about 2.0 m away, head about 0.3 m below your eyes |
| Character02 (table) | about 5.1 m away |
| Props after 4 s of physics | 62 of 62 stayed put (nothing jitters or falls through) |
| Thrown glass | landed and stopped on the back counter |
| Reset | glass returned exactly to its spot |

Hand tracking itself has **not** been tried yet. This PC has no headset or Link runtime, so the first real test is the lab.

Things to judge in the headset:
- How far you reach to the glass station (left) and the bottles (right).
- Whether the glasses feel too big or too small in your hand.
- Whether Kintsu feels too low behind the counter.

## Known gaps (on purpose for now)

- **Glasses are solid to physics:** you can't drop a cherry *into* a glass yet. That needs a separate collider setup for each glass type, once the glass sizes are final.
- **Hands don't push things:** they grab, but don't knock things over without grabbing. Pushing needs physics colliders on the hand joints.
- **Fixed props:** tap handles, dispenser levers and order tickets don't move yet. Levers come with the drink-making logic.
- **Static batching is off:** so the bar can be rescaled live. Turn it back on (select the bar > Static) once sizes are final.
