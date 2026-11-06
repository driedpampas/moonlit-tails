namespace MoonlitTails.Drinks
{
    /// <summary>Glass lengths. The value is how many units the glass holds. Variants (names/models) don't change capacity.</summary>
    public enum GlassLength { Short = 3, Medium = 4, Long = 5 }

    public static class Glass
    {
        public static int Capacity(GlassLength length) => (int)length;
        public static bool Fits(int units, GlassLength length) => units >= 1 && units <= (int)length;
    }
}
