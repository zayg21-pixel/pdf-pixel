using PdfPixel.Color;
using PdfPixel.Geometry;
using System.Linq;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Represents a recorded set of commands replayed under its own matrix, with a specified tint color.
/// A save/restore boundary surrounds the replay because the nested commands' state (matrix, clip).
/// </summary>
public sealed class DrawRecordingCommand : PdfCommand, IMatrixCommand
{
    /// <summary>
    /// Initializes the command with a recorder, matrix, and an optional tint color.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, in PdfMatrix matrix, PdfColor? tintColor)
    {
        Recorder = recorder;
        Matrix = matrix;
        TintColor = tintColor;
    }

    /// <summary>
    /// Initializes the command with a recorder and an optional tint color; identity matrix.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, PdfColor? tintColor)
        : this(recorder, PdfMatrix.Identity, tintColor)
    {
    }

    /// <summary>
    /// Initializes the command with a recorder; identity matrix, no tint color applied.
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
    /// Tint color of a current command batch.
    /// </summary>
    public PdfColor? TintColor { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => Recorder.Commands.Aggregate(PdfCommandFeatures.None, (acc, cmd) => acc | cmd.Features);

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawRecording;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawRecordingCommand)} {PdfCommandFormatting.FormatMatrix(Matrix)}";
}
