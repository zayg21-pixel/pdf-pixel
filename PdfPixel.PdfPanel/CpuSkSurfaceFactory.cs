using SkiaSharp;

namespace PdfPixel.PdfPanel;

public class CpuSkSurfaceFactory : ISkSurfaceFactory
{
    public SKSurface GetSurface(SKImageInfo imageInfo)
    {
        return SKSurface.Create(imageInfo);
    }
}
