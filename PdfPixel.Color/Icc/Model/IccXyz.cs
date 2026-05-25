namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// Immutable triple of floating-point XYZ tristimulus values used in ICC profile parsing and color conversion.
/// All components are assumed to be referenced to the D50 white point unless otherwise documented.
/// </summary>
public struct IccXyz
{
    /// <summary>
    /// Create a new XYZ value.
    /// </summary>
    /// <param name="x">X component.</param>
    /// <param name="y">Y (luminance) component.</param>
    /// <param name="z">Z component.</param>
    public IccXyz(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// X component.
    /// </summary>
    public readonly float X { get; }

    /// <summary>
    /// Y component (luminance channel).
    /// </summary>
    public readonly float Y { get; }

    /// <summary>
    /// Z component.
    /// </summary>
    public readonly float Z { get; }

    /// <summary>
    /// String representation in (X, Y, Z) format.
    /// </summary>
    public override string ToString() => $"({X}, {Y}, {Z})";
}
