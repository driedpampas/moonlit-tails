using System.Collections.Generic;

namespace MoonlitTails.Drinks
{
    public enum IngredientKind { Base, Modifier }

    /// <summary>A raw ingredient: a base drink or a flavour modifier. One pour of it is 1 unit.</summary>
    public sealed class Ingredient
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly IngredientKind Kind;
        public readonly Taste Taste;

        public Ingredient(string id, IngredientKind kind, Taste taste, string displayName = null)
        {
            Id = id; Kind = kind; Taste = taste; DisplayName = displayName ?? id;
        }
        public override string ToString() => Id;
    }

    /// <summary>The ingredient list from the drink crafting guide. Change numbers here (or move to assets later).</summary>
    public static class Ingredients
    {
        static Ingredient B(string id, string name, int b, int sw, int so) => new Ingredient(id, IngredientKind.Base, new Taste(b, sw, so), name);
        static Ingredient M(string id, string name, int b, int sw, int so) => new Ingredient(id, IngredientKind.Modifier, new Taste(b, sw, so), name);

        // Base drinks
        public static readonly Ingredient Whiskey = B("Whiskey", "Whiskey", 1, 0, 0);
        public static readonly Ingredient Bourbon = B("Bourbon", "Bourbon", 0, 1, 0);
        public static readonly Ingredient Tequila = B("Tequila", "Tequila", 0, 0, 1);

        // Bitter modifiers
        public static readonly Ingredient TonicWater = M("TonicWater", "Tonic Water", 1, 0, 0);
        public static readonly Ingredient OrangePeel = M("OrangePeel", "Orange Peel", 2, 0, 1);
        public static readonly Ingredient Espresso = M("Espresso", "Espresso", 2, -1, 0);
        public static readonly Ingredient BlackTea = M("BlackTea", "Black Tea", 3, -1, -1);

        // Sweet modifiers
        public static readonly Ingredient Syrup = M("Syrup", "Syrup", 0, 1, 0);
        public static readonly Ingredient Cola = M("Cola", "Cola", 1, 2, 0);
        public static readonly Ingredient DarkCaramel = M("DarkCaramel", "Dark Caramel", 0, 2, -1);
        public static readonly Ingredient Grenadine = M("Grenadine", "Grenadine", -2, 3, 0);

        // Sour modifiers
        public static readonly Ingredient LimeJuice = M("LimeJuice", "Lime Juice", 0, 0, 1);
        public static readonly Ingredient AppleCider = M("AppleCider", "Apple Cider", 0, 1, 2);
        public static readonly Ingredient GrapefruitAcid = M("GrapefruitAcid", "Grapefruit Acid", 0, -1, 2);
        public static readonly Ingredient SourMix = M("SourMix", "Sour Mix", -1, -1, 3);

        public static readonly IReadOnlyList<Ingredient> Bases = new[] { Whiskey, Bourbon, Tequila };

        public static Ingredient ById(string id)
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
