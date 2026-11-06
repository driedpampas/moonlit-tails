# Bar pieces

The bar used to be one FBX with 248 objects in it. Now every piece is its own file and its own prefab, so you can change, move or replace one thing without touching the rest.

## Where things are

| Path | What |
|---|---|
| `Art/Bar/MoonlitBar.blend` | The Blender source of the whole bar. Lock it before editing: `git lfs lock Art/Bar/MoonlitBar.blend`. |
| `Art/Bar/build_bar.py`, `moonlit_kit.py` | The script that originally generated the bar. **Keep it as a reference only; don't re-run it.** Its random placements would shift, and the neon sign needs a Windows font. |
| `Art/Bar/build_props.py` | Extra props and liquid shapes that aren't in the game yet. |
| `Art/Bar/split_bar.py` | Exports the `.blend` as one FBX per piece into `Unity/Assets/MoonlitTails/Environment/Bar`. |
| `Environment/Bar/<Group>/*.fbx` | The pieces: 141 files for 166 objects. Identical shapes, like the two copies of each glass, share one file. |
| `Environment/Bar/Prefabs/<Group>/*.prefab` | One prefab per piece, with its colliders. Props also get physics, grabbing and respawn. |
| `Environment/Bar/MoonlitBar.prefab` | The whole bar, built from the piece prefabs. The scene holds one copy of it. |
| `Environment/Bar/bar_split.json` | Which object uses which file. The Unity tools read it. |
| `Environment/StivModels/` | Stiv's remade pieces, each with a prefab in `StivModels/Prefabs` (colliders and materials included). |

## Inside `MoonlitBar`

The bar is grouped like the Blender collections:

| Group | What's in it |
|---|---|
| **Architecture** | Floors, walls, ceiling, windows, vestibules |
| **BarCounters** | The counter, back bar, stations, fridge, taps, sink |
| **Decor** | Plants, lights, signs, furniture, pictures |
| **NightSky** | Everything outside the windows |
| **Stools** | The two bar stools |
| **Props** | Everything you can grab: `Glasses`, `Bottles`, `Fridge`, `Garnish` and `Tools` |
| **Markers** | `MK_` points where customers stand, props live, and so on |
| **Lights** | The bar's lights |
| **StivModels** | `woodslab` (the wall panelling), `archdoor` (the door arches), `counterthingy` (the middle window sill and the back-wall panelling), as prefabs with colliders. They replace 8 pieces of the original bar, which `split_bar.py` leaves out. Put a new model in `Environment/StivModels` and run **7. Refresh Bar Pieces**: it gets a prefab too, and copies of the bare model in the bar are swapped for it. |

**Pieces made of parts.** Architecture and counter pieces are split into their parts, so you can move one slab, door or shelf on its own. For example, `ENV_BarCounter` has 20 children: the counter bodies, the top slabs, cabinet doors, fluting and trim.
- Up to 8 parts of one material stay separate.
- More than that become one group, like the 40 flutes or the floor planks of one colour.

Each part's pivot is at its bottom centre, and so is each piece's.

**Keep the names.** The code finds things by name: `PROP_*`, `MK_*`, `ENV_IceWell` and `DECO_MenuBoard`.

## Changing the bar

**Move, rotate or delete a piece:** open `MoonlitBar.prefab` (double-click it), or edit it in the scene and choose **Overrides > Apply All**. Changes in the prefab don't touch the scene file, so they merge more easily.

**Change a piece's shape:**
1. Lock and open `Art/Bar/MoonlitBar.blend`, and edit the object. Keep its name.
2. Export the pieces again:
   ```
   blender -b --factory-startup --disable-autoexec Art/Bar/MoonlitBar.blend --python Art/Bar/split_bar.py -- Unity/Assets/MoonlitTails/Environment/Bar
   ```
3. In Unity, run **Tools > Moonlit Tails > 7. Refresh Bar Pieces**. It updates the imports and prefabs in place, keeping the same files and links, so the scene keeps working.

Or edit one piece's FBX directly in Blender and export it again under the same name.

**Add a new piece:** model it, export it into the right `Environment/Bar/<Group>` folder, make a prefab, and drag it into `MoonlitBar.prefab`. Use `M_*` names for materials that already exist in `Environment/Materials`, so it matches the bar.

## Checking the bar

**Tools > Moonlit Tails > 8. Check Bar Pieces** (changes nothing) checks that:
- every object in the bar is a piece prefab, a marker or a light, with no loose meshes;
- every material is one of the shared bar materials;
- room pieces have colliders, and props have a collider, physics, grabbing and respawn;
- `BarLayout` knows every prop;
- there are no missing scripts or duplicate names.

Run it after editing the bar.

## How the split was checked

**6. Assemble Split Bar** swapped every piece for its prefab in the scene. Before saving, it compared each piece before and after:
- all 168 pieces are at the same position and size (0.0 mm difference);
- all have the same triangle count;
- 17 pieces have a slightly different vertex count, because Blender 5.2 splits vertices at sharp edges a little differently from 4.4. The triangles are the same.

Player-view renders before and after are identical, apart from random customer lines and props settling. The tool refuses to save if anything moved.
