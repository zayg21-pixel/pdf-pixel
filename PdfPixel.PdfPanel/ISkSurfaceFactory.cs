using SkiaSharp;

namespace PdfPixel.PdfPanel;

public interface ISkSurfaceFactory
{
    SKSurface GetSurface(SKImageInfo imageInfo);
}
