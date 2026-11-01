using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Creates the row processing pipelines that produce the tiles of an image and of its mask.
/// </summary>
public interface IPdfImagePipelineFactory
{
    /// <summary>
    /// Creates the pipeline producing the image tile that reads <paramref name="sourceRegion"/> of the decoded samples
    /// and covers <paramref name="outputRegion"/> of the output grid.
    /// </summary>
    /// <param name="parameters">Description of the decoded samples.</param>
    /// <param name="sourceRegion">Region of the decoded samples the tile reads.</param>
    /// <param name="outputRegion">Region of the output grid the tile covers.</param>
    /// <param name="alphaSource">Alpha rows of the image's mask on the tile's output grid, or null when it has none.</param>
    PdfImageRowProcessingPipeline CreateImagePipeline(
        PdfImageRowDecodingParameters parameters,
        in PdfIntegerRectangle sourceRegion,
        in PdfIntegerRectangle outputRegion,
        IPdfAlphaRowSource? alphaSource);

    /// <summary>
    /// Creates the pipeline producing the float alpha of the mask tile that reads <paramref name="sourceRegion"/> of the
    /// mask's decoded samples and covers <paramref name="outputRegion"/> of the output grid.
    /// </summary>
    /// <param name="parameters">Description of the mask's decoded samples.</param>
    /// <param name="sourceRegion">Region of the decoded samples the tile reads.</param>
    /// <param name="outputRegion">Region of the output grid the tile covers.</param>
    PdfImageRowProcessingPipeline CreateMaskPipeline(
        PdfImageRowDecodingParameters parameters,
        in PdfIntegerRectangle sourceRegion,
        in PdfIntegerRectangle outputRegion);
}
