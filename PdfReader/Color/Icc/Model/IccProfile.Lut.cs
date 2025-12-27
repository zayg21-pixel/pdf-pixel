using PdfReader.Color.Icc.Utilities;

namespace PdfReader.Color.Icc.Model;

/// <summary>
/// Partial ICC profile implementation: parsing of LUT-based A2B pipelines (lut8 / lut16, mAB).
/// Keeps raw LUT data for evaluation; evaluation logic is handled elsewhere.
/// </summary>
internal sealed partial class IccProfile
{
    /// <summary>
    /// Parse a LUT-based A2B tag entry (supports legacy lut8 / lut16 and mAB multi-process elements).
    /// </summary>
    /// <param name="reader">Big endian reader.</param>
    /// <param name="tag">Tag entry.</param>
    internal static IccLutPipeline ParseA2BLut(BigEndianReader reader, IccTagEntry tag)
    {
        if (reader == null)
        {
            return null;
        }

        if (tag == null)
        {
            return null;
        }

        uint typeSignature = reader.ReadUInt32(tag.Offset);

        if (typeSignature == BigEndianReader.FourCC(IccConstants.TypeLut8))
        {
            return ParseLut8(reader, tag.Offset, tag.Size);
        }

        if (typeSignature == BigEndianReader.FourCC(IccConstants.TypeLut16))
        {
            return ParseLut16(reader, tag.Offset, tag.Size);
        }

        if (typeSignature == BigEndianReader.FourCC(IccConstants.TypeMAB))
        {
            return ParseMab(reader, tag.Offset, tag.Size);
        }

        return null;
    }

    /// <summary>
    /// Parse legacy lut8 A2B structure.
    /// </summary>
    private static IccLutPipeline ParseLut8(BigEndianReader reader, int tagOffset, int tagSize)
    {
        int inputChannels = reader.ReadByte(tagOffset + 8);
        int outputChannels = reader.ReadByte(tagOffset + 9);
        int uniformGridPoints = reader.ReadByte(tagOffset + 10);

        // 3x3 matrix (s15Fixed16)
        float[,] matrix = new float[3, 3];
        int matrixPos = tagOffset + 12;
        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < 3; columnIndex++)
            {
                int raw = reader.ReadInt32(matrixPos + (rowIndex * 3 + columnIndex) * 4);
                matrix[rowIndex, columnIndex] = BigEndianReader.S15Fixed16ToSingle(raw);
            }
        }

        int cursor = matrixPos + 9 * 4;

        // Input tables: inputChannels * 256 bytes each
        IccTrc[] inputTables = new IccTrc[inputChannels];
        for (int channel = 0; channel < inputChannels; channel++)
        {
            float[] table = new float[256];
            for (int i = 0; i < 256; i++)
            {
                table[i] = reader.ReadByte(cursor + channel * 256 + i) / 255f;
            }

            inputTables[channel] = IccTrc.FromSamples(table);
        }
        cursor += inputChannels * 256;

        // CLUT
        int gridTotal = 1;
        for (int d = 0; d < inputChannels; d++)
        {
            gridTotal *= uniformGridPoints;
        }

        int clutSampleCount = gridTotal * outputChannels;
        float[] clut = new float[clutSampleCount];
        for (int i = 0; i < clutSampleCount; i++)
        {
            clut[i] = reader.ReadByte(cursor + i) / 255f;
        }
        cursor += clutSampleCount;

        // Output tables
        IccTrc[] outputTables = new IccTrc[outputChannels];
        for (int channel = 0; channel < outputChannels; channel++)
        {
            float[] table = new float[256];
            for (int i = 0; i < 256; i++)
            {
                table[i] = reader.ReadByte(cursor + channel * 256 + i) / 255f;
            }

            outputTables[channel] = IccTrc.FromSamples(table);
        }

        var gridPerDim = new int[inputChannels];
        for (int i = 0; i < inputChannels; i++)
        {
            gridPerDim[i] = uniformGridPoints;
        }

        return new IccLutPipeline(inputChannels, outputChannels, gridPerDim, inputTables, clut, outputTables, matrix);
    }

    /// <summary>
    /// Parse legacy lut16 A2B structure.
    /// </summary>
    private static IccLutPipeline ParseLut16(BigEndianReader reader, int tagOffset, int tagSize)
    {
        int inputChannels = reader.ReadByte(tagOffset + 8);
        int outputChannels = reader.ReadByte(tagOffset + 9);
        int uniformGridPoints = reader.ReadByte(tagOffset + 10);

        float[,] matrix = new float[3, 3];
        int matrixPos = tagOffset + 12;
        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < 3; columnIndex++)
            {
                int raw = reader.ReadInt32(matrixPos + (rowIndex * 3 + columnIndex) * 4);
                matrix[rowIndex, columnIndex] = BigEndianReader.S15Fixed16ToSingle(raw);
            }
        }

        int cursor = matrixPos + 9 * 4;
        int inputTableEntries = reader.ReadUInt16(cursor);
        int outputTableEntries = reader.ReadUInt16(cursor + 2);
        cursor += 4;

        IccTrc[] inputTables = new IccTrc[inputChannels];
        for (int channel = 0; channel < inputChannels; channel++)
        {
            float[] table = new float[inputTableEntries];
            for (int i = 0; i < inputTableEntries; i++)
            {
                table[i] = reader.ReadUInt16(cursor + (channel * inputTableEntries + i) * 2) / 65535f;
            }
            inputTables[channel] = IccTrc.FromSamples(table);
        }
        cursor += inputChannels * inputTableEntries * 2;

        int gridTotal = 1;
        for (int d = 0; d < inputChannels; d++)
        {
            gridTotal *= uniformGridPoints;
        }
        int clutSampleCount = gridTotal * outputChannels;
        float[] clut = new float[clutSampleCount];
        for (int i = 0; i < clutSampleCount; i++)
        {
            clut[i] = reader.ReadUInt16(cursor + i * 2) / 65535f;
        }
        cursor += clutSampleCount * 2;

        IccTrc[] outputTables = new IccTrc[outputChannels];
        for (int channel = 0; channel < outputChannels; channel++)
        {
            float[] table = new float[outputTableEntries];
            for (int i = 0; i < outputTableEntries; i++)
            {
                table[i] = reader.ReadUInt16(cursor + (channel * outputTableEntries + i) * 2) / 65535f;
            }
            outputTables[channel] = IccTrc.FromSamples(table);
        }

        var gridPerDim = new int[inputChannels];
        for (int i = 0; i < inputChannels; i++)
        {
            gridPerDim[i] = uniformGridPoints;
        }

        return new IccLutPipeline(inputChannels, outputChannels, gridPerDim, inputTables, clut, outputTables, matrix);
    }

    /// <summary>
    /// Parse mAB (multi-process elements) A2B structure.
    /// </summary>
    private static IccLutPipeline ParseMab(BigEndianReader reader, int tagStart, int tagSize)
    {
        if (reader == null)
        {
            return null;
        }

        const int HeaderSize = 32;
        if (tagSize < HeaderSize)
        {
            return null;
        }

        int inputChannels = reader.ReadByte(tagStart + 8);
        int outputChannels = reader.ReadByte(tagStart + 9);
        if (inputChannels <= 0 || outputChannels <= 0 || inputChannels > 8 || outputChannels > 8)
        {
            return null;
        }

        uint offsetB = reader.ReadUInt32(tagStart + 12);
        uint offsetMatrix = reader.ReadUInt32(tagStart + 16);
        uint offsetM = reader.ReadUInt32(tagStart + 20);
        uint offsetClut = reader.ReadUInt32(tagStart + 24);
        uint offsetA = reader.ReadUInt32(tagStart + 28);

        IccTrc[] curvesA = null;
        IccTrc[] curvesB = null;
        IccTrc[] curvesM = null;

        if (offsetA != 0)
        {
            int posA = tagStart + (int)offsetA;
            if (!IsInsideTag(posA, tagStart, tagSize))
            {
                return null;
            }
            curvesA = ParseCurveSequence(reader, posA, inputChannels);
        }

        if (offsetB != 0)
        {
            int posB = tagStart + (int)offsetB;
            if (!IsInsideTag(posB, tagStart, tagSize))
            {
                return null;
            }
            curvesB = ParseCurveSequence(reader, posB, outputChannels);
        }

        if (offsetM != 0)
        {
            int posM = tagStart + (int)offsetM;
            if (!IsInsideTag(posM, tagStart, tagSize))
            {
                return null;
            }
            curvesM = ParseCurveSequence(reader, posM, outputChannels);
        }

        float[,] matrix3x3 = null;
        float[] matrixOffset = null;
        if (offsetMatrix != 0 && inputChannels == 3)
        {
            int posMatrix = tagStart + (int)offsetMatrix;
            const int MatrixBlockSize = 48; // 9 + 3 s15Fixed16 values
            if (!IsInsideTag(posMatrix, tagStart, tagSize) || !IsInsideTag(posMatrix + MatrixBlockSize - 1, tagStart, tagSize))
            {
                return null;
            }
            matrix3x3 = new float[3, 3];
            for (int rowIndex = 0; rowIndex < 3; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < 3; columnIndex++)
                {
                    int raw = reader.ReadInt32(posMatrix + (rowIndex * 3 + columnIndex) * 4);
                    matrix3x3[rowIndex, columnIndex] = BigEndianReader.S15Fixed16ToSingle(raw);
                }
            }
            matrixOffset = new float[3];
            for (int i = 0; i < 3; i++)
            {
                int raw = reader.ReadInt32(posMatrix + 9 * 4 + i * 4);
                matrixOffset[i] = BigEndianReader.S15Fixed16ToSingle(raw);
            }
        }

        int[] gridPerDim = null;
        float[] clut = null;
        byte precision = 0;
        if (offsetClut != 0)
        {
            int clutPos = tagStart + (int)offsetClut;

            gridPerDim = new int[inputChannels];
            for (int i = 0; i < inputChannels; i++)
            {
                int gp = reader.ReadByte(clutPos + i);
                gridPerDim[i] = gp <= 0 ? 1 : gp;
            }

            // Precision is fixed at payload offset + 16 per ICC spec
            precision = reader.ReadByte(clutPos + 16);
            if (precision != 1 && precision != 2)
            {
                return null;
            }

            // Data starts after 4 bytes of padding at payload + 20
            int dataStart = clutPos + 20;
            if (!IsInsideTag(dataStart, tagStart, tagSize))
            {
                return null;
            }

            long pointCount = 1;
            for (int i = 0; i < inputChannels; i++)
            {
                pointCount *= gridPerDim[i];
                if (pointCount <= 0)
                {
                    return null;
                }
            }

            long sampleCount = pointCount * outputChannels;
            if (sampleCount <= 0 || sampleCount > int.MaxValue)
            {
                return null;
            }

            long byteCount = precision == 1 ? sampleCount : sampleCount * 2;
            int endPos = dataStart + (int)byteCount - 1;
            if (!IsInsideTag(endPos, tagStart, tagSize))
            {
                return null;
            }

            clut = new float[sampleCount];
            if (precision == 1)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    clut[i] = reader.ReadByte(dataStart + i) / 255f;
                }
            }
            else
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    clut[i] = reader.ReadUInt16(dataStart + i * 2) / 65535f;
                }
            }
        }

        IccLutPipeline pipeline = IccLutPipeline.CreateMab(inputChannels, outputChannels, gridPerDim, curvesA, curvesM, curvesB, clut, matrix3x3, matrixOffset);
        return pipeline;
    }

    private static bool IsInsideTag(int absoluteOffset, int tagStart, int tagSize)
    {
        return absoluteOffset >= tagStart && absoluteOffset < tagStart + tagSize;
    }

    private static IccTrc[] ParseCurveSequence(BigEndianReader reader, int sequenceStart, int count)
    {
        IccTrc[] list = new IccTrc[count];
        int cursor = sequenceStart;
        for (int i = 0; i < count; i++)
        {
            list[i] = ReadTrcPayload(reader, cursor, out int curveSize);
            int next = cursor + curveSize;
            int pad = (4 - (next & 3)) & 3;
            cursor = next + pad;
        }
        return list;
    }
}
