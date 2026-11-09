# Moonlit Tails

A cozy VR bartending game for Quest. Mix drinks with your own hands for animal customers who come back every night with their stories.

[AI conversation log](Docs/AI_Conversation_Log/README.md)

[Main modules](https://docs.google.com/document/d/1RSFIeXCF41nIRa_LPg4IBjAwsAV0ZEPrtElibrYGyQE/edit?usp=sharing)

[State of the art](https://docs.google.com/document/d/1jW3J8CAuBcPGa2orrIAQPoJuJKJFUZwphDUCLjqxw80/edit?tab=t.0)

## Contact (Discord)
- @syulze
- @bianca_alexandru_
- @stiv69
- @broko25

## Week 1
- Placeholder environment with interactable assets integrated in Unity
- Character brainstorming and creation

AI was used for:
- creating placeholder 3D models
- help with integration
- debugging
- writing the README

## Week 2
<a href="https://driedpampas.github.io/moonlit-tails/"><img src="https://driedpampas.github.io/moonlit-tails/poster.jpg" width="480" alt="Week 2 demo video"></a>

[Watch the Week 2 demo](https://driedpampas.github.io/moonlit-tails/) (2 min, plays in the browser)

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

## What's here
| Folder | What |
|---|---|
| `Unity/` | The Unity project (Unity **6000.3.25f1**, URP, OpenXR hand tracking). Open this folder in Unity Hub. |
| `Docs/` | `LAB_SETUP.md`: how to run the scene on the Quest 2 over Link, change sizes, and rebuild. `DRINK_GAME.md`: how drinks are made, the rules, and how to test them. `BAR_PIECES.md`: how the bar is split into pieces and how to change them. `MENU_AND_CHARACTERS.md`: all ingredients, recipes and characters. `Characters/`: one page per character, and the blank character sheet. |
| `Tests/` | Tests for the drink rules (`dotnet run` in `Tests/DrinkRules.Tests`). |
| `Art/` | Blender sources. `Art/Bar/` has the bar `.blend`, the scripts that built it, and `split_bar.py`, which exports it as separate pieces. See `Docs/BAR_PIECES.md`. |

The current scene is `Unity/Assets/MoonlitTails/Scenes/MoonlitBar_VR.unity`. It's the bar with three placeholder customers and grabbable, throwable props, for testing scale and hand grabbing in VR. All sizes live in `Unity/Assets/MoonlitTails/BarTuning.asset`.

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
- **One person edits `MoonlitBar_VR.unity` at a time.** Scene merges are painful. Size tweaks go in `BarTuning.asset` instead, a small text file that merges fine. Changes to the bar itself go in its prefabs (`Environment/Bar`), so they don't touch the scene.
- The scene can be regenerated with **Tools > Moonlit Tails > 2. Rebuild VR Scene**. Agree who runs it, because it replaces manual scene edits.
- **Before editing a `.blend` file, lock it** so nobody edits it at the same time: `git lfs lock path/to/file.blend`. Unlock it when you're done: `git lfs unlock path/to/file.blend`.
- Work on a branch and open a pull request into `main`.

## What's not in git
- **Unity cache folders:** `Library`, `Temp`, `Logs`, `UserSettings`.
- **Builds/APKs and desk-check renders** (`Unity/Captures`).
- **IDE files.**
