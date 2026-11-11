namespace PdfPixel.Commands;

/// <summary>
/// Identifies the concrete type of an <see cref="IPdfCommand"/>, letting a processor dispatch
/// on <see cref="IPdfCommand.Kind"/> instead of pattern-matching the command's type.
/// </summary>
public enum PdfCommandKind
{
    /// <summary>
    /// <see cref="BeginScopeCommand"/>.
    /// </summary>
    BeginScope,

    /// <summary>
    /// <see cref="BeginTextObjectCommand"/>.
    /// </summary>
    BeginTextObject,

    /// <summary>
    /// <see cref="ClearSoftMaskCommand"/>.
    /// </summary>
    ClearSoftMask,

    /// <summary>
    /// <see cref="ConcatMatrixCommand"/>.
    /// </summary>
    ConcatMatrix,

    /// <summary>
    /// <see cref="PdfContentCommand{T}"/>.
    /// </summary>
    Content,

    /// <summary>
    /// <see cref="DrawAnnotationCommand"/>.
    /// </summary>
    DrawAnnotation,

    /// <summary>
    /// <see cref="DrawGroupCommand"/>.
    /// </summary>
    DrawGroup,

    /// <summary>
    /// <see cref="DrawImageCommand"/>.
    /// </summary>
    DrawImage,

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
    /// <see cref="EndScopeCommand"/>.
    /// </summary>
    EndScope,

    /// <summary>
    /// <see cref="EndTextObjectCommand"/>.
    /// </summary>
    EndTextObject,

    /// <summary>
    /// <see cref="RestoreStateCommand"/>.
    /// </summary>
    RestoreState,

    /// <summary>
    /// <see cref="SaveStateCommand"/>.
    /// </summary>
    SaveState,

    /// <summary>
    /// <see cref="SetSoftMaskCommand"/>.
    /// </summary>
    SetSoftMask
}
