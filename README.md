# Moonlit Tails

A cozy VR bartending game for Quest. Mix drinks with your own hands for animal customers who come back every night with their stories.

[AI conversation log](Docs/AI_Conversation_Log.md)

[Main modules](https://docs.google.com/document/d/1RSFIeXCF41nIRa_LPg4IBjAwsAV0ZEPrtElibrYGyQE/edit?usp=sharing)

[State of the art](https://docs.google.com/document/d/1jW3J8CAuBcPGa2orrIAQPoJuJKJFUZwphDUCLjqxw80/edit?tab=t.0)

## What's here
| Folder | What |
|---|---|
| `Unity/` | The Unity project (Unity **6000.3.25f1**, URP, OpenXR hand tracking). Open this folder in Unity Hub. |
| `Docs/` | `LAB_SETUP.md`: how to run the scene on the Quest 2 over Link, change sizes, and rebuild. |

The current scene is `Unity/Assets/MoonlitTails/Scenes/MoonlitBar_VR.unity`. It's the bar with three customers and grabbable, throwable props, for testing scale and hand grabbing in VR. All sizes live in `Unity/Assets/MoonlitTails/BarTuning.asset`.

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
- **One person edits `MoonlitBar_VR.unity` at a time.** Scene merges are painful. Size tweaks go in `BarTuning.asset` instead, a small text file that merges fine.
- The scene can be regenerated with **Tools > Moonlit Tails > 2. Rebuild VR Scene**. Agree who runs it, because it replaces manual scene edits.
- **Before editing a `.blend` file, lock it** so nobody edits it at the same time: `git lfs lock path/to/file.blend`. Unlock it when you're done: `git lfs unlock path/to/file.blend`.
- Work on a branch and open a pull request into `main`.

## What's not in git
- **Unity cache folders:** `Library`, `Temp`, `Logs`, `UserSettings`.
- **Builds/APKs and desk-check renders** (`Unity/Captures`).
- **IDE files.**
