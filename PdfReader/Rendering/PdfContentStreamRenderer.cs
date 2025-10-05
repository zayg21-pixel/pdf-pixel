using System;
using System.Collections.Generic;
using System.Text;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Streams;
using SkiaSharp;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Handles PDF content stream parsing and rendering coordination
    /// </summary>
    public static class PdfContentStreamRenderer
    {
        /// <summary>
        /// Apply page-level transformations for coordinate system conversion
        /// This properly transforms from PDF coordinate system (bottom-left origin, Y-up) 
        /// to Skia coordinate system (top-left origin, Y-down)
        /// </summary>
        public static void ApplyPageTransformations(SKCanvas canvas, SKRect mediaBox, SKRect cropBox, int rotation)
        {
            // NOTE: Page rotation (0, 90, 180, 270 degrees) is not applied during rendering
            // because it doesn't affect the actual content positioning within the PDF coordinate system.
            // The content is authored with the rotation already taken into account.
            // If rotation handling is needed, it should be done at the viewer/display level,
            // not during content stream processing.
            
            // Convert from PDF coordinate system (bottom-left origin, Y-up) 
            // to Skia coordinate system (top-left origin, Y-down)
            
            // Use the effective height for coordinate system conversion
            var effectiveHeight = cropBox.Height;

            // Step 1: Translate to move origin from bottom-left to top-left
            canvas.Translate(0, effectiveHeight);

            // Step 2: Flip Y-axis to convert from Y-up to Y-down
            // This will handle ALL coordinate transformations at once
            canvas.Scale(1, -1);

            // Step 3: Handle crop box offset if different from media box
            if (cropBox != mediaBox)
            {
                // Apply clipping to crop box bounds
                var clipRect = new SKRect(0, 0, cropBox.Width, cropBox.Height);
                canvas.ClipRect(clipRect);

                // Translate to crop box origin if needed
                if (cropBox.Left != 0 || cropBox.Bottom != 0)
                {
                    // Note: In PDF coordinate system, cropBox.Bottom is the Y offset from page bottom
                    canvas.Translate(-cropBox.Left, -cropBox.Bottom);
                }
            }
        }

        /// <summary>
        /// Get content streams for a PDF page
        /// </summary>
        public static List<ReadOnlyMemory<byte>> GetPageContentStreams(PdfPage page)
        {
            var contentStreams = new List<ReadOnlyMemory<byte>>();

            var contents = page.PageObject.Dictionary.GetPageObjects(PdfTokens.ContentsKey);

            foreach (var contentObject in contents)
            {
                var contentData = PdfStreamDecoder.DecodeContentStream(contentObject);

                if (!contentData.IsEmpty)
                {
                    contentStreams.Add(contentData);
                }
            }

            return contentStreams;
        }

        /// <summary>
        /// Render multiple content streams sequentially as one continuous stream without memory allocation.
        /// This treats all content streams as logically one stream while preserving graphics state continuity.
        /// </summary>
        public static void RenderContentStreamsSequentially(SKCanvas canvas, List<ReadOnlyMemory<byte>> contentStreams, PdfPage page)
        {
            if (contentStreams.Count == 0)
                return;

            // Create unified context that treats all streams as one continuous stream
            var parseContext = new PdfParseContext(contentStreams);

            // Render using the unified context - this is now the standard approach
            RenderContentStream(canvas, ref parseContext, page);
        }

        /// <summary>
        /// Renders a content stream directly to canvas using stack-based approach with PdfParsers.
        /// Now works efficiently with both single and multiple memory chunks.
        /// </summary>
        public static void RenderContentStream(SKCanvas canvas, ref PdfParseContext parseContext, PdfPage page)
        {
            var state = new PdfGraphicsState();
            var processingXObjects = new HashSet<int>();

            //var data = Encoding.UTF8.GetString(parseContext.GetSlice(0, parseContext.Length).ToArray());
            //Console.WriteLine("Rendering Page content");
            //Console.WriteLine(data);

            RenderContentStream(canvas, ref parseContext, page, state, processingXObjects);
        }

        /// <summary>
        /// Renders a content stream directly to canvas using stack-based approach with PdfParsers.
        /// Includes XObject recursion tracking to prevent infinite loops.
        /// </summary>
        public static void RenderContentStream(SKCanvas canvas, ref PdfParseContext parseContext, PdfPage page, PdfGraphicsState graphicsState, HashSet<int> processingXObjects)
        {
            var graphicsStack = new Stack<PdfGraphicsState>();
            var operandStack = new Stack<IPdfValue>();
            var currentPath = new SKPath();

            IPdfValue value;

            // Parse and process the entire stream (single or multiple chunks) continuously
            while ((value = PdfParsers.ParsePdfValue(ref parseContext, page.Document)) != null)
            {
                if (value.Type == PdfValueType.Operator)
                {
                    string op = value.AsString();
                    
                    if (PdfOperatorProcessor.IsValidOperator(op))
                    {
                        PdfOperatorProcessor.ProcessOperator(op, operandStack, ref parseContext, ref graphicsState, graphicsStack, canvas,
                                      currentPath, page, processingXObjects);
                    }
                    else
                    {
                        Console.WriteLine($"Invalid operator {op}");
                        operandStack.Push(value);
                    }
                }
                else
                {
                    operandStack.Push(value);
                }
            }

            currentPath.Dispose();
            
            // Note: graphicsState changes are local to this content stream
            // and don't affect the calling context (proper for Form XObjects)
        }
    }
}