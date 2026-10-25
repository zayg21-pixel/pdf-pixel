using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Imaging;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel;

/// <summary>
/// Holds the PDF object index and the document-wide caches of the resources parsed out of it, and
/// resolves objects from the index.
/// </summary>
internal class PdfDocumentObjectCache : IDisposable
{
    private readonly IPdfDocumentInternal _document;
    private readonly PdfXrefRecoveryScanner _recoveryScanner;
    private readonly ILogger<PdfDocumentObjectCache> _logger;
    private readonly Dictionary<Type, Dictionary<PdfReference, object?>> _objectsByType = [];
    private PdfObjectParser _pdfObjectParser;
    private bool _isLoaded;
    private bool _recoveryScanAttempted;

    public PdfDocumentObjectCache(IPdfDocumentInternal document)
    {
        _document = document;
        _pdfObjectParser = new PdfObjectParser(document);
        _recoveryScanner = new PdfXrefRecoveryScanner(document);
        _logger = document.LoggerFactory.CreateLogger<PdfDocumentObjectCache>();
    }

    /// <summary>
    /// Document object index collection.
    /// </summary>
    public Dictionary<PdfReference, PdfObjectInfo> ObjectIndex { get; private set; } = [];

    /// <summary>
    /// Images the document keeps for reuse, among them the inline images read from its content streams.
    /// </summary>
    public PdfImageCache Images { get; } = new();

    /// <summary>
    /// Takes <paramref name="objectIndex"/> as the object index. Until called, no object resolves.
    /// </summary>
    /// <param name="objectIndex">Object index of the document.</param>
    public void Load(Dictionary<PdfReference, PdfObjectInfo> objectIndex)
    {
        ObjectIndex = objectIndex;
        _isLoaded = true;
    }

    /// <summary>
    /// Returns the cache to its state before <see cref="Load"/>: no index, no cached objects, no scan attempted.
    /// </summary>
    public void Reset()
    {
        DisposeCachedObjects();
        _objectsByType.Clear();
        Images.Clear();
        _pdfObjectParser = new PdfObjectParser(_document);
        ObjectIndex = new Dictionary<PdfReference, PdfObjectInfo>();
        _isLoaded = false;
        _recoveryScanAttempted = false;
    }

    /// <summary>
    /// Parses the object a reference names, from the offset the index holds for it.
    /// </summary>
    /// <param name="reference">Target object reference.</param>
    /// <returns>Materialized <see cref="PdfObject"/> or null if unavailable.</returns>
    public PdfObject? GetObject(PdfReference? reference)
    {
        if (reference == null || !_isLoaded)
        {
            return null;
        }

        PdfObject? parsed = null;
        if (ObjectIndex.TryGetValue(reference.Value, out PdfObjectInfo? info))
        {
            parsed = _pdfObjectParser.ParseSingleIndexedObject(reference.Value, info);
        }

        if (parsed == null && !_recoveryScanAttempted)
        {
            _logger.LogWarning("Object {Reference} could not be resolved from the xref table; running a fallback recovery scan.", reference);
            MergeRecoveryScan();

            if (ObjectIndex.TryGetValue(reference.Value, out info))
            {
                parsed = _pdfObjectParser.ParseSingleIndexedObject(reference.Value, info);
            }
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
    /// <param name="dictionary">Dictionary holding the entry, or null when there is none.</param>
    /// <param name="key">Key of the entry.</param>
    /// <param name="factory">Creates the <typeparamref name="T"/> from the resolved object, or returns null when it cannot.</param>
    /// <param name="requiresStream">When true, an object without a stream yields null without invoking <paramref name="factory"/>.</param>
    /// <returns>Cached or created <typeparamref name="T"/>, or null if the dictionary or the entry is absent or the factory returned null.</returns>
    public T? GetOrCreate<T>(PdfDictionary? dictionary, in PdfString key, Func<PdfObject, T?> factory, bool requiresStream = false) where T : class
    {
        if (dictionary == null)
        {
            return null;
        }

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
    /// <param name="array">Array holding the entry, or null when there is none.</param>
    /// <param name="index">Index of the entry.</param>
    /// <param name="factory">Creates the <typeparamref name="T"/> from the resolved object, or returns null when it cannot.</param>
    /// <param name="requiresStream">When true, an object without a stream yields null without invoking <paramref name="factory"/>.</param>
    /// <returns>Cached or created <typeparamref name="T"/>, or null if the array or the entry is absent or the factory returned null.</returns>
    public T? GetOrCreate<T>(PdfArray? array, int index, Func<PdfObject, T?> factory, bool requiresStream = false) where T : class
    {
        if (array == null)
        {
            return null;
        }

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
    /// Scans the file and takes the scanned entries into the index, replacing the entries it holds for the same references.
    /// </summary>
    private void MergeRecoveryScan()
    {
        _recoveryScanAttempted = true;
        PdfCrossReference scanned = _recoveryScanner.Scan();

        foreach (KeyValuePair<PdfReference, PdfObjectInfo> entry in scanned.ObjectIndex)
        {
            ObjectIndex[entry.Key] = entry.Value;
        }
    }

    private void DisposeCachedObjects()
    {
        foreach (Dictionary<PdfReference, object?> objects in _objectsByType.Values)
        {
            foreach (object? value in objects.Values)
            {
                if (value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => DisposeCachedObjects();
}
