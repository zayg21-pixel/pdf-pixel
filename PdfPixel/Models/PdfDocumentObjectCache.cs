using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Fonts.Model;
using PdfPixel.Parsing;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Holds the PDF object index and the document-wide caches of the resources parsed out of it, and
/// resolves objects from the index.
/// </summary>
internal class PdfDocumentObjectCache : IDisposable
{
    private readonly PdfObjectParser _pdfObjectParser;
    private readonly PdfXrefRecoveryScanner _recoveryScanner;
    private readonly ILogger<PdfDocumentObjectCache> _logger;
    private readonly Dictionary<Type, Dictionary<PdfReference, object?>> _objectsByType = [];
    private bool _recoveryScanAttempted;

    public PdfDocumentObjectCache(IPdfDocumentInternal document, PdfObjectParser parser)
    {
        _pdfObjectParser = parser;
        _recoveryScanner = new PdfXrefRecoveryScanner(document);
        _logger = document.LoggerFactory.CreateLogger<PdfDocumentObjectCache>();
    }

    /// <summary>
    /// Document object index collection.
    /// </summary>
    public Dictionary<PdfReference, PdfObjectInfo> ObjectIndex { get; } = [];

    /// <summary>
    /// Parses the object a reference names, from the offset the index holds for it.
    /// </summary>
    /// <param name="reference">Target object reference.</param>
    /// <returns>Materialized <see cref="PdfObject"/> or null if unavailable.</returns>
    public PdfObject? GetObject(PdfReference? reference)
    {
        if (reference == null)
        {
            return null;
        }

        PdfObject? parsed = ResolveIndexedObject(reference.Value);

        if (parsed == null && !_recoveryScanAttempted)
        {
            _logger.LogWarning("Object {Reference} could not be resolved from the xref table; running a fallback recovery scan.", reference);
            RunRecoveryScan();
            parsed = ResolveIndexedObject(reference.Value);
        }

        return parsed;
    }

    /// <summary>
    /// Returns the <typeparamref name="T"/> cached for a reference, or creates it with
    /// <paramref name="factory"/> from the object the reference names and caches it.
    /// </summary>
    /// <typeparam name="T">Type created from the object.</typeparam>
    /// <param name="reference">Target object reference.</param>
    /// <param name="factory">Creates the <typeparamref name="T"/> from the resolved object, or returns null when it cannot.</param>
    /// <param name="requiresStream">When true, an object without a stream yields null without invoking <paramref name="factory"/>.</param>
    /// <returns>Cached or created <typeparamref name="T"/>, or null if the object is unavailable or the factory returned null.</returns>
    public T? GetOrCreate<T>(PdfReference? reference, Func<PdfObject, T?> factory, bool requiresStream = false) where T : class
    {
        if (reference == null)
        {
            return null;
        }

        if (!_objectsByType.TryGetValue(typeof(T), out Dictionary<PdfReference, object?>? objects))
        {
            objects = new Dictionary<PdfReference, object?>();
            _objectsByType[typeof(T)] = objects;
        }

        if (objects.TryGetValue(reference.Value, out object? cached))
        {
            return (T?)cached;
        }

        PdfObject? pdfObject = GetObject(reference);
        T? created = null;
        if (pdfObject != null && (!requiresStream || pdfObject.HasStream))
        {
            created = factory(pdfObject);
        }

        objects[reference.Value] = created;

        return created;
    }

    /// <summary>
    /// Returns the <typeparamref name="T"/> for the entry <paramref name="key"/> of <paramref name="dictionary"/>.
    /// An indirect entry is cached by its reference; a direct entry is created with <paramref name="factory"/> and not cached.
    /// </summary>
    /// <typeparam name="T">Type created from the object.</typeparam>
    /// <param name="dictionary">Dictionary holding the entry.</param>
    /// <param name="key">Key of the entry.</param>
    /// <param name="factory">Creates the <typeparamref name="T"/> from the resolved object, or returns null when it cannot.</param>
    /// <param name="requiresStream">When true, an object without a stream yields null without invoking <paramref name="factory"/>.</param>
    /// <returns>Cached or created <typeparamref name="T"/>, or null if the entry is absent or the factory returned null.</returns>
    public T? GetOrCreate<T>(PdfDictionary dictionary, in PdfString key, Func<PdfObject, T?> factory, bool requiresStream = false) where T : class
    {
        PdfReference? reference = dictionary.GetReference(key);
        if (reference != null)
        {
            return GetOrCreate(reference, factory, requiresStream);
        }

        PdfObject? directObject = dictionary.GetObject(key);
        if (directObject == null || (requiresStream && !directObject.HasStream))
        {
            return null;
        }

        return factory(directObject);
    }

    /// <summary>
    /// Returns the <typeparamref name="T"/> for the entry at <paramref name="index"/> of <paramref name="array"/>.
    /// An indirect entry is cached by its reference; a direct entry is created with <paramref name="factory"/> and not cached.
    /// </summary>
    /// <typeparam name="T">Type created from the object.</typeparam>
    /// <param name="array">Array holding the entry.</param>
    /// <param name="index">Index of the entry.</param>
    /// <param name="factory">Creates the <typeparamref name="T"/> from the resolved object, or returns null when it cannot.</param>
    /// <param name="requiresStream">When true, an object without a stream yields null without invoking <paramref name="factory"/>.</param>
    /// <returns>Cached or created <typeparamref name="T"/>, or null if the entry is absent or the factory returned null.</returns>
    public T? GetOrCreate<T>(PdfArray array, int index, Func<PdfObject, T?> factory, bool requiresStream = false) where T : class
    {
        PdfReference? reference = array.GetReference(index);
        if (reference != null)
        {
            return GetOrCreate(reference, factory, requiresStream);
        }

        PdfObject? directObject = array.GetObject(index);
        if (directObject == null || (requiresStream && !directObject.HasStream))
        {
            return null;
        }

        return factory(directObject);
    }

    /// <summary>
    /// Rebuilds the object index and the document root by scanning the file itself. Runs at most once
    /// per document, so that resolving objects while the scan picks a root cannot recurse into it.
    /// </summary>
    public void RunRecoveryScan()
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
    public void SetObjectOffset(in PdfReference reference, long fileOffset)
        => ObjectIndex[reference] = PdfObjectInfo.ForUncompressed(fileOffset);

    private PdfObject? ResolveIndexedObject(in PdfReference reference)
    {
        if (!ObjectIndex.TryGetValue(reference, out PdfObjectInfo? info))
        {
            return null;
        }

        return _pdfObjectParser.ParseSingleIndexedObject(reference, info);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_objectsByType.TryGetValue(typeof(IPdfTypeface), out Dictionary<PdfReference, object?>? typefaces))
        {
            return;
        }

        foreach (object? value in typefaces.Values)
        {
            var typeface = (IPdfTypeface?)value;
            typeface?.Dispose();
        }
    }
}
