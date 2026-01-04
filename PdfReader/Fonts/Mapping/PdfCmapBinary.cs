using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using PdfReader.Models;

namespace PdfReader.Fonts.Mapping;

public static class PdfCmapBinary
{
    private enum CMapBinaryBlockId : byte
    {
        Ranges = 2,
        Singles = 3,
        OverridesHeader = 4,
        Name = 5,
        CidSystemInfo = 6,
        WMode = 7
    }

    public static void CompressCmaps(IEnumerable<PdfCMap> cmaps, string outputDirectory)
    {
        if (cmaps == null)
        {
            throw new ArgumentNullException(nameof(cmaps));
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory must be provided.", nameof(outputDirectory));
        }

        Directory.CreateDirectory(outputDirectory);

        var signatures = CmapClustering.BuildCMapColumnSignatures(cmaps);
        var clusters = CmapClustering.ClusterByColumnAgreement(signatures, 0.8);
        var bases = CmapClustering.BuildClusterBases(clusters, signatures);

        CmapClustering.WriteClustersReport(clusters, Path.Combine(outputDirectory, "clusters.txt"));

        foreach (var baseEntry in bases)
        {
            string basePath = Path.Combine(outputDirectory, $"{baseEntry.Key}.bin");
            WriteClusterBaseBinary(baseEntry.Value, basePath);
        }

        foreach (var cmap in cmaps)
        {
            int clusterIndex = CmapClustering.FindClusterIndex(clusters, cmap.Name.ToString());
            if (clusterIndex < 0)
            {
                continue;
            }

            var clusterBase = bases[clusterIndex];
            string overridesPath = Path.Combine(outputDirectory, $"{cmap.Name.ToString()}.bin");
            WriteCMapOverridesBinary(cmap, clusterBase, clusterIndex, overridesPath);
        }
    }

    /// <summary>
    /// Parse a CMap from the custom binary format written by this class.
    /// If an OverridesHeader block is present, the provided baseResolver will be used to merge the cluster base.
    /// </summary>
    public static PdfCMap ParseCMapBinary(ReadOnlyMemory<byte> data, Func<PdfString, PdfCMap> baseResolver)
    {
        var cmap = new PdfCMap();
        int offset = 0;
        byte codeLengthContext = 0;
        uint prevCode = 0;
        uint prevCid = 0;

        var span = data.Span;

        while (offset < span.Length)
        {
            byte blockId = span[offset++];
            switch ((CMapBinaryBlockId)blockId)
            {
                case CMapBinaryBlockId.OverridesHeader:
                {
                    uint count = ReadVarUInt(span, ref offset);
                    byte reserved = span[offset++];
                    uint clusterIndex = ReadVarUInt(span, ref offset);
                    if (baseResolver != null)
                    {
                        var baseCmap = baseResolver(PdfString.FromString(clusterIndex.ToString()));
                        if (baseCmap != null)
                        {
                            cmap.MergeFrom(baseCmap);
                        }
                    }
                    prevCode = 0;
                    prevCid = 0;
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
                    var info = new Fonts.Model.PdfCidSystemInfo();
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
                        var startBytes = PdfCharacterCode.PackUIntToBigEndian(codeStart, codeLengthContext);
                        var endBytes = PdfCharacterCode.PackUIntToBigEndian(codeEnd, codeLengthContext);
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

                        var codeBytes = PdfCharacterCode.PackUIntToBigEndian(codeValue, codeLengthContext);
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

    private static uint ReadVarUInt(ReadOnlySpan<byte> data, ref int offset)
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

    private static int ReadVarInt(ReadOnlySpan<byte> data, ref int offset)
    {
        uint zigzag = ReadVarUInt(data, ref offset);
        int value = (int)((zigzag >> 1) ^ (uint)-(int)(zigzag & 1));
        return value;
    }

    private struct Entry
    {
        public uint CodeValue { get; set; }
        public uint Cid { get; set; }
    }

    private static void WriteClusterBaseBinary(Dictionary<byte, Dictionary<uint, int>> baseMap, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var stream = File.Create(outputPath);

        foreach (var lengthEntry in baseMap.OrderBy(k => k.Key))
        {
            byte codeLength = lengthEntry.Key;
            var sortedColumns = lengthEntry.Value.OrderBy(c => c.Key).ToList();
            if (sortedColumns.Count == 0)
            {
                continue;
            }

            var entries = sortedColumns
                .Select(c => new Entry { CodeValue = c.Key, Cid = (uint)c.Value })
                .OrderBy(e => e.CodeValue)
                .ToList();

            WriteRangeBlocks(stream, codeLength, entries);
        }
    }

    private static void WriteCMapOverridesBinary(PdfCMap cmap, Dictionary<byte, Dictionary<uint, int>> clusterBase, int clusterIndex, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var stream = File.Create(outputPath);

        stream.WriteByte((byte)CMapBinaryBlockId.OverridesHeader);
        WriteVarUInt(stream, 1);
        stream.WriteByte(0);
        WriteVarUInt(stream, (uint)clusterIndex);

        if (!cmap.Name.IsEmpty)
        {
            stream.WriteByte((byte)CMapBinaryBlockId.Name);
            WriteString(stream, cmap.Name);
        }

        if (cmap.CidSystemInfo != null)
        {
            stream.WriteByte((byte)CMapBinaryBlockId.CidSystemInfo);
            WriteString(stream, cmap.CidSystemInfo.Registry);
            WriteString(stream, cmap.CidSystemInfo.Ordering);
            WriteVarUInt(stream, (uint)cmap.CidSystemInfo.Supplement);
        }

        stream.WriteByte((byte)CMapBinaryBlockId.WMode);
        WriteVarUInt(stream, (uint)cmap.WMode);

        var entriesByLength = cmap.GetCodeToCid()
            .Select(kvp => new { Code = kvp.Key, Cid = (uint)kvp.Value, CodeValue = PdfCharacterCode.UnpackBigEndianToUInt(kvp.Key.Bytes.Span) })
            .Where(entry => entry.Code.Length > 0)
            .GroupBy(entry => entry.Code.Length)
            .ToList();

        foreach (var group in entriesByLength)
        {
            byte codeLength = (byte)group.Key;
            clusterBase.TryGetValue(codeLength, out var baseColumnsSigned);
            baseColumnsSigned ??= new Dictionary<uint, int>();

            var diffs = new List<(uint CodeValue, uint Cid)>();
            foreach (var entry in group)
            {
                baseColumnsSigned.TryGetValue(entry.CodeValue, out int baseCidSigned);
                uint baseCid = (uint)baseCidSigned;
                if (baseCid != entry.Cid)
                {
                    diffs.Add((entry.CodeValue, entry.Cid));
                }
            }

            if (diffs.Count == 0)
            {
                continue;
            }

            diffs.Sort((a, b) => a.CodeValue.CompareTo(b.CodeValue));

            WriteRangeBlocks(stream, codeLength, diffs.Select(d => new Entry { CodeValue = d.CodeValue, Cid = d.Cid }).ToList());
        }
    }

    private static void WriteRangeBlocks(Stream stream, byte codeLength, List<Entry> entries)
    {
        var ranges = new List<(uint CodeStartValue, uint CidStart, uint Length)>();
        var singles = new List<(uint CodeValue, uint Cid)>();

        for (int i = 0; i < entries.Count; i++)
        {
            var current = entries[i];
            var start = current;
            var end = current;

            while (i + 1 < entries.Count)
            {
                var next = entries[i + 1];
                if (next.CodeValue == current.CodeValue + 1 && next.Cid == current.Cid + 1)
                {
                    end = next;
                    current = next;
                    i++;
                }
                else
                {
                    break;
                }
            }

            uint length = end.CodeValue - start.CodeValue + 1;
            if (length > 1)
            {
                ranges.Add((start.CodeValue, start.Cid, length));
            }
            else
            {
                singles.Add((start.CodeValue, start.Cid));
            }
        }

        if (ranges.Count > 0)
        {
            stream.WriteByte((byte)CMapBinaryBlockId.Ranges);
            WriteVarUInt(stream, (uint)ranges.Count);
            stream.WriteByte(codeLength);

            uint prevCode = 0;
            uint prevCid = 0;
            for (int i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                if (i == 0)
                {
                    WriteVarUInt(stream, range.CodeStartValue);
                    WriteVarUInt(stream, range.CidStart);
                }
                else
                {
                    WriteVarUInt(stream, range.CodeStartValue - prevCode);
                    int cidDelta = unchecked((int)(range.CidStart - prevCid));
                    WriteVarInt(stream, cidDelta);
                }
                WriteVarUInt(stream, range.Length);
                prevCode = range.CodeStartValue;
                prevCid = range.CidStart;
            }
        }

        if (singles.Count > 0)
        {
            stream.WriteByte((byte)CMapBinaryBlockId.Singles);
            WriteVarUInt(stream, (uint)singles.Count);
            stream.WriteByte(codeLength);

            uint prevCode = 0;
            uint prevCid = 0;
            for (int i = 0; i < singles.Count; i++)
            {
                var single = singles[i];
                if (i == 0)
                {
                    WriteVarUInt(stream, single.CodeValue);
                    WriteVarUInt(stream, single.Cid);
                }
                else
                {
                    WriteVarUInt(stream, single.CodeValue - prevCode);
                    int cidDelta = unchecked((int)(single.Cid - prevCid));
                    WriteVarInt(stream, cidDelta);
                }
                prevCode = single.CodeValue;
                prevCid = single.Cid;
            }
        }
    }

    private static void WriteVarUInt(Stream stream, uint value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        stream.WriteByte((byte)value);
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        uint zigzag = (uint)((value << 1) ^ (value >> 31));
        WriteVarUInt(stream, zigzag);
    }

    private static void WriteString(Stream stream, PdfString value)
    {
        var bytes = value.Value;
        WriteVarUInt(stream, (uint)bytes.Length);
        var arr = bytes.ToArray();
        stream.Write(arr, 0, arr.Length);
    }
}
