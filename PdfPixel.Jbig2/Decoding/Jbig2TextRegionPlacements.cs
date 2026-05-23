using System;
using System.Collections.Generic;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Captured symbol placements for a JBIG2 text region (ITU-T T.88 Section 6.4).
/// Holds the region dimensions, default pixel, symbol-to-region combination operator (SBCOMBOP)
/// and the ordered list of (symbol, x, y) placements emitted by the placement decoder.
///
/// <see cref="Compose"/> selects between three execution paths:
///   • Regime 1 — both stages collapse unconditionally (SBCOMBOP == externalOp, both associative
///     with DP as identity). Direct per-symbol composite, overlap-safe.
///   • Regime 2 — stages collapse only when no two placements overlap (SBCOMBOP differs from
///     externalOp but both reduce s and DP to no-ops in their respective stages). Direct
///     per-symbol composite gated on a bbox-overlap check.
///   • Fallback — materialise the region (DP fill + per-symbol SBCOMBOP), then composite onto
///     target with externalOp. Matches the spec's two-stage definition exactly.
/// </summary>
internal sealed class Jbig2TextRegionPlacements
{
    private readonly int _width;
    private readonly int _height;
    private readonly byte _defaultPixel;
    private readonly Jbig2CombinationOperator _symbolCombinationOperator;
    private readonly List<Placement> _placements = new();

    public Jbig2TextRegionPlacements(
        int width,
        int height,
        byte defaultPixel,
        Jbig2CombinationOperator symbolCombinationOperator)
    {
        _width = width;
        _height = height;
        _defaultPixel = defaultPixel;
        _symbolCombinationOperator = symbolCombinationOperator;
    }

    /// <summary>Records one symbol instance at the given region-local coordinates.</summary>
    public void Add(Jbig2Bitmap symbol, int x, int y)
    {
        _placements.Add(new Placement(symbol, x, y));
    }

    /// <summary>
    /// Composites the placements onto <paramref name="target"/> at <paramref name="x"/>,<paramref name="y"/>
    /// using <paramref name="op"/> for the region-to-target step and the captured SBCOMBOP for the
    /// per-symbol step. See class summary for the three execution paths.
    /// </summary>
    public void Compose(Jbig2Bitmap target, int x, int y, Jbig2CombinationOperator op)
    {
        if (CanDirectCompose(op, out bool requiresNoOverlap)
            && (!requiresNoOverlap || !HasBboxOverlap()))
        {
            DirectCompose(target, x, y, op);
            return;
        }

        MaterialiseAndCompose(target, x, y, op);
    }

    private void DirectCompose(Jbig2Bitmap target, int x, int y, Jbig2CombinationOperator op)
    {
        foreach (var placement in _placements)
        {
            target.Composite(
                placement.Symbol,
                x + placement.X,
                y + placement.Y,
                op,
                x,
                y,
                _width,
                _height);
        }
    }

    private void MaterialiseAndCompose(Jbig2Bitmap target, int x, int y, Jbig2CombinationOperator op)
    {
        var region = new Jbig2Bitmap(_width, _height, _defaultPixel);

        foreach (var placement in _placements)
        {
            region.Composite(placement.Symbol, placement.X, placement.Y, _symbolCombinationOperator);
        }

        target.Composite(region, x, y, op);
    }

    /// <summary>
    /// True when both stages reduce to a single per-symbol <c>target ⊙₂ s</c> blit.
    /// Requires:
    ///   • Stage 1: <c>DP ⊙₁ s = s</c> — SBCOMBOP and DP combine to a passthrough of the symbol pixel.
    ///   • Stage 2: <c>P ⊙₂ DP = P</c> — DP is the identity element for the external operator.
    /// When both hold, <c>P ⊙₂ (DP ⊙₁ s) = P ⊙₂ s</c> at single-coverage pixels and uncovered
    /// pixels are unchanged. <paramref name="requiresNoOverlap"/> is set when SBCOMBOP differs
    /// from <paramref name="externalOp"/>: the operators no longer share idempotence/associativity
    /// across stages, so set-pixel overlap of two placements would diverge from the spec.
    /// </summary>
    private bool CanDirectCompose(Jbig2CombinationOperator externalOp, out bool requiresNoOverlap)
    {
        requiresNoOverlap = false;

        if (!IsSymbolStagePassthrough())
        {
            return false;
        }

        if (!IsDefaultPixelIdentityFor(externalOp))
        {
            return false;
        }

        // Same operator both stages → idempotent + associative → overlap-safe (Regime 1).
        // Different operators → safe only when no two placements overlap (Regime 2).
        requiresNoOverlap = _symbolCombinationOperator != externalOp;
        return true;
    }

    /// <summary>
    /// True when <c>DP ⊙ s = s</c> for all s (the symbol stage acts as a passthrough of the
    /// symbol pixel). Holds for OR/XOR with DP=0, AND/XNOR with DP=1, and REPLACE for any DP.
    /// </summary>
    private bool IsSymbolStagePassthrough() => _symbolCombinationOperator switch
    {
        Jbig2CombinationOperator.Or => _defaultPixel == 0,
        Jbig2CombinationOperator.Xor => _defaultPixel == 0,
        Jbig2CombinationOperator.And => _defaultPixel == 1,
        Jbig2CombinationOperator.Xnor => _defaultPixel == 1,
        Jbig2CombinationOperator.Replace => true,
        _ => false,
    };

    /// <summary>
    /// True when <c>P ⊙ DP = P</c> for all P (DP is the identity element of the operator).
    /// Holds for OR/XOR with DP=0 and AND/XNOR with DP=1. REPLACE has no identity element.
    /// </summary>
    private bool IsDefaultPixelIdentityFor(Jbig2CombinationOperator op) => op switch
    {
        Jbig2CombinationOperator.Or => _defaultPixel == 0,
        Jbig2CombinationOperator.Xor => _defaultPixel == 0,
        Jbig2CombinationOperator.And => _defaultPixel == 1,
        Jbig2CombinationOperator.Xnor => _defaultPixel == 1,
        _ => false,
    };

    /// <summary>
    /// Returns true if any two placements have overlapping bounding boxes. Conservative: bbox
    /// overlap doesn't imply set-pixel overlap, but bbox-disjoint guarantees pixel-disjoint, which
    /// is what Regime 2 needs. Y-sorted sweep — O(n log n) sort plus a near-linear scan for
    /// typical text layouts where each glyph's Y-range overlaps only its own line's neighbours.
    /// </summary>
    private bool HasBboxOverlap()
    {
        int count = _placements.Count;
        if (count < 2)
        {
            return false;
        }

        var sorted = _placements.ToArray();
        Array.Sort(sorted, static (a, b) => a.Y.CompareTo(b.Y));

        for (int i = 0; i < count; i++)
        {
            var pi = sorted[i];
            int piBottom = pi.Y + pi.Symbol.Height;
            int piRight = pi.X + pi.Symbol.Width;

            for (int j = i + 1; j < count; j++)
            {
                var pj = sorted[j];

                // Sorted by Y → once pj.Y >= piBottom, no later placement can overlap pi in Y either.
                if (pj.Y >= piBottom)
                {
                    break;
                }

                int pjRight = pj.X + pj.Symbol.Width;
                if (pi.X < pjRight && pj.X < piRight)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private readonly struct Placement
    {
        public readonly Jbig2Bitmap Symbol;
        public readonly int X;
        public readonly int Y;

        public Placement(Jbig2Bitmap symbol, int x, int y)
        {
            Symbol = symbol;
            X = x;
            Y = y;
        }
    }
}
