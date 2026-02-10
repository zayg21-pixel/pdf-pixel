using SkiaSharp;

namespace PdfPixel.Canvas;

public interface ISkSurfaceFactory
{
    SKSurface GetSurface(SKImageInfo imageInfo);
}

public class CpuSkSurfaceFactory : ISkSurfaceFactory
{
    public SKSurface GetSurface(SKImageInfo imageInfo)
    {
        return SKSurface.Create(imageInfo);
    }
}
