using System;
using PdfReader.Fonts;
using Microsoft.Extensions.Logging;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles PDF version detection and validation.
    /// Supports PDF versions from 1.0 to 2.0.
    /// Instance-based to allow structured logging via the owning document's logger factory.
    /// Exposes a single entry point returning a rich <see cref="PdfVersionInfo"/> model.
    /// </summary>
    public class PdfVersionParser
    {
        private readonly PdfDocument _document;
        private readonly ILogger<PdfVersionParser> _logger;

        /// <summary>
        /// Create a new version parser bound to the specified PDF document.
        /// </summary>
        /// <param name="document">Owning <see cref="PdfDocument"/> providing logging infrastructure.</param>
        public PdfVersionParser(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = _document.LoggerFactory.CreateLogger<PdfVersionParser>();
        }

        /// <summary>
        /// Parse the PDF header and return a populated <see cref="PdfVersionInfo"/> containing
        /// the detected version string, validity state, advanced feature requirement and feature flags.
        /// </summary>
        /// <param name="context">Parsing context positioned at the start of the file.</param>
        /// <returns>Populated <see cref="PdfVersionInfo"/> (never null).</returns>
        public PdfVersionInfo ParsePdfVersionInfo(ref PdfParseContext context)
        {
            if (context.Length < 8)
            {
                return PdfVersionInfo.Invalid();
            }

            var headerBytes = context.GetSlice(0, Math.Min(16, context.Length));
            var headerText = EncodingExtensions.PdfDefault.GetString(headerBytes);

            if (!headerText.StartsWith("%PDF-"))
            {
                _logger.LogWarning("Invalid PDF header - missing %PDF- signature");
                return PdfVersionInfo.Invalid();
            }

            int versionStart = 5; // After "%PDF-"
            int versionEnd = headerText.IndexOfAny(new char[] { '\r', '\n' }, versionStart);
            if (versionEnd == -1)
            {
                versionEnd = Math.Min(headerText.Length, versionStart + 3);
            }

            string version = headerText.Substring(versionStart, versionEnd - versionStart).Trim();

            var isValidVersion = IsValid(version);
            if (!isValidVersion)
            {
                _logger.LogWarning("Unsupported or invalid PDF version: {Version}", version);
                return PdfVersionInfo.Create(version, false, new PdfVersionFeatures());
            }

            var features = BuildFeatures(version);
            return PdfVersionInfo.Create(version, true, features);
        }

        /// <summary>
        /// Validate PDF version string and determine if advanced feature parsing paths are required.
        /// </summary>
        /// <param name="version">Version string (e.g. 1.4, 1.7, 2.0).</param>
        /// <returns>Tuple indicating validity and advanced feature requirement.</returns>
        private static bool IsValid(string version)
        {
            switch (version)
            {
                case "1.0":
                case "1.1":
                case "1.2":
                case "1.3":
                case "1.4":
                case "1.5":
                case "1.6":
                case "1.7":
                case "2.0":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Build feature flags for a given version string.
        /// </summary>
        private static PdfVersionFeatures BuildFeatures(string version)
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
                _ => new PdfVersionFeatures()
            };
        }
    }
}