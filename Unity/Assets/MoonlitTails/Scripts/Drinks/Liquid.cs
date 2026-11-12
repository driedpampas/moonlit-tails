using System.Collections.Generic;
using System.Linq;

namespace MoonlitTails.Drinks
{
    /// <summary>
    /// What's in a shaker, stir cup or glass layer: the summed taste stats, how many units it fills, and which
    /// ingredients went in. Immutable; mixing makes a new one.
    /// </summary>
    public sealed class Liquid
    {
        public readonly Taste Stats;
        /// <summary>Units this liquid fills. One pour = 1 unit, and mixing adds the units of both parts.</summary>
        public readonly int Units;
        /// <summary>Ingredients in it, sorted by id.</summary>
        public readonly IReadOnlyList<Ingredient> Ingredients;

        /// <summary>A prepared drink has a base drink plus at least one modifier. Only these can be layered.</summary>
        public readonly bool IsPrepared;
        /// <summary>Identifies the recipe. Two drinks are "the same drink" only if they have the same ingredients.</summary>
        public readonly string RecipeKey;

        Liquid(Taste stats, int units, Ingredient[] ingredients)
        {
            Stats = stats; Units = units; Ingredients = ingredients;
            bool hasBase = false, hasModifier = false;
            foreach (var i in ingredients)
            {
                if (i.Kind == IngredientKind.Base) hasBase = true; else hasModifier = true;
            }
            IsPrepared = hasBase && hasModifier;
            RecipeKey = string.Join("+", ingredients.Select(i => i.Id));
        }

        public static Liquid Of(Ingredient i) => new Liquid(i.Taste, 1, new[] { i });

        /// <summary>Joins two liquids. Callers must have checked the rules in <see cref="Mixing"/> first.</summary>
        internal static Liquid Join(Liquid a, Liquid b)
        {
            var all = a.Ingredients.Concat(b.Ingredients).OrderBy(i => i.Id).ToArray();
            return new Liquid(a.Stats + b.Stats, a.Units + b.Units, all);
        }

        public bool Contains(Ingredient i)
        {
            foreach (var x in Ingredients) if (x.Id == i.Id) return true;
            return false;
        }

        public bool SharesIngredientWith(Liquid other)
        {
            foreach (var x in Ingredients) if (other.Contains(x)) return true;
            return false;
        }

        public override string ToString() => $"{RecipeKey} {Stats} [{Units}u]";
    }
}
