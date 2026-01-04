using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfReader.Fonts.Mapping;

internal static class CmapClustering
{
    public static Dictionary<string, Dictionary<byte, Dictionary<uint, int>>> BuildCMapColumnSignatures(IEnumerable<PdfCMap> cmaps)
    {
        if (cmaps == null)
        {
            throw new ArgumentNullException(nameof(cmaps));
        }

        var signatures = new Dictionary<string, Dictionary<byte, Dictionary<uint, int>>>();
        foreach (var cmap in cmaps)
        {
            var codeToCid = cmap.GetCodeToCid();
            if (codeToCid.Count == 0)
            {
                continue;
            }

            var signature = new Dictionary<byte, Dictionary<uint, int>>();
            foreach (var entry in codeToCid
                .Select(kvp => new { Code = kvp.Key, Cid = kvp.Value, CodeValue = PdfCharacterCode.UnpackBigEndianToUInt(kvp.Key.Bytes.Span) })
                .Where(entry => entry.Code.Length > 0))
            {
                byte codeLength = (byte)entry.Code.Length;
                if (!signature.TryGetValue(codeLength, out var columns))
                {
                    columns = new Dictionary<uint, int>();
                    signature[codeLength] = columns;
                }

                columns[entry.CodeValue] = entry.Cid;
            }

            signatures[cmap.Name.ToString()] = signature;
        }

        return signatures;
    }

    public static List<List<string>> ClusterByColumnAgreement(Dictionary<string, Dictionary<byte, Dictionary<uint, int>>> signatures, double similarityThreshold)
    {
        if (signatures == null)
        {
            throw new ArgumentNullException(nameof(signatures));
        }

        var unassigned = new HashSet<string>(signatures.Keys);
        var clusters = new List<List<string>>();

        while (unassigned.Count > 0)
        {
            string seed = unassigned.First();
            unassigned.Remove(seed);
            var cluster = new List<string> { seed };

            var remaining = unassigned.ToList();
            foreach (string name in remaining)
            {
                double similarity = ComputeSimilarity(signatures[seed], signatures[name]);
                if (similarity >= similarityThreshold)
                {
                    cluster.Add(name);
                    unassigned.Remove(name);
                }
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    public static void WriteClustersReport(List<List<string>> clusters, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("CMap clusters by column agreement:");
        for (int i = 0; i < clusters.Count; i++)
        {
            writer.WriteLine($"Cluster {i + 1} (size={clusters[i].Count}):");
            foreach (string name in clusters[i].OrderBy(n => n))
            {
                writer.WriteLine($"  {name}");
            }
        }
    }

    public static Dictionary<int, Dictionary<byte, Dictionary<uint, int>>> BuildClusterBases(List<List<string>> clusters, Dictionary<string, Dictionary<byte, Dictionary<uint, int>>> signatures)
    {
        var bases = new Dictionary<int, Dictionary<byte, Dictionary<uint, int>>>();
        for (int i = 0; i < clusters.Count; i++)
        {
            var votesByLength = new Dictionary<byte, Dictionary<uint, Dictionary<int, int>>>();
            foreach (string name in clusters[i])
            {
                if (!signatures.TryGetValue(name, out var signature))
                {
                    continue;
                }

                foreach (var lengthEntry in signature)
                {
                    byte codeLength = lengthEntry.Key;
                    if (!votesByLength.TryGetValue(codeLength, out var votesByCode))
                    {
                        votesByCode = new Dictionary<uint, Dictionary<int, int>>();
                        votesByLength[codeLength] = votesByCode;
                    }

                    foreach (var columnEntry in lengthEntry.Value)
                    {
                        if (!votesByCode.TryGetValue(columnEntry.Key, out var votes))
                        {
                            votes = new Dictionary<int, int>();
                            votesByCode[columnEntry.Key] = votes;
                        }

                        votes.TryGetValue(columnEntry.Value, out int count);
                        votes[columnEntry.Value] = count + 1;
                    }
                }
            }

            var baseMap = new Dictionary<byte, Dictionary<uint, int>>();
            foreach (var lengthEntry in votesByLength)
            {
                var baseColumns = new Dictionary<uint, int>();
                foreach (var columnEntry in lengthEntry.Value)
                {
                    var top = columnEntry.Value.OrderByDescending(v => v.Value).First();
                    baseColumns[columnEntry.Key] = top.Key;
                }

                baseMap[lengthEntry.Key] = baseColumns;
            }

            bases[i] = baseMap;
        }

        return bases;
    }

    public static int FindClusterIndex(List<List<string>> clusters, string name)
    {
        for (int i = 0; i < clusters.Count; i++)
        {
            if (clusters[i].Contains(name))
            {
                return i;
            }
        }
        return -1;
    }

    private static double ComputeSimilarity(Dictionary<byte, Dictionary<uint, int>> a, Dictionary<byte, Dictionary<uint, int>> b)
    {
        long intersection = 0;
        long union = 0;

        var codeLengths = new HashSet<byte>(a.Keys.Concat(b.Keys));
        foreach (byte cl in codeLengths)
        {
            a.TryGetValue(cl, out var columnsA);
            b.TryGetValue(cl, out var columnsB);
            columnsA ??= new Dictionary<uint, int>();
            columnsB ??= new Dictionary<uint, int>();

            var keys = new HashSet<uint>(columnsA.Keys.Concat(columnsB.Keys));
            foreach (uint key in keys)
            {
                bool hasA = columnsA.TryGetValue(key, out int cidA);
                bool hasB = columnsB.TryGetValue(key, out int cidB);
                if (hasA && hasB)
                {
                    union++;
                    if (cidA == cidB)
                    {
                        intersection++;
                    }
                }
                else
                {
                    union++;
                }
            }
        }

        if (union == 0)
        {
            return 0.0;
        }

        return (double)intersection / union;
    }
}
