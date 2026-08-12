using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Fonts.Model;
using PdfPixel.Functions;
using PdfPixel.Imaging.Model;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Parsing;
using PdfPixel.Shading.Model;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Holds the PDF object index and the document-wide caches of the resources parsed out of it, and
/// resolves objects from the index.
/// </summary>
internal class PdfDocumentObjectCache
{
    private readonly PdfObjectParser _pdfObjectParser;
    private readonly PdfXrefRecoveryScanner _recoveryScanner;
    private readonly ILogger<PdfDocumentObjectCache> _logger;
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
    /// Document typeface cache, keyed by the reference of the object holding the embedded font
    /// program, so a program several font descriptors share is parsed only once. Owns the typefaces
    /// it holds; they live as long as the document.
    /// </summary>
    internal Dictionary<PdfReference, IPdfTypeface> Typefaces { get; } = [];

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
    /// Cache for recorded tiling pattern cells, keyed by the reference of the pattern object. A cell
    /// is recorded in a graphics state built from scratch, so it depends only on the pattern object
    /// itself, never on the state the pattern is used from.
    /// </summary>
    internal Dictionary<PdfReference, PdfCommandRecorder> TilingCells { get; } = [];

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
    /// Parses the object a reference names, from the offset the index holds for it.
    /// </summary>
    /// <param name="reference">Target object reference.</param>
    /// <returns>Materialized <see cref="PdfObject"/> or null if unavailable.</returns>
    public PdfObject? GetObject(in PdfReference reference)
    {
        if (!reference.IsValid)
        {
            return null;
        }

        PdfObject? parsed = ResolveIndexedObject(reference);

        if (parsed == null && !_recoveryScanAttempted)
        {
            _logger.LogWarning("Object {Reference} could not be resolved from the xref table; running a fallback recovery scan.", reference);
            RunRecoveryScan();
            parsed = ResolveIndexedObject(reference);
        }

        return parsed;
    }

    /// <summary>
    /// Rebuilds the object index and the document root by scanning the file itself. Runs at most once
    /// per document, so that resolving objects while the scan picks a root cannot recurse into it.
    /// </summary>
    internal void RunRecoveryScan()
    {
        if (_recoveryScanAttempted)
        {
            return;
        }

        _recoveryScanAttempted = true;
        _recoveryScanner.Scan();
    }

    /// <summary>
    /// Records where an object was found without materializing it. The object is parsed on demand from
    /// this offset, which keeps it out of the cache until the document is fully set up — an object
    /// parsed before the decryptor exists would hold its strings undecrypted forever.
    /// </summary>
    /// <param name="reference">Object reference declared by the object header.</param>
    /// <param name="fileOffset">Absolute byte offset in the source file where the object header begins.</param>
    internal void SetObjectOffset(in PdfReference reference, long fileOffset)
        => ObjectIndex[reference] = PdfObjectInfo.ForUncompressed(reference, fileOffset, fromXrefStream: false);

    private PdfObject? ResolveIndexedObject(in PdfReference reference)
    {
        if (!ObjectIndex.TryGetValue(reference, out PdfObjectInfo? info))
        {
            return null;
        }

        return _pdfObjectParser.ParseSingleIndexedObject(info);
    }
}
