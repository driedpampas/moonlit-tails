# Drink game

How drinks are made in Moonlit Tails, the rules as the team agreed them, and where things live in the code.

## Playing it

Press Play in `MoonlitBar_VR.unity`. The drink game adds itself to the scene, so the scene file doesn't need to change.

1. **Read a ticket** on the ticket rail. Each ticket belongs to one customer and shows:
   - the three taste bars to hit;
   - Mixed or Layered;
   - the glass;
   - Cold and a garnish colour, if wanted.
2. **Pour ingredients.** Tip a bottle or carton over the **Stir Cup** (the blender jar) or the **Shake Mixer** (the shaker tin). One tip is 1 unit. Tilt it back up to pour again.
3. **Mix.** With two things in, combine them:
   - if their densities are the same, **stir** by swirling the bar spoon inside;
   - if they differ, **shake** by holding the cup and shaking it back and forth.

   The wrong method does nothing. The same ingredient can't go in twice.
4. **Fill the glass.**
   - **Mixed:** pour the Shake Mixer into the glass. The whole drink goes in at once.
   - **Layered:** pour from the Stir Cup. Each tip adds one layer. A 2-unit drink gives 2 layers.
   - A mixed drink can also go from one vessel into the other, to combine it with more.
5. **Finish the glass.**
   - Dip the ice scoop in the ice well, then bring it to the glass: the drink becomes **Cold**.
   - Let go of a fruit slice near the rim: it sticks there and gives the drink its **colour**.
6. **Serve.** Put the glass on the serve coaster. Let go of it close to or just above the coaster and it snaps on, upright and in the middle, and stays there until you pick it up.
   - The panel next to it shows the final taste and has **+ / −** buttons for the end-of-drink adjustment: 1 point for a mixed drink, 2 for a layered one.
   - Ring the bell (touch or grab it), or press **Serve**.
   - A right drink: the customer says the next line of their story.
   - A wrong drink: they say what's off, and their ticket shows a tip.

To empty anything, tip it out over nothing. The big red **DO NOT PUSH** button on the front counter (left corner, next to the glass rack) puts every prop back where it started, even one in your hand or behind the bar. What's in the glasses and cups stays. **R** on the keyboard does the same.

The rules are on a sign on the back wall right behind you, where the round cocktail logo was, so you can read them from behind the bar. The logo is only hidden while playing: it's still in the bar prefab. The chalkboard on the far wall shows the same rules.

Labels float over the bottles, cups and glasses when you're close. Buttons work with a fingertip, or with a mouse click when you use the XR Device Simulator.

## Rules

Based on the drink crafting guide, with the team's answers to the open questions (6 Nov 2026).

Each character's usual drink is made with these rules: see [Their drinks](MENU_AND_CHARACTERS.md#their-drinks).

| Rule | Value |
|---|---|
| Stats | Bitter, Sweet and Sour. They can be negative. |
| Base Value (density) | The highest single stat. |
| Pour | 1 unit. Mixing adds up the units of both parts. |
| Self-mixing | The same ingredient can't be mixed with itself. |
| Stir or shake | Equal densities need a stir. Different densities need a shake. The wrong one does nothing. Adding the base drink at the end follows the same rule, so it isn't always shaken. |
| Layers | Only prepared drinks (a base plus at least one modifier) can be layered. The same drink poured on itself makes that layer thicker. |
| Layer collapse | Touching layers of *different* drinks collapse if their densities are equal, or if they share fewer than **2** positive points. Only those two layers become 0, but they still count as units. |
| Layered score | The per-unit average of all pours × 1.5, rounded **up**: ceil toward +∞, so −1.5 becomes −1. For example, (2A + B) / 3 × 1.5. |
| Glasses | Short = 3 units (Shot). Medium = 4 (Rocks, Mug, Coupe). Long = 5 (Highball, Martini, Wine). |
| Adjustment | Mixed: 1 point. Layered: 2 points in total. Applied at the very end. |
| Same stats, different recipes | They count as different drinks. Several recipes can make the same order. |

Two choices aren't in the guide yet. Check them with the designers:
- A layer pour is 1 unit, even though the drink in the Stir Cup holds 2 or more units.
- The Shake Mixer always gives a mixed drink, and the Stir Cup always gives layers.

## Where things live

| Folder or file | What |
|---|---|
| `Unity/Assets/MoonlitTails/Scripts/Drinks/` | The rules as plain C# with no Unity dependency: ingredients, mixing, layering, glasses, adjustments, order checking and the order generator. |
| `Unity/Assets/MoonlitTails/Scripts/Runtime/Gameplay/` | The VR side: pouring, containers, the stir spoon, ice, garnish, the serve station, orders and labels. |
| `Gameplay/DrinkGame.cs` | **The tables to edit**: which prop is which ingredient, and the glass lengths. |
| `Gameplay/CustomerScripts.cs` | What each customer says. **This is placeholder writing** for the old placeholder customers: replace it with the real characters in [Docs/Characters](Characters/). |
| `Tests/DrinkRules.Tests/` | Rules tests, including the characters' usual drinks: `dotnet run`. |

**Ingredients and props.** The bar has cocktail bottles, so for now each ingredient is assigned to an existing prop:

| Ingredient | Prop |
|---|---|
| Whiskey | Rum bottle |
| Bourbon | Vodka bottle |
| Tequila | Tequila bottle |
| Tonic Water | Tonic carton |
| Orange Peel | Triple Sec bottle |
| Espresso | Gin bottle |
| Black Tea | Violet bottle |
| Syrup | Peach Syrup bottle |
| Cola | Cola carton |
| Dark Caramel | Milk carton |
| Grenadine | Grenadine bottle |
| Lime Juice | Soda carton |
| Apple Cider | Juice carton |
| Grapefruit Acid | Melon Liqueur bottle |
| Sour Mix | Blue Curaçao bottle |

The label over each prop shows the ingredient's name. Swap the models later, or change the table in `DrinkGame.cs`.

## Testing

- **Rules:** `cd Tests/DrinkRules.Tests && dotnet run`. If the .NET workload resolver is broken on your machine, run `MSBuildEnableWorkloadResolver=false dotnet run` instead.
- **Tools > Moonlit Tails > 4. Check Drink Setup:** lists which prop got which role, and warns about anything missing.
- **Tools > Moonlit Tails > Automation > Play Smoke Test:** plays the scene and makes drinks by moving the real props: pours, mixing, stirring, a mixed and a layered glass, ice, garnish, adjustments, serving, the coaster snap and the reset button.
- **Headless:** run the smoke test from a terminal. Exit code 0 means it passed.
  ```
  Unity -batchmode -nographics -projectPath Unity -executeMethod MoonlitTails.EditorTools.DrinkPlaySmokeTest.RunBatch -logFile -
  ```

  This needs the editor to be closed. It switches off the XR Interaction Simulator, because the simulator's menu crashes without a display. The scene is never saved.
