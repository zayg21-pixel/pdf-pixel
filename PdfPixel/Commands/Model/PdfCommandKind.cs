using PdfPixel.Commands.Model;

namespace PdfPixel.Commands;

/// <summary>
/// Identifies the concrete type of an <see cref="IPdfCommand"/>, letting a processor dispatch
/// on <see cref="IPdfCommand.Kind"/> instead of pattern-matching the command's type.
/// </summary>
public enum PdfCommandKind
{
    /// <summary>
    /// <see cref="BeginMarkedContentCommand"/>.
    /// </summary>
    BeginMarkedContent,

    /// <summary>
    /// <see cref="ClipPathCommand"/>.
    /// </summary>
    ClipPath,

    /// <summary>
    /// <see cref="ClipRectangleCommand"/>.
    /// </summary>
    ClipRectangle,

    /// <summary>
    /// <see cref="ConcatMatrixCommand"/>.
    /// </summary>
    ConcatMatrix,

    /// <summary>
    /// <see cref="DrawNormalImageTileCommand"/>.
    /// </summary>
    DrawNormalImageTile,

    /// <summary>
    /// <see cref="DrawPathCommand"/>.
    /// </summary>
    DrawPath,

    /// <summary>
    /// <see cref="DrawRecordingCommand"/>.
    /// </summary>
    DrawRecording,

    /// <summary>
    /// <see cref="DrawShadingCommand"/>.
    /// </summary>
    DrawShading,

    /// <summary>
    /// <see cref="DrawShapedTextCommand"/>.
    /// </summary>
    DrawShapedText,

    /// <summary>
    /// <see cref="DrawTilingCommand"/>.
    /// </summary>
    DrawTiling,

    /// <summary>
    /// <see cref="EndMarkedContentCommand"/>.
    /// </summary>
    EndMarkedContent,

    /// <summary>
    /// <see cref="InitializeTileCacheCommand"/>.
    /// </summary>
    InitializeTileCache,

    /// <summary>
    /// <see cref="RestoreLayerCommand"/>.
    /// </summary>
    RestoreLayer,

    /// <summary>
    /// <see cref="RestoreStateCommand"/>.
    /// </summary>
    RestoreState,

    /// <summary>
    /// <see cref="SaveLayerCommand"/>.
    /// </summary>
    SaveLayer,

    /// <summary>
    /// <see cref="SaveStateCommand"/>.
    /// </summary>
    SaveState,

    /// <summary>
    /// <see cref="TextCharactersCommand"/>.
    /// </summary>
    TextCharacters
}
