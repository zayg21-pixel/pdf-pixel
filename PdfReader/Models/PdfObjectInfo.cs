using System;

namespace PdfReader.Models;

/// <summary>
/// Describes a single indirect object entry as declared in a PDF cross-reference (xref) table or xref stream.
/// The information contained here is limited strictly to what can be obtained from the xref structures
/// without parsing the actual object body. It is intended for building an object index that enables
/// lazy (on-demand) materialization of <see cref="PdfObject"/> instances.
/// </summary>
public sealed class PdfObjectInfo
{
    /// <summary>
    /// Creates an instance representing an in-use, uncompressed (type 1) indirect object whose bytes
    /// start at the specified absolute file offset.
    /// </summary>
    /// <param name="reference">Object reference (number + generation).</param>
    /// <param name="fileOffset">Absolute byte offset in the original PDF file where the object header begins.</param>
    /// <param name="fromXrefStream">True if sourced from a cross-reference stream entry; false if from classic xref table.</param>
    /// <returns>Configured <see cref="PdfObjectInfo"/> instance.</returns>
    public static PdfObjectInfo ForUncompressed(PdfReference reference, long fileOffset, bool fromXrefStream)
    {
        if (!reference.IsValid)
        {
            throw new ArgumentException("Reference must be valid.", nameof(reference));
        }
        if (fileOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileOffset));
        }

        return new PdfObjectInfo(reference)
        {
            Offset = fileOffset,
            IsFree = false,
            IsCompressed = false,
            FromCrossReferenceStream = fromXrefStream
        };
    }

    /// <summary>
    /// Creates an instance representing a compressed indirect object (type 2 entry in a cross-reference stream).
    /// The object resides inside an object stream identified by <paramref name="objectStreamNumber"/> at
    /// the specified <paramref name="indexInObjectStream"/> (0-based) position.
    /// </summary>
    /// <param name="reference">Object reference (number + generation, usually generation 0 in compressed streams).</param>
    /// <param name="objectStreamNumber">The object number of the containing /ObjStm object.</param>
    /// <param name="indexInObjectStream">Zero-based index within the object stream.</param>
    /// <param name="fromXrefStream">True if sourced from a cross-reference stream.</param>
    /// <returns>Configured <see cref="PdfObjectInfo"/> instance.</returns>
    public static PdfObjectInfo ForCompressed(PdfReference reference, uint objectStreamNumber, int indexInObjectStream, bool fromXrefStream)
    {
        if (objectStreamNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectStreamNumber));
        }
        if (indexInObjectStream < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indexInObjectStream));
        }

        return new PdfObjectInfo(reference)
        {
            IsFree = false,
            IsCompressed = true,
            ObjectStreamNumber = objectStreamNumber,
            ObjectStreamIndex = indexInObjectStream,
            FromCrossReferenceStream = fromXrefStream
        };
    }

    /// <summary>
    /// Creates an instance representing a free object (type 0 entry). Free objects are kept so that
    /// incremental update chains (linked list through <c>next free object</c>) could be validated if needed.
    /// They are otherwise ignored during normal object materialization.
    /// </summary>
    /// <param name="reference">Object reference (number + generation).</param>
    /// <param name="nextFreeObjectNumber">The object number of the next free object in the free list (may be zero).</param>
    /// <param name="nextFreeGeneration">The generation number of the next free object (usually incremented).</param>
    /// <param name="fromXrefStream">True if sourced from a cross-reference stream.</param>
    /// <returns>Configured <see cref="PdfObjectInfo"/> instance.</returns>
    public static PdfObjectInfo ForFree(PdfReference reference, int nextFreeObjectNumber, int nextFreeGeneration, bool fromXrefStream)
    {
        if (nextFreeObjectNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextFreeObjectNumber));
        }
        if (nextFreeGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nextFreeGeneration));
        }

        return new PdfObjectInfo(reference)
        {
            IsFree = true,
            NextFreeObjectNumber = nextFreeObjectNumber,
            NextFreeGeneration = nextFreeGeneration,
            FromCrossReferenceStream = fromXrefStream
        };
    }

    private PdfObjectInfo(PdfReference reference)
    {
        Reference = reference;
    }

    /// <summary>
    /// Indirect object reference (object number and generation) uniquely identifying this entry.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// For uncompressed in-use objects: absolute byte offset of the object header in the source file.
    /// Undefined (null) for compressed or free objects.
    /// </summary>
    public long? Offset { get; private set; }

    /// <summary>
    /// True if the entry represents a free object (type 0). Free entries are not materialized.
    /// </summary>
    public bool IsFree { get; private set; }

    /// <summary>
    /// True if the object is stored inside an object stream (type 2 entry in a cross-reference stream).
    /// </summary>
    public bool IsCompressed { get; private set; }

    /// <summary>
    /// Object number of the containing object stream when <see cref="IsCompressed"/> is true.
    /// Null for uncompressed or free objects.
    /// </summary>
    public uint? ObjectStreamNumber { get; private set; }

    /// <summary>
    /// Zero-based index inside the containing object stream when <see cref="IsCompressed"/> is true.
    /// Null for uncompressed or free objects.
    /// </summary>
    public int? ObjectStreamIndex { get; private set; }

    /// <summary>
    /// Next free object number in the free list (only meaningful if <see cref="IsFree"/> is true).
    /// </summary>
    public int? NextFreeObjectNumber { get; private set; }

    /// <summary>
    /// Generation number of the next free object in the free list (only meaningful if <see cref="IsFree"/> is true).
    /// </summary>
    public int? NextFreeGeneration { get; private set; }

    /// <summary>
    /// True if this info was produced from a cross-reference stream (PDF 1.5+) instead of a classic xref table.
    /// </summary>
    public bool FromCrossReferenceStream { get; private set; }

    /// <summary>
    /// True if this entry was synthesized by a fallback scanner (no valid xref data). Used for diagnostics.
    /// </summary>
    public bool FromFallbackScan { get; internal set; }

    /// <summary>
    /// Optional relative byte offset inside the decoded object stream where this compressed object's bytes start.
    /// Populated on-demand when the containing /ObjStm is first indexed.
    /// </summary>
    internal int? ObjectStreamRelativeOffset { get; set; }

    /// <summary>
    /// Returns a friendly textual representation for logging and diagnostics.
    /// </summary>
    public override string ToString()
    {
        if (IsFree)
        {
            return $"{Reference} free -> next {NextFreeObjectNumber} {NextFreeGeneration}";
        }
        if (IsCompressed)
        {
            return $"{Reference} compressed in {ObjectStreamNumber} index {ObjectStreamIndex}";
        }
        return $"{Reference} offset {Offset}";
    }
}
