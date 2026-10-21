using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// Exposes the path a command operates on.
/// </summary>
public interface IPathCommand
{
    /// <summary>
    /// Gets the path this command operates on.
    /// </summary>
    PdfPath Path { get; }
}
