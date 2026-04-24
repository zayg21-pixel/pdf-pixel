using SkiaSharp;
using System;

namespace PdfPixel.Commands;

// TODO: [HIGH] we need this
///// <summary>
///// Bridge adapter that wraps an <see cref="SKCanvas"/> and immediately executes commands against it.
///// Used at system boundaries (e.g. <see cref="PdfPixel.Rendering.PdfContentStreamRenderer"/>)
///// where a real canvas is available, enabling the rest of the pipeline to work with
///// <see cref="IPdfCommandProcessor"/> uniformly.
///// </summary>
//public sealed class SkCanvasCommandProcessor : IPdfCommandProcessor
//{
//    private readonly PdfCommandExecutionContext _executionContext;

//    /// <summary>
//    /// Gets the underlying <see cref="SKCanvas"/> for sub-renderers that still need direct canvas access
//    /// during the incremental migration. This property should be used sparingly and only where
//    /// deeper layers have not yet been converted to emit commands.
//    /// </summary>
//    public SKCanvas Canvas { get; }

//    public SkCanvasCommandProcessor(SKCanvas canvas, PdfCommandExecutionContext executionContext)
//    {
//        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
//        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
//    }

//    /// <inheritdoc />
//    public SKMatrix TotalMatrix => Canvas.TotalMatrix;

//    /// <inheritdoc />
//    public void Process(IPdfCommand command)
//    {
//        command.Execute(Canvas, Array.Empty<IPdfCommandModifier>(), _executionContext);
//        command.Dispose();
//    }

//    public void Dispose()
//    {
//        // Does not own the canvas; caller manages its lifetime.
//    }
//}
