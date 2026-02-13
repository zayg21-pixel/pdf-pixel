using Microsoft.Extensions.Logging;
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
public class PdfContentStreamRenderer
{
    private readonly PdfPage _page;
    private readonly ILogger<PdfContentStreamRenderer> _logger;
    private readonly IPdfRenderer _renderer;

    public PdfContentStreamRenderer(IPdfRenderer renderer, PdfPage page)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _logger = page.Document.LoggerFactory.CreateLogger<PdfContentStreamRenderer>();
    }

    /// <summary>
    /// Render multiple content streams sequentially as one continuous stream without memory allocation.
    /// This treats all content streams as logically one stream while preserving graphics state continuity.
    /// </summary>
    public void RenderContent(SKCanvas canvas, PdfRenderingParameters renderingParameters)
    {
        var contentStreams = GetPageContentStreams();

        if (contentStreams.Count == 0)
            return;

        // Create unified context that treats all streams as one continuous stream
        var parseContext = new PdfParseContext(contentStreams);

        var state = new PdfGraphicsState(_page, new HashSet<uint>(), renderingParameters, externalTransform: null);
        state.DeviceMatrix = canvas.TotalMatrix;

        RenderContext(canvas, ref parseContext, state);
    }

    private List<ReadOnlyMemory<byte>> GetPageContentStreams()
    {
        var contentStreams = new List<ReadOnlyMemory<byte>>();

        var contents = _page.PageObject.Dictionary.GetObjects(PdfTokens.ContentsKey);

        if (contents == null)
        {
            return contentStreams;
        }

        foreach (var contentObject in contents)
        {
            var contentData = contentObject.DecodeAsMemory();

            if (!contentData.IsEmpty)
            {
                contentStreams.Add(contentData);
            }
        }

        return contentStreams;
    }

    /// <summary>
    /// Renders a content stream directly to canvas using stack-based approach with PdfParsers.
    /// Includes XObject recursion tracking to prevent infinite loops.
    /// </summary>
    public void RenderContext(SKCanvas canvas, ref PdfParseContext parseContext, PdfGraphicsState graphicsState)
    {
        var graphicsStack = new Stack<PdfGraphicsState>();
        var operandStack = new Stack<IPdfValue>();
        using var currentPath = new SKPath();
        var operatorProcessor = new PdfOperatorProcessor(_renderer, _page, canvas, operandStack, graphicsStack, currentPath);
        var parser = new PdfParser(parseContext, _page.Document, allowReferences: false, decrypt: false);
        IPdfValue value;

        while ((value = parser.ReadNextValue()) != null)
        {
            if (value.Type == PdfValueType.Operator)
            {
                string op = value.AsString().ToString();

                try
                {
                    operatorProcessor.ProcessOperator(op, ref graphicsState);
                }
                catch
                {
                    _logger.LogError("Error processing PDF content stream operator {Operator}. Continuing to next.", op);
                }

                operandStack.Clear();
            }
            else
            {
                operandStack.Push(value);
            }
        }

        currentPath.Dispose();
    }
}