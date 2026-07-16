using System;
using System.Globalization;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Deserialises CMap data from the compact custom binary format produced by the ResourceGenerator tool.
/// The format uses variable-length integers, delta coding, and run-length range blocks to minimise file size.
/// </summary>
public static class PdfCmapBinary
{
    private enum CMapBinaryBlockId : byte
    {
        CodeSpaceRanges = 1,
        Ranges = 2,
        Singles = 3,
        OverridesHeader = 4,
        Name = 5,
        CidSystemInfo = 6,
        WMode = 7
    }

    /// <summary>
    /// Parse a CMap from the custom binary format written by this class.
    /// If an OverridesHeader block is present, the provided baseResolver will be used to merge the cluster base.
    /// </summary>
    public static PdfCMap ParseCMapBinary(in ReadOnlyMemory<byte> data, Func<PdfString, PdfCMap?> baseResolver)
    {
        PdfCMap cmap = new();
        int offset = 0;
        byte codeLengthContext;
        uint prevCode;
        uint prevCid;

        ReadOnlySpan<byte> span = data.Span;

        while (offset < span.Length)
        {
            byte blockId = span[offset++];
            switch ((CMapBinaryBlockId)blockId)
            {
                case CMapBinaryBlockId.CodeSpaceRanges:
                {
                    uint count = ReadVarUInt(span, ref offset);
                    for (uint i = 0; i < count; i++)
                    {
                        byte codeLength = span[offset++];
                        uint start = ReadVarUInt(span, ref offset);
                        uint end = ReadVarUInt(span, ref offset);
                        ReadOnlyMemory<byte> startBytes = PdfCharacterCode.PackUIntToBigEndian(start, codeLength);
                        ReadOnlyMemory<byte> endBytes = PdfCharacterCode.PackUIntToBigEndian(end, codeLength);
                        cmap.AddCodespaceRange(startBytes.Span, endBytes.Span);
                    }

                    break;
                }
                case CMapBinaryBlockId.OverridesHeader:
                {
                    uint clusterIndex = ReadVarUInt(span, ref offset);
                    if (baseResolver != null)
                    {
                        PdfCMap? baseCmap = baseResolver(PdfString.FromString(clusterIndex.ToString(CultureInfo.InvariantCulture)));
                        if (baseCmap != null)
                        {
                            cmap.MergeFrom(baseCmap);
                        }
                    }

                    break;
                }
                case CMapBinaryBlockId.Name:
                {
                    uint len = ReadVarUInt(span, ref offset);
                    cmap.Name = data.Slice(offset, (int)len);
                    offset += (int)len;
                    break;
                }
                case CMapBinaryBlockId.CidSystemInfo:
                {
                    PdfCidSystemInfo info = new();
                    uint regLen = ReadVarUInt(span, ref offset);
                    info.Registry = data.Slice(offset, (int)regLen);
                    offset += (int)regLen;

                    uint ordLen = ReadVarUInt(span, ref offset);
                    info.Ordering = data.Slice(offset, (int)ordLen);
                    offset += (int)ordLen;

                    uint supplement = ReadVarUInt(span, ref offset);
                    info.Supplement = (int)supplement;

                    cmap.CidSystemInfo = info;
                    break;
                }
                case CMapBinaryBlockId.WMode:
                {
                    uint wmode = ReadVarUInt(span, ref offset);
                    cmap.WMode = (CMapWMode)wmode;
                    break;
                }
                case CMapBinaryBlockId.Ranges:
                {
                    uint count = ReadVarUInt(span, ref offset);
                    codeLengthContext = span[offset++];
                    prevCode = 0;
                    prevCid = 0;
                    for (uint i = 0; i < count; i++)
                    {
                        uint codeStart;
                        uint cidStart;
                        if (i == 0)
                        {
                            codeStart = ReadVarUInt(span, ref offset);
                            cidStart = ReadVarUInt(span, ref offset);
                        }
                        else
                        {
                            codeStart = prevCode + ReadVarUInt(span, ref offset);
                            int cidDelta = ReadVarInt(span, ref offset);
                            cidStart = unchecked(prevCid + (uint)cidDelta);
                        }

                        uint length = ReadVarUInt(span, ref offset);

                        uint codeEnd = codeStart + length - 1;
                            ReadOnlyMemory<byte> startBytes = PdfCharacterCode.PackUIntToBigEndian(codeStart, codeLengthContext);
                            ReadOnlyMemory<byte> endBytes = PdfCharacterCode.PackUIntToBigEndian(codeEnd, codeLengthContext);
                        cmap.AddCidRangeMapping(startBytes.Span, endBytes.Span, (int)cidStart);

                        prevCode = codeStart;
                        prevCid = cidStart;
                    }

                    break;
                }
                case CMapBinaryBlockId.Singles:
                {
                    uint count = ReadVarUInt(span, ref offset);
                    codeLengthContext = span[offset++];
                    prevCode = 0;
                    prevCid = 0;
                    for (uint i = 0; i < count; i++)
                    {
                        uint codeValue;
                        uint cidValue;
                        if (i == 0)
                        {
                            codeValue = ReadVarUInt(span, ref offset);
                            cidValue = ReadVarUInt(span, ref offset);
                        }
                        else
                        {
                            codeValue = prevCode + ReadVarUInt(span, ref offset);
                            int cidDelta = ReadVarInt(span, ref offset);
                            cidValue = unchecked(prevCid + (uint)cidDelta);
                        }

                            ReadOnlyMemory<byte> codeBytes = PdfCharacterCode.PackUIntToBigEndian(codeValue, codeLengthContext);
                        cmap.AddCidMapping(new PdfCharacterCode(codeBytes), (int)cidValue);

                        prevCode = codeValue;
                        prevCid = cidValue;
                    }

                    break;
                }
                default:
                {
                    offset = span.Length;
                    break;
                }
            }
        }

        return cmap;
    }

    private static uint ReadVarUInt(in ReadOnlySpan<byte> data, ref int offset)
    {
        uint result = 0;
        int shift = 0;
        while (offset < data.Length)
        {
            byte b = data[offset++];
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                break;
            }

            shift += 7;
        }

        return result;
    }

    private static int ReadVarInt(in ReadOnlySpan<byte> data, ref int offset)
    {
        uint zigzag = ReadVarUInt(data, ref offset);
        var value = (int)((zigzag >> 1) ^ (uint)-(int)(zigzag & 1));
        return value;
    }
}
