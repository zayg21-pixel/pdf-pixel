using PdfPixel.Commands.Image;
using SkiaSharp;
using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Base for all single-pass image draw commands.
/// Execute is intentionally minimal: update CTM → build shader → apply modifiers → DrawPaint.
/// Save / ConcatMatrix(GetImageMatrix) / Restore are emitted as separate commands by the
/// caller so the compositing frame is explicit and reusable.
/// </summary>
internal abstract class DrawImageCommandBase : PdfCommand
{
    protected readonly ImageDecodingContext Context;

    protected DrawImageCommandBase(ImageDecodingContext context)
    {
        Context = context;
    }

    public override bool IsScaleDependent => true;

    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        var ctm = CommandHelpers.GetScaledMatrix(canvas, executionContext);
        Context.CTM = ctm;

        using var shader = BuildShader(ctm, executionContext.CancellationToken);
        if (shader == null)
        {
            return;
        }

        using var paint = PdfImageCommandUtilities.GetBaseImagePaint(shader, Context);
        CommandHelpers.ApplyModifiers(paint, modifiers);

        canvas.DrawPaint(paint);
    }

    /// <summary>
    /// Decodes required images and returns the composed shader.
    /// Return null to skip drawing (e.g. decode failed).
    /// </summary>
    protected abstract SKShader BuildShader(SKMatrix ctm, CancellationToken cancellationToken);
}
