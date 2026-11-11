using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Objects;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// The samples of an image drawn by the image rule: its tiles and how they are drawn.
/// </summary>
public interface IPdfImageDecoder
{
    /// <summary>
    /// Gets what masks the image.
    /// </summary>
    PdfImageMaskKind MaskKind { get; }


    /// <summary>
    /// Gets whether the image is a stencil mask painted in the fill paint.
    /// </summary>
    bool HasImageMask { get; }

    /// <summary>
    /// Gets the ranges the sample codes are decoded to, or <see langword="null"/> for the defaults of the color space.
    /// </summary>
    PdfRange[]? Decode { get; }

    /// <summary>
    /// Gets whether the image asks to be interpolated where it is enlarged.
    /// </summary>
    bool Interpolate { get; }

    /// <summary>
    /// Gets the minimum and maximum code of each component masked out, or <see langword="null"/> when none is.
    /// </summary>
    int[]? ColorKeyMask { get; }

    /// <summary>
    /// Gets the color the image's colors are premultiplied with by its soft mask, or <see langword="null"/> when they
    /// are not.
    /// </summary>
    float[]? Matte { get; }

    /// <summary>
    /// Selects the tiles <see cref="GetTile"/> returns until the next selection: the smallest resolution with at least
    /// <paramref name="targetSize"/> samples per axis, and the part of the image that will be read. Decodes no tile.
    /// </summary>
    /// <param name="targetSize">The fewest samples per axis the image is drawn with.</param>
    /// <param name="region">The part of the unit square the image occupies that will be read, first row at the top; <see langword="null"/> for all of it.</param>
    /// <param name="cache">The cache tiles are decoded into and kept in until <see cref="ReleaseTiles"/>.</param>
    /// <param name="observer">Observer notified on progress; may be null.</param>
    PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfImageCache cache, IPdfExecutionObserver? observer);

    /// <summary>
    /// Returns the selected tile at <paramref name="tileIndex"/>, decoding it first when needed, or
    /// <see langword="null"/> when it cannot be produced.
    /// </summary>
    PdfImageTile? GetTile(int tileIndex, IPdfExecutionObserver? observer);

    /// <summary>
    /// Returns the selected alpha tile at <paramref name="tileIndex"/>, or <see langword="null"/> when the samples carry
    /// no alpha.
    /// </summary>
    PdfImageTile? GetAlphaTile(int tileIndex, IPdfExecutionObserver? observer);

    /// <summary>
    /// Returns the selected tiles to the cache they were selected with.
    /// </summary>
    void ReleaseTiles();
}
