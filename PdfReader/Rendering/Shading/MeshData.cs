using SkiaSharp;

namespace PdfReader.Rendering.Shading
{
    /// <summary>
    /// Represents a decoded PDF mesh patch.
    /// Handles Gouraud triangle meshes (Type 4/5), Coons (Type 6), and Tensor-Product (Type 7) mesh types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Points"/> property contains the patch's vertices or control points in user-space coordinates.
    /// For Gouraud triangle meshes (Type 4/5), this will contain three vertices per triangle.
    /// For Coons (Type 6) and Tensor-Product (Type 7) meshes, this will contain all control points for the patch.
    /// The PDF decode array has already been applied; these points are not normalized.
    /// </para>
    /// <para>
    /// The <see cref="CornerColors"/> property contains the colors for the patch corners.
    /// For Gouraud triangle meshes, this will contain three colors (one per vertex).
    /// For Coons and Tensor-Product meshes, this will contain four colors (one per corner).
    /// </para>
    /// <para>
    /// The <see cref="Flag"/> property contains the edge flag for the patch, as decoded from the shading stream.
    /// For Gouraud triangle meshes, this is the edge flag for the triangle's first vertex.
    /// For Coons and Tensor-Product meshes, this may be unused.
    /// </para>
    /// </remarks>
    class MeshData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeshData"/> class.
        /// </summary>
        /// <param name="points">Vertices or control points in user-space coordinates (decode applied).</param>
        /// <param name="cornerInputs">Colors for the patch corners or triangle vertices.</param>
        /// <param name="flag">Edge flag for the patch, as decoded from the shading stream.</param>
        public MeshData(SKPoint[] points, SKColor[] cornerInputs, uint flag)
        {
            Points = points;
            CornerColors = cornerInputs;
            Flag = flag;
        }

        /// <summary>
        /// Gets the vertices or control points for the patch in user-space coordinates.
        /// The PDF decode array has already been applied; these are not normalized.
        /// </summary>
        public SKPoint[] Points { get; }

        /// <summary>
        /// Gets the colors for the patch corners or triangle vertices.
        /// </summary>
        public SKColor[] CornerColors { get; }

        /// <summary>
        /// Gets the edge flag for the patch, as decoded from the shading stream.
        /// </summary>
        public uint Flag { get; }
    }
}