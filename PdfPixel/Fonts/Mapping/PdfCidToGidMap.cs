using PdfPixel.Fonts.Cff;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Represents a CID-to-GID mapping for CID fonts
/// Maps Character IDs (CIDs) to Glyph IDs (GIDs) in the embedded font
/// </summary>
public sealed class PdfCidToGidMap
{
    private readonly Dictionary<uint, ushort> _cidToGidMap = [];

    /// <summary>
    /// Create an identity mapping (CID == GID)
    /// </summary>
    public static PdfCidToGidMap CreateIdentityMapping() => new(true);

    /// <summary>
    /// Create a mapping from stream data
    /// </summary>
    public static PdfCidToGidMap FromStreamData(in ReadOnlyMemory<byte> streamData)
    {
        PdfCidToGidMap map = new(false);
        map.ParseStreamData(streamData);
        return map;
    }

    /// <summary>
    /// Creates a <see cref="PdfCidToGidMap"/> from the specified CFF font's charset.
    /// </summary>
    /// <remarks>If <paramref name="font"/> is not CID-keyed or has an empty GID-to-SID charset, a
    /// default <see cref="PdfCidToGidMap"/> is returned with no mappings.</remarks>
    /// <param name="font">The CFF font containing the GID-to-SID charset and CID font status.</param>
    /// <returns>A <see cref="PdfCidToGidMap"/> instance representing the mapping from CID to GID.</returns>
    internal static PdfCidToGidMap FromCffFont(CffFont font)
    {
        bool isCidFont = font.FdArray.Length > 0;
        ushort[]? gidToSid = font.Charset?.SidsByGid;

        if (!isCidFont || gidToSid == null || gidToSid.Length == 0)
        {
            return new PdfCidToGidMap(true);
        }

        PdfCidToGidMap map = new(false);

        for (uint gid = 0; gid < gidToSid.Length; gid++)
        {
            ushort sid = gidToSid[gid];
            map._cidToGidMap[sid] = (ushort)gid;
        }

        return map;
    }

    private PdfCidToGidMap(bool isIdentity) => IsIdentityMapping = isIdentity;

    /// <summary>
    /// Parse CIDToGIDMap stream data
    /// The stream contains a sequence of 2-byte glyph indices, where the CID is the index position
    /// </summary>
    private void ParseStreamData(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < 2)
        {
            return;
        }

        ReadOnlySpan<byte> bytes = data.Span;

        // Each pair of bytes represents a GID for the corresponding CID
        for (uint cid = 0; cid < bytes.Length / 2; cid++)
        {
            int byteIndex = (int)cid * 2;

            if (byteIndex + 1 < bytes.Length)
            {
                // Read 2-byte big-endian GID
                var gid = (ushort)(bytes[byteIndex] << 8 | bytes[byteIndex + 1]);

                // Store all mappings, including GID 0 (.notdef is still valid)
                _cidToGidMap[cid] = gid;
            }
        }
    }

    /// <summary>
    /// Get the GID for a given CID
    /// </summary>
    public ushort GetGID(uint cid)
    {
        if (IsIdentityMapping)
        {
            return (ushort)cid; // Identity mapping: GID = CID
        }

        // Check explicit mapping
        if (_cidToGidMap.TryGetValue(cid, out ushort gid))
        {
            return gid;
        }

        // If CID is not in the explicit mapping, it means there's no glyph for this CID
        // Return 0 (.notdef) as per PDF specification
        return 0;
    }

    /// <summary>
    /// Check if there's an explicit mapping for the given CID
    /// </summary>
    public bool HasMapping(uint cid)
    {
        if (IsIdentityMapping)
        {
            return true; // Identity mapping covers all CIDs
        }

        return _cidToGidMap.ContainsKey(cid);
    }

    /// <summary>
    /// Get the number of explicit mappings
    /// </summary>
    public int MappingCount => IsIdentityMapping ? -1 : _cidToGidMap.Count; // -1 indicates identity mapping

    /// <summary>
    /// Check if this is an identity mapping
    /// </summary>
    public bool IsIdentityMapping { get; }
}
