using PdfPixel.Annotations;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// The recorded appearances of an annotation by visual state, replayed in the state the execution context's
/// <see cref="Execution.PdfCommandExecutionContext.AnnotationStates"/> give the annotation, under its matrix.
/// </summary>
public sealed class DrawAnnotationCommand : PdfCommand
{
    /// <summary>
    /// Initializes the command with the recorded appearance of each visual state of <paramref name="annotation"/>.
    /// </summary>
    /// <param name="annotation">The annotation the appearances are recorded for.</param>
    /// <param name="normal">The recorded normal appearance, or <see langword="null"/> when there is none.</param>
    /// <param name="rollover">The recorded rollover appearance, or <see langword="null"/> when there is none.</param>
    /// <param name="down">The recorded down appearance, or <see langword="null"/> when there is none.</param>
    public DrawAnnotationCommand(PdfAnnotation annotation, PdfCommandRecorder? normal, PdfCommandRecorder? rollover, PdfCommandRecorder? down)
    {
        Annotation = annotation ?? throw new ArgumentNullException(nameof(annotation));
        Normal = normal;
        Rollover = rollover;
        Down = down;
    }

    /// <summary>
    /// Gets the annotation the appearances are recorded for.
    /// </summary>
    public PdfAnnotation Annotation { get; }

    /// <summary>
    /// Gets the recorded normal appearance, or <see langword="null"/> when there is none.
    /// </summary>
    public PdfCommandRecorder? Normal { get; }

    /// <summary>
    /// Gets the recorded rollover appearance, or <see langword="null"/> when there is none.
    /// </summary>
    public PdfCommandRecorder? Rollover { get; }

    /// <summary>
    /// Gets the recorded down appearance, or <see langword="null"/> when there is none.
    /// </summary>
    public PdfCommandRecorder? Down { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawAnnotation;

    /// <summary>
    /// Returns the recorded appearance of <paramref name="visualState"/>, falling back to the states recorded, down to
    /// rollover and both to normal, or <see langword="null"/> when none of them is.
    /// </summary>
    public PdfCommandRecorder? GetRecording(PdfAnnotationVisualStateKind visualState)
    {
        return visualState switch
        {
            PdfAnnotationVisualStateKind.Down => Down ?? Rollover ?? Normal,
            PdfAnnotationVisualStateKind.Rollover => Rollover ?? Normal,
            _ => Normal
        };
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawAnnotationCommand)} {Annotation}";
}
