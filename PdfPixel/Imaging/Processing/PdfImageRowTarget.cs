using PdfPixel.Imaging.Model;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// One destination a <see cref="PdfImageRowProcessor"/> fills, holding the region it reads from each
/// source row, the resamplers that bring it to the output grid, and the image being written.
/// </summary>
internal sealed class PdfImageRowTarget
{
    private int _outputRowIndex;

    public PdfImageRowTarget(
        int sourceStart,
        int sourceWidth,
        int outputWidth,
        IRowConverter? colorConverter,
        IRowConverter? alphaConverter,
        PdfDecodedImage image)
    {
        SourceStart = sourceStart;
        SourceWidth = sourceWidth;
        OutputWidth = outputWidth;
        ColorConverter = colorConverter;
        AlphaConverter = alphaConverter;
        Image = image;
    }

    /// <summary>
    /// Index of the first source pixel this target reads from each row.
    /// </summary>
    public int SourceStart { get; }

    /// <summary>
    /// Number of source pixels this target reads from each row.
    /// </summary>
    public int SourceWidth { get; }

    /// <summary>
    /// Number of pixels in each row of <see cref="Image"/>.
    /// </summary>
    public int OutputWidth { get; }

    /// <summary>
    /// Resampler for the color samples, or null when the pipeline writes the output row itself.
    /// </summary>
    public IRowConverter? ColorConverter { get; }

    /// <summary>
    /// Resampler for the alpha plane, or null when there is no separate plane.
    /// </summary>
    public IRowConverter? AlphaConverter { get; }

    /// <summary>
    /// Image being filled.
    /// </summary>
    public PdfDecodedImage Image { get; }

    /// <summary>
    /// True while <see cref="Image"/> still has a row left to write.
    /// </summary>
    public bool HasRoomForRow => _outputRowIndex < Image.Height;

    /// <summary>
    /// The row the next completed conversion is written to.
    /// </summary>
    public Span<byte> CurrentRow => Image.GetRow(_outputRowIndex);

    /// <summary>
    /// Moves to the next output row.
    /// </summary>
    public void AdvanceRow() => _outputRowIndex++;
}
