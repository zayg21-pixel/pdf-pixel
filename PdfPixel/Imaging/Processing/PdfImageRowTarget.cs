using PdfPixel.Imaging.Model;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// One destination a <see cref="PdfImageRowProcessor"/> fills, holding the region it reads from each
/// source row, where that region lands on the output grid, the resampler that brings it there, and
/// the image being written.
/// </summary>
internal sealed class PdfImageRowTarget
{
    private int _outputRowIndex;

    public PdfImageRowTarget(
        int sourceStart,
        int sourceWidth,
        int outputStart,
        int outputRowStart,
        IRowConverter? colorConverter,
        PdfDecodedImage image)
    {
        SourceStart = sourceStart;
        SourceWidth = sourceWidth;
        OutputStart = outputStart;
        OutputRowStart = outputRowStart;
        ColorConverter = colorConverter;
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
    /// Index of the first output pixel this target covers on the output grid of the whole image.
    /// </summary>
    public int OutputStart { get; }

    /// <summary>
    /// Index of the first output row this target covers on the output grid of the whole image.
    /// </summary>
    public int OutputRowStart { get; }

    /// <summary>
    /// Number of pixels in each row of <see cref="Image"/>.
    /// </summary>
    public int OutputWidth => Image.Width;

    /// <summary>
    /// Resampler for the color samples, or null when the pipeline writes the output row itself.
    /// </summary>
    public IRowConverter? ColorConverter { get; }

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
    /// Index of <see cref="CurrentRow"/> on the output grid of the whole image.
    /// </summary>
    public int CurrentOutputRow => OutputRowStart + _outputRowIndex;

    /// <summary>
    /// Moves to the next output row.
    /// </summary>
    public void AdvanceRow() => _outputRowIndex++;
}
