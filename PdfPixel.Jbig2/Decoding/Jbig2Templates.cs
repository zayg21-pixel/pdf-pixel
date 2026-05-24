using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Static template data and builders for JBIG2 generic and refinement context templates.
/// Provides pre-defined pixel layouts per the ITU-T T.88 specification and methods to
/// build <see cref="Jbig2RowTemplate"/> instances for use by the fast row decoder.
/// </summary>
internal static class Jbig2Templates
{
    // ─────────────────────────────────────────────────────────────────────────
    // Generic region template data (ITU-T T.88 Section 6.2)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full default-AT templates (fixed + default AT positions) for all four generic templates,
    /// sorted by (dy, dx) with descending bit shifts matching the ITU-T T.88 context layout.
    /// Indexed via <see cref="DefaultTemplateOffsets"/> and <see cref="DefaultTemplateLengths"/>.
    /// </summary>
    private static readonly Jbig2ContextPixel[] DefaultTemplatePixels =
    [
        // Template 0: 16-bit context (offset 0)
        new(-2, -2, 15), new(-1, -2, 14), new(0, -2, 13), new(1, -2, 12), new(2, -2, 11),
        new(-3, -1, 10), new(-2, -1, 9), new(-1, -1, 8), new(0, -1, 7), new(1, -1, 6), new(2, -1, 5), new(3, -1, 4),
        new(-4, 0, 3), new(-3, 0, 2), new(-2, 0, 1), new(-1, 0, 0),
        // Template 1: 13-bit context (offset 16)
        new(-1, -2, 12), new(0, -2, 11), new(1, -2, 10), new(2, -2, 9),
        new(-2, -1, 8), new(-1, -1, 7), new(0, -1, 6), new(1, -1, 5), new(2, -1, 4), new(3, -1, 3),
        new(-3, 0, 2), new(-2, 0, 1), new(-1, 0, 0),
        // Template 2: 10-bit context (offset 29)
        new(-1, -2, 9), new(0, -2, 8), new(1, -2, 7),
        new(-2, -1, 6), new(-1, -1, 5), new(0, -1, 4), new(1, -1, 3), new(2, -1, 2),
        new(-2, 0, 1), new(-1, 0, 0),
        // Template 3: 10-bit context (offset 39)
        new(-3, -1, 9), new(-2, -1, 8), new(-1, -1, 7), new(0, -1, 6), new(1, -1, 5), new(2, -1, 4),
        new(-4, 0, 3), new(-3, 0, 2), new(-2, 0, 1), new(-1, 0, 0)
    ];

    /// <summary>Start offset into <see cref="DefaultTemplatePixels"/> for each template.</summary>
    private static readonly int[] DefaultTemplateOffsets = [0, 16, 29, 39];

    /// <summary>Number of context pixels for each default template.</summary>
    private static readonly int[] DefaultTemplateLengths = [16, 13, 10, 10];

    /// <summary>
    /// Indices into <see cref="DefaultTemplatePixels"/> (per-template slice) that correspond
    /// to adaptive-template (AT) pixel positions. Ordered by AT array index (atX[0], atX[1], …).
    /// </summary>
    private static readonly int[] AtIndicesTemplate0 = [11, 5, 4, 0];
    private static readonly int[] AtIndicesTemplate1 = [9];
    private static readonly int[] AtIndicesTemplate2 = [7];
    private static readonly int[] AtIndicesTemplate3 = [5];

    // ─────────────────────────────────────────────────────────────────────────
    // Refinement region template data (ITU-T T.88 Section 6.3)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Default refinement template 0 coding pixels (current bitmap, ITU-T T.88 Table 12A).
    /// Bit positions 0..3. The pixel at index 3 is the AT pixel (default: -1, -1).
    /// </summary>
    private static readonly Jbig2ContextPixel[] RefinementCodingTemplate0 =
    [
        new(-1, 0, 0),   // bit 0
        new(1, -1, 1),   // bit 1
        new(0, -1, 2),   // bit 2
        new(-1, -1, 3)   // bit 3 (AT pixel 0, default position)
    ];

    /// <summary>
    /// Default refinement template 0 reference pixels (reference bitmap, ITU-T T.88 Table 12A).
    /// Bit positions 4..12. The pixel at index 8 is the AT pixel (default: -1, -1).
    /// </summary>
    private static readonly Jbig2ContextPixel[] RefinementReferenceTemplate0 =
    [
        new(1, 1, 4),    // bit 4
        new(0, 1, 5),    // bit 5
        new(-1, 1, 6),   // bit 6
        new(1, 0, 7),    // bit 7
        new(0, 0, 8),    // bit 8
        new(-1, 0, 9),   // bit 9
        new(1, -1, 10),  // bit 10
        new(0, -1, 11),  // bit 11
        new(-1, -1, 12)  // bit 12 (AT pixel 1, default position)
    ];

    /// <summary>
    /// Default refinement template 1 coding pixels (current bitmap, ITU-T T.88 Table 12B).
    /// Bit positions 0..3, all fixed (no AT pixels).
    /// </summary>
    private static readonly Jbig2ContextPixel[] RefinementCodingTemplate1 =
    [
        new(-1, 0, 0),   // bit 0
        new(1, -1, 1),   // bit 1
        new(0, -1, 2),   // bit 2
        new(-1, -1, 3)   // bit 3
    ];

    /// <summary>
    /// Default refinement template 1 reference pixels (reference bitmap, ITU-T T.88 Table 12B).
    /// Bit positions 4..9, all fixed (no AT pixels).
    /// </summary>
    private static readonly Jbig2ContextPixel[] RefinementReferenceTemplate1 =
    [
        new(1, 1, 4),    // bit 4
        new(0, 1, 5),    // bit 5
        new(1, 0, 6),    // bit 6
        new(0, 0, 7),    // bit 7
        new(-1, 0, 8),   // bit 8
        new(0, -1, 9)    // bit 9
    ];

    // ─────────────────────────────────────────────────────────────────────────
    // AT pixel readers and factories
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the number of adaptive template pixel pairs for a generic template.
    /// Template 0 uses 4 AT pixels; templates 1–3 use 1.
    /// </summary>
    internal static int AtPixelCount(int templateId) => templateId == 0 ? 4 : 1;

    /// <summary>
    /// Reads <paramref name="count"/> (X, Y) adaptive template pixel pairs from the front of
    /// <paramref name="data"/>. Pairs that fall outside the available data are left as zero.
    /// </summary>
    internal static Jbig2AtPixels ReadAtPixelPairs(ReadOnlySpan<byte> data, int count)
    {
        var atX = new sbyte[count];
        var atY = new sbyte[count];

        for (int i = 0; i < count; i++)
        {
            int byteIndex = i * 2;
            if (byteIndex + 1 < data.Length)
            {
                atX[i] = (sbyte)data[byteIndex];
                atY[i] = (sbyte)data[byteIndex + 1];
            }
        }

        return new Jbig2AtPixels(atX, atY);
    }

    /// <summary>
    /// Returns the default AT pixels for generic region arithmetic decoding.
    /// These are the fixed positions used by halftone regions (ITU-T T.88 Section 7.4.5.1.2).
    /// </summary>
    internal static Jbig2AtPixels GetDefaultAtPixels(int templateId)
    {
        int atCount = AtPixelCount(templateId);
        var atX = new sbyte[atCount];
        var atY = new sbyte[atCount];
        atX[0] = (sbyte)(templateId <= 1 ? 3 : 2);
        atY[0] = -1;
        if (templateId == 0)
        {
            atX[1] = -3; atY[1] = -1;
            atX[2] = 2;  atY[2] = -2;
            atX[3] = -2; atY[3] = -2;
        }
        return new Jbig2AtPixels(atX, atY);
    }

    /// <summary>
    /// Returns the AT pixels for pattern dictionary arithmetic decoding.
    /// AT[0] = (-patternWidth, 0) prevents cross-pattern context contamination;
    /// remaining positions follow the template defaults (ITU-T T.88 Section 7.4.4.1.2).
    /// </summary>
    internal static Jbig2AtPixels GetPatternDictionaryAtPixels(int templateId, int patternWidth)
    {
        int atCount = AtPixelCount(templateId);
        var atX = new sbyte[atCount];
        var atY = new sbyte[atCount];
        atX[0] = (sbyte)(-patternWidth);
        atY[0] = 0;
        if (templateId == 0)
        {
            atX[1] = -3; atY[1] = -1;
            atX[2] = 2;  atY[2] = -2;
            atX[3] = -2; atY[3] = -2;
        }
        return new Jbig2AtPixels(atX, atY);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Generic template API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> of the full default-AT context pixels for a template.
    /// </summary>
    private static ReadOnlySpan<Jbig2ContextPixel> GetDefaultTemplatePixels(int templateId)
    {
        return new ReadOnlySpan<Jbig2ContextPixel>(DefaultTemplatePixels, DefaultTemplateOffsets[templateId], DefaultTemplateLengths[templateId]);
    }

    /// <summary>
    /// Checks whether adaptive template pixels match the default positions.
    /// </summary>
    private static bool IsDefaultAt(int templateId, sbyte[] atX, sbyte[] atY)
    {
        return templateId switch
        {
            0 => atX[0] == 3 && atY[0] == -1 &&
                 atX[1] == -3 && atY[1] == -1 &&
                 atX[2] == 2 && atY[2] == -2 &&
                 atX[3] == -2 && atY[3] == -2,
            1 => atX[0] == 3 && atY[0] == -1,
            2 => atX[0] == 2 && atY[0] == -1,
            3 => atX[0] == 2 && atY[0] == -1,
            _ => false
        };
    }

    /// <summary>
    /// Resolves the context template to use for decoding.
    /// Returns the default-AT template slice when the supplied AT pixels match the default
    /// positions, otherwise builds and returns a custom template with the AT positions replaced.
    /// </summary>
    /// <param name="templateId">Template index (0–3).</param>
    /// <param name="atX">Adaptive template X offsets.</param>
    /// <param name="atY">Adaptive template Y offsets.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<Jbig2ContextPixel> ResolveTemplate(int templateId, sbyte[] atX, sbyte[] atY)
    {
        return IsDefaultAt(templateId, atX, atY)
            ? GetDefaultTemplatePixels(templateId)
            : BuildCustomTemplate(templateId, atX, atY);
    }

    /// <summary>
    /// Builds a custom context template by copying the default template for the given template
    /// and replacing the adaptive-template (AT) pixel positions with the supplied offsets.
    /// Each replaced pixel keeps its original bit shift from the spec-defined layout.
    /// </summary>
    internal static Jbig2ContextPixel[] BuildCustomTemplate(int templateId, sbyte[] atX, sbyte[] atY)
    {
        ReadOnlySpan<Jbig2ContextPixel> defaultPixels = GetDefaultTemplatePixels(templateId);
        int count = defaultPixels.Length;

        var result = new Jbig2ContextPixel[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = defaultPixels[i];
        }

        // Replace AT pixel positions, keeping their spec-defined bit shifts
        int[] atIndices = templateId switch
        {
            0 => AtIndicesTemplate0,
            1 => AtIndicesTemplate1,
            2 => AtIndicesTemplate2,
            _ => AtIndicesTemplate3
        };

        for (int i = 0; i < atX.Length && i < atIndices.Length; i++)
        {
            int idx = atIndices[i];
            result[idx] = new Jbig2ContextPixel(atX[i], atY[i], result[idx].Shift);
        }

        return result;
    }

    /// <summary>
    /// Returns the context array size for a given generic template.
    /// </summary>
    internal static int GetContextSize(int templateId)
    {
        return templateId switch
        {
            0 => 1 << 16,
            1 => 1 << 13,
            2 => 1 << 10,
            3 => 1 << 10,
            _ => 1 << 16
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Row template builders
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="Jbig2RowTemplate"/> from the given generic template pixels by grouping
    /// them by dy and computing min/max dx and context shift for each group.
    /// </summary>
    internal static Jbig2RowTemplate BuildFastTemplate(int templateId, Jbig2AtPixels atPixels)
    {
        ReadOnlySpan<Jbig2ContextPixel> pixels = ResolveTemplate(templateId, atPixels.AtX, atPixels.AtY);
        return BuildRowTemplateFromPixels(pixels);
    }

    /// <summary>
    /// Builds a <see cref="Jbig2RowTemplate"/> from an arbitrary set of context pixels.
    /// Pixels within the same dy are split into contiguous runs where both dx and shift
    /// are strictly consecutive (dx increases by 1, shift decreases by 1). This correctly
    /// handles non-consecutive custom AT positions such as (-1, 1, 17) within a single row.
    /// </summary>
    internal static Jbig2RowTemplate BuildRowTemplateFromPixels(ReadOnlySpan<Jbig2ContextPixel> pixels)
    {
        Jbig2ContextPixel[] sorted = pixels.ToArray();

        // Sort by (dy ascending, dx ascending) so runs are easy to detect.
        Array.Sort(sorted, (a, b) => a.Dy != b.Dy ? a.Dy.CompareTo(b.Dy) : a.Dx.CompareTo(b.Dx));

        var groups = new List<Jbig2RowGroupInfo>(sorted.Length);

        int i = 0;
        while (i < sorted.Length)
        {
            sbyte dy = sorted[i].Dy;
            int runStart = i;

            // Extend the run as long as dx and shift are both consecutive.
            while (i + 1 < sorted.Length &&
                   sorted[i + 1].Dy == dy &&
                   sorted[i + 1].Dx == sorted[i].Dx + 1 &&
                   sorted[i + 1].Shift == sorted[i].Shift - 1)
            {
                i++;
            }

            // sorted[runStart].Shift is the highest shift (leftmost pixel = minDx).
            // sorted[i].Shift is the lowest shift (rightmost pixel = maxDx) = contextShift.
            groups.Add(new Jbig2RowGroupInfo(
                dy,
                sorted[runStart].Dx,
                sorted[i].Dx,
                sorted[i].Shift));

            i++;
        }

        return new Jbig2RowTemplate(groups.ToArray(), pixels.ToArray());
    }

    /// <summary>
    /// Builds a <see cref="Jbig2RowTemplate"/> for a refinement region by combining coding (primary)
    /// and reference pixel templates, applying the supplied adaptive template offsets.
    /// Reference groups are marked with <see cref="Jbig2RowGroupInfo.IsReference"/> so that
    /// the decoder applies refDx/refDy offsets and reads from the reference bitmap.
    /// </summary>
    /// <param name="templateId">Refinement template index (0 or 1).</param>
    /// <param name="atX">Adaptive template X offsets (template 0 uses atX[0] for coding, atX[1] for reference).</param>
    /// <param name="atY">Adaptive template Y offsets.</param>
    internal static Jbig2RowTemplate BuildRefinementTemplate(int templateId, sbyte[] atX, sbyte[] atY)
    {
        Jbig2ContextPixel[] coding;
        Jbig2ContextPixel[] reference;

        if (templateId == 0)
        {
            coding = (Jbig2ContextPixel[])RefinementCodingTemplate0.Clone();
            reference = (Jbig2ContextPixel[])RefinementReferenceTemplate0.Clone();

            // Replace AT pixel 0 in coding (index 3)
            coding[3] = new Jbig2ContextPixel(atX[0], atY[0], coding[3].Shift);
            // Replace AT pixel 1 in reference (index 8)
            reference[8] = new Jbig2ContextPixel(atX[1], atY[1], reference[8].Shift);
        }
        else
        {
            coding = RefinementCodingTemplate1;
            reference = RefinementReferenceTemplate1;
        }

        return BuildRowTemplateFromDualPixels(coding, reference);
    }

    /// <summary>
    /// Builds a <see cref="Jbig2RowTemplate"/> from two sets of context pixels: coding (primary bitmap)
    /// and reference (reference bitmap). Groups from each set are marked accordingly.
    /// </summary>
    private static Jbig2RowTemplate BuildRowTemplateFromDualPixels(
        ReadOnlySpan<Jbig2ContextPixel> codingPixels,
        ReadOnlySpan<Jbig2ContextPixel> referencePixels)
    {
        var groups = new List<Jbig2RowGroupInfo>(codingPixels.Length + referencePixels.Length);

        BuildGroupsFromPixels(codingPixels, isReference: false, groups);
        BuildGroupsFromPixels(referencePixels, isReference: true, groups);

        // Combine all originals for diagnostics
        var allOriginals = new Jbig2ContextPixel[codingPixels.Length + referencePixels.Length];
        codingPixels.CopyTo(allOriginals);
        referencePixels.CopyTo(allOriginals.AsSpan(codingPixels.Length));

        return new Jbig2RowTemplate(groups.ToArray(), allOriginals);
    }

    /// <summary>
    /// Groups a set of context pixels into consecutive runs and appends them to the list.
    /// </summary>
    private static void BuildGroupsFromPixels(
        ReadOnlySpan<Jbig2ContextPixel> pixels,
        bool isReference,
        List<Jbig2RowGroupInfo> groups)
    {
        if (pixels.IsEmpty)
        {
            return;
        }

        Jbig2ContextPixel[] sorted = pixels.ToArray();
        Array.Sort(sorted, (a, b) => a.Dy != b.Dy ? a.Dy.CompareTo(b.Dy) : a.Dx.CompareTo(b.Dx));

        int i = 0;
        while (i < sorted.Length)
        {
            sbyte dy = sorted[i].Dy;
            int runStart = i;

            while (i + 1 < sorted.Length &&
                   sorted[i + 1].Dy == dy &&
                   sorted[i + 1].Dx == sorted[i].Dx + 1 &&
                   sorted[i + 1].Shift == sorted[i].Shift - 1)
            {
                i++;
            }

            groups.Add(new Jbig2RowGroupInfo(
                dy,
                sorted[runStart].Dx,
                sorted[i].Dx,
                sorted[i].Shift,
                isReference));

            i++;
        }
    }
}
