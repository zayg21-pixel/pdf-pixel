using PdfPixel.Color.Icc.Utilities;
using System;
using System.Collections.Generic;

namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// Partial class containing ICC profile parsing logic (tag table + selected tag decoders).
/// Kept separate from the data container portion for readability and maintenance.
/// </summary>
internal sealed partial class IccProfile
{
    /// <summary>
    /// Parse an ICC profile from raw byte content.
    /// </summary>
    /// <param name="data">Complete ICC profile bytes.</param>
    /// <returns>Parsed <see cref="IccProfile"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when data is null or shorter than the 128-byte header.</exception>
    public static IccProfile Parse(byte[] data)
    {
        if (data == null || data.Length < 132)
        {
            throw new ArgumentException("Invalid ICC profile: too short", nameof(data));
        }

        var reader = new BigEndianReader(data);
        var profile = new IccProfile(data, IccProfileHeader.Read(reader));

        ParseTagDirectory(reader, profile);
        ParseSelectedTags(reader, profile);

        var lut = profile.A2BLut0 ?? profile.A2BLut1 ?? profile.A2BLut2;

        if (lut != null)
        {
            profile.ChannelsCount = lut.InChannels;
        }
        else if (profile.Header.ColorSpace == IccColorSpace.Lab)
        {
            profile.ChannelsCount = 3;
        }
        else if (profile.RedMatrix != null && profile.GreenMatrix != null && profile.BlueMatrix != null)
        {
            profile.ChannelsCount = 3;
        }
        else if (profile.GrayTrc != null)
        {
            profile.ChannelsCount = 1;
        }

        return profile;
    }

    private static void ParseTagDirectory(BigEndianReader reader, IccProfile profile)
    {
        int tagCount = reader.ReadInt32(128);
        var entries = new List<IccTagEntry>(Math.Max(0, tagCount));
        int cursor = 132; // 128 header + 4 count
        for (int index = 0; index < tagCount; index++)
        {
            if (!reader.CanRead(cursor, 12))
            {
                break; // Stop on truncated directory
            }

            uint signature = reader.ReadUInt32(cursor);
            int offset = reader.ReadInt32(cursor + 4);
            int size = reader.ReadInt32(cursor + 8);
            entries.Add(new IccTagEntry(signature, offset, size));
            cursor += 12;
        }
        profile.Tags = entries;
    }

    private static void ParseSelectedTags(BigEndianReader reader, IccProfile profile)
    {
        if (profile.Tags == null)
        {
            return;
        }

        foreach (var tag in profile.Tags)
        {
            switch (tag.SignatureString)
            {
                case IccConstants.TagWtpt:
                    profile.WhitePoint = ReadXyzType(reader, tag);
                    break;
                case IccConstants.TagBkpt:
                    profile.BlackPoint = ReadXyzType(reader, tag);
                    break;
                case IccConstants.Tag_rXYZ:
                    profile.RedMatrix = ReadXyzType(reader, tag);
                    break;
                case IccConstants.Tag_gXYZ:
                    profile.GreenMatrix = ReadXyzType(reader, tag);
                    break;
                case IccConstants.Tag_bXYZ:
                    profile.BlueMatrix = ReadXyzType(reader, tag);
                    break;
                case IccConstants.Tag_rTRC:
                    profile.RedTrc = ReadTrcType(reader, tag);
                    break;
                case IccConstants.Tag_gTRC:
                    profile.GreenTrc = ReadTrcType(reader, tag);
                    break;
                case IccConstants.Tag_bTRC:
                    profile.BlueTrc = ReadTrcType(reader, tag);
                    break;
                case IccConstants.Tag_kTRC:
                    profile.GrayTrc = ReadTrcType(reader, tag);
                    break;
                case IccConstants.TagChad:
                    profile.ChromaticAdaptation = ReadChadMatrix(reader, tag);
                    break;
                case IccConstants.TagDesc:
                    profile.Description = ReadDescType(reader, tag);
                    break;
                case IccConstants.TagMluc:
                    if (string.IsNullOrEmpty(profile.Description))
                    {
                        profile.Description = ReadMlucType(reader, tag);
                    }
                    break;
                case IccConstants.TagA2B0:
                    profile.A2BLut0 = ParseA2BLut(reader, tag);
                    break;
                case IccConstants.TagA2B1:
                    profile.A2BLut1 = ParseA2BLut(reader, tag);
                    break;
                case IccConstants.TagA2B2:
                    profile.A2BLut2 = ParseA2BLut(reader, tag);
                    break;
            }
        }
    }

    #region Tag Readers (shared with other partials)

    private static IccXyz? ReadXyzType(BigEndianReader reader, IccTagEntry tag)
    {
        if (!reader.CanRead(tag.Offset, 20))
        {
            return null;
        }
        uint type = reader.ReadUInt32(tag.Offset);
        if (type != BigEndianReader.FourCC(IccConstants.TypeXYZ))
        {
            return null;
        }
        int x = reader.ReadInt32(tag.Offset + 8);
        int y = reader.ReadInt32(tag.Offset + 12);
        int z = reader.ReadInt32(tag.Offset + 16);
        return new IccXyz(
            BigEndianReader.S15Fixed16ToSingle(x),
            BigEndianReader.S15Fixed16ToSingle(y),
            BigEndianReader.S15Fixed16ToSingle(z));
    }

    /// <summary>
    /// Common TRC payload reader. Handles both 'curv' and 'para' types at the given position.
    /// Returns the decoded <see cref="IccTrc"/> and outputs the payload size for alignment.
    /// </summary>
    private static IccTrc ReadTrcPayload(BigEndianReader reader, int pos, out int payloadSize)
    {
        payloadSize = 12;
        if (!reader.CanRead(pos, 12))
        {
            return null;
        }

        uint type = reader.ReadUInt32(pos);
        if (type == BigEndianReader.FourCC(IccConstants.TypeCurv))
        {
            uint count = reader.ReadUInt32(pos + 8);
            if (count == 1)
            {
                if (!reader.CanRead(pos + 12, 2))
                {
                    payloadSize = 14;
                    return null;
                }
                ushort gammaFixed = reader.ReadUInt16(pos + 12);
                payloadSize = 14;
                return IccTrc.FromGamma(BigEndianReader.U8Fixed8ToSingle(gammaFixed));
            }

            int sampleCount = (int)Math.Min(int.MaxValue, count);
            payloadSize = 12 + sampleCount * 2;

            if (!reader.CanRead(pos + 12, sampleCount * 2))
            {
                return IccTrc.FromSamples(new float[sampleCount]);
            }

            float[] samples = new float[sampleCount];
            int dataPos = pos + 12;
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = reader.ReadUInt16(dataPos + i * 2) / 65535f;
            }
            return IccTrc.FromSamples(samples);
        }

        if (type == BigEndianReader.FourCC(IccConstants.TypePara))
        {
            if (!reader.CanRead(pos, 16))
            {
                payloadSize = 16;
                return null;
            }

            ushort funcType = reader.ReadUInt16(pos + 8);
            int paramCount = GetParamCount(funcType);
            payloadSize = 12 + paramCount * 4;
            if (!reader.CanRead(pos + 12, paramCount * 4))
            {
                return IccTrc.FromParametric((IccTrcParametricType)funcType, default);
            }

            if (funcType == 0 && paramCount >= 1)
            {
                int gammaRaw = reader.ReadInt32(pos + 12);
                return IccTrc.FromGamma(BigEndianReader.S15Fixed16ToSingle(gammaRaw));
            }

            float[] parameters = new float[paramCount];
            for (int i = 0; i < paramCount; i++)
            {
                parameters[i] = BigEndianReader.S15Fixed16ToSingle(reader.ReadInt32(pos + 12 + i * 4));
            }
            return IccTrc.FromParametric((IccTrcParametricType)funcType, parameters);
        }

        return null;
    }

    private static IccTrc ReadTrcType(BigEndianReader reader, IccTagEntry tag)
    {
        return ReadTrcPayload(reader, tag.Offset, out _);
    }

    private static float[,] ReadChadMatrix(BigEndianReader reader, IccTagEntry tag)
    {
        if (!reader.CanRead(tag.Offset, 8 + 9 * 4))
        {
            return null;
        }
        int baseOffset = tag.Offset + 8;
        float[,] matrix = new float[3, 3];
        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < 3; columnIndex++)
            {
                int raw = reader.ReadInt32(baseOffset + (rowIndex * 3 + columnIndex) * 4);
                matrix[rowIndex, columnIndex] = BigEndianReader.S15Fixed16ToSingle(raw);
            }
        }
        return matrix;
    }

    private static string ReadDescType(BigEndianReader reader, IccTagEntry tag)
    {
        if (!reader.CanRead(tag.Offset, 12))
        {
            return null;
        }
        uint type = reader.ReadUInt32(tag.Offset);
        if (type != BigEndianReader.FourCC(IccConstants.TypeDesc))
        {
            return null;
        }
        uint asciiCount = reader.ReadUInt32(tag.Offset + 8);
        if (asciiCount > 0 && reader.CanRead(tag.Offset + 12, (int)asciiCount))
        {
            byte[] bytes = reader.ReadBytes(tag.Offset + 12, (int)Math.Min(int.MaxValue, asciiCount));
            try
            {
                return System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0');
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private static string ReadMlucType(BigEndianReader reader, IccTagEntry tag)
    {
        if (!reader.CanRead(tag.Offset, 16))
        {
            return null;
        }
        uint type = reader.ReadUInt32(tag.Offset);
        if (type != BigEndianReader.FourCC(IccConstants.TypeMluc))
        {
            return null;
        }
        uint count = reader.ReadUInt32(tag.Offset + 8);
        uint recordSize = reader.ReadUInt32(tag.Offset + 12);
        int recordBase = tag.Offset + 16;
        if (count == 0 || recordSize < 12)
        {
            return null;
        }
        if (!reader.CanRead(recordBase, (int)recordSize))
        {
            return null;
        }
        uint length = reader.ReadUInt32(recordBase + 4);
        uint stringOffset = reader.ReadUInt32(recordBase + 8);
        int strPos = tag.Offset + (int)stringOffset;
        if (!reader.CanRead(strPos, (int)length))
        {
            return null;
        }
        byte[] raw = reader.ReadBytes(strPos, (int)length);
        try
        {
            return System.Text.Encoding.BigEndianUnicode.GetString(raw).TrimEnd('\0');
        }
        catch
        {
            return null;
        }
    }

    private static int GetParamCount(int funcType)
    {
        switch (funcType)
        {
            case 0: return 1;
            case 1: return 3;
            case 2: return 4;
            case 3: return 5;
            case 4: return 7;
            default: return 1;
        }
    }

    #endregion
}
