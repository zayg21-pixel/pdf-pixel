using PdfPixel.Color;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// The color stops of the page shadow over one background color: the outside of a Gaussian-blurred page, its strength
/// halved at the page edge and faded out at <see cref="Extent"/>, blended over the background so every stop is opaque.
/// </summary>
internal sealed class PageShadow
{
    /// <summary>
    /// Distance, in panel pixels, from the page edge at which the shadow has faded out.
    /// </summary>
    public const float Extent = 3f * Sigma;

    // Standard deviation, in panel pixels, of the blur the shadow follows.
    private const float Sigma = 3f;

    private const int StopCount = 10;

    // Coefficients of the complementary error function approximation (Abramowitz and Stegun 7.1.26).
    private const float ErfcP = 0.3275911f;
    private const float ErfcA1 = 0.254829592f;
    private const float ErfcA2 = -0.284496736f;
    private const float ErfcA3 = 1.421413741f;
    private const float ErfcA4 = -1.453152027f;
    private const float ErfcA5 = 1.061405429f;

    private static readonly PdfRgbaColor ShadowColor = new(128f / 255f, 128f / 255f, 128f / 255f, 160f / 255f);

    /// <summary>
    /// Initializes the stops of the shadow over <paramref name="background"/>.
    /// </summary>
    public PageShadow(in PdfRgbaColor background)
    {
        Background = background;
        Colors = new PdfColor[StopCount];
        Positions = new float[StopCount];
        float lastStop = StopCount - 1;

        for (int stop = 0; stop < StopCount; stop++)
        {
            float position = stop / lastStop;
            float strength = 0f;

            if (stop < StopCount - 1)
            {
                strength = ShadowColor.Alpha * 0.5f * Erfc(position * Extent / (Sigma * MathF.Sqrt(2f)));
            }

            Positions[stop] = position;
            Colors[stop] = new PdfColor(
            [
                Blend(background.Red, ShadowColor.Red, strength),
                Blend(background.Green, ShadowColor.Green, strength),
                Blend(background.Blue, ShadowColor.Blue, strength)
            ]);
        }
    }

    /// <summary>
    /// Gets the background color the stops are blended over.
    /// </summary>
    public PdfRgbaColor Background { get; }

    /// <summary>
    /// Gets the DeviceRGB color stops, one per entry in <see cref="Positions"/>.
    /// </summary>
    public PdfColor[] Colors { get; }

    /// <summary>
    /// Gets the stop positions from the page edge (0) to <see cref="Extent"/> (1).
    /// </summary>
    public float[] Positions { get; }

    private static float Blend(float background, float shadow, float strength) => background + ((shadow - background) * strength);

    /// <summary>
    /// Returns the complementary error function of a non-negative <paramref name="value"/>.
    /// </summary>
    private static float Erfc(float value)
    {
        float term = 1f / (1f + (ErfcP * value));
        float polynomial = term * (ErfcA1 + (term * (ErfcA2 + (term * (ErfcA3 + (term * (ErfcA4 + (term * ErfcA5))))))));

        return polynomial * MathF.Exp(-value * value);
    }
}
