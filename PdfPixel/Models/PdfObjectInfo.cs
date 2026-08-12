using System;

namespace PdfPixel.Models;

/// <summary>
/// Describes a single indirect object entry as declared in a PDF cross-reference (xref) table or xref stream.
/// The information contained here is limited strictly to what can be obtained from the xref structures
/// without parsing the actual object body. It is intended for building an object index that enables
/// lazy (on-demand) materialization of <see cref="PdfObject"/> instances.
/// </summary>
public sealed class PdfObjectInfo
{
    /// <summary>
    /// The entry every free object in an index shares. A free object carries nothing beyond being free.
    /// </summary>
    public static readonly PdfObjectInfo Free = new(PdfObjectInfoFlags.Free, 0, 0);

    // An entry describes itself with at most two numbers, and which two depends on its state: an
    // uncompressed object gives a file offset, a compressed one gives its object stream and its index
    // inside it, and a free one gives neither.
    private readonly long _primary;
    private readonly int _secondary;

    private int _objectStreamRelativeOffset;
    private PdfObjectInfoFlags _flags;

    private PdfObjectInfo(PdfObjectInfoFlags flags, long primary, int secondary)
    {
        _flags = flags;
        _primary = primary;
        _secondary = secondary;
    }

    /// <summary>
    /// True if the entry represents a free object (type 0). Free entries are not materialized.
    /// </summary>
    public bool IsFree => (_flags & PdfObjectInfoFlags.Free) != 0;

    /// <summary>
    /// True if the object is stored inside an object stream (type 2 entry in a cross-reference stream).
    /// </summary>
    public bool IsCompressed => (_flags & PdfObjectInfoFlags.Compressed) != 0;

    /// <summary>
    /// For uncompressed in-use objects: absolute byte offset of the object header in the source file.
    /// Undefined (null) for compressed or free objects.
    /// </summary>
    public long? Offset => ((_flags & (PdfObjectInfoFlags.Free | PdfObjectInfoFlags.Compressed)) != 0) ? null : _primary;

    /// <summary>
    /// Object number of the containing object stream when <see cref="IsCompressed"/> is true.
    /// Null for uncompressed or free objects.
    /// </summary>
    public uint? ObjectStreamNumber => IsCompressed ? (uint)_primary : null;

    /// <summary>
    /// Zero-based index inside the containing object stream when <see cref="IsCompressed"/> is true.
    /// Null for uncompressed or free objects.
    /// </summary>
    public int? ObjectStreamIndex => IsCompressed ? _secondary : null;

    /// <summary>
    /// Optional relative byte offset inside the decoded object stream where this compressed object's bytes start.
    /// Populated on-demand when the containing /ObjStm is first indexed.
    /// </summary>
    internal int? ObjectStreamRelativeOffset
    {
        get => ((_flags & PdfObjectInfoFlags.HasObjectStreamRelativeOffset) != 0) ? _objectStreamRelativeOffset : null;

        set
        {
            if (value == null)
            {
                _flags &= ~PdfObjectInfoFlags.HasObjectStreamRelativeOffset;
                _objectStreamRelativeOffset = 0;

                return;
            }

            _flags |= PdfObjectInfoFlags.HasObjectStreamRelativeOffset;
            _objectStreamRelativeOffset = value.Value;
        }
    }

    /// <summary>
    /// Creates an instance representing an in-use, uncompressed (type 1) indirect object whose bytes
    /// start at the specified absolute file offset.
    /// </summary>
    /// <param name="fileOffset">Absolute byte offset in the original PDF file where the object header begins.</param>
    /// <returns>Configured <see cref="PdfObjectInfo"/> instance.</returns>
    public static PdfObjectInfo ForUncompressed(long fileOffset)
    {
        if (fileOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileOffset));
        }

        return new PdfObjectInfo(PdfObjectInfoFlags.None, fileOffset, 0);
    }

    /// <summary>
    /// Creates an instance representing a compressed indirect object (type 2 entry in a cross-reference stream).
    /// The object resides inside an object stream identified by <paramref name="objectStreamNumber"/> at
    /// the specified <paramref name="indexInObjectStream"/> (0-based) position.
    /// </summary>
    /// <param name="objectStreamNumber">The object number of the containing /ObjStm object.</param>
    /// <param name="indexInObjectStream">Zero-based index within the object stream.</param>
    /// <returns>Configured <see cref="PdfObjectInfo"/> instance.</returns>
    public static PdfObjectInfo ForCompressed(uint objectStreamNumber, int indexInObjectStream)
    {
        if (objectStreamNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectStreamNumber));
        }

        if (indexInObjectStream < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indexInObjectStream));
        }

        return new PdfObjectInfo(PdfObjectInfoFlags.Compressed, objectStreamNumber, indexInObjectStream);
    }

    /// <summary>
    /// Returns a friendly textual representation for logging and diagnostics.
    /// </summary>
    public override string ToString()
    {
        if (IsFree)
        {
            return "free";
        }

        if (IsCompressed)
        {
            return $"compressed in {ObjectStreamNumber} index {ObjectStreamIndex}";
        }

        return $"offset {Offset}";
    }

    [Flags]
    private enum PdfObjectInfoFlags : byte
    {
        None = 0,
        Free = 1 << 0,
        Compressed = 1 << 1,
        HasObjectStreamRelativeOffset = 1 << 2
    }
}
