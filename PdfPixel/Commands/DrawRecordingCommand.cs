using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Represents a recorded set of commands replayed under its own matrix, with a specified tint paint.
/// A save/restore boundary surrounds the replay because the nested commands' state (matrix, clip).
/// </summary>
public sealed class DrawRecordingCommand : PdfCommand, IMatrixCommand
{
    /// <summary>
    /// Initializes the command with a recorder, matrix, and an optional tint paint.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, in PdfMatrix matrix, PdfPaint? tintPaint)
    {
        Recorder = recorder;
        Matrix = matrix;
        TintPaint = tintPaint;
    }

    /// <summary>
    /// Initializes the command with a recorder and an optional tint paint; identity matrix.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, PdfPaint? tintPaint)
        : this(recorder, PdfMatrix.Identity, tintPaint)
    {
    }

    /// <summary>
    /// Initializes the command with a recorder; identity matrix, no tint paint applied.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder)
        : this(recorder, PdfMatrix.Identity, null)
    {
    }

    /// <summary>
    /// Recorder used to draw command batch.
    /// </summary>
    public PdfCommandRecorder Recorder { get; }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Tint paint of a current command batch.
    /// </summary>
    public PdfPaint? TintPaint { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => Recorder.Commands.Aggregate(PdfCommandFeatures.None, (acc, cmd) => acc | cmd.Features);

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawRecording;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawRecordingCommand)} {PdfCommandFormatting.FormatMatrix(Matrix)}";
}
