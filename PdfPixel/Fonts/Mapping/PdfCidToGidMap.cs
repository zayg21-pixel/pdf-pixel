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
    private readonly PdfCidToGidMapKind _kind;
    private readonly ushort[] _gidsByCid = Array.Empty<ushort>();
    private readonly Dictionary<uint, ushort> _cffGidsByCid = [];

    private PdfCidToGidMap(PdfCidToGidMapKind kind) => _kind = kind;

    private PdfCidToGidMap(ushort[] gidsByCid)
    {
        _kind = PdfCidToGidMapKind.Stream;
        _gidsByCid = gidsByCid;
    }

    private PdfCidToGidMap(Dictionary<uint, ushort> cffGidsByCid)
    {
        _kind = PdfCidToGidMapKind.Cff;
        _cffGidsByCid = cffGidsByCid;
    }

    /// <summary>
    /// The identity mapping (CID == GID).
    /// </summary>
    public static PdfCidToGidMap Identity { get; } = new(PdfCidToGidMapKind.Identity);

    /// <summary>
    /// Creates a mapping from stream data.
    /// </summary>
    public static PdfCidToGidMap FromStreamData(in ReadOnlyMemory<byte> streamData) => new(ParseStreamData(streamData.Span));

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

            return Identity;
        }

        Dictionary<ushort, uint>? gidToCid = cidToGidMap?.BuildInverse();
        Dictionary<uint, ushort> gidsByCid = new(gidToSid.Length);

        for (uint gid = 0; gid < gidToSid.Length; gid++)
        {
            ushort sid = gidToSid[gid];
            uint cid = sid;

            if (gidToCid != null && gidToCid.TryGetValue(sid, out uint mappedCid))
            {
                cid = mappedCid;
            }

            gidsByCid[cid] = (ushort)gid;
        }

        return new PdfCidToGidMap(gidsByCid);
    }

    /// <summary>
    /// Gets whether this is an identity mapping.
    /// </summary>
    public bool IsIdentityMapping => _kind == PdfCidToGidMapKind.Identity;

    /// <summary>
    /// Gets the GID for a given CID, or null if not defined.
    /// </summary>
    public ushort? GetGID(uint cid)
    {
        switch (_kind)
        {
            case PdfCidToGidMapKind.Identity:
            {
                return (ushort)cid;
            }
            case PdfCidToGidMapKind.Stream:
            {
                return (cid < (uint)_gidsByCid.Length) ? _gidsByCid[cid] : null;
            }
            case PdfCidToGidMapKind.Cff:
            {
                return (_cffGidsByCid.TryGetValue(cid, out ushort gid)) ? gid : null;
            }
            default:
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Parses CIDToGIDMap stream data.
    /// The stream contains a sequence of 2-byte glyph indices, where the CID is the index position
    /// </summary>
    private static ushort[] ParseStreamData(in ReadOnlySpan<byte> bytes)
    {
        var gidsByCid = new ushort[bytes.Length / 2];

        for (int cid = 0; cid < gidsByCid.Length; cid++)
        {
            int byteIndex = cid * 2;
            gidsByCid[cid] = (ushort)(bytes[byteIndex] << 8 | bytes[byteIndex + 1]);
        }

        return gidsByCid;
    }

    /// <summary>
    /// Builds the reverse lookup from GID to CID. Entries mapping to GID 0 are left out: .notdef carries
    /// no glyph identity and every unmapped CID lands on it, so it names no single CID to come back to.
    /// </summary>
    private Dictionary<ushort, uint> BuildInverse()
    {
        switch (_kind)
        {
            case PdfCidToGidMapKind.Stream:
            {
                Dictionary<ushort, uint> inverse = new(_gidsByCid.Length);

                for (uint cid = 0; cid < _gidsByCid.Length; cid++)
                {
                    if (_gidsByCid[cid] != 0)
                    {
                        inverse[_gidsByCid[cid]] = cid;
                    }
                }

                return inverse;
            }
            case PdfCidToGidMapKind.Cff:
            {
                Dictionary<ushort, uint> inverse = new(_cffGidsByCid.Count);

                foreach (KeyValuePair<uint, ushort> entry in _cffGidsByCid)
                {
                    if (entry.Value != 0)
                    {
                        inverse[entry.Value] = entry.Key;
                    }
                }

                return inverse;
            }
            default:
            {
                return new Dictionary<ushort, uint>();
            }
        }
    }

    private enum PdfCidToGidMapKind
    {
        Identity,
        Stream,
        Cff
    }
}
