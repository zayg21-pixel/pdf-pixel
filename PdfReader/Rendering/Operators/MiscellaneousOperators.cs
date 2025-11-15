using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfReader.Forms;
using PdfReader.Models;
using PdfReader.Rendering.State;
using PdfReader.Shading.Model;
using SkiaSharp;

namespace PdfReader.Rendering.Operators
{
    /// <summary>
    /// Handles miscellaneous PDF operators that don't fit into other specialized categories.
    /// Includes XObject invocation, marked content, compatibility, Type 3 font metrics, and shading.
    /// </summary>
    public class MiscellaneousOperators : IOperatorProcessor
    {
        private static readonly HashSet<string> SupportedOperators = new HashSet<string>
        {
            // XObject invocation
            "Do",
            // Marked content
            "MP","DP","BMC","BDC","EMC",
            // Compatibility
            "BX","EX",
            // Type 3 font metrics
            "d0","d1",
            // Shading
            "sh"
        };

        private readonly Stack<IPdfValue> _operandStack;
        private readonly PdfPage _page;
        private readonly SKCanvas _canvas;
        private readonly HashSet<int> _processingXObjects;
        private readonly ILogger<MiscellaneousOperators> _logger;

        public MiscellaneousOperators(Stack<IPdfValue> operandStack, PdfPage page, SKCanvas canvas, HashSet<int> processingXObjects)
        {
            _operandStack = operandStack;
            _page = page;
            _canvas = canvas;
            _processingXObjects = processingXObjects;
            _logger = page.Document.LoggerFactory.CreateLogger<MiscellaneousOperators>();
        }

        public bool CanProcess(string op)
        {
            return SupportedOperators.Contains(op);
        }

        public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
        {
            switch (op)
            {
                case "Do":
                {
                    ProcessInvokeXObject(graphicsState);
                    break;
                }
                case "MP":
                {
                    ProcessMarkContentPoint();
                    break;
                }
                case "DP":
                {
                    ProcessMarkContentPointWithProperties();
                    break;
                }
                case "BMC":
                {
                    ProcessBeginMarkedContent();
                    break;
                }
                case "BDC":
                {
                    ProcessBeginMarkedContentWithProperties();
                    break;
                }
                case "EMC":
                {
                    ProcessEndMarkedContent();
                    break;
                }
                case "BX":
                {
                    ProcessBeginCompatibility();
                    break;
                }
                case "EX":
                {
                    ProcessEndCompatibility();
                    break;
                }
                case "d0":
                {
                    ProcessSetGlyphWidth();
                    break;
                }
                case "d1":
                {
                    ProcessSetGlyphWidthAndBoundingBox();
                    break;
                }
                case "sh":
                {
                    ProcessShading(graphicsState);
                    break;
                }
            }
        }

        private void ProcessInvokeXObject(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var xObjectName = operands[0].AsName();
            if (xObjectName.IsEmpty)
            {
                return;
            }

            PdfXObjectProcessor.ProcessXObject(xObjectName, graphicsState, _canvas, _page, _processingXObjects);
        }

        // Handles MP (Marked Content Point): expects 1 operand (tag name)
        private void ProcessMarkContentPoint()
        {
            // PDF spec: MP operator takes a single tag name operand.
            // Operand is ignored for rendering, but must be popped to maintain stack integrity.
            PdfOperatorProcessor.GetOperands(1, _operandStack); // Intentionally ignored.
        }

        // Handles DP (Marked Content Point with Properties): expects 2 operands (tag name, property dictionary)
        private void ProcessMarkContentPointWithProperties()
        {
            // PDF spec: DP operator takes a tag name and a property dictionary.
            // Operands are ignored for rendering, but must be popped to maintain stack integrity.
            PdfOperatorProcessor.GetOperands(2, _operandStack); // Intentionally ignored.
        }

        // Handles BMC (Begin Marked Content): expects 1 operand (tag name)
        private void ProcessBeginMarkedContent()
        {
            // PDF spec: BMC operator takes a single tag name operand.
            // Operand is ignored for rendering, but must be popped to maintain stack integrity.
            PdfOperatorProcessor.GetOperands(1, _operandStack); // Intentionally ignored.
        }

        // Handles BDC (Begin Marked Content with Properties): expects 2 operands (tag name, property dictionary)
        private void ProcessBeginMarkedContentWithProperties()
        {
            // PDF spec: BDC operator takes a tag name and a property dictionary.
            // Operands are ignored for rendering, but must be popped to maintain stack integrity.
            PdfOperatorProcessor.GetOperands(2, _operandStack); // Intentionally ignored.
        }

        // Handles EMC (End Marked Content): no operands
        private void ProcessEndMarkedContent()
        {
            // PDF spec: EMC operator takes no operands.
            // No state to manage presently.
        }

        private void ProcessBeginCompatibility()
        {
            // Ignored per PDF spec (compatibility section start).
        }

        private void ProcessEndCompatibility()
        {
            // Ignored per PDF spec (compatibility section end).
        }

        private void ProcessSetGlyphWidth()
        {
            var operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
            if (operands.Count < 2)
            {
                return;
            }

            var glyphWidthX = operands[0].AsFloat();
            var glyphWidthY = operands[1].AsFloat();
            _logger.LogDebug("Type3 glyph width: ({GlyphWidthX},{GlyphWidthY})", glyphWidthX, glyphWidthY);
        }

        private void ProcessSetGlyphWidthAndBoundingBox()
        {
            var operands = PdfOperatorProcessor.GetOperands(6, _operandStack);
            if (operands.Count < 6)
            {
                return;
            }

            var glyphWidthX = operands[0].AsFloat();
            var glyphWidthY = operands[1].AsFloat();
            var llx = operands[2].AsFloat();
            var lly = operands[3].AsFloat();
            var urx = operands[4].AsFloat();
            var ury = operands[5].AsFloat();
            _logger.LogDebug("Type3 glyph width and bbox: w=({GlyphWidthX},{GlyphWidthY}) bbox=({Llx},{Lly},{Urx},{Ury})", glyphWidthX, glyphWidthY, llx, lly, urx, ury);
        }

        private void ProcessShading(PdfGraphicsState graphicsState)
        {
            var operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var shadingName = operands[0].AsName();
            if (shadingName.IsEmpty)
            {
                return;
            }

            var shadings = _page.ResourceDictionary.GetDictionary(PdfTokens.ShadingKey);
            var shadingObject = shadings?.GetPageObject(shadingName);

            if (shadingObject == null)
            {
                _logger.LogWarning("Shading '{ShadingName}' not found in resources", shadingName);
                return;
            }

            var shading = new PdfShading(shadingObject, _page);

            _page.Document.PdfRenderer.DrawShading(_canvas, shading, graphicsState, _page);
        }
    }
}