namespace PdfReader.Color.Icc.Model;

/// <summary>
/// Immutable triple of floating-point XYZ tristimulus values used in ICC profile parsing and color conversion.
/// All components are assumed to be referenced to the D50 white point unless otherwise documented.
/// </summary>
internal struct IccXyz
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
    public readonly float X;

    /// <summary>
    /// Y component (luminance channel).
    /// </summary>
    public readonly float Y;

    /// <summary>
    /// Z component.
    /// </summary>
    public readonly float Z;

    /// <summary>
    /// String representation in (X, Y, Z) format.
    /// </summary>
    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}
