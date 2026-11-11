using PdfPixel.Annotations;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Annotations;
using System;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// One square tile of a page on the tile grid of a <see cref="PdfPageTiles"/>, empty until it is rasterized.
/// </summary>
internal sealed class PdfPageTile
{
    /// <summary>
    /// The paint tiles are drawn with: opaque DeviceRGB, the color space of the surface they are rasterized on.
    /// </summary>
    public static readonly PdfPaint TilePaint =new PdfPaint(PdfPaintStyle.Fill).WithColorSpace(PdfDeviceRgbColorSpaceConverter.Instance, PdfDeviceColorMapping.None);

    /// <summary>
    /// Initializes an empty tile holding <paramref name="samples"/> that covers <paramref name="destination"/>.
    /// </summary>
    public PdfPageTile(PdfImageSamples samples, in PdfRectangle destination)
    {
        Samples = samples ?? throw new ArgumentNullException(nameof(samples));
        Decoder = new PdfImageSamplesDecoder(samples, TilePaint, interpolate: true);
        Destination = destination;
    }

    /// <summary>
    /// Gets the pixels of the tile.
    /// </summary>
    public PdfImageSamples Samples { get; }

    /// <summary>
    /// Gets the decoder of the tile's pixels.
    /// </summary>
    public PdfImageSamplesDecoder Decoder { get; }

    /// <summary>
    /// Gets the part of the page the whole tile covers, in content coordinates; it reaches past the page at its edges.
    /// </summary>
    public PdfRectangle Destination { get; }

    /// <summary>
    /// Gets whether the tile's pixels were rasterized.
    /// </summary>
    public bool IsRasterized => Antialias != null;

    /// <summary>
    /// Gets whether the tile was rasterized with antialiasing, or <see langword="null"/> while it is empty.
    /// </summary>
    public bool? Antialias { get; private set; }

    /// <summary>
    /// Gets the optional content states the tile was rasterized with, or <see langword="null"/> while it is empty.
    /// </summary>
    public PdfOptionalContentStates? OptionalContentStates { get; private set; }

    /// <summary>
    /// Gets the states the annotations of the tile were rasterized in, or <see langword="null"/> while it is empty.
    /// </summary>
    public PdfAnnotationStates? AnnotationStates { get; private set; }

    /// <summary>
    /// Returns whether the tile must be rasterized for <paramref name="antialias"/>,
    /// <paramref name="optionalContentStates"/> and <paramref name="annotationStates"/>: it is empty, was rasterized
    /// with another antialiasing or other optional content states, or covers an annotation of
    /// <paramref name="page"/> whose state differs.
    /// </summary>
    public bool IsOutOfDate(bool antialias, PdfOptionalContentStates optionalContentStates, PdfAnnotationStates annotationStates, PdfPanelPage page)
    {
        if (Antialias != antialias || OptionalContentStates != optionalContentStates || AnnotationStates == null)
        {
            return true;
        }

        foreach (PdfRectangle region in page.GetChangedAnnotationRegions(AnnotationStates, annotationStates))
        {
            PdfRectangle overlap = PdfRectangle.Intersect(Destination, region);

            if (overlap.Width > 0 && overlap.Height > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Records that the tile's pixels were rasterized with <paramref name="antialias"/>,
    /// <paramref name="optionalContentStates"/> and <paramref name="annotationStates"/>.
    /// </summary>
    public void SetRasterized(bool antialias, PdfOptionalContentStates optionalContentStates, PdfAnnotationStates annotationStates)
    {
        Antialias = antialias;
        OptionalContentStates = optionalContentStates ?? throw new ArgumentNullException(nameof(optionalContentStates));
        AnnotationStates = annotationStates ?? throw new ArgumentNullException(nameof(annotationStates));
    }
}
