using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering.Image;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Handles PDF content stream parsing and rendering coordination
    /// </summary>
    public class PdfContentStreamRenderer
    {
        private readonly PdfPage _page;
        private readonly ILogger<PdfContentStreamRenderer> _logger;

        public PdfContentStreamRenderer(PdfPage page)
        {
            _page = page;
            _logger = page.Document.LoggerFactory.CreateLogger<PdfContentStreamRenderer>();
        }

        /// <summary>
        /// Apply page-level transformations for coordinate system conversion
        /// This properly transforms from PDF coordinate system (bottom-left origin, Y-up) 
        /// to Skia coordinate system (top-left origin, Y-down)
        /// </summary>
        public void ApplyPageTransformations(SKCanvas canvas)
        {
            // Step 1: Translate to move origin from bottom-left to top-left with crop box offset
            canvas.Translate(-_page.CropBox.Left, _page.CropBox.Height + _page.CropBox.Top);

            // Step 2: Flip Y-axis to convert from Y-up to Y-down
            // This will handle ALL coordinate transformations at once
            canvas.Scale(1, -1);
        }

        /// <summary>
        /// Render multiple content streams sequentially as one continuous stream without memory allocation.
        /// This treats all content streams as logically one stream while preserving graphics state continuity.
        /// </summary>
        public void RenderContent(SKCanvas canvas)
        {
            var contentStreams = GetPageContentStreams();

            if (contentStreams.Count == 0)
                return;

            // Create unified context that treats all streams as one continuous stream
            var parseContext = new PdfParseContext(contentStreams);

            var state = new PdfGraphicsState();
            state.DeviceMatrix = canvas.TotalMatrix;
            var processingXObjects = new HashSet<int>();

            RenderContext(canvas, ref parseContext, state, processingXObjects);
        }

        private List<ReadOnlyMemory<byte>> GetPageContentStreams()
        {
            var contentStreams = new List<ReadOnlyMemory<byte>>();

            var contents = _page.PageObject.Dictionary.GetPageObjects(PdfTokens.ContentsKey);

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
        public void RenderContext(SKCanvas canvas, ref PdfParseContext parseContext, PdfGraphicsState graphicsState, HashSet<int> processingXObjects)
        {
            var graphicsStack = new Stack<PdfGraphicsState>();
            var operandStack = new Stack<IPdfValue>();
            using var currentPath = new SKPath();
            var operatorProcessor = new PdfOperatorProcessor(_page, canvas, operandStack, graphicsStack, currentPath, processingXObjects);
            var parser = new PdfParser(parseContext, _page.Document, allowReferences: false);
            IPdfValue value;

            while ((value = parser.ReadNextValue()) != null)
            {
                if (value.Type == PdfValueType.Operator)
                {
                    string op = value.AsString().ToString();
                    operatorProcessor.ProcessOperator(op, ref graphicsState);
                }
                else
                {
                    operandStack.Push(value);
                }
            }

            currentPath.Dispose();
        }
    }
}