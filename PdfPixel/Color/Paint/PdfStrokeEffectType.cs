namespace PdfPixel.Color.Paint;

/// <summary>
/// Border effect applied to a stroked paint.
/// </summary>
public enum PdfStrokeEffectType
{
    /// <summary>
    /// No effect — border is drawn normally.
    /// </summary>
    Solid,

    // TODO: [MEDIUM] Cloudy is ignored: PdfStrokeStyle carries EffectType and EffectIntensity, but stroke-to-fill
    // builds the outline from the plain path. Used by annotation borders (/BE) only. See AnnotationFallbackTest.pdf.
    /// <summary>
    /// Cloudy effect — border appears soft and bumpy.
    /// </summary>
    Cloudy
}
