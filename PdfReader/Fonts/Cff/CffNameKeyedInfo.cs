using PdfReader.Models;
using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Cff
{
    /// <summary>
    /// Holds parsed metadata for a name-keyed CFF (Type 1C) font.
    /// Provides glyph count, glyph name to GID mapping, and raw CFF data.
    /// </summary>
    internal sealed class CffNameKeyedInfo
    {
        /// <summary>
        /// Gets or sets the total number of glyphs in the font.
        /// </summary>
        public int GlyphCount { get; set; }

        /// <summary>
        /// Gets or sets the mapping from glyph names to glyph IDs (GIDs).
        /// </summary>
        public Dictionary<PdfString, ushort> NameToGid { get; set; }

        /// <summary>
        /// Gets or sets the raw CFF font data.
        /// </summary>
        public ReadOnlyMemory<byte> CffData { get; set; }
    }
}
