using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Represents a CID-to-GID mapping for CID fonts
    /// Maps Character IDs (CIDs) to Glyph IDs (GIDs) in the embedded font
    /// </summary>
    public class PdfCIDToGIDMap
    {
        private readonly Dictionary<uint, uint> _cidToGidMap = new Dictionary<uint, uint>();
        private readonly bool _isIdentityMapping;

        /// <summary>
        /// Create an identity mapping (CID == GID)
        /// </summary>
        public static PdfCIDToGIDMap CreateIdentityMapping()
        {
            return new PdfCIDToGIDMap(true);
        }

        /// <summary>
        /// Create a mapping from stream data
        /// </summary>
        public static PdfCIDToGIDMap FromStreamData(ReadOnlyMemory<byte> streamData)
        {
            var map = new PdfCIDToGIDMap(false);
            map.ParseStreamData(streamData);
            return map;
        }

        private PdfCIDToGIDMap(bool isIdentity)
        {
            _isIdentityMapping = isIdentity;
        }

        /// <summary>
        /// Parse CIDToGIDMap stream data
        /// The stream contains a sequence of 2-byte glyph indices, where the CID is the index position
        /// </summary>
        private void ParseStreamData(ReadOnlyMemory<byte> data)
        {
            if (data.Length < 2) return;

            var bytes = data.Span;
            
            // Each pair of bytes represents a GID for the corresponding CID
            for (uint cid = 0; cid < bytes.Length / 2; cid++)
            {
                int byteIndex = (int)cid * 2;
                
                if (byteIndex + 1 < bytes.Length)
                {
                    // Read 2-byte big-endian GID
                    int gid = bytes[byteIndex] << 8 | bytes[byteIndex + 1];
                    
                    // Store all mappings, including GID 0 (.notdef is still valid)
                    _cidToGidMap[cid] = (uint)gid;
                }
            }
        }

        /// <summary>
        /// Get the GID for a given CID
        /// </summary>
        public uint GetGID(uint cid)
        {
            if (_isIdentityMapping)
            {
                return cid; // Identity mapping: GID = CID
            }

            // Check explicit mapping
            if (_cidToGidMap.TryGetValue(cid, out uint gid))
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
            if (_isIdentityMapping)
                return true; // Identity mapping covers all CIDs

            return _cidToGidMap.ContainsKey(cid);
        }

        /// <summary>
        /// Get the number of explicit mappings
        /// </summary>
        public int MappingCount => _isIdentityMapping ? -1 : _cidToGidMap.Count; // -1 indicates identity mapping

        /// <summary>
        /// Check if this is an identity mapping
        /// </summary>
        public bool IsIdentityMapping => _isIdentityMapping;
    }
}