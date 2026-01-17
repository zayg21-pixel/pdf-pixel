using PdfRender.Fonts.Model;
using System;

namespace PdfRender.Fonts.Management
{
    /// <summary>
    /// Holds substitution candidates and default encoding for a standard 14 PDF font family.
    /// </summary>
    internal class Standard14Info
    {
        /// <summary>
        /// List of font family names to try for substitution, in order of preference.
        /// </summary>
        public string[] SubstitutionCandidates { get; }

        /// <summary>
        /// The default encoding for this standard 14 font family.
        /// </summary>
        public PdfFontEncoding DefaultEncoding { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Standard14Info"/> class.
        /// </summary>
        /// <param name="substitutionCandidates">Font family names to try for substitution.</param>
        /// <param name="defaultEncoding">The default encoding for this font family.</param>
        public Standard14Info(string[] substitutionCandidates, PdfFontEncoding defaultEncoding)
        {
            SubstitutionCandidates = substitutionCandidates ?? Array.Empty<string>();
            DefaultEncoding = defaultEncoding;
        }
    }
}
