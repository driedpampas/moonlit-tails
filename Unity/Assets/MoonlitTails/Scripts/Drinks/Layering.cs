using System;
using System.Collections.Generic;

namespace MoonlitTails.Drinks
{
    public enum LayerStatus
    {
        Ok,
        /// <summary>Nothing was poured.</summary>
        Empty,
        /// <summary>A poured liquid is not a prepared drink (a raw ingredient or a bare base).</summary>
        NotPrepared,
        /// <summary>More pours than the glass holds.</summary>
        OverCapacity,
    }

    public readonly struct LayerResult
    {
        public readonly LayerStatus Status;
        /// <summary>Final stats, rounded up (ceil). Zero unless Status is Ok.</summary>
        public readonly Taste Stats;
        /// <summary>For each pour, true if its layer collapsed (its stats counted as 0).</summary>
        public readonly IReadOnlyList<bool> Collapsed;

        public LayerResult(LayerStatus status, Taste stats, IReadOnlyList<bool> collapsed)
        {
            Status = status; Stats = stats; Collapsed = collapsed;
        }
        public bool Ok => Status == LayerStatus.Ok;
    }

    /// <summary>Path B of the guide: layering prepared drinks straight into the glass.</summary>
    public static class Layering
    {
        /// <summary>Touching layers of different drinks must share at least this many positive points.</summary>
        public const int MinSharedPoints = 2;

        /// <summary>Two touching layers of different drinks hold only if their Base Values differ and they share enough points.</summary>
        public static bool Holds(Taste a, Taste b)
            => a.BaseValue != b.BaseValue && Taste.SharedPositive(a, b) >= MinSharedPoints;

        /// <summary>
        /// Works out a layered drink from the pours, bottom to top. Each pour is 1 unit. The same drink poured
        /// again directly on itself just thickens that layer. Different touching drinks that don't hold collapse
        /// (only those two layers count as 0). The score is the per-unit average of all pours, times 1.5, rounded up.
        /// </summary>
        public static LayerResult Resolve(IReadOnlyList<Liquid> pours, GlassLength glass)
        {
            if (pours.Count == 0) return new LayerResult(LayerStatus.Empty, Taste.Zero, new bool[0]);
            foreach (var p in pours)
                if (!p.IsPrepared) return new LayerResult(LayerStatus.NotPrepared, Taste.Zero, new bool[pours.Count]);
            if (pours.Count > Glass.Capacity(glass)) return new LayerResult(LayerStatus.OverCapacity, Taste.Zero, new bool[pours.Count]);

            // Group touching pours of the same drink into layers.
            var layerOf = new int[pours.Count];
            int layers = 0;
            for (int i = 0; i < pours.Count; i++)
            {
                if (i > 0 && pours[i].RecipeKey == pours[i - 1].RecipeKey) layerOf[i] = layerOf[i - 1];
                else layerOf[i] = layers++;
            }

            // Check every pair of touching, different layers.
            var layerCollapsed = new bool[layers];
            for (int i = 1; i < pours.Count; i++)
            {
                if (layerOf[i] == layerOf[i - 1]) continue;
                if (!Holds(pours[i - 1].Stats, pours[i].Stats))
                {
                    layerCollapsed[layerOf[i - 1]] = true;
                    layerCollapsed[layerOf[i]] = true;
                }
            }

            int b = 0, sw = 0, so = 0;
            var collapsed = new bool[pours.Count];
            for (int i = 0; i < pours.Count; i++)
            {
                collapsed[i] = layerCollapsed[layerOf[i]];
                if (collapsed[i]) continue;
                b += pours[i].Stats.Bitter; sw += pours[i].Stats.Sweet; so += pours[i].Stats.Sour;
            }

            int n = pours.Count;
            var stats = new Taste(Score(b, n), Score(sw, n), Score(so, n));
            return new LayerResult(LayerStatus.Ok, stats, collapsed);
        }

        /// <summary>ceil(1.5 * sum / n) in whole numbers. "Round up" means toward +infinity, so -1.5 becomes -1.</summary>
        public static int Score(int sum, int n) => CeilDiv(3 * sum, 2 * n);

        public static int CeilDiv(int a, int b)
        {
            if (b <= 0) throw new ArgumentOutOfRangeException(nameof(b));
            return a >= 0 ? (a + b - 1) / b : -(-a / b);
        }
    }
}
