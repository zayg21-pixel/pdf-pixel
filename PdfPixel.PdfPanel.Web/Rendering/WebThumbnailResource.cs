using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Thumbnails;
using PdfPixel.PdfPanel.Web.Emscripten;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// The pixels of a page thumbnail as a canvas in the JS canvas store.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebThumbnailResource : IPdfPanelThumbnailResource
{
    private const int RgbBytesPerPixel = 3;
    private const int RgbaBytesPerPixel = 4;
    private const byte OpaqueAlpha = 255;

    /// <summary>
    /// Creates the canvas of the thumbnail and puts <paramref name="samples"/>, opaque 8-bit RGB, on it.
    /// </summary>
    public WebThumbnailResource(PdfImageSamples samples)
    {
        Id = WebCanvasIds.Next();
        EmscriptenInterop.CreateCanvas(Id, samples.Width, samples.Height);

        var rgba = new byte[samples.Width * samples.Height * RgbaBytesPerPixel];

        for (int row = 0; row < samples.Height; row++)
        {
            ReadOnlySpan<byte> source = samples.GetRow(row);
            Span<byte> target = rgba.AsSpan(row * samples.Width * RgbaBytesPerPixel, samples.Width * RgbaBytesPerPixel);

            for (int column = 0; column < samples.Width; column++)
            {
                target[(column * RgbaBytesPerPixel) + 0] = source[(column * RgbBytesPerPixel) + 0];
                target[(column * RgbaBytesPerPixel) + 1] = source[(column * RgbBytesPerPixel) + 1];
                target[(column * RgbaBytesPerPixel) + 2] = source[(column * RgbBytesPerPixel) + 2];
                target[(column * RgbaBytesPerPixel) + 3] = OpaqueAlpha;
            }
        }

        GCHandle pixels = GCHandle.Alloc(rgba, GCHandleType.Pinned);

        try
        {
            EmscriptenInterop.WriteCanvasRgba(Id, pixels.AddrOfPinnedObject(), samples.Width, samples.Height);
        }
        finally
        {
            pixels.Free();
        }
    }

    /// <summary>
    /// Gets the id of the thumbnail's canvas.
    /// </summary>
    public int Id { get; }

    /// <inheritdoc />
    public void Dispose() => EmscriptenInterop.DeleteCanvas(Id);
}
