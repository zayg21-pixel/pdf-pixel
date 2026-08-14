using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Models;

namespace PdfPixel.ResourceGenerator.Cmaps;

/// <summary>
/// Clusters the supplied CMaps by column agreement and writes each cluster base and per-CMap override
/// binary file into a target directory.
/// </summary>
public static class CmapCompressor
{
    private enum BlockId : byte
    {
        CodeSpaceRanges = 1,
        Ranges = 2,
        Singles = 3,
        OverridesHeader = 4,
        Name = 5,
        CidSystemInfo = 6,
        WMode = 7
    }

    private struct Entry
    {
        public uint CodeValue { get; set; }
        public uint Cid { get; set; }
    }

    /// <summary>
    /// Clusters the supplied CMaps by column agreement and writes each cluster base and per-CMap override
    /// binary file into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="cmaps">The collection of <see cref="PdfCMap"/> instances to compress.</param>
    /// <param name="outputDirectory">The directory in which to write the output binary files.</param>
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

        Dictionary<string, Dictionary<byte, Dictionary<uint, int>>> signatures = CmapClustering.BuildCMapColumnSignatures(cmaps);
        List<List<string>> clusters = CmapClustering.ClusterByColumnAgreement(signatures, 0.8);
        Dictionary<int, Dictionary<byte, Dictionary<uint, int>>> bases = CmapClustering.BuildClusterBases(clusters, signatures);

        CmapClustering.WriteClustersReport(clusters, Path.Combine(outputDirectory, "clusters.txt"));

        foreach (KeyValuePair<int, Dictionary<byte, Dictionary<uint, int>>> baseEntry in bases)
        {
            string basePath = Path.Combine(outputDirectory, $"{baseEntry.Key}.bin");
            WriteClusterBaseBinary(baseEntry.Value, basePath);
        }

        foreach (PdfCMap cmap in cmaps)
        {
            if (cmap.Name == null)
            {
                Console.WriteLine("  Skipped (no name)");
                continue;
            }

            string cmapName = cmap.Name.Value.ToString();
            int clusterIndex = CmapClustering.FindClusterIndex(clusters, cmapName);
            if (clusterIndex < 0)
            {
                Console.WriteLine($"  Skipped (no cluster): {cmapName}");
                continue;
            }

            Dictionary<byte, Dictionary<uint, int>> clusterBase = bases[clusterIndex];
            string overridesPath = Path.Combine(outputDirectory, $"{cmapName}.bin");
            Console.WriteLine($"  Exporting: {cmap.Name} -> {overridesPath}");
            WriteCMapOverridesBinary(cmap, clusterBase, clusterIndex, overridesPath);
        }
    }

    private static void WriteClusterBaseBinary(Dictionary<byte, Dictionary<uint, int>> baseMap, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using FileStream stream = File.Create(outputPath);

        foreach (KeyValuePair<byte, Dictionary<uint, int>> lengthEntry in baseMap.OrderBy(k => k.Key))
        {
            byte codeLength = lengthEntry.Key;
            List<KeyValuePair<uint, int>> sortedColumns = lengthEntry.Value.OrderBy(c => c.Key).ToList();
            if (sortedColumns.Count == 0)
            {
                continue;
            }

            List<Entry> entries = sortedColumns
                .Select(c => new Entry { CodeValue = c.Key, Cid = (uint)c.Value })
                .OrderBy(e => e.CodeValue)
                .ToList();

            WriteRangeBlocks(stream, codeLength, entries);
        }
    }

    private static void WriteCMapOverridesBinary(PdfCMap cmap, Dictionary<byte, Dictionary<uint, int>> clusterBase, int clusterIndex, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using FileStream stream = File.Create(outputPath);

        if (cmap.CodeSpaceRanges.Count > 0)
        {
            stream.WriteByte((byte)BlockId.CodeSpaceRanges);
            WriteVarUInt(stream, (uint)cmap.CodeSpaceRanges.Count);
            foreach (CodeSpaceRange range in cmap.CodeSpaceRanges)
            {
                stream.WriteByte((byte)range.Length);
                WriteVarUInt(stream, range.Start);
                WriteVarUInt(stream, range.End);
            }
        }

        stream.WriteByte((byte)BlockId.OverridesHeader);
        WriteVarUInt(stream, (uint)clusterIndex);

        if (cmap.Name?.IsEmpty == false)
        {
            stream.WriteByte((byte)BlockId.Name);
            WriteString(stream, cmap.Name);
        }

        if (cmap.CidSystemInfo != null)
        {
            stream.WriteByte((byte)BlockId.CidSystemInfo);
            WriteString(stream, cmap.CidSystemInfo.Registry);
            WriteString(stream, cmap.CidSystemInfo.Ordering);
            WriteVarUInt(stream, (uint)cmap.CidSystemInfo.Supplement);
        }

        stream.WriteByte((byte)BlockId.WMode);
        WriteVarUInt(stream, (uint)cmap.WMode);

        Dictionary<byte, Dictionary<uint, int>> allEntries = CmapClustering.BuildCMapSignature(cmap);

        foreach (KeyValuePair<byte, Dictionary<uint, int>> group in allEntries)
        {
            byte codeLength = group.Key;
            clusterBase.TryGetValue(codeLength, out Dictionary<uint, int>? baseColumnsSigned);
            baseColumnsSigned ??= new Dictionary<uint, int>();

            List<(uint CodeValue, uint Cid)> diffs = [];
            foreach (KeyValuePair<uint, int> entry in group.Value)
            {
                baseColumnsSigned.TryGetValue(entry.Key, out int baseCidSigned);
                var baseCid = (uint)baseCidSigned;
                var entryCid = (uint)entry.Value;
                if (baseCid != entryCid)
                {
                    diffs.Add((entry.Key, entryCid));
                }
            }

            if (diffs.Count == 0)
            {
                continue;
            }

            diffs.Sort((a, b) => a.CodeValue.CompareTo(b.CodeValue));

            // Diffs are written exclusively as Singles, never coalesced into Ranges: a Single always
            // overwrites whatever the cluster base defined for that code once merged, regardless of
            // whether the base happened to store it as a Range or a Single. A coalesced Range diff
            // cannot make that guarantee, since CID lookups always favor an existing Single over a Range.
            WriteSinglesOnly(stream, codeLength, diffs.ConvertAll(d => new Entry { CodeValue = d.CodeValue, Cid = d.Cid }));
        }
    }

    private static void WriteSinglesOnly(FileStream stream, byte codeLength, List<Entry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        stream.WriteByte((byte)BlockId.Singles);
        WriteVarUInt(stream, (uint)entries.Count);
        stream.WriteByte(codeLength);

        uint prevCode = 0;
        uint prevCid = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (i == 0)
            {
                WriteVarUInt(stream, entry.CodeValue);
                WriteVarUInt(stream, entry.Cid);
            }
            else
            {
                WriteVarUInt(stream, entry.CodeValue - prevCode);
                int cidDelta = unchecked((int)(entry.Cid - prevCid));
                WriteVarInt(stream, cidDelta);
            }

            prevCode = entry.CodeValue;
            prevCid = entry.Cid;
        }
    }

    private static void WriteRangeBlocks(FileStream stream, byte codeLength, List<Entry> entries)
    {
        List<(uint CodeStartValue, uint CidStart, uint Length)> ranges = [];
        List<(uint CodeValue, uint Cid)> singles = [];

        for (int i = 0; i < entries.Count; i++)
        {
            Entry current = entries[i];
            Entry start = current;
            Entry end = current;

            while (i + 1 < entries.Count)
            {
                Entry next = entries[i + 1];
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
            stream.WriteByte((byte)BlockId.Ranges);
            WriteVarUInt(stream, (uint)ranges.Count);
            stream.WriteByte(codeLength);

            uint prevCode = 0;
            uint prevCid = 0;
            for (int i = 0; i < ranges.Count; i++)
            {
                (uint CodeStartValue, uint CidStart, uint Length) range = ranges[i];
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
            stream.WriteByte((byte)BlockId.Singles);
            WriteVarUInt(stream, (uint)singles.Count);
            stream.WriteByte(codeLength);

            uint prevCode = 0;
            uint prevCid = 0;
            for (int i = 0; i < singles.Count; i++)
            {
                (uint CodeValue, uint Cid) single = singles[i];
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

    private static void WriteVarUInt(FileStream stream, uint value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }

    private static void WriteVarInt(FileStream stream, int value)
    {
        var zigzag = (uint)((value << 1) ^ (value >> 31));
        WriteVarUInt(stream, zigzag);
    }

    private static void WriteString(FileStream stream, PdfString? value)
    {
        ReadOnlyMemory<byte> bytes = value?.Value ?? ReadOnlyMemory<byte>.Empty;
        WriteVarUInt(stream, (uint)bytes.Length);
        byte[] arr = bytes.ToArray();
        stream.Write(arr, 0, arr.Length);
    }
}
