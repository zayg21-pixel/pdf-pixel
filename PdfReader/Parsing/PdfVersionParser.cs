using PdfReader.Fonts;
using System;
using System.Text;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles PDF version detection and validation
    /// Supports PDF versions from 1.0 to 2.0
    /// </summary>
    public static class PdfVersionParser
    {
        /// <summary>
        /// Parse and validate PDF version from header
        /// </summary>
        public static (bool isValid, string version, bool requiresAdvancedFeatures) ParsePdfVersion(ref PdfParseContext context)
        {            
            // Look for PDF header at the beginning
            if (context.Length < 8) // Minimum for "%PDF-1.0"
                return (false, null, false);

            // Check for PDF magic number
            var headerBytes = context.GetSlice(0, Math.Min(16, context.Length));
            var headerText = EncodingExtensions.PdfDefault.GetString(headerBytes);

            if (!headerText.StartsWith("%PDF-"))
            {
                Console.WriteLine("Invalid PDF header - missing %PDF- signature");
                return (false, null, false);
            }

            // Extract version string
            var versionStart = 5; // After "%PDF-"
            var versionEnd = headerText.IndexOfAny(new char[] { '\r', '\n' }, versionStart);
            if (versionEnd == -1)
                versionEnd = Math.Min(headerText.Length, versionStart + 3);

            var version = headerText.Substring(versionStart, versionEnd - versionStart).Trim();

            Console.WriteLine($"Detected PDF version: {version}");

            // Validate version and determine feature requirements
            var (isValidVersion, requiresAdvanced) = ValidateVersion(version);

            if (!isValidVersion)
            {
                Console.WriteLine($"Unsupported or invalid PDF version: {version}");
                return (false, version, false);
            }

            return (true, version, requiresAdvanced);
        }

        /// <summary>
        /// Validate PDF version and determine if advanced features are required
        /// </summary>
        private static (bool isValid, bool requiresAdvancedFeatures) ValidateVersion(string version)
        {
            return version switch
            {
                "1.0" => (true, false),
                "1.1" => (true, false), 
                "1.2" => (true, false),
                "1.3" => (true, false),
                "1.4" => (true, false), // Last version before major changes
                "1.5" => (true, true),  // Introduced object streams, cross-reference streams
                "1.6" => (true, true),  // Enhanced security, metadata streams
                "1.7" => (true, true),  // Extension level, 3D annotations, etc.
                "2.0" => (true, true),  // Modern PDF features
                _ => (false, false)     // Unknown/unsupported version
            };
        }

        /// <summary>
        /// Get required features for a specific PDF version
        /// </summary>
        public static PdfVersionFeatures GetRequiredFeatures(string version)
        {
            return version switch
            {
                "1.0" or "1.1" or "1.2" or "1.3" or "1.4" => new PdfVersionFeatures
                {
                    SupportsObjectStreams = false,
                    SupportsXrefStreams = false,
                    SupportsIncrementalUpdates = true,
                    SupportsEncryption = version switch
                    {
                        "1.1" or "1.2" or "1.3" or "1.4" => true,
                        _ => false
                    }
                },
                "1.5" => new PdfVersionFeatures
                {
                    SupportsObjectStreams = true,
                    SupportsXrefStreams = true,
                    SupportsIncrementalUpdates = true,
                    SupportsEncryption = true,
                    SupportsMetadataStreams = false
                },
                "1.6" => new PdfVersionFeatures
                {
                    SupportsObjectStreams = true,
                    SupportsXrefStreams = true,
                    SupportsIncrementalUpdates = true,
                    SupportsEncryption = true,
                    SupportsMetadataStreams = true
                },
                "1.7" => new PdfVersionFeatures
                {
                    SupportsObjectStreams = true,
                    SupportsXrefStreams = true,
                    SupportsIncrementalUpdates = true,
                    SupportsEncryption = true,
                    SupportsMetadataStreams = true,
                    SupportsExtensionLevel = true,
                    Supports3DAnnotations = true
                },
                "2.0" => new PdfVersionFeatures
                {
                    SupportsObjectStreams = true,
                    SupportsXrefStreams = true,
                    SupportsIncrementalUpdates = true,
                    SupportsEncryption = true,
                    SupportsMetadataStreams = true,
                    SupportsExtensionLevel = true,
                    Supports3DAnnotations = true,
                    SupportsModernFeatures = true
                },
                _ => new PdfVersionFeatures() // Default empty features
            };
        }
    }

    /// <summary>
    /// Represents the features available in a specific PDF version
    /// </summary>
    public class PdfVersionFeatures
    {
        public bool SupportsObjectStreams { get; set; }
        public bool SupportsXrefStreams { get; set; }
        public bool SupportsIncrementalUpdates { get; set; }
        public bool SupportsEncryption { get; set; }
        public bool SupportsMetadataStreams { get; set; }
        public bool SupportsExtensionLevel { get; set; }
        public bool Supports3DAnnotations { get; set; }
        public bool SupportsModernFeatures { get; set; }

        public override string ToString()
        {
            var features = new StringBuilder();
            if (SupportsObjectStreams) features.Append("ObjectStreams ");
            if (SupportsXrefStreams) features.Append("XrefStreams ");
            if (SupportsEncryption) features.Append("Encryption ");
            if (SupportsMetadataStreams) features.Append("MetadataStreams ");
            if (SupportsExtensionLevel) features.Append("ExtensionLevel ");
            if (Supports3DAnnotations) features.Append("3DAnnotations ");
            if (SupportsModernFeatures) features.Append("ModernFeatures ");
            
            return features.ToString().Trim();
        }
    }
}