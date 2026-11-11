using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Text;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Records drawing onto a <see cref="PdfCommandRecorder"/> the way it is drawn on a surface, without building the
/// commands.
/// </summary>
public static class PdfCommandRecorderExtensions
{
    /// <summary>
    /// Records saving the current matrix, clip and drawing state, restored by the matching <see cref="Restore"/>.
    /// </summary>
    /// <param name="recorder">The recorder to record onto.</param>
    public static void Save(this PdfCommandRecorder recorder)
    {
        if (recorder == null)
        {
            throw new ArgumentNullException(nameof(recorder));
        }

        recorder.Process(SaveStateCommand.Instance);
    }

    /// <summary>
    /// Records restoring the state of the last <see cref="Save"/>.
    /// </summary>
    /// <param name="recorder">The recorder to record onto.</param>
    public static void Restore(this PdfCommandRecorder recorder)
    {
        if (recorder == null)
        {
            throw new ArgumentNullException(nameof(recorder));
        }

        recorder.Process(RestoreStateCommand.Instance);
    }

    /// <summary>
    /// Records concatenating <paramref name="matrix"/> onto the current matrix.
    /// </summary>
    /// <param name="recorder">The recorder to record onto.</param>
    /// <param name="matrix">The matrix applied before the current one.</param>
    public static void Concat(this PdfCommandRecorder recorder, in PdfMatrix matrix)
    {
        if (recorder == null)
        {
            throw new ArgumentNullException(nameof(recorder));
        }

        recorder.Process(new ConcatMatrixCommand(matrix));
    }

    /// <summary>
    /// Records filling or stroking <paramref name="path"/>, or adding it to the clip, as the style of
    /// <paramref name="paint"/> says.
    /// </summary>
    /// <param name="recorder">The recorder to record onto.</param>
    /// <param name="path">The path, in current user space.</param>
    /// <param name="paint">The paint the path is drawn with.</param>
    public static void DrawPath(this PdfCommandRecorder recorder, PdfPath path, PdfPaint paint)
    {
        if (recorder == null)
        {
            throw new ArgumentNullException(nameof(recorder));
        }

        recorder.Process(new DrawPathCommand(path, paint));
    }

    /// <summary>
    /// Records filling or stroking shaped <paramref name="glyphs"/>, or adding their outlines to the clip, as the style
    /// of <paramref name="paint"/> says.
    /// </summary>
    /// <param name="recorder">The recorder to record onto.</param>
    /// <param name="glyphs">The shaped glyphs, in text space.</param>
    /// <param name="matrix">The matrix from text space to current user space.</param>
    /// <param name="paint">The paint the glyphs are drawn with.</param>
    public static void DrawText(this PdfCommandRecorder recorder, in ReadOnlyMemory<ShapedGlyph> glyphs, in PdfMatrix matrix, PdfPaint paint)
    {
        if (recorder == null)
        {
            throw new ArgumentNullException(nameof(recorder));
        }

        recorder.Process(new DrawShapedTextCommand(matrix, glyphs, paint));
    }
}
