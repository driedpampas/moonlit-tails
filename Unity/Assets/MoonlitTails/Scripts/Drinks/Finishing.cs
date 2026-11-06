using System;
using System.Collections.Generic;

namespace MoonlitTails.Drinks
{
    public enum PrepMethod { Mixed, Layered }

    /// <summary>What actually goes to the customer.</summary>
    public sealed class ServedDrink
    {
        public Taste Stats;
        public PrepMethod Method;
        public string GlassVariant;
        public bool Cold;
        /// <summary>From the garnish only (liquids never change colour), or null.</summary>
        public string Color;
    }

    /// <summary>What the customer asked for. Stats must match exactly; the rest only if requested.</summary>
    public sealed class DrinkOrder
    {
        public Taste Target;
        public PrepMethod Method;
        /// <summary>Exact glass variant (name), or null if any will do.</summary>
        public string GlassVariant;
        public bool Cold;
        /// <summary>Garnish colour, or null if not requested.</summary>
        public string Color;
        /// <summary>One recipe that makes it (shown after a wrong attempt). Not used for checking.</summary>
        public string Hint;
    }

    [Flags]
    public enum Mismatch { None = 0, Taste = 1, Method = 2, Glass = 4, Cold = 8, Color = 16 }

    public static class Finishing
    {
        /// <summary>Stat adjustment points: 1 for a pure mix, 2 for a layered drink.</summary>
        public static int AdjustmentPoints(PrepMethod method) => method == PrepMethod.Layered ? 2 : 1;

        /// <summary>
        /// Applies the end-of-process bonus: any shifts to the stats as long as the total (ignoring sign) is within
        /// the allowance. Returns false and leaves the stats alone if it's too much.
        /// </summary>
        public static bool TryAdjust(Taste stats, PrepMethod method, Taste delta, out Taste adjusted)
        {
            adjusted = stats;
            if (delta.AbsTotal > AdjustmentPoints(method)) return false;
            adjusted = stats + delta;
            return true;
        }

        public static Mismatch Check(DrinkOrder order, ServedDrink served)
        {
            var m = Mismatch.None;
            if (order.Target != served.Stats) m |= Mismatch.Taste;
            if (order.Method != served.Method) m |= Mismatch.Method;
            if (order.GlassVariant != null && order.GlassVariant != served.GlassVariant) m |= Mismatch.Glass;
            if (order.Cold && !served.Cold) m |= Mismatch.Cold;
            if (order.Color != null && order.Color != served.Color) m |= Mismatch.Color;
            return m;
        }
    }
}
