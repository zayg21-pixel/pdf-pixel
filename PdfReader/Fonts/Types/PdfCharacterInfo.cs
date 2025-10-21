using PdfReader.Fonts.Mapping;

namespace PdfReader.Fonts
{
    /// <summary>
    /// Holds all resolved information for a single PDF character code.
    /// </summary>
    public struct PdfCharacterInfo
    {
        /// <summary>
        /// The original character code.
        /// </summary>
        public PdfCharacterCode CharacterCode { get; }

        /// <summary>
        /// The Unicode string for this character code.
        /// </summary>
        public string Unicode { get; }

        /// <summary>
        /// The glyph ID(s) for this character code.
        /// </summary>
        public ushort[] Gids { get; }

        /// <summary>
        /// The width(s) for each glyph.
        /// </summary>
        public float[] Widths { get; }

        /// <summary>
        /// Creates a PdfCharacterInfo for multiple Unicode, GIDs, and widths.
        /// </summary>
        public PdfCharacterInfo(
            PdfCharacterCode characterCode,
            string unicode,
            ushort[] gids,
            float[] widths)
        {
            CharacterCode = characterCode;
            Unicode = unicode;
            Gids = gids;
            Widths = widths;
        }

        /// <summary>
        /// Creates a PdfCharacterInfo for a single Unicode, GID, and width.
        /// </summary>
        public PdfCharacterInfo(
            PdfCharacterCode characterCode,
            string unicode,
            ushort gid,
            float width)
        {
            CharacterCode = characterCode;
            Unicode = unicode;
            Gids = [gid];
            Widths = [width];
        }
    }
}
