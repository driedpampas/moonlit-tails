namespace MoonlitTails.Drinks
{
    public enum MixMethod { Stir, Shake }

    public enum MixOutcome
    {
        /// <summary>The two liquids combined.</summary>
        Mixed,
        /// <summary>The same ingredient can't be combined with itself. Nothing happens.</summary>
        SelfMix,
        /// <summary>Stirred when it needed shaking, or the reverse. Nothing happens.</summary>
        WrongMethod,
    }

    public readonly struct MixResult
    {
        public readonly MixOutcome Outcome;
        /// <summary>The combined liquid, or null if nothing happened (the inputs are unchanged).</summary>
        public readonly Liquid Liquid;
        public MixResult(MixOutcome outcome, Liquid liquid) { Outcome = outcome; Liquid = liquid; }
        public bool Succeeded => Outcome == MixOutcome.Mixed;
    }

    /// <summary>Path A of the guide: mixing. Equal Base Values need a stir, unequal ones need a shake.</summary>
    public static class Mixing
    {
        /// <summary>Which method two liquids need. This also applies when combining the base drink at the end.</summary>
        public static MixMethod RequiredMethod(Liquid a, Liquid b)
            => a.Stats.BaseValue == b.Stats.BaseValue ? MixMethod.Stir : MixMethod.Shake;

        public static MixResult Combine(Liquid a, Liquid b, MixMethod used)
        {
            if (a.SharesIngredientWith(b)) return new MixResult(MixOutcome.SelfMix, null);
            if (used != RequiredMethod(a, b)) return new MixResult(MixOutcome.WrongMethod, null);
            return new MixResult(MixOutcome.Mixed, Liquid.Join(a, b));
        }
    }
}
