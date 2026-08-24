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
    /// Creates an identity mapping (CID == GID).
    /// </summary>
    public static PdfCidToGidMap CreateIdentityMapping() => new(true);

    /// <summary>
    /// Creates a mapping from stream data.
    /// </summary>
    public static PdfCidToGidMap FromStreamData(in ReadOnlyMemory<byte> streamData)
    {
        PdfCidToGidMap map = new(false);
        map.ParseStreamData(streamData);
        return map;
    }

    /// <summary>
    /// Creates a <see cref="PdfCidToGidMap"/> from the specified CFF font's charset, which lists the CID
    /// of every glyph in the font program by GID.
    /// </summary>
    /// <param name="font">The CFF font containing the GID-to-SID charset and CID font status.</param>
    /// <param name="cidToGidMap">The mapping from the font dictionary's /CIDToGIDMap entry, or <see langword="null"/> when the entry is absent.</param>
    /// <returns>A <see cref="PdfCidToGidMap"/> instance representing the mapping from CID to GID.</returns>
    internal static PdfCidToGidMap FromCffFont(CffFont font, PdfCidToGidMap? cidToGidMap)
    {
        bool isCidFont = font.FdArray.Length > 0;
        ushort[]? gidToSid = font.Charset?.SidsByGid;

        if (!isCidFont || gidToSid == null || gidToSid.Length == 0)
        {
            if (cidToGidMap != null)
            {
                return cidToGidMap;
            }

            return new PdfCidToGidMap(true);
        }

        Dictionary<ushort, uint>? gidToCid = cidToGidMap?.BuildInverse();
        PdfCidToGidMap map = new(false);

        for (uint gid = 0; gid < gidToSid.Length; gid++)
        {
            ushort sid = gidToSid[gid];
            uint cid = sid;

            if (gidToCid != null && gidToCid.TryGetValue(sid, out uint mappedCid))
            {
                cid = mappedCid;
            }

            map._cidToGidMap[cid] = (ushort)gid;
        }

        return map;
    }

    private PdfCidToGidMap(bool isIdentity) => IsIdentityMapping = isIdentity;

    /// <summary>
    /// Parses CIDToGIDMap stream data.
    /// The stream contains a sequence of 2-byte glyph indices, where the CID is the index position
    /// </summary>
    private void ParseStreamData(in ReadOnlyMemory<byte> data)
    {
        if (data.Length < 2)
        {
            return;
        }

        ReadOnlySpan<byte> bytes = data.Span;

        for (uint cid = 0; cid < bytes.Length / 2; cid++)
        {
            int byteIndex = (int)cid * 2;

            if (byteIndex + 1 < bytes.Length)
            {
                var gid = (ushort)(bytes[byteIndex] << 8 | bytes[byteIndex + 1]);

                _cidToGidMap[cid] = gid;
            }
        }
    }

    /// <summary>
    /// Gets the GID for a given CID, or null if not defined.
    /// </summary>
    public ushort? GetGID(uint cid)
    {
        if (IsIdentityMapping)
        {
            return (ushort)cid; // Identity mapping: GID = CID
        }

        if (_cidToGidMap.TryGetValue(cid, out ushort gid))
        {
            return gid;
        }

        return null;
    }

    /// <summary>
    /// Gets whether this is an identity mapping.
    /// </summary>
    public bool IsIdentityMapping { get; }

    /// <summary>
    /// Builds the reverse lookup from GID to CID. Entries mapping to GID 0 are left out: .notdef carries
    /// no glyph identity and every unmapped CID lands on it, so it names no single CID to come back to.
    /// </summary>
    private Dictionary<ushort, uint> BuildInverse()
    {
        Dictionary<ushort, uint> inverse = new(_cidToGidMap.Count);

        foreach (KeyValuePair<uint, ushort> entry in _cidToGidMap)
        {
            if (entry.Value != 0)
            {
                inverse[entry.Value] = entry.Key;
            }
        }

        return inverse;
    }
}
