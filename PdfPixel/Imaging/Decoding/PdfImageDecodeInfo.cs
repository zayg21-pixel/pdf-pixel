using PdfPixel.Imaging.Processing;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// The tiles one <see cref="IPdfImageDecoder.SelectTiles"/> selected: the samples and the alpha they carry.
/// </summary>
public sealed class PdfImageDecodeInfo
{
    /// <summary>
    /// Initializes the selection of <paramref name="samples"/> and, when they carry any, <paramref name="alpha"/>.
    /// </summary>
    public PdfImageDecodeInfo(PdfImageSampleInfo samples, PdfImageSampleInfo? alpha)
    {
        Samples = samples;
        Alpha = alpha;
    }

    /// <summary>
    /// Gets the selected color samples.
    /// </summary>
    public PdfImageSampleInfo Samples { get; }

    /// <summary>
    /// Gets the alpha at the same resolution, or <see langword="null"/> when the samples carry none.
    /// </summary>
    public PdfImageSampleInfo? Alpha { get; }
}
