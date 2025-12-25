using PdfReader.Rendering.State;
using PdfReader.Shading.Model;
using SkiaSharp;

namespace PdfReader.Shading;


/// <summary>
/// Provides methods for building SKShader instances from PDF shading models.
/// Supports axial (type 2) and radial (type 3) shadings.
/// </summary>
internal static partial class PdfShadingBuilder
{
    public static SKPicture ToPicture(PdfShading shading, PdfGraphicsState state, SKRect bounds)
    {
        return shading.ShadingType switch
        {
            1 => BuildFunctionBased(shading, state),
            2 => BuildAxial(shading, state, bounds),
            3 => BuildRadial(shading, state, bounds),
            4 or 5 => BuildGouraud(shading, state),
            6 => BuildType6(shading, state),
            7 => BuildType7(shading, state),
            _ => null,
        };
    }
}
