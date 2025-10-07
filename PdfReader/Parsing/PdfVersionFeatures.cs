using System.Text;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Describes feature capabilities implied by a specific parsed PDF version.
    /// Populated by the version parser so downstream components can branch
    /// on supported constructs (e.g. object streams, cross-reference streams).
    /// </summary>
    public class PdfVersionFeatures
    {
        /// <summary>
        /// True when the PDF version introduces compressed object streams (/ObjStm).
        /// Added in PDF 1.5.
        /// </summary>
        public bool SupportsObjectStreams { get; set; }

        /// <summary>
        /// True when cross-reference streams are permitted (/XRef streams replacing traditional xref tables).
        /// Added in PDF 1.5.
        /// </summary>
        public bool SupportsXrefStreams { get; set; }

        /// <summary>
        /// True if incremental update append operations are supported (most versions >= 1.1).
        /// </summary>
        public bool SupportsIncrementalUpdates { get; set; }

        /// <summary>
        /// True if encryption dictionaries (standard security handler etc.) are supported.
        /// </summary>
        public bool SupportsEncryption { get; set; }

        /// <summary>
        /// True if metadata streams (XMP packets stored in stream objects) are supported.
        /// Introduced in later 1.x revisions (1.4+ / 1.6 for broader adoption).
        /// </summary>
        public bool SupportsMetadataStreams { get; set; }

        /// <summary>
        /// True if extension levels (1.7 Adobe extensions) are recognized.
        /// </summary>
        public bool SupportsExtensionLevel { get; set; }

        /// <summary>
        /// True if 3D annotations and related RichMedia constructs are supported (1.7+).
        /// </summary>
        public bool Supports3DAnnotations { get; set; }

        /// <summary>
        /// True if modern PDF 2.0 feature set indicators are present.
        /// </summary>
        public bool SupportsModernFeatures { get; set; }
    }
}