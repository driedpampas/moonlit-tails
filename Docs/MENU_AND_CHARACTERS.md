# Ingredients, recipes and characters

Everything the game has so far. The numbers come from the code (`Scripts/Drinks/Ingredient.cs`), so if they change there, update this page. The rules for combining them are in [DRINK_GAME.md](DRINK_GAME.md).

## Ingredients

Every ingredient has three taste stats: Bitter, Sweet and Sour. One pour is 1 unit. Until the bar gets its own models, each ingredient uses an existing bottle or carton.

| Ingredient | Type | Bitter | Sweet | Sour | Prop in the bar |
|---|---|---:|---:|---:|---|
| Whiskey | Base | +1 | 0 | 0 | Rum bottle |
| Bourbon | Base | 0 | +1 | 0 | Vodka bottle |
| Tequila | Base | 0 | 0 | +1 | Tequila bottle |
| Tonic Water | Bitter | +1 | 0 | 0 | Tonic carton |
| Orange Peel | Bitter | +2 | 0 | +1 | Triple Sec bottle |
| Espresso | Bitter | +2 | -1 | 0 | Gin bottle |
| Black Tea | Bitter | +3 | -1 | -1 | Violet bottle |
| Syrup | Sweet | 0 | +1 | 0 | Peach Syrup bottle |
| Cola | Sweet | +1 | +2 | 0 | Cola carton |
| Dark Caramel | Sweet | 0 | +2 | -1 | Milk carton |
| Grenadine | Sweet | -2 | +3 | 0 | Grenadine bottle |
| Lime Juice | Sour | 0 | 0 | +1 | Soda carton |
| Apple Cider | Sour | 0 | +1 | +2 | Juice carton |
| Grapefruit Acid | Sour | 0 | -1 | +2 | Melon Liqueur bottle |
| Sour Mix | Sour | -1 | -1 | +3 | Blue Curaçao bottle |

**Extras.** Ice makes a drink Cold. Garnishes go on the rim: Blueberry, Cherry, Lemon, Lime, Mint, Orange.

**Glasses.** Short holds 3 units (Shot). Medium holds 4 (Rocks, Mug, Coupe). Long holds 5 (Highball, Martini, Wine).

## Recipes

A recipe starts as a base plus one modifier: 3 × 12 = 36 recipes. Stir when both have the same density (their highest stat), shake when they don't. Every recipe below can then be:

- served **mixed** from the Shake Mixer, with 1 adjustment point at the end;
- **layered** from the Stir Cup, scoring the per-unit average × 1.5 rounded up, with 2 adjustment points at the end;
- extended with more modifiers, as long as it fits the glass.

Customer orders are made from these recipes, so every order can be made.

### Whiskey

| Recipe | Bitter | Sweet | Sour | Mix |
|---|---:|---:|---:|---|
| Whiskey + Tonic Water | +2 | 0 | 0 | stir |
| Whiskey + Orange Peel | +3 | 0 | +1 | shake |
| Whiskey + Espresso | +3 | -1 | 0 | shake |
| Whiskey + Black Tea | +4 | -1 | -1 | shake |
| Whiskey + Syrup | +1 | +1 | 0 | stir |
| Whiskey + Cola | +2 | +2 | 0 | shake |
| Whiskey + Dark Caramel | +1 | +2 | -1 | shake |
| Whiskey + Grenadine | -1 | +3 | 0 | shake |
| Whiskey + Lime Juice | +1 | 0 | +1 | stir |
| Whiskey + Apple Cider | +1 | +1 | +2 | shake |
| Whiskey + Grapefruit Acid | +1 | -1 | +2 | shake |
| Whiskey + Sour Mix | 0 | -1 | +3 | shake |

### Bourbon

| Recipe | Bitter | Sweet | Sour | Mix |
|---|---:|---:|---:|---|
| Bourbon + Tonic Water | +1 | +1 | 0 | stir |
| Bourbon + Orange Peel | +2 | +1 | +1 | shake |
| Bourbon + Espresso | +2 | 0 | 0 | shake |
| Bourbon + Black Tea | +3 | 0 | -1 | shake |
| Bourbon + Syrup | 0 | +2 | 0 | stir |
| Bourbon + Cola | +1 | +3 | 0 | shake |
| Bourbon + Dark Caramel | 0 | +3 | -1 | shake |
| Bourbon + Grenadine | -2 | +4 | 0 | shake |
| Bourbon + Lime Juice | 0 | +1 | +1 | stir |
| Bourbon + Apple Cider | 0 | +2 | +2 | shake |
| Bourbon + Grapefruit Acid | 0 | 0 | +2 | shake |
| Bourbon + Sour Mix | -1 | 0 | +3 | shake |

### Tequila

| Recipe | Bitter | Sweet | Sour | Mix |
|---|---:|---:|---:|---|
| Tequila + Tonic Water | +1 | 0 | +1 | stir |
| Tequila + Orange Peel | +2 | 0 | +2 | shake |
| Tequila + Espresso | +2 | -1 | +1 | shake |
| Tequila + Black Tea | +3 | -1 | 0 | shake |
| Tequila + Syrup | 0 | +1 | +1 | stir |
| Tequila + Cola | +1 | +2 | +1 | shake |
| Tequila + Dark Caramel | 0 | +2 | 0 | shake |
| Tequila + Grenadine | -2 | +3 | +1 | shake |
| Tequila + Lime Juice | 0 | 0 | +2 | stir |
| Tequila + Apple Cider | 0 | +1 | +3 | shake |
| Tequila + Grapefruit Acid | 0 | -1 | +3 | shake |
| Tequila + Sour Mix | -1 | -1 | +4 | shake |

## Characters

Three customers so far. Each one tells one line of their story for every right drink. **The lines are placeholder writing** (in `Scripts/Runtime/Gameplay/CustomerScripts.cs`), to be replaced with the real stories.

| Name in game | Model | Who | Story so far |
|---|---|---|---|
| Kintsu | `Characters/Kintsu` | A white cat with gold-seamed ears. Warm and polite. | Kintsu's grandmother mended broken cups with gold. Kintsu broke the grandmother's favourite teacup and is ready to fix it, gold seams and all. |
| Fox | `Characters/Character03` | A grey fox in a punk jacket, plays in a band. Blunt. | The drummer quit again. The new one "plays like a thunderstorm in a teacup", and the band has a gig on Friday. |
| Guest | `Characters/Character02` | Quiet, sits at the table by the jukebox. Formal. | Used to come here with someone who moved to the city by the sea. A first letter in a year arrives, and Guest decides to write back. |

What they say when a drink is right or wrong, and their greetings, are in the same file.
