using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Transform;
using PdfPixel.Fonts.Model;
using PdfPixel.Functions;
using PdfPixel.Imaging.Model;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Parsing;
using PdfPixel.Shading.Model;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Caches PDF object index and provides lazy object resolution as well as access to parsed objects.
/// </summary>
internal class PdfDocumentObjectCache
{
    private readonly PdfObjectParser _pdfObjectParser;
    private readonly PdfXrefRecoveryScanner _recoveryScanner;
    private readonly ILogger<PdfDocumentObjectCache> _logger;
    private readonly Dictionary<PdfReference, PdfObject> _objects = [];
    private bool _recoveryScanAttempted;

    public PdfDocumentObjectCache(IPdfDocumentInternal document, PdfObjectParser parser)
    {
        _pdfObjectParser = parser;
        _recoveryScanner = new PdfXrefRecoveryScanner(document);
        _logger = document.LoggerFactory.CreateLogger<PdfDocumentObjectCache>();
    }

    /// <summary>
    /// Parsed catalog output intent ICC profile (first preferred or first valid). Null when none present or invalid.
    /// Populated by <see cref="Parsing.PdfOutputIntentParser"/> post xref/catalog load.
    /// </summary>
    internal IccProfile? OutputIntentProfile { get; set; }

    /// <summary>
    /// Parsed catalog output intent profile converter. Null when none present or invalid.
    /// </summary>
    internal IccBasedConverter? OutputIntentProfileConverter { get; set; }

    /// <summary>
    /// Catalog output intent converter parsed from <see cref="IccProfile"/>. Null when none present or invalid.
    /// </summary>
    internal IccBasedConverter? OutputIntentConverter { get; set; }

    /// <summary>
    /// Document font cache.
    /// </summary>
    internal Dictionary<PdfReference, PdfFontBase> Fonts { get; } = [];

    /// <summary>
    /// Document color space converter cache.
    /// </summary>
    internal Dictionary<PdfReference, PdfColorSpaceConverter?> ColorSpaceConverters { get; } = [];

    /// <summary>
    /// High-level cache for parsed PDF functions, keyed by reference.
    /// </summary>
    internal Dictionary<PdfReference, PdfFunction> Functions { get; } = [];

    /// <summary>
    /// Document cache for parsed transfer function transforms (TR), keyed by reference.
    /// </summary>
    internal Dictionary<PdfReference, TransferFunctionTransform> TransferFunctionTransforms { get; } = [];

    /// <summary>
    /// High-level cache for parsed PDF shadings, keyed by reference.
    /// </summary>
    internal Dictionary<PdfReference, PdfShading> Shadings { get; } = [];

    /// <summary>
    /// High-level cache for parsed PDF image XObjects, keyed by reference.
    /// </summary>
    internal Dictionary<PdfReference, PdfImage> Images { get; } = [];

    /// <summary>
    /// JBIG2 globals caches, keyed by the PDF reference of the /JBIG2Globals stream object.
    /// Populated on first use so each globals stream is decoded only once per document.
    /// </summary>
    internal Dictionary<PdfReference, Jbig2SegmentCache> Jbig2GlobalCaches { get; } = [];

    /// <summary>
    /// Document object index collection.
    /// </summary>
    public Dictionary<PdfReference, PdfObjectInfo> ObjectIndex { get; } = [];

    /// <summary>
    /// Retrieves an object by reference, parsing it lazily if present in the index but not yet materialized.
    /// </summary>
    /// <param name="reference">Target object reference.</param>
    /// <returns>Materialized <see cref="PdfObject"/> or null if unavailable.</returns>
    public PdfObject? GetObject(in PdfReference reference)
    {
        if (!reference.IsValid)
        {
            return null;
        }

        if (_objects.TryGetValue(reference, out PdfObject? existing))
        {
            return existing;
        }

        PdfObject? parsed = ResolveIndexedObject(reference);

        if (parsed == null && !_recoveryScanAttempted)
        {
            _recoveryScanAttempted = true;
            _logger.LogWarning("Object {Reference} could not be resolved from the xref table; running a fallback recovery scan.", reference);
            _recoveryScanner.Scan();
            parsed = ResolveIndexedObject(reference);
        }

        if (parsed != null)
        {
            _objects[parsed.Reference] = parsed;
        }

        return parsed;
    }

    private PdfObject? ResolveIndexedObject(in PdfReference reference)
    {
        if (!ObjectIndex.TryGetValue(reference, out PdfObjectInfo? info))
        {
            return null;
        }

        return _pdfObjectParser.ParseSingleIndexedObject(info);
    }
}
