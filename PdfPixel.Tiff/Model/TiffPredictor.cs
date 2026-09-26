namespace PdfPixel.Tiff.Model;

/// <summary>
/// Prediction applied to the samples before compression (tag 317).
/// </summary>
public enum TiffPredictor
{
    /// <summary>
    /// Predictor not determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// No prediction.
    /// </summary>
    NoPrediction = 1,

    /// <summary>
    /// Horizontal differencing of integer samples.
    /// </summary>
    Horizontal = 2,

    /// <summary>
    /// Horizontal differencing of byte-shuffled floating-point samples.
    /// </summary>
    FloatingPoint = 3
}
