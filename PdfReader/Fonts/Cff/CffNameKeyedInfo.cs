using System;
using System.Collections.Generic;

namespace PdfReader.Fonts.Cff
{
    internal sealed class CffNameKeyedInfo
    {
        public bool IsCidKeyed { get; set; }

        public int CharsetOffset { get; set; }

        public int CharStringsOffset { get; set; }

        public int EncodingOffset { get; set; }

        public int GlyphCount { get; set; }

        public Dictionary<string, ushort> NameToGid { get; set; }

        public Dictionary<ushort, ushort> SidToGid { get; set; }

        public Dictionary<byte, ushort> CodeToGid { get; set; }

        public ReadOnlyMemory<byte> CffData { get; set; }
    }
}
