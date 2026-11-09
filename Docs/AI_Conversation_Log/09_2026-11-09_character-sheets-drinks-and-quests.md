# Character sheets, their drinks and quests  (2026-11-09)

[← All conversations](README.md)

*Claude Code, in a cloud session on the repo.*


### User  ·  2026-11-09

@"[local path]\Sabre.md" @"[local path]\Ember.md" @"[local path]\Vex.md" @"[local path]\Cloudy.md" We need to add these to the documentation. These are the first four characters that we have actually made. I believe we have some currently placeholder characters, like randomly generated ones, and like we do not want those anymore. Also, review documentation in general, especially for drinks. There should be a file with like drinks and characters or something. You're gonna have to review that, please.

*[4 character sheets attached: Sabre, Ember, Vex, Cloudy]*


### Claude

- *[Bash: Check repo status and list non-asset files]*

- *[Bash: Read menu/characters and drink game docs]*

- *[Bash: Search for drinks file, quest guide, placeholder names]*

- *[Bash: Search docs for drink-system terms and read README]*

- *[Bash: List scripts and read CustomerScripts.cs]*

I've read the four sheets and the current docs; now checking the drink code and character models so the docs match what the game actually does.

- *[Bash: Read drink rule source files]*

- *[Bash: Read order generator and DrinkGame tables]*

- *[Bash: Find mentions of character models in logs]*

- *[Bash: Find model descriptions of Character01-03]*

- *[Bash: Inspect character material names and Character02 descriptions]*

Next I'm checking the recipe tables in the docs against the code, and the garnish and glass props in the scene.

- *[Bash: Read layering rules and list glass/garnish props]*

- *[Bash: Verify recipe table against ingredient stats]*

- *[Bash: Find character references in other docs]*

The docs for the current drink system are accurate (all 36 recipe rows match the code). The bigger issue is that the four character sheets were written against a different drink system. Now writing the character pages.

- *[Write: Docs/Characters/Sabre.md]*

- *[Write: Docs/Characters/Ember.md]*

- *[Write: Docs/Characters/Vex.md]*

- *[Write: Docs/Characters/Cloudy.md]*

- *[Edit: Docs/Characters/Sabre.md]*

- *[Write: Docs/Characters/TEMPLATE.md]*

Now the main page: replacing the placeholder characters and adding the drink gap list.

- *[Bash: Replace the characters section in the menu page]*

- *[Bash: Update intro, drink game, README and lab setup references]*

- *[Bash: Add the drinks-file mismatch note to the rules]*

Everything's written. Committing and pushing.

- *[Bash: Commit doc changes and push the branch]*

I've added the four characters to the docs and taken the placeholder ones out. The docs review turned up one big problem: **none of the characters' usual drinks can be made in the game right now.** Everything is pushed.

**What changed**
- **`Docs/Characters/`** now has one page each for Sabre, Ember, Vex and Cloudy. They hold everything from the sheets, in the same style as the other docs. I fixed only obvious typos ("Vix" → Vex, "Greeting" → Greetings, "come in terms" → come to terms). There's also a blank `TEMPLATE.md` to copy for new characters.
- **`MENU_AND_CHARACTERS.md`** (the drinks-and-characters page): Kintsu, "Fox" and "Guest" are gone. In their place is a summary of the four characters, their usual drinks, their quest answers, and the characters they name who don't have a sheet yet (Razor, Sniper, Fiza, Koda).
- `DRINK_GAME.md`, `LAB_SETUP.md` and the README now link to the new pages.

**The drinks problem**
- **Different taste system:** the sheets say to use Sweet / Sour / Strong / Fizzy and "see the drinks file". That file isn't in the repo. The game was built from the drink crafting guide, which uses Bitter / Sweet / Sour, so Strong and Fizzy (used in every quest answer) don't exist.
- **Different ingredients:** every recipe in the game starts with whiskey, bourbon or tequila, and Ember is about 16. Nothing on the sheets exists yet: cold brew, ginger, strawberry or chocolate syrup, cranberry, milk, cream, warm drinks. The new "Drinks the characters ask for" section lists each one and the closest thing the game has.
- **Rest of the drink docs:** these are accurate. I checked all 36 recipe rows, the glasses, the garnishes and the rules against the code.

**What I didn't change:** the game itself. The scene still has the three placeholder customers, with their made-up lines in `CustomerScripts.cs`, and the docs now say that.

**Decisions for you:**
1. **Which drink system wins?** If you send me the drinks file, I can update the ingredient code to match it. Otherwise the sheets need rewriting with the game's ingredients.
2. **Which 3D model is which character?** I need this before I can put the real characters into the game.
3. **Story clashes:** each page has a "To check" list. The main ones:
   - Vex's and Cloudy's quests happen at the same showcase, and their endings can contradict each other.
   - Cloudy "never has specific preferences" but hates anything cold.
   - Ember's Sweet ending means she leaves the game for good.
   - One of Sabre's lines is unclear: "give me to strike me to the cord".

I haven't added this conversation to the AI conversation log; I can if you want it there.


### User  ·  2026-11-09

Okay, so we need to make them, first of all, adults. Second of all, we need to make up drinks that are at least similar but still alcoholic. Please leave story clashes alone for now. That is not your domain.


### Claude

Got it: all four become adults, and their drinks get rebuilt as alcoholic drinks made from the game's ingredients. First I'm checking how the mixing code adds drinks together, so the new recipes really work in the game.

- *[Bash: Read mixing rules and test harness]*

- *[Bash: Check whether a .NET 10 SDK package is available]*

- *[Bash: Refresh apt and look for .NET SDK packages]*

- *[Bash: Install .NET 10 SDK and run the existing rules tests]*

The rules tests run here now (57 pass). Next I'm designing the four drinks from the game's ingredients and adding tests that make each one with the real rules.

- *[Bash: Show the end of the test program]*

- *[Edit: Tests/DrinkRules.Tests/Program.cs]*

- *[Edit: Tests/DrinkRules.Tests/Program.cs]*

- *[Bash: Run rules tests and list the mix steps for each drink]*

All 73 tests pass, including the 16 new ones for the character drinks. Now updating the character pages and the main page.

- *[Bash: Rewrite drinks, ages and check lists in character pages]*

- *[Bash: Add the drinks section and update links and the template]*

- *[Bash: Review the characters section and rerun tests]*

- *[Bash: Commit and push the drink and age changes]*

All four characters are adults now, and each has an alcoholic version of their usual drink. I built every drink only from the ingredients already in the game, so they can all be made today. I added tests that make each drink step by step with the game's real rules, and all 73 tests pass.

**Ages:** Ember goes from 16 to about 21, and Cloudy from 18 to about 21. They keep their playful, kid-like energy. Sabre is 22 and Vex is 20, so I left them alone. Vex is an adult, but under the US drinking age of 21, if that matters for you.

**The new drinks:**

| Drink | For | Recipe | Served | In place of |
|---|---|---|---|---|
| **Neon Storm** | Sabre | Whiskey + Espresso + Cola | Mug, cold, cherry | A spiked cold brew, with cola instead of ginger syrup |
| **Spirit Order** | Ember | A red layer (Bourbon + Grenadine) under a dark layer (Bourbon + Dark Caramel) | Coupe, cherry | Cranberry, strawberry and chocolate; layered because she loves bright colours |
| **Golden Standard** | Vex | Tequila + Lime Juice + Orange Peel + Syrup | Martini glass, cold, orange slice | Basically a margarita (the Orange Peel prop is the Triple Sec bottle) |
| **Cozy Cup** | Cloudy | Bourbon + Dark Caramel + Syrup | Mug, no ice, mint | A bourbon hot chocolate (Dark Caramel's prop is the milk carton) |

The characters page has a new "Their drinks" section. It gives each recipe, says what it replaces, and lists each character's loves and hates in game terms (Vex hates the Mug, Cloudy hates Ice, and so on).

**Lines I changed:** each character's "easy" order now names the new ingredients. All other dialogue is unchanged.

**Stories:** I didn't touch any story or quest text, and I took my story notes off the character pages.

**Still missing from the game**, listed on the page:
- The quests are answered with Strong or Fizzy drinks, but the game's only tastes are Bitter, Sweet and Sour.
- Drinks can be made cold but not warm, so Cloudy's Cozy Cup is just served without ice.
- There are no toppings like whipped cream or marshmallows.


### User  ·  2026-11-09

were in europe... so 16 to 18 and 18 to 18 stays... fix the quests part from what you can infer, and dont mind the missing toppings, then add this chat anonymized and commit and push everything asap


### Claude

- *[Bash: Read AI log index and an example log format]*

Doing the age and quest fixes first, then the chat log.

- *[Bash: Set ages to 18 and map quest answers to game tastes]*

- *[Bash: Trim trailing space in Ember's quest note]*

Now writing the anonymized chat log.

- *[Write: Docs/AI_Conversation_Log/09_2026-11-09_character-sheets-drinks-and-quests.md]*

- *[Edit: Docs/AI_Conversation_Log/README.md]*

- *[Bash: Run the rules tests, commit and push]*

Done and pushed.

- **Ages:** Ember and Cloudy are both about 18 now. Sabre (22) and Vex (20) are unchanged.
- **Quests:** each one is answered with the next drink you serve, and its highest taste picks the answer. The sheets' Strong is Bitter in the game (the "kick"), and Vex's Fizzy is folded into Sweet. Any other taste, or a tie between the two answers, gives the third ending. The quest lines and outcomes themselves are unchanged.
- **Toppings:** removed from the "still missing" list.
- **Chat log:** this conversation is entry 9 in the AI conversation log, with local paths replaced.
