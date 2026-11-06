using System;

namespace MoonlitTails.Drinks
{
    /// <summary>The three taste stats of a liquid. Stats can be negative (drawn left of centre on the stat bars).</summary>
    public readonly struct Taste : IEquatable<Taste>
    {
        public readonly int Bitter;
        public readonly int Sweet;
        public readonly int Sour;

        public static readonly Taste Zero = new Taste(0, 0, 0);

        public Taste(int bitter, int sweet, int sour) { Bitter = bitter; Sweet = sweet; Sour = sour; }

        /// <summary>Base Value / Density: the highest single taste stat. Ties and the other stats don't matter.</summary>
        public int BaseValue => Math.Max(Bitter, Math.Max(Sweet, Sour));

        /// <summary>Sum of the absolute values of all three stats (used to cap stat adjustments).</summary>
        public int AbsTotal => Math.Abs(Bitter) + Math.Abs(Sweet) + Math.Abs(Sour);

        /// <summary>
        /// Points two liquids share across tastes that are positive in BOTH: the sum of min(a, b) over those tastes.
        /// Used by the layering compatibility check.
        /// </summary>
        public static int SharedPositive(Taste a, Taste b)
        {
            return Shared(a.Bitter, b.Bitter) + Shared(a.Sweet, b.Sweet) + Shared(a.Sour, b.Sour);
        }

        static int Shared(int x, int y) => x > 0 && y > 0 ? Math.Min(x, y) : 0;

        public static Taste operator +(Taste a, Taste b) => new Taste(a.Bitter + b.Bitter, a.Sweet + b.Sweet, a.Sour + b.Sour);

        public bool Equals(Taste o) => Bitter == o.Bitter && Sweet == o.Sweet && Sour == o.Sour;
        public override bool Equals(object obj) => obj is Taste t && Equals(t);
        public override int GetHashCode() => (Bitter * 397 ^ Sweet) * 397 ^ Sour;
        public static bool operator ==(Taste a, Taste b) => a.Equals(b);
        public static bool operator !=(Taste a, Taste b) => !a.Equals(b);
        public override string ToString() => $"({Bitter:+0;-0;0} Bitter, {Sweet:+0;-0;0} Sweet, {Sour:+0;-0;0} Sour)";
    }
}
