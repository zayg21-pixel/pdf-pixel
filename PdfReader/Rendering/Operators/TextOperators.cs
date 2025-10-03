using System.Collections.Generic;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Rendering.Operators
{
    /// <summary>
    /// Handles text-related PDF operators
    /// </summary>
    public static class TextOperators
    {
        /// <summary>
        /// Process text-related operators
        /// </summary>
        public static bool ProcessOperator(string op, Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState,
                                         SKCanvas canvas, PdfPage page)
        {
            switch (op)
            {
                // Text object operators
                case "BT": // Begin text
                    ProcessBeginText(graphicsState);
                    return true;

                case "ET": // End text
                    ProcessEndText(graphicsState);
                    return true;

                // Text state operators
                case "Tf": // Set font
                    ProcessSetFont(operandStack, graphicsState);
                    return true;

                case "Tc": // Set character spacing
                    ProcessSetCharacterSpacing(operandStack, graphicsState);
                    return true;

                case "Tw": // Set word spacing
                    ProcessSetWordSpacing(operandStack, graphicsState);
                    return true;

                case "Tz": // Set horizontal scaling
                    ProcessSetHorizontalScaling(operandStack, graphicsState);
                    return true;

                case "TL": // Set text leading
                    ProcessSetTextLeading(operandStack, graphicsState);
                    return true;

                case "Ts": // Set text rise
                    ProcessSetTextRise(operandStack, graphicsState);
                    return true;

                case "Tr": // Set text rendering mode
                    ProcessSetTextRenderingMode(operandStack, graphicsState);
                    return true;

                // Text positioning operators
                case "Td": // Move text position
                    ProcessMoveTextPosition(operandStack, graphicsState);
                    return true;

                case "TD": // Move text position and set leading
                    ProcessMoveTextPositionAndSetLeading(operandStack, graphicsState);
                    return true;

                case "T*": // Next line
                    ProcessNextLine(graphicsState);
                    return true;

                case "Tm": // Set text matrix
                    ProcessSetTextMatrix(operandStack, graphicsState);
                    return true;

                // Text showing operators
                case "Tj": // Show text
                    ProcessShowText(operandStack, canvas, graphicsState, page);
                    return true;

                case "'": // Show text next line
                    ProcessShowTextNextLine(operandStack, canvas, graphicsState, page);
                    return true;

                case "TJ": // Show text with positioning
                    ProcessShowTextWithPositioning(operandStack, canvas, graphicsState, page);
                    return true;

                case "\"": // Set spacing and show text
                    ProcessSetSpacingAndShowText(operandStack, canvas, graphicsState, page);
                    return true;

                default:
                    return false; // Not handled by this class
            }
        }

        private static void ProcessBeginText(PdfGraphicsState graphicsState)
        {
            graphicsState.InTextObject = true;
            // Reset text matrices to identity (coordinate transformation handled globally)
            graphicsState.TextMatrix = SKMatrix.Identity;
            graphicsState.TextLineMatrix = SKMatrix.Identity;
        }

        private static void ProcessEndText(PdfGraphicsState graphicsState)
        {
            graphicsState.InTextObject = false;
            // Clear text matrices
            graphicsState.TextMatrix = SKMatrix.Identity;
            graphicsState.TextLineMatrix = SKMatrix.Identity;
        }

        private static void ProcessSetFont(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var fontOperands = PdfOperatorProcessor.GetOperands(2, operandStack);
            if (fontOperands.Count >= 2)
            {
                var fontName = fontOperands[0].AsName();
                var fontSize = fontOperands[1].AsFloat();

                if (fontName != null && fontSize > 0)
                {
                    graphicsState.CurrentFont = fontName;
                    graphicsState.FontSize = fontSize;
                }
            }
        }

        private static void ProcessSetCharacterSpacing(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var tcOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (tcOperands.Count > 0)
                graphicsState.CharacterSpacing = tcOperands[0].AsFloat();
        }

        private static void ProcessSetWordSpacing(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var twOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (twOperands.Count > 0)
                graphicsState.WordSpacing = twOperands[0].AsFloat();
        }

        private static void ProcessSetHorizontalScaling(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var tzOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (tzOperands.Count > 0)
                graphicsState.HorizontalScaling = tzOperands[0].AsFloat();
        }

        private static void ProcessSetTextLeading(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var tlOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (tlOperands.Count > 0)
                graphicsState.Leading = tlOperands[0].AsFloat();
        }

        private static void ProcessSetTextRise(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var tsOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (tsOperands.Count > 0)
                graphicsState.Rise = tsOperands[0].AsFloat();
        }

        /// <summary>
        /// Set text rendering mode (Tr operator)
        /// Supports modes 0-7: Fill, Stroke, FillAndStroke, Invisible, FillAndClip, StrokeAndClip, FillAndStrokeAndClip, Clip
        /// </summary>
        private static void ProcessSetTextRenderingMode(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var trOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (trOperands.Count > 0)
            {
                var mode = (int)trOperands[0].AsFloat();
                
                // Validate the mode is in the valid range (0-7)
                if (mode >= 0 && mode <= 7)
                {
                    graphicsState.TextRenderingMode = (PdfTextRenderingMode)mode;
                }
                else
                {
                    graphicsState.TextRenderingMode = PdfTextRenderingMode.Fill;
                }
            }
        }

        private static void ProcessMoveTextPosition(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var tdOperands = PdfOperatorProcessor.GetOperands(2, operandStack);
            if (tdOperands.Count >= 2 && graphicsState.InTextObject)
            {
                var tx = tdOperands[0].AsFloat();
                var ty = tdOperands[1].AsFloat();

                // PDF Specification: Apply displacement using translation.PostConcat(matrix)
                // This preserves the original matrix properties and applies proper scaling
                var translation = SKMatrix.CreateTranslation(tx, ty);

                graphicsState.TextLineMatrix = translation.PostConcat(graphicsState.TextLineMatrix);
                graphicsState.TextMatrix = graphicsState.TextLineMatrix; // Copy TextLineMatrix to TextMatrix
            }
        }

        private static void ProcessMoveTextPositionAndSetLeading(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var tdOperands2 = PdfOperatorProcessor.GetOperands(2, operandStack);
            if (tdOperands2.Count >= 2 && graphicsState.InTextObject)
            {
                var tx = tdOperands2[0].AsFloat();
                var ty = tdOperands2[1].AsFloat();

                // TD sets the leading to -ty (PDF specification: TL = -ty)
                graphicsState.Leading = -ty;

                // PDF Specification: Apply displacement using translation.PostConcat(matrix)
                // This preserves the original matrix properties and applies proper scaling
                var translation = SKMatrix.CreateTranslation(tx, ty);
                
                graphicsState.TextLineMatrix = translation.PostConcat(graphicsState.TextLineMatrix);
                graphicsState.TextMatrix = graphicsState.TextLineMatrix; // Copy TextLineMatrix to TextMatrix
            }
        }

        private static void ProcessShowText(Stack<IPdfValue> operandStack, SKCanvas canvas, PdfGraphicsState graphicsState, PdfPage page)
        {
            var textOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (textOperands.Count > 0 && graphicsState.InTextObject)
            {                
                // Use PdfText wrapper instead of direct text extraction
                var pdfText = PdfText.FromOperand(textOperands[0]);
                
                if (!pdfText.IsEmpty)
                {
                    var font = page.GetFont(graphicsState.CurrentFont);

                    // Draw text and get advancement
                    var advancement = page.Document.PdfRenderer.DrawText(canvas, ref pdfText, page, graphicsState, font);
                    
                    // Update only TextMatrix with advancement (TextLineMatrix stays at line start)
                    // Use the same pattern as positioning operators for consistency
                    var advanceMatrix = SKMatrix.CreateTranslation(advancement, 0);
                    graphicsState.TextMatrix = advanceMatrix.PostConcat(graphicsState.TextMatrix);
                }
            }
        }

        private static void ProcessShowTextNextLine(Stack<IPdfValue> operandStack, SKCanvas canvas, PdfGraphicsState graphicsState, PdfPage page)
        {
            if (graphicsState.InTextObject)
            {
                // First move to next line (T* operator)
                ProcessNextLine(graphicsState);
                
                // Then show text (Tj operator)
                ProcessShowText(operandStack, canvas, graphicsState, page);
            }
        }

        private static void ProcessShowTextWithPositioning(Stack<IPdfValue> operandStack, SKCanvas canvas, PdfGraphicsState graphicsState, PdfPage page)
        {
            var arrayOperands = PdfOperatorProcessor.GetOperands(1, operandStack);
            if (arrayOperands.Count > 0 && graphicsState.InTextObject)
            {
                var font = page.GetFont(graphicsState.CurrentFont);

                // Draw text with positioning and get total advancement
                var totalAdvancement = page.Document.PdfRenderer.DrawTextWithPositioning(canvas, arrayOperands[0], page, graphicsState, font);
                
                // Update text matrix with advancement (critical for proper positioning per PDF spec)
                // Use the same pattern as positioning operators for consistency
                var advanceMatrix = SKMatrix.CreateTranslation(totalAdvancement, 0);
                graphicsState.TextMatrix = advanceMatrix.PostConcat(graphicsState.TextMatrix);
            }
        }

        private static void ProcessNextLine(PdfGraphicsState graphicsState)
        {
            if (graphicsState.InTextObject)
            {
                // PDF Specification: Apply leading displacement using translation.PostConcat(matrix)
                // This preserves the original matrix properties and applies proper scaling
                var translation = SKMatrix.CreateTranslation(0, graphicsState.Leading);
                
                graphicsState.TextLineMatrix = translation.PostConcat(graphicsState.TextLineMatrix);
                graphicsState.TextMatrix = graphicsState.TextLineMatrix; // Reset to line start
            }
        }

        private static void ProcessSetTextMatrix(Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState)
        {
            var tmOperands = PdfOperatorProcessor.GetOperands(6, operandStack);
            if (tmOperands.Count >= 6 && graphicsState.InTextObject)
            {
                // Use matrix operands directly - global transformation handles coordinate system conversion
                var matrix = PdfMatrixUtilities.CreateMatrix(tmOperands);
                graphicsState.TextMatrix = matrix;
                graphicsState.TextLineMatrix = matrix;
            }
        }

        private static void ProcessSetSpacingAndShowText(Stack<IPdfValue> operandStack, SKCanvas canvas, PdfGraphicsState graphicsState, PdfPage page)
        {
            var spacingOperands = PdfOperatorProcessor.GetOperands(3, operandStack);
            if (spacingOperands.Count >= 3 && graphicsState.InTextObject)
            {
                // Set word and character spacing
                graphicsState.WordSpacing = spacingOperands[0].AsFloat();
                graphicsState.CharacterSpacing = spacingOperands[1].AsFloat();
                
                // Use PdfText wrapper instead of direct text extraction
                var pdfText = PdfText.FromOperand(spacingOperands[2]);
                
                if (!pdfText.IsEmpty)
                {
                    var font = page.GetFont(graphicsState.CurrentFont);

                    // Draw text and get advancement
                    var advancement = page.Document.PdfRenderer.DrawText(canvas, ref pdfText, page, graphicsState, font);
                    
                    // Update text matrix with advancement (critical for proper positioning per PDF spec)
                    // Use the same pattern as positioning operators for consistency
                    var advanceMatrix = SKMatrix.CreateTranslation(advancement, 0);
                    graphicsState.TextMatrix = advanceMatrix.PostConcat(graphicsState.TextMatrix);
                }
            }
        }
    }
}