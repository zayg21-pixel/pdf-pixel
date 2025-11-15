using PdfReader.Shading.Model;
using SkiaSharp;

namespace PdfReader.Shading;


/// <summary>
/// Provides methods for building SKShader instances from PDF shading models.
/// Supports axial (type 2) and radial (type 3) shadings.
/// </summary>
internal static partial class PdfShadingBuilder
{
    /// <summary>
    /// Creates an SKShader for the given shading model.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <returns>SKShader instance or null if shading type is unsupported or data is invalid.</returns>
    public static SKShader ToShader(PdfShading shading)
    {
        if (shading == null)
        {
            return null;
        }

        return shading.ShadingType switch
        {
            1 => BuildFunctionBased(shading),
            2 => BuildAxial(shading),
            3 => BuildRadial(shading),
            4 or 5 => BuildGouraud(shading),
            6 => BuildType6(shading),
            7 => BuildType7(shading),
            _ => null,
        };
    }
}
