using System.Collections.Generic;
using static PdfPixel.Jbig2.Decoding.Jbig2HuffmanTable;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Standard Huffman tables as defined in ITU-T T.88 Annex B.
/// These tables are referenced by table selection indices in symbol dictionary
/// and text region segment headers.
/// </summary>
internal static class Jbig2StandardHuffmanTables
{
    /// <summary>
    /// Table B.1 — unsigned integers (bitmap size, aggregate instances).
    /// </summary>
    public static Jbig2HuffmanTable TableB1 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(0, 4, 1),
        new Jbig2HuffmanLine(16, 8, 2),
        new Jbig2HuffmanLine(272, 16, 3),
        new Jbig2HuffmanLine(65808, 32, 3),
    ]);

    /// <summary>
    /// Table B.2 — delta width with OOB (SDHUFFDW selector 0).
    /// </summary>
    public static Jbig2HuffmanTable TableB2 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(0, 0, 1),
        new Jbig2HuffmanLine(1, 0, 2),
        new Jbig2HuffmanLine(2, 0, 3),
        new Jbig2HuffmanLine(3, 3, 4),
        new Jbig2HuffmanLine(11, 6, 5),
        new Jbig2HuffmanLine(75, 32, 6),
        new Jbig2HuffmanLine(0, 0, 6, isOob: true),
    ]);

    /// <summary>
    /// Table B.3 — delta width with OOB and lower range (SDHUFFDW selector 1).
    /// </summary>
    public static Jbig2HuffmanTable TableB3 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(-256, 8, 8),
        new Jbig2HuffmanLine(0, 0, 1),
        new Jbig2HuffmanLine(1, 0, 2),
        new Jbig2HuffmanLine(2, 0, 3),
        new Jbig2HuffmanLine(3, 3, 4),
        new Jbig2HuffmanLine(11, 6, 5),
        new Jbig2HuffmanLine(-257, 32, 8, isLowerRange: true),
        new Jbig2HuffmanLine(75, 32, 7),
        new Jbig2HuffmanLine(0, 0, 6, isOob: true),
    ]);

    /// <summary>
    /// Table B.4 — unsigned delta height (SDHUFFDH selector 0).
    /// </summary>
    public static Jbig2HuffmanTable TableB4 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(1, 0, 1),
        new Jbig2HuffmanLine(2, 0, 2),
        new Jbig2HuffmanLine(3, 0, 3),
        new Jbig2HuffmanLine(4, 3, 4),
        new Jbig2HuffmanLine(12, 6, 5),
        new Jbig2HuffmanLine(76, 32, 5),
    ]);

    /// <summary>
    /// Table B.5 — signed delta height with lower range (SDHUFFDH selector 1).
    /// </summary>
    public static Jbig2HuffmanTable TableB5 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(-255, 8, 7),
        new Jbig2HuffmanLine(1, 0, 1),
        new Jbig2HuffmanLine(2, 0, 2),
        new Jbig2HuffmanLine(3, 0, 3),
        new Jbig2HuffmanLine(4, 3, 4),
        new Jbig2HuffmanLine(12, 6, 5),
        new Jbig2HuffmanLine(-256, 32, 7, isLowerRange: true),
        new Jbig2HuffmanLine(76, 32, 6),
    ]);

    /// <summary>
    /// Table B.6 — signed first-S (SBHUFFFS selector 0).
    /// </summary>
    public static Jbig2HuffmanTable TableB6 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(-2048, 10, 5),
        new Jbig2HuffmanLine(-1024, 9, 4),
        new Jbig2HuffmanLine(-512, 8, 4),
        new Jbig2HuffmanLine(-256, 7, 4),
        new Jbig2HuffmanLine(-128, 6, 5),
        new Jbig2HuffmanLine(-64, 5, 5),
        new Jbig2HuffmanLine(-32, 5, 4),
        new Jbig2HuffmanLine(0, 7, 2),
        new Jbig2HuffmanLine(128, 7, 3),
        new Jbig2HuffmanLine(256, 8, 3),
        new Jbig2HuffmanLine(512, 9, 4),
        new Jbig2HuffmanLine(1024, 10, 4),
        new Jbig2HuffmanLine(-2049, 32, 6, isLowerRange: true),
        new Jbig2HuffmanLine(2048, 32, 6),
    ]);

    /// <summary>
    /// Table B.7 — signed first-S (SBHUFFFS selector 1).
    /// </summary>
    public static Jbig2HuffmanTable TableB7 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(-1024, 9, 4),
        new Jbig2HuffmanLine(-512, 8, 3),
        new Jbig2HuffmanLine(-256, 7, 4),
        new Jbig2HuffmanLine(-128, 6, 5),
        new Jbig2HuffmanLine(-64, 5, 5),
        new Jbig2HuffmanLine(-32, 5, 4),
        new Jbig2HuffmanLine(0, 5, 4),
        new Jbig2HuffmanLine(32, 5, 5),
        new Jbig2HuffmanLine(64, 6, 5),
        new Jbig2HuffmanLine(128, 7, 4),
        new Jbig2HuffmanLine(256, 8, 3),
        new Jbig2HuffmanLine(512, 9, 3),
        new Jbig2HuffmanLine(1024, 10, 3),
        new Jbig2HuffmanLine(-1025, 32, 5, isLowerRange: true),
        new Jbig2HuffmanLine(2048, 32, 5),
    ]);

    /// <summary>
    /// Table B.8 — signed delta-S with OOB (SBHUFFDS selector 0).
    /// </summary>
    public static Jbig2HuffmanTable TableB8 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(0, 1, 2),
        new Jbig2HuffmanLine(0, 0, 2, isOob: true),
        new Jbig2HuffmanLine(4, 4, 3),
        new Jbig2HuffmanLine(-1, 0, 4),
        new Jbig2HuffmanLine(22, 4, 4),
        new Jbig2HuffmanLine(38, 5, 4),
        new Jbig2HuffmanLine(2, 0, 5),
        new Jbig2HuffmanLine(70, 6, 5),
        new Jbig2HuffmanLine(134, 7, 5),
        new Jbig2HuffmanLine(3, 0, 6),
        new Jbig2HuffmanLine(20, 1, 6),
        new Jbig2HuffmanLine(262, 7, 6),
        new Jbig2HuffmanLine(646, 10, 6),
        new Jbig2HuffmanLine(-2, 0, 7),
        new Jbig2HuffmanLine(390, 8, 7),
        new Jbig2HuffmanLine(-15, 3, 8),
        new Jbig2HuffmanLine(-5, 1, 8),
        new Jbig2HuffmanLine(-7, 1, 9),
        new Jbig2HuffmanLine(-3, 0, 9),
        new Jbig2HuffmanLine(-16, 32, 9, isLowerRange: true),
        new Jbig2HuffmanLine(1670, 32, 9),
    ]);

    /// <summary>
    /// Table B.9 — signed delta-S with OOB (SBHUFFDS selector 1).
    /// </summary>
    public static Jbig2HuffmanTable TableB9 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(0, 0, 2, isOob: true),
        new Jbig2HuffmanLine(-1, 1, 3),
        new Jbig2HuffmanLine(1, 1, 3),
        new Jbig2HuffmanLine(7, 5, 3),
        new Jbig2HuffmanLine(-3, 1, 4),
        new Jbig2HuffmanLine(43, 5, 4),
        new Jbig2HuffmanLine(75, 6, 4),
        new Jbig2HuffmanLine(3, 1, 5),
        new Jbig2HuffmanLine(139, 7, 5),
        new Jbig2HuffmanLine(267, 8, 5),
        new Jbig2HuffmanLine(5, 1, 6),
        new Jbig2HuffmanLine(39, 2, 6),
        new Jbig2HuffmanLine(523, 8, 6),
        new Jbig2HuffmanLine(1291, 11, 6),
        new Jbig2HuffmanLine(-5, 1, 7),
        new Jbig2HuffmanLine(779, 9, 7),
        new Jbig2HuffmanLine(-31, 4, 8),
        new Jbig2HuffmanLine(-11, 2, 8),
        new Jbig2HuffmanLine(-15, 2, 9),
        new Jbig2HuffmanLine(-7, 1, 9),
        new Jbig2HuffmanLine(-32, 32, 9, isLowerRange: true),
        new Jbig2HuffmanLine(3339, 32, 9),
    ]);

    /// <summary>
    /// Table B.10 — signed delta-S with OOB (SBHUFFDS selector 2).
    /// </summary>
    public static Jbig2HuffmanTable TableB10 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(-2, 2, 2),
        new Jbig2HuffmanLine(6, 6, 2),
        new Jbig2HuffmanLine(0, 0, 2, isOob: true),
        new Jbig2HuffmanLine(-3, 0, 5),
        new Jbig2HuffmanLine(2, 0, 5),
        new Jbig2HuffmanLine(70, 5, 5),
        new Jbig2HuffmanLine(3, 0, 6),
        new Jbig2HuffmanLine(102, 5, 6),
        new Jbig2HuffmanLine(134, 6, 6),
        new Jbig2HuffmanLine(198, 7, 6),
        new Jbig2HuffmanLine(326, 8, 6),
        new Jbig2HuffmanLine(582, 9, 6),
        new Jbig2HuffmanLine(1094, 10, 6),
        new Jbig2HuffmanLine(-21, 4, 7),
        new Jbig2HuffmanLine(-4, 0, 7),
        new Jbig2HuffmanLine(4, 0, 7),
        new Jbig2HuffmanLine(2118, 11, 7),
        new Jbig2HuffmanLine(-5, 0, 8),
        new Jbig2HuffmanLine(5, 0, 8),
        new Jbig2HuffmanLine(-22, 32, 8, isLowerRange: true),
        new Jbig2HuffmanLine(4166, 32, 8),
    ]);

    /// <summary>
    /// Table B.11 — unsigned delta-T (SBHUFFDT selector 0).
    /// </summary>
    public static Jbig2HuffmanTable TableB11 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(1, 0, 1),
        new Jbig2HuffmanLine(2, 1, 2),
        new Jbig2HuffmanLine(4, 0, 4),
        new Jbig2HuffmanLine(5, 1, 4),
        new Jbig2HuffmanLine(7, 1, 5),
        new Jbig2HuffmanLine(9, 2, 5),
        new Jbig2HuffmanLine(13, 2, 6),
        new Jbig2HuffmanLine(17, 2, 7),
        new Jbig2HuffmanLine(21, 3, 7),
        new Jbig2HuffmanLine(29, 4, 7),
        new Jbig2HuffmanLine(45, 5, 7),
        new Jbig2HuffmanLine(77, 6, 7),
        new Jbig2HuffmanLine(141, 32, 7),
    ]);

    /// <summary>
    /// Table B.12 — unsigned delta-T (SBHUFFDT selector 1).
    /// </summary>
    public static Jbig2HuffmanTable TableB12 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(1, 0, 1),
        new Jbig2HuffmanLine(2, 0, 2),
        new Jbig2HuffmanLine(3, 1, 3),
        new Jbig2HuffmanLine(5, 0, 5),
        new Jbig2HuffmanLine(6, 1, 5),
        new Jbig2HuffmanLine(8, 1, 6),
        new Jbig2HuffmanLine(10, 0, 7),
        new Jbig2HuffmanLine(11, 1, 7),
        new Jbig2HuffmanLine(13, 2, 7),
        new Jbig2HuffmanLine(17, 3, 7),
        new Jbig2HuffmanLine(25, 4, 7),
        new Jbig2HuffmanLine(41, 5, 8),
        new Jbig2HuffmanLine(73, 32, 8),
    ]);

    /// <summary>
    /// Table B.13 — unsigned delta-T (SBHUFFDT selector 2).
    /// </summary>
    public static Jbig2HuffmanTable TableB13 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(1, 0, 1),
        new Jbig2HuffmanLine(2, 0, 3),
        new Jbig2HuffmanLine(7, 3, 3),
        new Jbig2HuffmanLine(3, 0, 4),
        new Jbig2HuffmanLine(5, 1, 4),
        new Jbig2HuffmanLine(4, 0, 5),
        new Jbig2HuffmanLine(15, 1, 6),
        new Jbig2HuffmanLine(17, 2, 6),
        new Jbig2HuffmanLine(21, 3, 6),
        new Jbig2HuffmanLine(29, 4, 6),
        new Jbig2HuffmanLine(45, 5, 6),
        new Jbig2HuffmanLine(77, 6, 7),
        new Jbig2HuffmanLine(141, 32, 7),
    ]);

    /// <summary>
    /// Table B.14 — simple signed (used in some refinement contexts).
    /// </summary>
    public static Jbig2HuffmanTable TableB14 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(0, 0, 1),
        new Jbig2HuffmanLine(-2, 0, 3),
        new Jbig2HuffmanLine(-1, 0, 3),
        new Jbig2HuffmanLine(1, 0, 3),
        new Jbig2HuffmanLine(2, 0, 3),
    ]);

    /// <summary>
    /// Table B.15 — signed with lower/upper ranges (used in some refinement contexts).
    /// </summary>
    public static Jbig2HuffmanTable TableB15 { get; } = Jbig2HuffmanTable.Build(
    [
        new Jbig2HuffmanLine(0, 0, 1),
        new Jbig2HuffmanLine(-1, 0, 3),
        new Jbig2HuffmanLine(1, 0, 3),
        new Jbig2HuffmanLine(-2, 0, 4),
        new Jbig2HuffmanLine(2, 0, 4),
        new Jbig2HuffmanLine(-4, 1, 5),
        new Jbig2HuffmanLine(3, 1, 5),
        new Jbig2HuffmanLine(-8, 2, 6),
        new Jbig2HuffmanLine(5, 2, 6),
        new Jbig2HuffmanLine(-24, 4, 7),
        new Jbig2HuffmanLine(9, 4, 7),
        new Jbig2HuffmanLine(-25, 32, 7, isLowerRange: true),
        new Jbig2HuffmanLine(25, 32, 7),
    ]);

    /// <summary>
    /// Retrieves the next custom Huffman table from the referred tables list.
    /// Falls back to <paramref name="fallback"/> if not available.
    /// </summary>
    /// <param name="customTables">Custom tables from referred segments.</param>
    /// <param name="index">Current index (advanced on success).</param>
    /// <param name="fallback">Table to return when no custom table is available.</param>
    /// <returns>The next custom table, or the fallback.</returns>
    public static Jbig2HuffmanTable GetCustomTable(
        List<Jbig2HuffmanTable> customTables,
        ref int index,
        Jbig2HuffmanTable fallback)
    {
        if (customTables != null && index < customTables.Count)
        {
            return customTables[index++];
        }

        return fallback;
    }

    /// <summary>
    /// Selects the symbol dictionary delta-height table (SDHUFFDH, ITU-T T.88 Section 7.4.2.1.6).
    /// </summary>
    public static Jbig2HuffmanTable SelectDeltaHeight(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        switch (selection)
        {
            case 0:
                return TableB4;
            case 1:
                return TableB5;
            case 3:
                return GetCustomTable(customTables, ref customIndex, TableB4);
            default:
                return TableB4;
        }
    }

    /// <summary>
    /// Selects the symbol dictionary delta-width table (SDHUFFDW, ITU-T T.88 Section 7.4.2.1.6).
    /// </summary>
    public static Jbig2HuffmanTable SelectDeltaWidth(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        switch (selection)
        {
            case 0:
                return TableB2;
            case 1:
                return TableB3;
            case 3:
                return GetCustomTable(customTables, ref customIndex, TableB2);
            default:
                return TableB2;
        }
    }

    /// <summary>
    /// Selects the symbol dictionary bitmap-size table (SDHUFFBMSIZE, ITU-T T.88 Section 7.4.2.1.6).
    /// </summary>
    public static Jbig2HuffmanTable SelectBitmapSize(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        if (selection == 0)
        {
            return TableB1;
        }

        return GetCustomTable(customTables, ref customIndex, TableB1);
    }

    /// <summary>
    /// Selects the symbol dictionary aggregate-instances table (SDHUFFAGGINST, ITU-T T.88 Section 7.4.2.1.6).
    /// </summary>
    public static Jbig2HuffmanTable SelectAggregateInstances(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        if (selection == 0)
        {
            return TableB1;
        }

        return GetCustomTable(customTables, ref customIndex, TableB1);
    }

    /// <summary>
    /// Selects the text region first-S table (SBHUFFFS, ITU-T T.88 Section 7.4.3.1.6).
    /// </summary>
    public static Jbig2HuffmanTable SelectFirstS(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        switch (selection)
        {
            case 0:
                return TableB6;
            case 1:
                return TableB7;
            case 3:
                return GetCustomTable(customTables, ref customIndex, TableB6);
            default:
                return TableB6;
        }
    }

    /// <summary>
    /// Selects the text region delta-S table (SBHUFFDS, ITU-T T.88 Section 7.4.3.1.6).
    /// </summary>
    public static Jbig2HuffmanTable SelectDeltaS(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        switch (selection)
        {
            case 0:
                return TableB8;
            case 1:
                return TableB9;
            case 2:
                return TableB10;
            case 3:
                return GetCustomTable(customTables, ref customIndex, TableB8);
            default:
                return TableB8;
        }
    }

    /// <summary>
    /// Selects the text region delta-T table (SBHUFFDT, ITU-T T.88 Section 7.4.3.1.6).
    /// </summary>
    public static Jbig2HuffmanTable SelectDeltaT(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        switch (selection)
        {
            case 0:
                return TableB11;
            case 1:
                return TableB12;
            case 2:
                return TableB13;
            case 3:
                return GetCustomTable(customTables, ref customIndex, TableB11);
            default:
                return TableB11;
        }
    }

    /// <summary>
    /// Selects a refinement dimension table (SBHUFFRDW/RDH/RDX/RDY, ITU-T T.88 Section 7.4.3.1.6).
    /// Selection: 0→B.14, 1→B.15, 3→custom.
    /// </summary>
    public static Jbig2HuffmanTable SelectRefinementDimension(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        switch (selection)
        {
            case 0:
                return TableB14;
            case 1:
                return TableB15;
            case 3:
                return GetCustomTable(customTables, ref customIndex, TableB14);
            default:
                return TableB14;
        }
    }

    /// <summary>
    /// Selects the refinement bitmap size table (SBHUFFRSIZE, ITU-T T.88 Section 7.4.3.1.6).
    /// Selection: 0→B.1, 1→custom.
    /// </summary>
    public static Jbig2HuffmanTable SelectRefinementSize(
        int selection,
        List<Jbig2HuffmanTable> customTables,
        ref int customIndex)
    {
        if (selection == 0)
        {
            return TableB1;
        }

        return GetCustomTable(customTables, ref customIndex, TableB1);
    }
}
