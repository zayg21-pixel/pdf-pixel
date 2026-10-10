using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Streams;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering;

/// <summary>
/// Handles PDF content stream parsing and rendering coordination
/// </summary>
internal class PdfContentStreamRenderer
{
    private readonly IPdfContentHost _host;
    private readonly ILogger<PdfContentStreamRenderer> _logger;
    private readonly IPdfRenderer _renderer;

    public PdfContentStreamRenderer(IPdfRenderer renderer, IPdfContentHost host)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _logger = host.Document.LoggerFactory.CreateLogger<PdfContentStreamRenderer>();
    }

    /// <summary>
    /// Render multiple content streams sequentially as one continuous stream without memory allocation.
    /// This treats all content streams as logically one stream while preserving graphics state continuity.
    /// </summary>
    /// <param name="processor">The command processor to emit drawing commands to.</param>
    /// <param name="pageContentStreams">Stream sources named by the page's /Contents entry, in reading order.</param>
    /// <param name="clipBounds">Initial clip bounds of the graphics state.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    public void RenderContent(
        IPdfCommandProcessor processor,
        IReadOnlyList<PdfObjectStream> pageContentStreams,
        in PdfRectangle clipBounds,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        List<ReadOnlyMemory<byte>> contentStreams = GetPageContentStreams(pageContentStreams);

        if (contentStreams.Count == 0)
        {
            return;
        }

        // Create unified context that treats all streams as one continuous stream
        PdfParseContext parseContext = new(contentStreams);

        PdfGraphicsState state = new(_host, clipBounds, new HashSet<uint>(), observer, renderingParameters);

        RenderContext(processor, ref parseContext, state);
    }

    private static List<ReadOnlyMemory<byte>> GetPageContentStreams(IReadOnlyList<PdfObjectStream> pageContentStreams)
    {
        List<ReadOnlyMemory<byte>> contentStreams = [];

        foreach (PdfObjectStream contentStream in pageContentStreams)
        {
            ReadOnlyMemory<byte> contentData = contentStream.DecodeAsMemory();

            if (!contentData.IsEmpty)
            {
                contentStreams.Add(contentData);
            }
        }

        return contentStreams;
    }

    /// <summary>
    /// Renders a content stream using the given command processor with stack-based approach.
    /// Includes XObject recursion tracking to prevent infinite loops.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void RenderContext(IPdfCommandProcessor processor, ref PdfParseContext parseContext, PdfGraphicsState graphicsState)
    {
        Stack<PdfGraphicsState> graphicsStack = [];
        Stack<IPdfValue> operandStack = [];
        PdfPathBuilder currentPath = new();
        PdfOperatorProcessor operatorProcessor = new(_renderer, _host, processor, operandStack, graphicsStack, currentPath);
        PdfParser parser = new(parseContext, _host.Document, allowReferences: false, decrypt: false);
        IPdfValue? value;

        while ((value = parser.ReadNextValue(operandStack)) != null)
        {
            PdfString? operatorName = (value.Type == PdfValueType.Operator) ? value.AsString() : null;

            if (operatorName != null)
            {
                string op = operatorName.Value.ToString();

                try
                {
                    operatorProcessor.ProcessOperator(op, ref graphicsState);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error \"{Message}\" processing PDF content stream operator {Operator}. Continuing to next.", ex.Message, op);
                }
#pragma warning restore CA1031

                operandStack.Clear();
            }
            else
            {
                operandStack.Push(value);
            }

            graphicsState.ExecutionObserver?.Notify();
        }
    }
}
