using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Rendering;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf.Drawing;

/// <summary>
/// The pixels of a content tile as a premultiplied BGRA bitmap.
/// </summary>
internal sealed class WpfTileResource : IPdfPanelTileResource
{
    private const double BitmapDpi = 96;

    public WpfTileResource(int tileSize) => Bitmap = new WriteableBitmap(tileSize, tileSize, BitmapDpi, BitmapDpi, PixelFormats.Pbgra32, null);

    /// <summary>
    /// Gets the bitmap of the tile.
    /// </summary>
    public WriteableBitmap Bitmap { get; }

    /// <inheritdoc />
    public void Write(PdfImageSamples samples)
    {
        GCHandle pixels = samples.PinBuffer();

        try
        {
            Bitmap.WritePixels(new Int32Rect(0, 0, samples.Width, samples.Height), pixels.AddrOfPinnedObject(), samples.RowBytes * samples.Height, samples.RowBytes);
        }
        finally
        {
            pixels.Free();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
