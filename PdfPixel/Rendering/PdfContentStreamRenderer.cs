using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Rendering;

/// <summary>
/// Handles PDF content stream parsing and rendering coordination
/// </summary>
internal class PdfContentStreamRenderer
{
    private readonly IPdfPageInternal _page;
    private readonly ILogger<PdfContentStreamRenderer> _logger;
    private readonly IPdfRenderer _renderer;

    public PdfContentStreamRenderer(IPdfRenderer renderer, IPdfPageInternal page)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _logger = page.Document.LoggerFactory.CreateLogger<PdfContentStreamRenderer>();
    }

    /// <summary>
    /// Render multiple content streams sequentially as one continuous stream without memory allocation.
    /// This treats all content streams as logically one stream while preserving graphics state continuity.
    /// </summary>
    /// <param name="processor">The command processor to emit drawing commands to.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    public void RenderContent(IPdfCommandProcessor processor, PdfRenderingParameters renderingParameters, IPdfExecutionObserver observer)
    {
        List<ReadOnlyMemory<byte>> contentStreams = GetPageContentStreams();

        if (contentStreams.Count == 0)
        {
            return;
        }

        // Create unified context that treats all streams as one continuous stream
        PdfParseContext parseContext = new(contentStreams);

        PdfGraphicsState state = new(_page, new HashSet<uint>(), externalTransform: null, observer, renderingParameters);

        RenderContext(processor, ref parseContext, state);
    }

    private List<ReadOnlyMemory<byte>> GetPageContentStreams()
    {
        List<ReadOnlyMemory<byte>> contentStreams = [];

        List<PdfObject>? contents = _page.PageObject.Dictionary.GetObjects(PdfTokens.ContentsKey);

        if (contents == null)
        {
            return contentStreams;
        }

        foreach (PdfObject contentObject in contents)
        {
            ReadOnlyMemory<byte> contentData = contentObject.DecodeAsMemory();

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
    public void RenderContext(IPdfCommandProcessor processor, ref PdfParseContext parseContext, PdfGraphicsState graphicsState)
    {
        Stack<PdfGraphicsState> graphicsStack = [];
        Stack<IPdfValue> operandStack = [];
        using SKPath currentPath = new();
        PdfOperatorProcessor operatorProcessor = new(_renderer, _page, processor, operandStack, graphicsStack, currentPath);
        PdfParser parser = new(parseContext, _page.Document, allowReferences: false, decrypt: false);
        IPdfValue? value;

        while ((value = parser.ReadNextValue()) != null)
        {
            if (value.Type == PdfValueType.Operator)
            {
                string op = value.AsString().ToString();

                try
                {
                    operatorProcessor.ProcessOperator(op, ref graphicsState);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
#if !DEBUG
#pragma warning disable CA1031
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error \"{Message}\" processing PDF content stream operator {Operator}. Continuing to next.", ex.Message, op);
                }
#pragma warning restore CA1031
#endif

                operandStack.Clear();
            }
            else
            {
                operandStack.Push(value);
            }

            graphicsState.ExecutionObserver?.Notify();
        }

        currentPath.Dispose();
    }
}
