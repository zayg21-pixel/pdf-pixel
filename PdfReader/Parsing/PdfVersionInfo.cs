using System.Xml.Serialization;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Aggregates PDF version parsing results including the raw version string,
    /// validation status, and the feature capability flags for that version.
    /// </summary>
    public sealed class PdfVersionInfo
    {
        /// <summary>
        /// Gets the semantic version string extracted from the PDF header (e.g. 1.4, 1.7, 2.0).
        /// May be null when invalid.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// True if the version string is recognized and supported.
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// Feature capability flags associated with the version.
        /// Empty (default constructed) when version is invalid or unknown.
        /// </summary>
        public PdfVersionFeatures Features { get; private set; }

        private PdfVersionInfo()
        {
        }

        /// <summary>
        /// Create an instance representing an invalid / unsupported version.
        /// </summary>
        public static PdfVersionInfo Invalid()
        {
            return new PdfVersionInfo
            {
                Version = null,
                IsValid = false,
                Features = new PdfVersionFeatures()
            };
        }

        /// <summary>
        /// Factory for a fully specified version info.
        /// </summary>
        public static PdfVersionInfo Create(string version, bool isValid, PdfVersionFeatures features)
        {
            return new PdfVersionInfo
            {
                Version = version,
                IsValid = isValid,
                Features = features ?? new PdfVersionFeatures()
            };
        }

        /// <summary>
        /// Returns the version string for debugging.
        /// </summary>
        public override string ToString()
        {
            return Version ?? "<invalid-version>";
        }
    }
}
