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
| [Ember](Characters/Ember.md) | Fox | Warm brown, red, cream | Keeps the soul shrine and talks with spirits, about 16. Silly, proud, playful. | "Hayo, my name's Ember, and I'm the one and only spirit caster in town!" | Her flying-carpet spell keeps failing. Does she ask the spirit grandmaster at the mausoleum for help? |
| [Vex](Characters/Vex.md) | Cat | Navy blue, gold | Fashion critic, about 20. Proud, ambitious, confident, very formal. | "Greetings, (player name). Make it your best today." | Guest of honour at the fashion showcase, and nobody has congratulated him. Keep his style, or go casual? |
| [Cloudy](Characters/Cloudy.md) | Fox | Cream, white, teal | The local baker, about 18. Cozy, gentle, shy. | "Hello! May I please know if this seat is taken?" | She's baked something for Vex's showcase. Give it to him herself, or leave it as a secret? |

Their usual drinks:

| Character | Usual | Glass | Loves | Hates |
|---|---|---|---|---|
| Sabre | Cold brew + ginger syrup + crushed ice | Mug | Strong kicks | Strawberry syrup, anything with lemon |
| Ember | Cranberry juice + strawberry syrup + chocolate syrup | Stemmed | Tiny particles, vibrant colours | Plain water, bitter drinks |
| Vex | Lemon juice + orange juice + simple syrup | Stemmed | Stemmed glasses, gold hues | Thick mugs, milk, cream |
| Cloudy | Milk + chocolate syrup + cream, warm | Mug | Warm mugs, whipped cream | Sour lemon juice, ice, anything cold |

Each quest is answered with a drink's taste:

| Character | Answers |
|---|---|
| Sabre | Strong or Sweet |
| Ember | Sweet or Sour |
| Vex | Strong, or Sweet / Fizzy |
| Cloudy | Strong or Sweet |

**Named, but no sheet yet:** Razor and Sniper (Sabre's bandmates), Fiza and Koda (in Vex's relationships).

### In the game right now

The scene still has the three old **placeholder** customers, with made-up lines in `Scripts/Runtime/Gameplay/CustomerScripts.cs`: Kintsu, "Fox" (model `Character03`) and "Guest" (model `Character02`). They aren't part of the game any more and will be replaced by the characters above. Until then:

- Which 3D model belongs to which character isn't decided yet.
- Customers order random drinks from the recipes above, not their usual.
- There's no gift, reaction or quest system yet: a right drink just plays the next story line.

## Drinks the characters ask for

**None of the four usual drinks can be made in the game yet.** The character sheets say to use "only Sweet / Sour / Strong / Fizzy and how the drink looks" from a drinks file. That file isn't in the repo, and the game was built from the drink crafting guide instead, which uses Bitter / Sweet / Sour and spirits as the base. The two have to be brought together: either the ingredient list in `Scripts/Drinks/Ingredient.cs` (and this page) changes to match the drinks file, or the drinks on the sheets are rewritten with the ingredients above.

| On the sheets | Who | In the game now |
|---|---|---|
| **Strong** and **Fizzy** tastes | All four quests | No. The tastes are Bitter, Sweet and Sour. |
| A drink with no spirit in it | All four | No. Every recipe starts with Whiskey, Bourbon or Tequila. Ember is about 16. |
| Cold brew | Sabre | No. Espresso is the closest. |
| Ginger syrup, strawberry syrup, chocolate syrup | Sabre, Ember, Cloudy | No. There's one plain Syrup. |
| Simple syrup | Vex | Yes: Syrup. |
| Lemon juice | Vex, and Sabre and Cloudy hate it | No. Lime Juice is the closest. Lemon is only a garnish. |
| Orange juice | Vex | No. Orange Peel is a bitter modifier, and Orange is a garnish. |
| Cranberry juice | Ember | No. |
| Milk, cream | Cloudy, and Vex hates them | No. The Milk carton is Dark Caramel for now. |
| Whipped cream, marshmallows | Cloudy | No. There are no toppings. |
| Crushed ice | Sabre | Partly. Ice makes a drink Cold. There's no crushed or cubed. |
| Warm or hot | Cloudy | No. A drink can only be made Cold. |
| Mug | Sabre, Cloudy | Yes. |
| Stemmed glass | Ember, Vex | Partly. Coupe, Martini and Wine are stemmed, but orders ask for one exact glass, not "any stemmed". |
| Gold or orange colour | Vex | Partly. Colour comes only from the garnish (Orange). |
| Tiny particles, vibrant colours | Ember | Partly. Colour from the garnish only, no particles. |
