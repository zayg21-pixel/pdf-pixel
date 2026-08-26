using PdfPixel.Geometry;
using PdfPixel.Models;
using System;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Extensions for <see cref="IPdfCommandProcessor"/>.
/// </summary>
public static class PdfCommandProcessorExtensions
{
    /// <summary>
    /// Maps the page's crop box to a top-left origin, rotated by the page's own rotation plus
    /// <paramref name="userRotation"/>. Clips to the box unless <paramref name="clipToBounds"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="processor">The processor to submit the commands to.</param>
    /// <param name="page">The page to transform to.</param>
    /// <param name="userRotation">Clockwise rotation in degrees; must be a multiple of 90.</param>
    /// <param name="scale">Uniform scale applied to the mapped box.</param>
    /// <param name="clipToBounds">Whether to clip to the crop box.</param>
    public static void ApplyPageTransformations(this IPdfCommandProcessor processor, IPdfPage page, int userRotation = 0, float scale = 1f, bool clipToBounds = true)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        PdfRotationUtilities.Validate(userRotation, nameof(userRotation));

        // A page whose /Rotate is not a quarter turn is displayed unrotated.
        int pageRotation = (page.Rotation % 90 == 0) ? page.Rotation : 0;

        ApplyPageTransformations(processor, page.CropBox, pageRotation + userRotation, scale, clipToBounds);
    }

    /// <summary>
    /// Maps <paramref name="pageBox"/> to a top-left origin, rotated by <paramref name="rotation"/>.
    /// Clips to the box unless <paramref name="clipToBounds"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="processor">The processor to submit the commands to.</param>
    /// <param name="pageBox">The page box to map, in PDF coordinates.</param>
    /// <param name="rotation">Clockwise rotation in degrees; must be a multiple of 90.</param>
    /// <param name="scale">Uniform scale applied to the mapped box.</param>
    /// <param name="clipToBounds">Whether to clip to <paramref name="pageBox"/>.</param>
    public static void ApplyPageTransformations(this IPdfCommandProcessor processor, in PdfRectangle pageBox, int rotation = 0, float scale = 1f, bool clipToBounds = true)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        PdfRotationUtilities.Validate(rotation, nameof(rotation));

        if (clipToBounds)
        {
            PdfSize clipSize = pageBox.GetTransformedSize(rotation, scale);

            processor.Process(new ClipRectangleCommand(
                new PdfRectangle(0, 0, clipSize.Width, clipSize.Height),
                PdfClipOperation.Intersect));
        }

        PdfMatrix pageToDevice = PdfMatrix.CreateScaleTranslation(1, -1, -pageBox.Left, pageBox.Bottom);
        PdfMatrix rotationMatrix = CreateRotationMatrix(pageBox.Width, pageBox.Height, PdfRotationUtilities.Normalize(rotation));
        PdfMatrix scaleMatrix = PdfMatrix.CreateScale(scale, scale);

        processor.Process(new ConcatMatrixCommand(pageToDevice.PostConcat(rotationMatrix).PostConcat(scaleMatrix)));
    }

    private static PdfMatrix CreateRotationMatrix(float width, float height, int rotation)
    {
        return rotation switch
        {
            90 => PdfMatrix.CreateRotationDegrees(90).PostConcat(PdfMatrix.CreateTranslation(height, 0)),
            180 => PdfMatrix.CreateRotationDegrees(180).PostConcat(PdfMatrix.CreateTranslation(width, height)),
            270 => PdfMatrix.CreateRotationDegrees(270).PostConcat(PdfMatrix.CreateTranslation(0, width)),
            _ => PdfMatrix.Identity
        };
    }
}
