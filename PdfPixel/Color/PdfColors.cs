namespace PdfPixel.Color;

/// <summary>
/// Well-known <see cref="PdfColor"/> values.
/// </summary>
public static class PdfColors
{
    /// <summary>
    /// Opaque black.
    /// </summary>
    public static PdfColor Black { get; } = new(0f, 0f, 0f);

    /// <summary>
    /// Opaque white.
    /// </summary>
    public static PdfColor White { get; } = new(1f, 1f, 1f);

    /// <summary>
    /// Fully transparent black.
    /// </summary>
    public static PdfColor Transparent { get; } = new(0f, 0f, 0f, 0f);

    /// <summary>
    /// Opaque red.
    /// </summary>
    public static PdfColor Red { get; } = new(1f, 0f, 0f);

    /// <summary>
    /// Opaque dark blue.
    /// </summary>
    public static PdfColor DarkBlue { get; } = new(0f, 0f, 139f / 255f);

    /// <summary>
    /// Opaque yellow.
    /// </summary>
    public static PdfColor Yellow { get; } = new(1f, 1f, 0f);

    /// <summary>
    /// Opaque light gray.
    /// </summary>
    public static PdfColor LightGray { get; } = new(211f / 255f, 211f / 255f, 211f / 255f);
}
