# Moonlit Tails: VR test scene (lab setup)

Scene: `Assets/MoonlitTails/Scenes/MoonlitBar_VR.unity`

The scene contains:
- **The bar.** It comes from the Blender model `D:\MoonlitTailsBar\MoonlitBar`.
- **Three customers:**
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

**Hands:**
- **Hold something:** close your hand around it, like a real glass. Your palm must be within about 7 cm of it. It stays in your palm exactly where you touched it, and opening your hand lets go.
- **Small things** (cherries, garnish, mint): pinch them between thumb and index, and they're held at your fingertips.
- **Throw:** let go while your hand is moving. The prop leaves at the fastest speed your hand reached in the last 0.12 s, with your hand's spin. A slow toss lands softly and a fast flick flies.
- **Reset:** press **R** on the PC keyboard to send every prop back to where it started. Anything that leaves the room (out of a window, through the floor) comes back by itself.

If grabbing feels off:
1. In Play mode, select `Near-Far Interactor` under the left or right hand in the XR rig.
2. Watch **Finger Curl** and **Pinch Distance** on its `HandGrab` component while you hold a glass.
3. Set **Grab Curl** / **Release Curl** (and the pinch distances) in `BarTuning` to match what you see.

## Changing sizes (nothing is hardcoded)

All sizes live in one asset: **`Assets/MoonlitTails/BarTuning.asset`**. Select it and drag the sliders. The scene updates immediately, in Play mode too.

Because it's an asset, changes you make while playing in the headset are **kept when you stop Play**. That's different from normal Inspector changes on scene objects, which Unity throws away.

| Setting | What it does |
|---|---|
| Environment Scale | Scales the whole bar uniformly: room, counters, furniture, props, and the spots where characters stand and sit. |
| Player Floor Offset | Raises or lowers the floor you stand on. Use it to make the counter feel lower or higher without rescaling the room. |
| Prop Scale | Scales every grabbable prop, on top of the room scale. |
| Prop Scale Rules | Extra scale for props whose name contains some text (`Glass_`, `Bottle_`, `Garnish_`...). Add rules for anything else, e.g. `Jigger`. |
| Characters > Height | Each customer's standing height in metres. They are all modelled 1.0 m tall. |
| Characters > Height Offset | Moves a customer up or down, e.g. to sit deeper in a seat. |
| Characters > Turn | Turns a customer left or right from facing their target. |
| Grab With Whole Hand | Hold props by closing your hand around them. |
| Grab Curl / Release Curl | How closed the hand must be to grab (0 = flat, 1 = fist), and how far it must open to let go. |
| Allow Pinch Grab, Pinch Distance / Pinch Release Distance | Thumb-index pinch for small things, in metres between the fingertips. |
| Grab Reach | How far around the palm (m) a prop can be and still get grabbed. |
| Palm Grab Offset | How far in front of the palm (m) a held prop sits. |
| Allow Distance Grab | Off: you have to touch a prop to grab it. On: the hand ray can grab from a distance too. |
| Throw Strength | 1 = a released prop flies exactly as fast as your hand was moving. Raise it if throws feel weak. |
| Throw Spin / Throw Window | How much of the hand's spin the prop keeps, and how far back (s) to look for the throwing speed. |
| Physics Rate | Physics steps per second. Match the headset refresh (72/90/120) so held props move smoothly. |

Seated customers measure their own hip height from the sitting animation, so they stay on the seat at any height you give them.

**Moving things:**
- **A customer:** move their marker inside `MoonlitBar` (`MK_CustomerSpot_C`, `MK_StoolSeat_R`, `MK_ChairSeat_L1`). The character follows.
- **Swapping who sits where:** change the **Spot** of each entry on the `BarLayout` object.
- **The player:** move `MK_PlayerStart`.

Props' "home" positions are stored relative to the bar, so a rescaled bar still resets props onto the right counter.

## Rebuilding

Run these from **Tools > Moonlit Tails**:

1. **Configure Project**: URP, OpenXR + hand tracking (PC and Android), XR Interaction Toolkit samples, and the character prefabs. Safe to run again.
2. **Rebuild VR Scene**: rebuilds the scene from the FBX files.
   - Run it after re-exporting the bar from Blender (`build_bar.py`). The bar's colours come from `MoonlitBar_materials.json`, which that script writes next to the FBX.
   - Your sizes in `BarTuning.asset` are kept.
3. **Capture Player Views**: a desk check without a headset. It renders what a standing player sees in every direction, throws a glass and resets it. Output goes to `Captures/` with a `report.txt`.

## Building an APK instead of Link

The Android settings are already filled in:
- OpenXR with Meta Quest support and hand tracking
- IL2CPP, ARM64, min SDK 29

To build: **File > Build Profiles > Android > Switch Platform**, then Build And Run with the Quest plugged in.

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
