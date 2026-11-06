using System;
using System.Collections.Generic;
using System.Linq;

namespace MoonlitTails.Drinks
{
    /// <summary>A glass the bar has: its variant name (what customers ask for) and its length.</summary>
    public readonly struct GlassVariant
    {
        public readonly string Name;
        public readonly GlassLength Length;
        public GlassVariant(string name, GlassLength length) { Name = name; Length = length; }
    }

    /// <summary>
    /// Makes customer orders that can always be made: it builds a real recipe with the rules, then asks for the
    /// result. So every order has at least one solution (kept in <see cref="DrinkOrder.Hint"/>).
    /// </summary>
    public static class OrderGenerator
    {
        public sealed class Settings
        {
            /// <summary>Chance an order is layered instead of mixed.</summary>
            public double LayeredChance = 0.4;
            /// <summary>Chance the target is nudged so the end-of-drink stat adjustment is needed.</summary>
            public double NeedsAdjustmentChance = 0.3;
            public double ColdChance = 0.5;
            public double GarnishChance = 0.5;
            /// <summary>Most modifiers in a mixed order.</summary>
            public int MaxMixModifiers = 2;
        }

        public static DrinkOrder Generate(Random rng, IReadOnlyList<GlassVariant> glasses, IReadOnlyList<string> garnishColors, Settings s = null)
        {
            s = s ?? new Settings();
            if (glasses == null || glasses.Count == 0) throw new ArgumentException("No glasses", nameof(glasses));

            var order = rng.NextDouble() < s.LayeredChance ? MakeLayered(rng, glasses) : null;
            if (order == null) order = MakeMixed(rng, glasses, s);

            if (rng.NextDouble() < s.NeedsAdjustmentChance)
            {
                // nudge within the bonus the method gives, so the player has to use it
                int points = rng.Next(1, Finishing.AdjustmentPoints(order.Method) + 1);
                var t = order.Target;
                int b = t.Bitter, sw = t.Sweet, so = t.Sour;
                for (int i = 0; i < points; i++)
                {
                    int d = rng.Next(2) == 0 ? -1 : 1;
                    switch (rng.Next(3)) { case 0: b += d; break; case 1: sw += d; break; default: so += d; break; }
                }
                var nudged = new Taste(b, sw, so);
                if (nudged != t)
                {
                    order.Target = nudged;
                    order.Hint += $", then adjust {Delta(nudged.Bitter - t.Bitter, nudged.Sweet - t.Sweet, nudged.Sour - t.Sour)}";
                }
            }

            order.Cold = rng.NextDouble() < s.ColdChance;
            if (garnishColors != null && garnishColors.Count > 0 && rng.NextDouble() < s.GarnishChance)
                order.Color = garnishColors[rng.Next(garnishColors.Count)];
            return order;
        }

        static DrinkOrder MakeMixed(Random rng, IReadOnlyList<GlassVariant> glasses, Settings s)
        {
            var glass = glasses[rng.Next(glasses.Count)];
            int maxMods = Math.Min(s.MaxMixModifiers, Glass.Capacity(glass.Length) - 1);
            int mods = rng.Next(1, Math.Max(1, maxMods) + 1);

            var baseDrink = Ingredients.Bases[rng.Next(Ingredients.Bases.Count)];
            var picked = Ingredients.Modifiers.OrderBy(_ => rng.Next()).Take(mods).ToList();

            var liquid = Liquid.Of(baseDrink);
            foreach (var m in picked)
            {
                var add = Liquid.Of(m);
                liquid = Mixing.Combine(liquid, add, Mixing.RequiredMethod(liquid, add)).Liquid;
            }

            return new DrinkOrder
            {
                Target = liquid.Stats,
                Method = PrepMethod.Mixed,
                GlassVariant = glass.Name,
                Hint = "Mix " + string.Join(" + ", new[] { baseDrink }.Concat(picked).Select(i => i.DisplayName)),
            };
        }

        static DrinkOrder MakeLayered(Random rng, IReadOnlyList<GlassVariant> glasses)
        {
            var drinks = Ingredients.Bases.SelectMany(b => Ingredients.Modifiers.Select(m => Prepared(b, m))).ToList();
            var a = drinks[rng.Next(drinks.Count)];
            var partners = drinks.Where(d => d.RecipeKey != a.RecipeKey && Layering.Holds(a.Stats, d.Stats)).ToList();
            if (partners.Count == 0) return null;
            var b2 = partners[rng.Next(partners.Count)];

            var fits = glasses.Where(g => Glass.Capacity(g.Length) >= 2).ToList();
            if (fits.Count == 0) return null;
            var glass = fits[rng.Next(fits.Count)];
            int cap = Glass.Capacity(glass.Length);

            // patterns that a prepared drink (2 units) can fill: A,B or A,A,B or A,B,A or A,B,B
            var patterns = new List<Liquid[]> { new[] { a, b2 } };
            if (cap >= 3) { patterns.Add(new[] { a, a, b2 }); patterns.Add(new[] { a, b2, a }); patterns.Add(new[] { a, b2, b2 }); }
            var pours = patterns[rng.Next(patterns.Count)];

            var result = Layering.Resolve(pours, glass.Length);
            return new DrinkOrder
            {
                Target = result.Stats,
                Method = PrepMethod.Layered,
                GlassVariant = glass.Name,
                Hint = "Layer " + string.Join(", ", pours.Select(Describe)),
            };
        }

        static Liquid Prepared(Ingredient b, Ingredient m)
        {
            var x = Liquid.Of(b); var y = Liquid.Of(m);
            return Mixing.Combine(x, y, Mixing.RequiredMethod(x, y)).Liquid;
        }

        public static string Describe(Liquid l) => string.Join(" + ", l.Ingredients.Select(i => i.DisplayName));

        static string Delta(int b, int sw, int so)
        {
            var parts = new List<string>();
            if (b != 0) parts.Add($"Bitter {b:+0;-0}");
            if (sw != 0) parts.Add($"Sweet {sw:+0;-0}");
            if (so != 0) parts.Add($"Sour {so:+0;-0}");
            return string.Join(", ", parts);
        }
    }
}
