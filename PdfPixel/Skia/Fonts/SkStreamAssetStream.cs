using SkiaSharp;
using System;
using System.IO;

namespace PdfPixel.Skia.Fonts;

/// <summary>
/// Adapts an <see cref="SKStreamAsset"/> (e.g. from <see cref="SKTypeface.OpenStream(out int)"/>) to a
/// plain, seekable <see cref="Stream"/>, so it can be wrapped by <see cref="PdfPixel.Fonts.Typeface.ReadOnlyFontStream"/>
/// without buffering the font program into memory up front.
/// </summary>
internal sealed class SkStreamAssetStream : Stream
{
    private readonly SKStreamAsset _streamAsset;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkStreamAssetStream"/> class. Takes ownership of
    /// <paramref name="streamAsset"/>; it is disposed together with this instance.
    /// </summary>
    /// <param name="streamAsset">The Skia stream asset to wrap.</param>
    public SkStreamAssetStream(SKStreamAsset streamAsset) => _streamAsset = streamAsset;

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _streamAsset.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _streamAsset.Position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // No-op: read-only.
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (offset == 0)
        {
            return (int)_streamAsset.Read(buffer, count);
        }

        var temporaryBuffer = new byte[count];
        var read = (int)_streamAsset.Read(temporaryBuffer, count);
        Array.Copy(temporaryBuffer, 0, buffer, offset, read);
        return read;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        _streamAsset.Seek((int)newPosition);
        return newPosition;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException("SkStreamAssetStream is read-only.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("SkStreamAssetStream is read-only.");

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _streamAsset.Dispose();
        }

        base.Dispose(disposing);
    }
}
