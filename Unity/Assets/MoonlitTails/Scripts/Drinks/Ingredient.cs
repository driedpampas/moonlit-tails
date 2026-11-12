using System.Collections.Generic;

namespace MoonlitTails.Drinks
{
    public enum IngredientKind { Base, Modifier }

    /// <summary>
    /// Every ingredient, as a value a prefab can store. The numbers are saved in prefabs, so never change or reuse one:
    /// add new ingredients with new numbers.
    /// </summary>
    public enum IngredientId
    {
        None = 0,
        Whiskey = 1, Bourbon = 2, Tequila = 3,
        TonicWater = 10, OrangePeel = 11, Espresso = 12, BlackTea = 13,
        Syrup = 20, Cola = 21, DarkCaramel = 22, Grenadine = 23,
        LimeJuice = 30, AppleCider = 31, GrapefruitAcid = 32, SourMix = 33,
    }

    /// <summary>A raw ingredient: a base drink or a flavour modifier. One pour of it is 1 unit.</summary>
    public sealed class Ingredient
    {
        public readonly IngredientId Id;
        public readonly string DisplayName;
        public readonly IngredientKind Kind;
        public readonly Taste Taste;

        public Ingredient(IngredientId id, IngredientKind kind, Taste taste, string displayName = null)
        {
            Id = id; Kind = kind; Taste = taste; DisplayName = displayName ?? id.ToString();
        }
        public override string ToString() => Id.ToString();
    }

    /// <summary>The ingredient list from the drink crafting guide. Change numbers here (or move to assets later).</summary>
    public static class Ingredients
    {
        static Ingredient B(IngredientId id, string name, int b, int sw, int so) => new Ingredient(id, IngredientKind.Base, new Taste(b, sw, so), name);
        static Ingredient M(IngredientId id, string name, int b, int sw, int so) => new Ingredient(id, IngredientKind.Modifier, new Taste(b, sw, so), name);

        // Base drinks
        public static readonly Ingredient Whiskey = B(IngredientId.Whiskey, "Whiskey", 1, 0, 0);
        public static readonly Ingredient Bourbon = B(IngredientId.Bourbon, "Bourbon", 0, 1, 0);
        public static readonly Ingredient Tequila = B(IngredientId.Tequila, "Tequila", 0, 0, 1);

        // Bitter modifiers
        public static readonly Ingredient TonicWater = M(IngredientId.TonicWater, "Tonic Water", 1, 0, 0);
        public static readonly Ingredient OrangePeel = M(IngredientId.OrangePeel, "Orange Peel", 2, 0, 1);
        public static readonly Ingredient Espresso = M(IngredientId.Espresso, "Espresso", 2, -1, 0);
        public static readonly Ingredient BlackTea = M(IngredientId.BlackTea, "Black Tea", 3, -1, -1);

        // Sweet modifiers
        public static readonly Ingredient Syrup = M(IngredientId.Syrup, "Syrup", 0, 1, 0);
        public static readonly Ingredient Cola = M(IngredientId.Cola, "Cola", 1, 2, 0);
        public static readonly Ingredient DarkCaramel = M(IngredientId.DarkCaramel, "Dark Caramel", 0, 2, -1);
        public static readonly Ingredient Grenadine = M(IngredientId.Grenadine, "Grenadine", -2, 3, 0);

        // Sour modifiers
        public static readonly Ingredient LimeJuice = M(IngredientId.LimeJuice, "Lime Juice", 0, 0, 1);
        public static readonly Ingredient AppleCider = M(IngredientId.AppleCider, "Apple Cider", 0, 1, 2);
        public static readonly Ingredient GrapefruitAcid = M(IngredientId.GrapefruitAcid, "Grapefruit Acid", 0, -1, 2);
        public static readonly Ingredient SourMix = M(IngredientId.SourMix, "Sour Mix", -1, -1, 3);

        public static readonly IReadOnlyList<Ingredient> Bases = new[] { Whiskey, Bourbon, Tequila };

        /// <summary>The ingredient for an id (what a prop's IngredientSource stores), or null for None or an unknown value.</summary>
        public static Ingredient Get(IngredientId id)
        {
            foreach (var i in Bases) if (i.Id == id) return i;
            foreach (var i in Modifiers) if (i.Id == id) return i;
            return null;
        }

        public static readonly IReadOnlyList<Ingredient> Modifiers = new[]
        {
            TonicWater, OrangePeel, Espresso, BlackTea,
            Syrup, Cola, DarkCaramel, Grenadine,
            LimeJuice, AppleCider, GrapefruitAcid, SourMix,
        };
    }
}
