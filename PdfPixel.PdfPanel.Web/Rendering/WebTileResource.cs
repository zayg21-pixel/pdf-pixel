using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Web.Emscripten;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// The pixels of a content tile as a canvas in the JS canvas store.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WebTileResource : IPdfPanelTileResource
{
    public WebTileResource(int tileSize)
    {
        Id = WebCanvasIds.Next();
        EmscriptenInterop.CreateCanvas(Id, tileSize, tileSize);
    }

    /// <summary>
    /// Gets the id of the tile's canvas.
    /// </summary>
    public int Id { get; }

    /// <inheritdoc />
    public void Write(PdfImageSamples samples)
    {
        GCHandle pixels = samples.PinBuffer();

        try
        {
            EmscriptenInterop.WriteCanvasRgba(Id, pixels.AddrOfPinnedObject(), samples.Width, samples.Height);
        }
        finally
        {
            pixels.Free();
        }
    }

    /// <inheritdoc />
    public void Dispose() => EmscriptenInterop.DeleteCanvas(Id);
}
