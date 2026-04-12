namespace PdfPixel.Shading.Model;

/// <summary>
/// Identifies the type of a PDF shading dictionary (ISO 32000-2, Table 78).
/// </summary>
public enum PdfShadingType
{
    /// <summary>
    /// Unknown or unrecognised shading type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Function-based shading (Type 1).
    /// </summary>
    FunctionBased = 1,

    /// <summary>
    /// Axial shading (Type 2) — linear gradient between two points.
    /// </summary>
    Axial = 2,

    /// <summary>
    /// Radial shading (Type 3) — gradient between two circles.
    /// </summary>
    Radial = 3,

    /// <summary>
    /// Free-form Gouraud-shaded triangle mesh (Type 4).
    /// </summary>
    FreeFormGouraud = 4,

    /// <summary>
    /// Lattice-form Gouraud-shaded triangle mesh (Type 5).
    /// </summary>
    LatticeFormGouraud = 5,

    /// <summary>
    /// Coons patch mesh (Type 6).
    /// </summary>
    CoonsPatchMesh = 6,

    /// <summary>
    /// Tensor-product patch mesh (Type 7).
    /// </summary>
    TensorProductPatchMesh = 7
}
