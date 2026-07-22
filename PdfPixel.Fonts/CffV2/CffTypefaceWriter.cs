using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Writes a <see cref="CffTypeface"/> model back out to CFF binary data.
/// </summary>
/// <remarks>
/// Every <see cref="CffCharacter.CharString"/> is already hint-free and subroutine-free, so the
/// written font never needs Local or Global Subrs: the Global Subr INDEX is always empty and no
/// Private DICT carries a Subrs entry.
/// </remarks>
public partial class CffTypefaceWriter
{
    /// <summary>
    /// Writes a typeface to CFF binary data.
    /// </summary>
    /// <param name="typeface">The typeface to write.</param>
    /// <returns>The complete CFF table bytes.</returns>
    public byte[] Write(CffTypeface typeface)
    {
        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        foreach (CffFont font in typeface.Fonts)
        {
            CombineFdFontMatrices(font);
        }

        CffBinaryWriter headerWriter = new();
        headerWriter.WriteHeader();
        byte[] header = headerWriter.ToArray();

        var fontNames = new ReadOnlyMemory<byte>[typeface.Fonts.Length];
        for (int fontIndex = 0; fontIndex < typeface.Fonts.Length; fontIndex++)
        {
            fontNames[fontIndex] = typeface.Fonts[fontIndex].Name.Value;
        }

        CffBinaryWriter nameIndexWriter = new();
        nameIndexWriter.WriteIndex(fontNames);
        byte[] nameIndex = nameIndexWriter.ToArray();

        CffBinaryWriter stringIndexWriter = new();
        stringIndexWriter.WriteIndex(typeface.Strings);
        byte[] stringIndex = stringIndexWriter.ToArray();

        CffBinaryWriter globalSubrIndexWriter = new();
        globalSubrIndexWriter.WriteIndex(Array.Empty<ReadOnlyMemory<byte>>());
        byte[] globalSubrIndex = globalSubrIndexWriter.ToArray();

        var fontBlocks = new FontBlock[typeface.Fonts.Length];
        for (int fontIndex = 0; fontIndex < typeface.Fonts.Length; fontIndex++)
        {
            fontBlocks[fontIndex] = BuildFontBlock(typeface.Fonts[fontIndex]);
        }

        // A Top DICT's own byte length only depends on which operators are present, never on the
        // value of an offset operand (see CffBinaryWriter.WriteDictOffset), so building it once with
        // each font block's not-yet-resolved (zero) offsets already yields the real, final length --
        // enough to size the Top DICT INDEX and lay out everything that follows in a single pass.
        var topDictEntries = new ReadOnlyMemory<byte>[typeface.Fonts.Length];
        for (int fontIndex = 0; fontIndex < typeface.Fonts.Length; fontIndex++)
        {
            topDictEntries[fontIndex] = BuildTopDict(typeface.Fonts[fontIndex].Dict.TopDict, fontBlocks[fontIndex]);
        }

        CffBinaryWriter topDictIndexWriter = new();
        topDictIndexWriter.WriteIndex(topDictEntries);
        byte[] topDictIndex = topDictIndexWriter.ToArray();

        int blockStart = header.Length + nameIndex.Length + topDictIndex.Length + stringIndex.Length + globalSubrIndex.Length;
        for (int fontIndex = 0; fontIndex < fontBlocks.Length; fontIndex++)
        {
            fontBlocks[fontIndex].ResolveOffsets(blockStart);
            blockStart += fontBlocks[fontIndex].TotalLength;
        }

        for (int fontIndex = 0; fontIndex < typeface.Fonts.Length; fontIndex++)
        {
            topDictEntries[fontIndex] = BuildTopDict(typeface.Fonts[fontIndex].Dict.TopDict, fontBlocks[fontIndex]);
        }

        CffBinaryWriter finalTopDictIndexWriter = new();
        finalTopDictIndexWriter.WriteIndex(topDictEntries);
        byte[] finalTopDictIndex = finalTopDictIndexWriter.ToArray();

        CffBinaryWriter output = new();
        output.WriteBytes(header);
        output.WriteBytes(nameIndex);
        output.WriteBytes(finalTopDictIndex);
        output.WriteBytes(stringIndex);
        output.WriteBytes(globalSubrIndex);
        foreach (FontBlock fontBlock in fontBlocks)
        {
            output.WriteBytes(fontBlock.GetData());
        }

        return output.ToArray();
    }

    // HACK: some CFF consumers combine a CID font's Top DICT and FD FontMatrix inconsistently (or not at
    // all). Baking the combined result into every FD entry and dropping the Top DICT's own copy makes
    // the outcome consistent regardless of which one a given consumer actually reads.
    private static void CombineFdFontMatrices(CffFont font)
    {
        PdfFontMatrix? topFontMatrix = font.Dict.TopDict.FontMatrix;
        if (font.FdArray.Length == 0 || !topFontMatrix.HasValue)
        {
            return;
        }

        foreach (CffFontDict fontDict in font.FdArray)
        {
            fontDict.TopDict.FontMatrix = CffTopDict.CombineFontMatrix(font.Dict.TopDict, fontDict.TopDict);
        }

        font.Dict.TopDict.FontMatrix = null;
    }

    private static ReadOnlyMemory<byte> BuildTopDict(CffTopDict topDict, FontBlock block)
    {
        CffBinaryWriter writer = new();
        WriteTopDictContentFields(writer, topDict);

        if (block.HasEncoding)
        {
            writer.WriteDictOffset(block.EncodingOffsetValue);
            writer.WriteDictOperator(CffConstants.TopDictOperatorEncoding);
        }

        writer.WriteDictOffset(block.CharsetOffsetValue);
        writer.WriteDictOperator(CffConstants.TopDictOperatorCharset);

        writer.WriteDictOffset(block.CharStringsOffsetValue);
        writer.WriteDictOperator(CffConstants.TopDictOperatorCharStrings);

        if (block.HasPrivateDict)
        {
            writer.WriteDictNumber(block.PrivateDictData.Length);
            writer.WriteDictOffset(block.PrivateDictOffsetValue);
            writer.WriteDictOperator(CffConstants.TopDictOperatorPrivate);
        }

        if (block.HasFdArray)
        {
            writer.WriteDictOffset(block.FdArrayOffsetValue);
            writer.WriteDictEscapedOperator(CffConstants.TopDictEscapedOperatorFdArray);
        }

        if (block.HasFdSelect)
        {
            writer.WriteDictOffset(block.FdSelectOffsetValue);
            writer.WriteDictEscapedOperator(CffConstants.TopDictEscapedOperatorFdSelect);
        }

        return writer.ToArray();
    }

    private static void WriteTopDictContentFields(CffBinaryWriter writer, CffTopDict topDict)
    {
        if (topDict.Weight.HasValue)
        {
            writer.WriteDictNumber(topDict.Weight.Value);
            writer.WriteDictOperator(CffConstants.TopDictOperatorWeight);
        }

        if (topDict.FontBBox != null)
        {
            foreach (float component in topDict.FontBBox)
            {
                writer.WriteDictNumber(component);
            }

            writer.WriteDictOperator(CffConstants.TopDictOperatorFontBBox);
        }

        if (topDict.ItalicAngle.HasValue)
        {
            writer.WriteDictNumber(topDict.ItalicAngle.Value);
            writer.WriteDictEscapedOperator(CffConstants.TopDictEscapedOperatorItalicAngle);
        }

        if (topDict.PaintType.HasValue)
        {
            writer.WriteDictNumber(topDict.PaintType.Value);
            writer.WriteDictEscapedOperator(CffConstants.TopDictEscapedOperatorPaintType);
        }

        if (topDict.CharstringType.HasValue)
        {
            writer.WriteDictNumber(topDict.CharstringType.Value);
            writer.WriteDictEscapedOperator(CffConstants.TopDictEscapedOperatorCharstringType);
        }

        if (topDict.FontMatrix.HasValue)
        {
            PdfFontMatrix matrix = topDict.FontMatrix.Value;

            // Skia's CFF glyph rasterizer returns an empty path for every glyph when FontMatrix carries
            // a nonzero translation (e/f) -- Acrobat renders such glyphs at their untranslated position
            // instead, so translation is dropped here to match that observed real-world behavior.
            foreach (float component in new PdfFontMatrix(matrix.ScaleX, matrix.SkewX, 0f, matrix.SkewY, matrix.ScaleY, 0f).ToArray())
            {
                writer.WriteDictNumber(component);
            }

            writer.WriteDictEscapedOperator(CffConstants.TopDictEscapedOperatorFontMatrix);
        }

        if (topDict.StrokeWidth.HasValue)
        {
            writer.WriteDictNumber(topDict.StrokeWidth.Value);
            writer.WriteDictEscapedOperator(CffConstants.TopDictEscapedOperatorStrokeWidth);
        }

        if (topDict.Ros != null)
        {
            writer.WriteDictNumber(topDict.Ros.Registry);
            writer.WriteDictNumber(topDict.Ros.Ordering);
            writer.WriteDictNumber(topDict.Ros.Supplement);
            writer.WriteDictEscapedOperator(CffConstants.TopDictEscapedOperatorRos);
        }

        foreach (CffDictEntry entry in topDict.Entries)
        {
            WriteDictEntry(writer, entry);
        }
    }

    private static void WriteDictEntry(CffBinaryWriter writer, CffDictEntry entry)
    {
        foreach (ICffValue operand in entry.Operands)
        {
            if (operand.Type == CffValueType.Integer)
            {
                writer.WriteDictNumber(operand.AsInt());
            }
            else
            {
                writer.WriteDictNumber(operand.AsFloat());
            }
        }

        if (entry.Operator.Type == CffValueType.EscapedOperator)
        {
            writer.WriteDictEscapedOperator(((CffValue<byte>)entry.Operator).Value);
        }
        else
        {
            writer.WriteDictOperator(((CffValue<byte>)entry.Operator).Value);
        }
    }
}
