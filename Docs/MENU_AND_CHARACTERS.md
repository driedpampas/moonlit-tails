# Ingredients, recipes and characters

Everything the game has so far, and the characters the team has written. The ingredient numbers come from the code (`Scripts/Drinks/Ingredient.cs`), so if they change there, update this page. The rules for combining them are in [DRINK_GAME.md](DRINK_GAME.md).

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

The first four characters, from the team's character sheets. Each one has a page with everything on their sheet: voice, drinks, gift, reactions, story and quest. To add a character, copy [the blank sheet](Characters/TEMPLATE.md).

| Name | Animal | Colours | Who | Hello | Story |
|---|---|---|---|---|---|
| [Sabre](Characters/Sabre.md) | Wolf | Grey, hot pink, black | Underground rock musician, about 22. Edgy, fierce, passionate. Says very little. | "Hit me up with something good." | Her bandmates Razor and Sniper won't stop arguing. Does she talk to them, or let them sort it out? |
| [Ember](Characters/Ember.md) | Fox | Warm brown, red, cream | Keeps the soul shrine and talks with spirits, about 18. Silly, proud, playful. | "Hayo, my name's Ember, and I'm the one and only spirit caster in town!" | Her flying-carpet spell keeps failing. Does she ask the spirit grandmaster at the mausoleum for help? |
| [Vex](Characters/Vex.md) | Cat | Navy blue, gold | Fashion critic, about 20. Proud, ambitious, confident, very formal. | "Greetings, (player name). Make it your best today." | Guest of honour at the fashion showcase, and nobody has congratulated him. Keep his style, or go casual? |
| [Cloudy](Characters/Cloudy.md) | Fox | Cream, white, teal | The local baker, about 18. Cozy, gentle, shy. | "Hello! May I please know if this seat is taken?" | She's baked something for Vex's showcase. Give it to him herself, or leave it as a secret? |

Each quest is answered with the next drink you serve after the question. Its highest taste picks the answer. If that taste is neither answer, or the two answers tie, the character gets their third ending. The sheets said Strong, which is Bitter in the game (the "kick"), and Vex's Fizzy is folded into Sweet.

| Character | Answers | Third ending |
|---|---|---|
| Sabre | Bitter or Sweet | Sour, or a tie |
| Ember | Sweet or Sour | Bitter, or a tie |
| Vex | Bitter or Sweet | Sour, or a tie |
| Cloudy | Bitter or Sweet | Sour, or a tie |

**Named, but no sheet yet:** Razor and Sniper (Sabre's bandmates), Fiza and Koda (in Vex's relationships).

### In the game right now

The scene still has the three old **placeholder** customers, with made-up lines in `Scripts/Runtime/Gameplay/CustomerScripts.cs`: Kintsu, "Fox" (model `Character03`) and "Guest" (model `Character02`). They aren't part of the game any more and will be replaced by the characters above. Until then:

- Which 3D model belongs to which character isn't decided yet.
- Customers order random drinks from the recipes above, not their usual.
- There's no gift, reaction or quest system yet: a right drink just plays the next story line.

## Their drinks

Each character's usual, made only from the ingredients above, so every one can be made in the game today. They're written to stay close to what the character sheets asked for. The rules tests (`Tests/DrinkRules.Tests`) make each one step by step and check the result.

| Drink | For | Recipe, in pouring order | Taste (Bitter / Sweet / Sour) | Served |
|---|---|---|---|---|
| **Neon Storm** | [Sabre](Characters/Sabre.md) | Whiskey, Espresso (shake), Cola (shake) | +4 / +1 / 0 | Mixed, Mug, Cold, Cherry |
| **Spirit Order** | [Ember](Characters/Ember.md) | Layer 1: Bourbon, Grenadine (shake). Layer 2: Bourbon, Dark Caramel (shake). | −1 / +6 / 0 | Layered, Coupe, Cherry |
| **Golden Standard** | [Vex](Characters/Vex.md) | Tequila, Lime Juice (stir), Orange Peel (stir), Syrup (shake) | +2 / +1 / +3 | Mixed, Martini, Cold, Orange |
| **Cozy Cup** | [Cloudy](Characters/Cloudy.md) | Bourbon, Dark Caramel (shake), Syrup (shake) | 0 / +4 / −1 | Mixed, Mug, Mint |

How each one replaces the drink on the sheet:

| Drink | On the sheet | Now |
|---|---|---|
| Neon Storm | Cold brew + ginger syrup + crushed ice, in a mug | A spiked cold brew. Espresso for the cold brew, Cola for the ginger kick, Ice for the crushed ice. |
| Spirit Order | Cranberry juice + strawberry syrup + chocolate syrup, in a stemmed glass | Grenadine for the red fruit, Dark Caramel for the chocolate. Layered, because she loves vibrant colours. |
| Golden Standard | Lemon juice + orange juice + simple syrup, in a stemmed glass | A margarita. Lime Juice for the lemon, Orange Peel (the Triple Sec bottle) for the orange, and the same Syrup. |
| Cozy Cup | Milk + chocolate syrup + cream, in a warm mug | A bourbon hot chocolate. Dark Caramel (the milk carton) for the milk and chocolate, Syrup for the extra sweetness, and no ice. |

What they love and hate, in game terms:

| Character | Loves | Hates |
|---|---|---|
| Sabre | A high Bitter | Grenadine (closest to strawberry syrup), the Lemon garnish |
| Ember | Layered drinks, a garnish | Tonic Water (closest to plain water), any Bitter above 0 |
| Vex | Coupe, Martini, Wine (stemmed), the Orange garnish (gold) | The Mug, Dark Caramel (milk and cream) |
| Cloudy | The Mug | Lime Juice, Sour Mix and the Lemon garnish (sour lemon), Ice |

**Still missing from the game:**
- **Warm drinks:** a drink can be made Cold, but not warm. Cloudy's Cozy Cup is just served without ice for now.
- **Any stemmed glass:** orders ask for one exact glass, so "a stemmed glass" has to be a Coupe, Martini or Wine.
- **Particles:** colour comes only from the garnish.
