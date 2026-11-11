using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Transparency;

namespace PdfPixel.Rendering;

/// <summary>
/// The implicit non-isolated knockout group of a combined fill and stroke (ISO 32000-2 11.7.4.4).
/// </summary>
internal static class ImplicitKnockoutGroup
{
    private static readonly PdfTransparencyGroupContext Group = new(null, isolated: false, knockout: true);

    private static readonly PdfPaint GroupPaint = new(PdfPaintStyle.Fill);

    /// <summary>
    /// Returns the recorder the group's elements are recorded into, starting with the soft mask of
    /// <paramref name="state"/>.
    /// </summary>
    public static PdfCommandRecorder Begin(PdfGraphicsState state)
    {
        PdfCommandRecorder recorder = new();

        if (state.SoftMask != null)
        {
            recorder.Process(new SetSoftMaskCommand(state.SoftMask));
        }

        return recorder;
    }

    /// <summary>
    /// Paints the recorded group with blend mode Normal, alpha 1 and no soft mask.
    /// </summary>
    public static void Draw(IPdfCommandProcessor processor, PdfCommandRecorder recorder, PdfGraphicsState state)
    {
        PdfGroupSource source = new(new DrawRecordingCommand(recorder), Group, null, null);
        DrawGroupCommand command = new(source, GroupPaint);

        if (state.SoftMask != null)
        {
            processor.Process(SaveStateCommand.Instance);
            processor.Process(ClearSoftMaskCommand.Instance);
            processor.Process(command);
            processor.Process(RestoreStateCommand.Instance);
        }
        else
        {
            processor.Process(command);
        }
    }
}
