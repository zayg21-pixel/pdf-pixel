using PdfPixel.Geometry;
using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Represents a recorded set of commands replayed under its own matrix, with a specified paint modifier.
/// A save/restore boundary surrounds the replay because the nested commands' state (matrix, clip).
/// </summary>
public sealed class DrawRecordingCommand : PdfCommand, IMatrixCommand
{
    /// <summary>
    /// Initializes the command with a recorder, matrix, and an optional paint modifier.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, in PdfMatrix matrix, UncoloredPaintModifier? modifier)
    {
        Recorder = recorder;
        Matrix = matrix;
        Modifier = modifier;
    }

    /// <summary>
    /// Initializes the command with a recorder and an optional paint modifier; identity matrix.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, UncoloredPaintModifier? modifier)
        : this(recorder, PdfMatrix.Identity, modifier)
    {
    }

    /// <summary>
    /// Initializes the command with a recorder; identity matrix, no paint modifier applied.
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
    /// Modifier of a current command batch.
    /// </summary>
    public UncoloredPaintModifier? Modifier { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => Recorder.Commands.Aggregate(PdfCommandFeatures.None, (acc, cmd) => acc | cmd.Features);

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawRecording;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawRecordingCommand)} {CommandHelpers.FormatMatrix(Matrix)}";
}
