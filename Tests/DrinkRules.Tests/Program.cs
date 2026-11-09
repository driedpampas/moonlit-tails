using System;
using System.Collections.Generic;
using System.Linq;
using MoonlitTails.Drinks;
using static MoonlitTails.Drinks.Ingredients;

static class Program
{
    static int passed, failed;

    static void Check(bool ok, string name)
    {
        if (ok) passed++; else { failed++; Console.WriteLine("FAIL: " + name); }
    }
    static void Eq<T>(T expected, T actual, string name)
    {
        bool ok = EqualityComparer<T>.Default.Equals(expected, actual);
        if (!ok) Console.WriteLine($"  expected {expected}, got {actual}");
        Check(ok, name);
    }

    static IEnumerable<List<T>> Combos<T>(List<T> items, int k, int start = 0)
    {
        if (k == 0) { yield return new List<T>(); yield break; }
        for (int i = start; i <= items.Count - k; i++)
            foreach (var rest in Combos(items, k - 1, i + 1))
            {
                rest.Insert(0, items[i]);
                yield return rest;
            }
    }

    static Liquid L(Ingredient i) => Liquid.Of(i);
    // A prepared drink: base + one modifier, mixed with whichever method the rules require.
    static Liquid Drink(Ingredient b, Ingredient m)
    {
        var a = L(b); var c = L(m);
        return Mixing.Combine(a, c, Mixing.RequiredMethod(a, c)).Liquid;
    }

    // Pours the ingredients one by one, mixing after each pour with the method the rules require.
    static Liquid Make(string who, params Ingredient[] pours)
    {
        var liquid = L(pours[0]);
        foreach (var i in pours.Skip(1))
        {
            var add = L(i);
            var r = Mixing.Combine(liquid, add, Mixing.RequiredMethod(liquid, add));
            Check(r.Succeeded, $"{who}: adding {i.DisplayName} mixes");
            if (r.Succeeded) liquid = r.Liquid;
        }
        return liquid;
    }

    static int Main()
    {
        // --- Taste
        Eq(3, new Taste(1, 2, 3).BaseValue, "base value is the highest stat");
        Eq(1, new Taste(-1, -1, 1).BaseValue, "base value with negatives");
        Eq(3, Taste.SharedPositive(new Taste(-1, 3, 2), new Taste(2, 2, 1)), "guide example: valid layer shares 3");
        Eq(2, Taste.SharedPositive(new Taste(-2, 3, 1), new Taste(3, 1, 1)), "guide example: old failed layer shares 2");
        Eq(0, Taste.SharedPositive(new Taste(-1, 0, 0), new Taste(-1, 0, 0)), "negatives share nothing");

        // --- Rounding
        Eq(2, Layering.CeilDiv(3, 2), "ceil 1.5 = 2");
        Eq(-1, Layering.CeilDiv(-3, 2), "ceil -1.5 = -1 (toward +inf)");
        Eq(0, Layering.CeilDiv(0, 5), "ceil 0");
        Eq(-2, Layering.CeilDiv(-4, 2), "ceil -2 = -2");

        // --- Mixing
        var tonic = L(TonicWater); var syrup = L(Syrup); var peel = L(OrangePeel);
        Eq(MixMethod.Stir, Mixing.RequiredMethod(tonic, syrup), "equal base values need a stir");
        Eq(MixMethod.Shake, Mixing.RequiredMethod(tonic, peel), "unequal base values need a shake");
        var stirred = Mixing.Combine(tonic, syrup, MixMethod.Stir);
        Check(stirred.Succeeded && stirred.Liquid.Stats == new Taste(1, 1, 0), "tonic + syrup stirred = +1 bitter +1 sweet");
        Eq(MixOutcome.WrongMethod, Mixing.Combine(tonic, syrup, MixMethod.Shake).Outcome, "shaking a stir-only pair does nothing");
        Eq(MixOutcome.WrongMethod, Mixing.Combine(tonic, peel, MixMethod.Stir).Outcome, "stirring a shake-only pair does nothing");
        var shaken = Mixing.Combine(tonic, peel, MixMethod.Shake);
        Check(shaken.Succeeded && shaken.Liquid.Stats == new Taste(3, 0, 1), "tonic + orange peel shaken");
        Eq(MixOutcome.SelfMix, Mixing.Combine(tonic, tonic, MixMethod.Stir).Outcome, "no self-mixing");
        var whiskeyTonic = Drink(Whiskey, TonicWater);
        Eq(MixOutcome.SelfMix, Mixing.Combine(whiskeyTonic, tonic, MixMethod.Stir).Outcome, "no self-mixing with a drink already holding it");
        Eq(2, whiskeyTonic.Units, "units add up when mixing");
        Eq(3, Mixing.Combine(whiskeyTonic, L(LimeJuice), Mixing.RequiredMethod(whiskeyTonic, L(LimeJuice))).Liquid.Units, "three ingredients = 3 units");
        Check(whiskeyTonic.IsPrepared, "base + modifier is a prepared drink");
        Check(!L(Whiskey).IsPrepared && !tonic.IsPrepared, "raw ingredients are not prepared drinks");
        Eq(new Taste(2, 0, 0), whiskeyTonic.Stats, "whiskey + tonic stats");
        // order of mixing doesn't matter for the recipe identity
        Eq(Drink(Bourbon, Cola).RecipeKey, Mixing.Combine(L(Cola), L(Bourbon), Mixing.RequiredMethod(L(Cola), L(Bourbon))).Liquid.RecipeKey, "recipe key ignores order");

        // --- Layering
        var bourbonCola = Drink(Bourbon, Cola);     // (1,3,0)
        var whiskeyCola = Drink(Whiskey, Cola);     // (2,2,0)
        Check(Layering.Holds(bourbonCola.Stats, whiskeyCola.Stats), "different base values sharing 3 hold");
        Check(Layering.Holds(new Taste(0, 3, 0), new Taste(0, 2, 0)), "2 shared points hold now that the threshold is 2");
        Check(!Layering.Holds(new Taste(0, 3, 0), new Taste(0, 0, 0)), "1 or fewer shared points collapse");
        Check(!Layering.Holds(new Taste(0, 2, 0), new Taste(0, 2, 0)), "same base value collapses even when everything is shared");

        var r1 = Layering.Resolve(new[] { bourbonCola, whiskeyCola }, GlassLength.Short);
        Check(r1.Ok && r1.Stats == new Taste(3, 4, 0), "A,B = ceil(1.5 * avg) = (2.25,3.75,0) -> (3,4,0)");
        var r2 = Layering.Resolve(new[] { bourbonCola, bourbonCola, whiskeyCola }, GlassLength.Short);
        Check(r2.Ok && r2.Stats == new Taste(2, 4, 0), "A,A,B averages per unit: (2A+B)/3 * 1.5");
        var r3 = Layering.Resolve(new[] { whiskeyCola }, GlassLength.Short);
        Check(r3.Ok && r3.Stats == new Taste(3, 3, 0), "a single layer still gets x1.5");
        var sourWhiskey = Drink(Whiskey, SourMix); // (0,-1,3)
        var r4 = Layering.Resolve(new[] { sourWhiskey }, GlassLength.Short);
        Check(r4.Ok && r4.Stats == new Taste(0, -1, 5), "negative stats round up toward zero: -1.5 -> -1");

        // Only the two layers that fail collapse. A,B collapse (same base value); B,C would hold but B already collapsed.
        var a = Drink(Whiskey, TonicWater);    // (2,0,0)
        var b = Drink(Bourbon, Syrup);         // (0,2,0)
        var c = Drink(Bourbon, Cola);          // (1,3,0)
        var r5 = Layering.Resolve(new[] { a, b, c }, GlassLength.Short);
        Check(r5.Ok && r5.Collapsed[0] && r5.Collapsed[1] && !r5.Collapsed[2], "only the failing pair collapses");
        Eq(new Taste(1, 2, 0), r5.Stats, "collapsed layers count as 0 but still count as units");
        // A,B,C where only the middle pair fails: the third layer collapses along with the second, the first survives
        var r6 = Layering.Resolve(new[] { c, bourbonCola /*same drink, merges*/, whiskeyCola }, GlassLength.Short);
        Check(r6.Ok && !r6.Collapsed.Any(x => x), "same drink touching itself merges and does not collapse");
        // same drink again later on top of another layer is a separate layer and is checked against both neighbours
        var r7 = Layering.Resolve(new[] { bourbonCola, whiskeyCola, bourbonCola }, GlassLength.Short);
        Check(r7.Ok && !r7.Collapsed.Any(x => x), "A,B,A holds when each touching pair holds");

        Eq(LayerStatus.OverCapacity, Layering.Resolve(new[] { a, c, a, c }, GlassLength.Short).Status, "short glass holds 3 pours");
        Check(Layering.Resolve(new[] { bourbonCola, whiskeyCola, bourbonCola, whiskeyCola }, GlassLength.Medium).Ok, "medium glass holds 4 pours");
        Eq(LayerStatus.NotPrepared, Layering.Resolve(new[] { L(Cola) }, GlassLength.Long).Status, "raw ingredients can't be layered");
        Eq(LayerStatus.NotPrepared, Layering.Resolve(new[] { bourbonCola, L(Whiskey) }, GlassLength.Long).Status, "a bare base can't be layered");
        Eq(LayerStatus.Empty, Layering.Resolve(new Liquid[0], GlassLength.Long).Status, "empty glass");

        // Different recipes with the same stats are different drinks, so they collapse when touching.
        var d1 = Drink(Whiskey, Syrup);            // (1,1,0)
        var d2 = Drink(Bourbon, TonicWater);       // (1,1,0)
        Eq(d1.Stats, d2.Stats, "two recipes, same stats");
        var r8 = Layering.Resolve(new[] { d1, d2 }, GlassLength.Short);
        Check(r8.Ok && r8.Collapsed[0] && r8.Collapsed[1] && r8.Stats == Taste.Zero, "different recipes with equal stats collapse");

        // --- Glass
        Eq(3, Glass.Capacity(GlassLength.Short), "short = 3");
        Eq(5, Glass.Capacity(GlassLength.Long), "long = 5");
        Check(Glass.Fits(3, GlassLength.Short) && !Glass.Fits(4, GlassLength.Short), "mixing: units must fit the glass");

        // --- Adjustments
        Taste r;
        Check(Finishing.TryAdjust(new Taste(1, 1, 0), PrepMethod.Mixed, new Taste(0, 1, 0), out r) && r == new Taste(1, 2, 0), "mix bonus: +1 on one stat");
        Check(Finishing.TryAdjust(new Taste(1, 1, 0), PrepMethod.Mixed, new Taste(0, -1, 0), out r) && r == new Taste(1, 0, 0), "mix bonus: -1 on one stat");
        Check(!Finishing.TryAdjust(new Taste(1, 1, 0), PrepMethod.Mixed, new Taste(1, 1, 0), out r) && r == new Taste(1, 1, 0), "mix bonus: 2 points rejected");
        Check(Finishing.TryAdjust(new Taste(1, 1, 0), PrepMethod.Layered, new Taste(1, 0, -1), out r) && r == new Taste(2, 1, -1), "layer bonus: 2 points split");
        Check(!Finishing.TryAdjust(new Taste(1, 1, 0), PrepMethod.Layered, new Taste(2, 1, 0), out r), "layer bonus: 3 points rejected");

        // --- Orders
        var order = new DrinkOrder { Target = new Taste(1, 2, 0), Method = PrepMethod.Mixed, GlassVariant = "Rocks_01", Cold = true, Color = "Lime" };
        var good = new ServedDrink { Stats = new Taste(1, 2, 0), Method = PrepMethod.Mixed, GlassVariant = "Rocks_01", Cold = true, Color = "Lime" };
        Eq(Mismatch.None, Finishing.Check(order, good), "perfect order");
        good.Stats = new Taste(1, 2, 1);
        Eq(Mismatch.Taste, Finishing.Check(order, good), "stats must match exactly");
        good.Stats = order.Target; good.Cold = false; good.Color = "Orange"; good.Method = PrepMethod.Layered;
        Eq(Mismatch.Method | Mismatch.Cold | Mismatch.Color, Finishing.Check(order, good), "several mismatches at once");
        var anyGlass = new DrinkOrder { Target = Taste.Zero, Method = PrepMethod.Mixed };
        Eq(Mismatch.None, Finishing.Check(anyGlass, new ServedDrink { Stats = Taste.Zero, Method = PrepMethod.Mixed, GlassVariant = "Mug_02", Cold = true, Color = "Pink" }), "unrequested extras are fine");

        // --- Order generator: every order must be makeable with the rules (brute force check)
        var glasses = new[] { new GlassVariant("Shot", GlassLength.Short), new GlassVariant("Rocks", GlassLength.Medium), new GlassVariant("Highball", GlassLength.Long) };
        var colors = new[] { "Lime", "Cherry" };
        var prepared = Bases.SelectMany(x => Modifiers.Select(y => Drink(x, y))).ToList();
        var mixReach = new Dictionary<int, HashSet<Taste>>();   // capacity -> reachable mixed stats
        foreach (var cap in new[] { 3, 4, 5 })
        {
            var set = new HashSet<Taste>();
            foreach (var bs in Bases)
                for (int k = 1; k <= Math.Min(2, cap - 1); k++)
                    foreach (var combo in Combos(Modifiers.ToList(), k))
                    {
                        var t = bs.Taste; foreach (var m in combo) t = t + m.Taste; set.Add(t);
                    }
            mixReach[cap] = set;
        }
        var layerReach = new Dictionary<int, HashSet<Taste>>();
        foreach (var cap in new[] { 2, 3, 4, 5 })
        {
            var set = new HashSet<Taste>();
            foreach (var x in prepared) foreach (var y in prepared)
            {
                if (x.RecipeKey == y.RecipeKey || !Layering.Holds(x.Stats, y.Stats)) continue;
                foreach (var pat in new[] { new[] { x, y }, new[] { x, x, y }, new[] { x, y, x }, new[] { x, y, y } })
                    if (pat.Length <= cap) set.Add(Layering.Resolve(pat, GlassLength.Long).Stats);
            }
            layerReach[cap] = set;
        }
        var rng = new Random(1234);
        bool allOk = true; int layeredCount = 0, coldCount = 0, colorCount = 0;
        for (int i = 0; i < 2000; i++)
        {
            var o = OrderGenerator.Generate(rng, glasses, colors);
            var g = glasses.First(x => x.Name == o.GlassVariant);
            int cap = Glass.Capacity(g.Length);
            var reach = o.Method == PrepMethod.Mixed ? mixReach[cap] : layerReach[cap];
            int allowance = Finishing.AdjustmentPoints(o.Method);
            bool ok = reach.Any(t => Math.Abs(t.Bitter - o.Target.Bitter) + Math.Abs(t.Sweet - o.Target.Sweet) + Math.Abs(t.Sour - o.Target.Sour) <= allowance);
            if (!ok || string.IsNullOrEmpty(o.Hint)) { allOk = false; Console.WriteLine($"  unmakeable: {o.Target} {o.Method} {o.GlassVariant} ({o.Hint})"); break; }
            if (o.Method == PrepMethod.Layered) layeredCount++;
            if (o.Cold) coldCount++;
            if (o.Color != null) colorCount++;
        }
        Check(allOk, "2000 generated orders are all makeable");
        Check(layeredCount > 400 && layeredCount < 1200, $"layered share is sensible ({layeredCount}/2000)");
        Check(coldCount > 700 && colorCount > 700, "cold and garnish requests appear");
        var sample = OrderGenerator.Generate(new Random(7), glasses, colors);
        Console.WriteLine($"Sample order: {sample.Target} {sample.Method} in {sample.GlassVariant}, cold={sample.Cold}, color={sample.Color ?? "-"}; hint: {sample.Hint}");

        // --- Report on how reachable layering is at the current threshold
        var drinks = Bases.SelectMany(x => Modifiers.Select(y => Drink(x, y))).ToList();
        int pairs = 0, possible = 0;
        foreach (var x in drinks) foreach (var y in drinks)
        {
            if (x.RecipeKey == y.RecipeKey) continue;
            possible++;
            if (Layering.Holds(x.Stats, y.Stats)) pairs++;
        }
        int canLayer = drinks.Count(x => drinks.Any(y => y.RecipeKey != x.RecipeKey && Layering.Holds(x.Stats, y.Stats)));
        Console.WriteLine($"Layering at threshold {Layering.MinSharedPoints}: {pairs} of {possible} ordered pairs hold; {canLayer} of {drinks.Count} drinks have a partner.");

        // --- The characters' usual drinks (Docs/Characters, Docs/MENU_AND_CHARACTERS.md). Each is poured in this
        // order, mixing after every pour with the method the rules ask for, so these are the steps a player takes.
        var sabre = Make("Sabre", Whiskey, Espresso, Cola);
        Eq(new Taste(4, 1, 0), sabre.Stats, "Sabre's Neon Storm: Whiskey + Espresso + Cola");
        Check(Glass.Fits(sabre.Units, GlassLength.Medium), "Sabre's Neon Storm fits a Mug");

        var vex = Make("Vex", Tequila, LimeJuice, OrangePeel, Syrup);
        Eq(new Taste(2, 1, 3), vex.Stats, "Vex's Golden Standard: Tequila + Lime Juice + Orange Peel + Syrup");
        Check(Glass.Fits(vex.Units, GlassLength.Long), "Vex's Golden Standard fits a Martini glass");

        var cloudy = Make("Cloudy", Bourbon, DarkCaramel, Syrup);
        Eq(new Taste(0, 4, -1), cloudy.Stats, "Cloudy's Cozy Cup: Bourbon + Dark Caramel + Syrup");
        Check(Glass.Fits(cloudy.Units, GlassLength.Medium), "Cloudy's Cozy Cup fits a Mug");

        var emberRed = Drink(Bourbon, Grenadine);
        var emberDark = Drink(Bourbon, DarkCaramel);
        Check(Layering.Holds(emberRed.Stats, emberDark.Stats), "Ember's two layers hold");
        var ember = Layering.Resolve(new[] { emberRed, emberDark }, GlassLength.Medium);
        Check(ember.Ok && !ember.Collapsed.Any(c => c), "Ember's Spirit Order layers in a Coupe without collapsing");
        Eq(new Taste(-1, 6, 0), ember.Stats, "Ember's Spirit Order: Bourbon + Grenadine under Bourbon + Dark Caramel");

        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
