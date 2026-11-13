# Performance on the Quest 2

What was slow, what changed (13 Nov 2026), and what's left. The target is 90 fps on a Quest 2 (11.1 ms per frame); 72 fps (13.9 ms) is the headset's default.

**The fps figures here are estimates.** Nothing has been measured on a headset yet. The draw calls, batches, triangles and light counts are measured: Unity's own render statistics, in Play mode, from the player's eye at the Quest 2's eye resolution (1832 × 1920), looking at the customer.

## What was slow

| Suspect | Finding |
|---|---|
| Too many polygons (the scenery outside the windows) | **Not the problem.** The whole scene is 132,000 triangles and the night sky is 4,300 of them. A Quest 2 handles several times that. |
| Lighting | **The main cost.** 21 realtime lights with the Forward+ renderer: every pixel was lit by every light near it, every frame, for both eyes. |
| Draw calls | 337 looking at the customer, none batched. The fixed bar pieces had every static flag except *Batching Static*, so static batching (on in Player Settings) did nothing. |
| Shaders | Metal and glass used URP *Lit* (the most expensive URP shader) on 25 materials. |
| Characters | All three customers were drawn, skinned on the CPU and had their spring bones solved all the time: about 35 materials and 70 draw calls. |
| Labels | Every bottle and glass label near the player was drawn, each up to 18 small pieces. |

## What changed

| Fix | Measured | Estimated gain on a Quest 2 |
|---|---|---|
| **Baked lighting.** The 20 point and spot lights are baked into 2 lightmaps (1024 px). Light probes carry the light to props, hands and customers. The moonlight stays realtime. The renderer is plain *Forward* with additional lights off. | Realtime lights: 21 → 1 | about 9–13 ms of GPU time per frame |
| **One customer at a time** (the new game loop). The others are switched off. | Skinned characters drawn: 3 → 1 | about 1.5–2.5 ms of CPU time |
| **Static batching** switched on for the 248 fixed pieces, and **labels** show only when looked at. | Batches: 337 → 202. Shader switches (SetPass): 89 → 53 | about 1–1.5 ms of CPU time |
| **Simple Lit** in place of Lit on the 25 metal, glass and liquid materials. | | about 0.5–1 ms of GPU time |

Looking at the customer, before and after:

| | Before | After |
|---|---:|---:|
| Draw calls | 337 | 247 |
| Batches | 337 | 202 |
| SetPass calls | 89 | 53 |
| Triangles | 102,000 | 58,800 |
| Realtime lights | 21 | 1 |
| Skinned characters | 3 | 1 |

**Estimated frame rate: about 40 fps before (36–50), about 90 fps after (72–90).** Before, the frame took an estimated 20–28 ms, so the headset would have dropped to half rate. After, it should take 8–11 ms. Check it on a headset with the OVR Metrics Tool before relying on this.

## If you change the bar

The lighting is baked, so the lightmaps go stale when a fixed piece or a light moves:
1. Open the scene, then **Window > Rendering > Lighting > Generate Lighting**. It takes under a minute. The settings are in `Settings/BarLighting.lighting`.
2. Commit the files in `Scenes/MoonlitBar_VR/` with the scene.

Props, customers and hands don't need a rebake. A new fixed piece needs lightmap UVs (*Generate Lightmap UVs* on its model) and the static flags the other pieces have, including *Batching Static* and *Contribute GI*.

## Still to do

- **Measure on a headset.** Everything above about fps is an estimate.
- **Draw calls from props.** The 61 grabbable props are now all near the player and have 2–4 materials each. Merging each prop's materials into one would cut about 60 more draw calls.
- **Customers.** Each has 10–13 materials and is drawn twice (the outline). One material per character would cut about 20 draw calls.
- **GPU skinning** is off. Try it on a Development build.
- **Glass** looks a little milkier with the baked light. Lower the alpha of the `M_Glass_*` materials if it bothers you.
- **Colliders.** The fixed pieces use mesh colliders; boxes would be cheaper for walls and counters.
