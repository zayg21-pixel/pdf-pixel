using PdfReader.Shading.Model;
using SkiaSharp;

namespace PdfReader.Shading;


/// <summary>
/// Provides methods for building SKShader instances from PDF shading models.
/// Supports axial (type 2) and radial (type 3) shadings.
/// </summary>
internal static partial class PdfShadingBuilder
{
    public static SKPicture ToPicture(PdfShading shading, SKRect bounds)
    {
        return shading.ShadingType switch
        {
            1 => BuildFunctionBased(shading),
            2 => BuildAxial(shading, bounds),
            3 => BuildRadial(shading, bounds),
            4 or 5 => BuildGouraud(shading),
            6 => BuildType6(shading),
            7 => BuildType7(shading),
            _ => null,
        };
    }
}
