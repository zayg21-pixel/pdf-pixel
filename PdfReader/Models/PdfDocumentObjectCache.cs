using PdfReader.Color.ColorSpace;
using PdfReader.Color.Icc.Model;
using PdfReader.Fonts.Model;
using PdfReader.Functions;
using PdfReader.Parsing;
using System.Collections.Generic;

namespace PdfReader.Models;

/// <summary>
/// Caches PDF object index and provides lazy object resolution as well as access to parsed objects.
/// </summary>
internal class PdfDocumentObjectCache
{
    private readonly PdfObjectParser _pdfObjectParser;
    private readonly Dictionary<PdfReference, PdfObject> _objects = new Dictionary<PdfReference, PdfObject>();

    public PdfDocumentObjectCache(PdfObjectParser parser)
    {
        _pdfObjectParser = parser;
    }

    /// <summary>
    /// Parsed catalog output intent ICC profile (first preferred or first valid). Null when none present or invalid.
    /// Populated by <see cref="Parsing.PdfOutputIntentParser"/> post xref/catalog load.
    /// </summary>
    internal IccProfile OutputIntentProfile { get; set; }

    /// <summary>
    /// Document font cache.
    /// </summary>
    internal Dictionary<PdfReference, PdfFontBase> Fonts { get; } = new Dictionary<PdfReference, PdfFontBase>();

    /// <summary>
    /// Document color space converter cache.
    /// </summary>
    internal Dictionary<PdfReference, PdfColorSpaceConverter> ColorSpaceConverters { get; } = new Dictionary<PdfReference, PdfColorSpaceConverter>();

    /// <summary>
    /// High-level cache for parsed PDF functions, keyed by reference.
    /// </summary>
    internal Dictionary<PdfReference, PdfFunction> Functions { get; } = new Dictionary<PdfReference, PdfFunction>();

    /// <summary>
    /// Document object index collection.
    /// </summary>
    public Dictionary<PdfReference, PdfObjectInfo> ObjectIndex { get; } = new Dictionary<PdfReference, PdfObjectInfo>();

    /// <summary>
    /// Retrieves an object by reference, parsing it lazily if present in the index but not yet materialized.
    /// </summary>
    /// <param name="reference">Target object reference.</param>
    /// <returns>Materialized <see cref="PdfObject"/> or null if unavailable.</returns>
    public PdfObject GetObject(PdfReference reference)
    {
        if (!reference.IsValid)
        {
            return null;
        }

        if (_objects.TryGetValue(reference, out var existing))
        {
            return existing;
        }

        if (!ObjectIndex.TryGetValue(reference, out var info))
        {
            return null;
        }

        var parsed = _pdfObjectParser.ParseSingleIndexedObject(info);
        if (parsed != null)
        {
            _objects[parsed.Reference] = parsed;
        }
        return parsed;
    }
}
