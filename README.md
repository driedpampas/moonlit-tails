# Moonlit Tails

A cozy VR bartending game for Quest. Mix drinks with your own hands for animal customers who come back every night with their stories.

[AI conversation log](Docs/AI_Conversation_Log/README.md)

[Main modules](https://docs.google.com/document/d/1RSFIeXCF41nIRa_LPg4IBjAwsAV0ZEPrtElibrYGyQE/edit?usp=sharing)

[State of the art](https://docs.google.com/document/d/1jW3J8CAuBcPGa2orrIAQPoJuJKJFUZwphDUCLjqxw80/edit?tab=t.0)

## Contact (Discord)
- @bianca_alexandru_
- @syulze
- @stiv69
- @broko25

## Milestone 1 (Oct 26 – Nov 2, 2026)
- Placeholder environment with interactable assets integrated in Unity
- Character brainstorming and creation

AI was used for:
- creating placeholder 3D models
- help with integration
- debugging
- writing the README

## Milestone 2 (Nov 3 – Nov 8, 2026)
<a href="https://driedpampas.github.io/moonlit-tails/"><img src="https://driedpampas.github.io/moonlit-tails/poster.jpg" width="480" alt="Milestone 2 demo video"></a>

[Watch the demo](https://driedpampas.github.io/moonlit-tails/) (2 min, plays in the browser)

- Quest build setup, and the XR Interaction Simulator for testing without a headset
- The drink game: pouring, stirring, shaking, layering, orders and customer reactions ([how it works](Docs/DRINK_GAME.md))
- The bar split into one prefab per piece ([details](Docs/BAR_PIECES.md))
- [Ingredients, recipes and characters](Docs/MENU_AND_CHARACTERS.md): everything the game has so far
- Playtest fixes: the serve coaster, a reset button, and rules you can read from behind the bar

AI was used for:
- writing the drink rules, the VR interactions and their tests
- splitting the bar into pieces
- the playtest fixes
- recording the demo video
- writing the docs

## Milestone 3 (Nov 9 – Nov 23, 2026)
<a href="https://youtu.be/mxYhWDxUZeU"><img src="https://img.youtube.com/vi/mxYhWDxUZeU/hqdefault.jpg" width="480" alt="Milestone 3 demo video"></a>

[Watch the demo](https://youtu.be/mxYhWDxUZeU) (YouTube)

- The game loop: customers walk in, order, wait until they get the right drink, then leave, and the next one comes in. There are 5 customers now, in a random order, including two new characters (Skunk and Wolf)
- A smaller bar: everything is within arm's reach, and the front counter is laid out in working order (ice, mixing, garnish, serve). Ingredients are sorted like the drink guide: base drinks, then bitter, sweet and sour
- The Shake Mixer has a cap that snaps on, and it has to be on before you shake. Tools that did nothing were removed
- Easier handling: props snap back to their spot (a white ring shows where), what you hold no longer knocks other things over, labels only show when your hand is near and have a dark backdrop, and anything dropped on the floor comes back
- Quest 2 performance: baked lighting, one customer drawn at a time and lighter materials ([details](Docs/PERFORMANCE.md))
- Playtest fixes from a PC VR session: the character shader in VR, poke buttons, ear and tail physics, hand models when using controllers, and readable dialogue bubbles
- The scene is now edited by hand in the Editor, and the testing tools are kept apart in `DevTools`
- Builds for the Quest (APK) and for PC VR through SteamVR or Link (Windows)

AI was used for:
- reviewing the scripts and the refactor
- the game loop, the bar layout, the shaker cap and the prop handling
- finding and fixing the performance problems
- the PC VR fixes
- the automated tests, the builds and the docs

## What's here
| Folder | What |
|---|---|
| `Unity/` | The Unity project (Unity **6000.3.25f1**, URP, OpenXR hand tracking). Open this folder in Unity Hub. |
| `Docs/` | `LAB_SETUP.md`: how to run the scene on the Quest 2 over Link, move things, and rebuild. `DRINK_GAME.md`: how drinks are made, the rules, and how to test them. `BAR_PIECES.md`: how the bar is split into pieces and how to change them. `MENU_AND_CHARACTERS.md`: all ingredients, recipes and characters. `Characters/`: one page per character, and the blank character sheet. |
| `Tests/` | Tests for the drink rules (`dotnet run` in `Tests/DrinkRules.Tests`). |
| `Art/` | Blender sources. `Art/Bar/` has the bar `.blend`, the scripts that built it, and `split_bar.py`, which exports it as separate pieces. See `Docs/BAR_PIECES.md`. |

The current scene is `Unity/Assets/MoonlitTails/Scenes/MoonlitBar_VR.unity`. It's the bar with five customers and grabbable, throwable props, and the drink game. Everything in it is placed by hand in the Editor.

## Getting the project (first time)
1. Install **Git LFS** once, if you don't have it: https://git-lfs.com, then run `git lfs install`. Models and textures are stored with LFS, so without it they arrive as tiny text files.
2. Clone the repo:
   ```
   git clone https://github.com/driedpampas/moonlit-tails
   ```
3. In Unity Hub: **Add > Add project from disk** → pick the `Unity` folder. Use exactly Unity **6000.3.25f1**.
   - The first open takes a while: Unity rebuilds its `Library` cache, which isn't in git.
4. Set up Unity's merge tool once (adjust the path to your Unity install):
   ```
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
   ```

## Working together
- **Pull before opening Unity**, and close Unity before switching branches that change scenes.
- **Always commit `.meta` files together with their assets.** Move or rename assets *inside Unity*, never in Explorer, or references break.
- **One person edits `MoonlitBar_VR.unity` at a time.** Scene merges are painful. Changes to the bar itself go in its prefabs (`Environment/Bar`), so they don't touch the scene.
- **Tools > Moonlit Tails > Automation** and `Assets/MoonlitTails/DevTools` are the headless desk checks, smoke test and demo recorder. You can ignore them: they never save the scene or change project settings.
- **Before editing a `.blend` file, lock it** so nobody edits it at the same time: `git lfs lock path/to/file.blend`. Unlock it when you're done: `git lfs unlock path/to/file.blend`.
- Work on a branch and open a pull request into `main`.

## What's not in git
- **Unity cache folders:** `Library`, `Temp`, `Logs`, `UserSettings`.
- **Builds/APKs and desk-check renders** (`Unity/Captures`).
- **IDE files.**
