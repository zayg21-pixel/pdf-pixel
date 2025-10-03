using System;
using System.Collections.Generic;
using PdfReader.Models;
using PdfReader.Rendering.Operators;
using SkiaSharp;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Handles PDF operator processing and execution for content streams
    /// Delegates to specialized operator classes for better organization
    /// </summary>
    public static class PdfOperatorProcessor
    {
        // Valid PDF operators for content streams
        private static readonly HashSet<string> ValidOperators = new HashSet<string>
        {
            // Graphics state operators
            "q", "Q", "cm", "w", "J", "j", "M", "d", "ri", "i", "gs",
            
            // Path construction operators
            "m", "l", "c", "v", "y", "h", "re",
            
            // Path painting operators
            "S", "s", "f", "F", "f*", "B", "B*", "b", "b*", "n",
            
            // Clipping path operators
            "W", "W*",
            
            // Text object operators
            "BT", "ET",
            
            // Text state operators
            "Tc", "Tw", "Tz", "TL", "Tf", "Tr", "Ts",
            
            // Text positioning operators
            "Td", "TD", "Tm", "T*",
            
            // Text showing operators
            "Tj", "TJ", "'", "\"",
            
            // Color operators
            "CS", "cs", "SC", "SCN", "sc", "scn", "G", "g", "RG", "rg", "K", "k",
            
            // Shading operators
            "sh",
            
            // Inline image operators
            "BI", "ID", "EI",
            
            // XObject operators
            "Do",
            
            // Marked content operators
            "MP", "DP", "BMC", "BDC", "EMC",
            
            // Compatibility operators
            "BX", "EX",
            
            // Type 3 font operators (for Type 3 font character procedures)
            "d0", "d1"
        };

        /// <summary>
        /// Extract operands from the operand stack in reverse order
        /// </summary>
        public static List<IPdfValue> GetOperands(int count, Stack<IPdfValue> operandStack)
        {
            var operands = new List<IPdfValue>(count);
            for (int i = 0; i < count && operandStack.Count > 0; i++)
            {
                operands.Insert(0, operandStack.Pop()); // Insert at beginning to reverse stack order
            }
            return operands;
        }

        /// <summary>
        /// Check if a string is a valid PDF operator
        /// </summary>
        public static bool IsValidOperator(string operatorName)
        {
            return ValidOperators.Contains(operatorName);
        }

        /// <summary>
        /// Process a PDF operator with operands from the stack
        /// Delegates to specialized operator classes based on operator type
        /// Includes XObject recursion tracking context
        /// </summary>
        public static void ProcessOperator(string op, Stack<IPdfValue> operandStack,
                                          ref PdfParseContext parseContext, 
                                          ref PdfGraphicsState graphicsState, Stack<PdfGraphicsState> graphicsStack,
                                          SKCanvas canvas, SKPath currentPath,
                                          PdfPage page, HashSet<int> processingXObjects)
        {
            // Try each specialized operator class in order
            bool handled = false;

            // Try graphics state operators first (most common)
            if (!handled)
            {
                handled = GraphicsStateOperators.ProcessOperator(op, operandStack, ref graphicsState,
                                                                graphicsStack, canvas, page);
            }

            // Try text operators
            if (!handled)
            {
                handled = TextOperators.ProcessOperator(op, operandStack, graphicsState, 
                                                       canvas, page);
            }

            // Try path operators
            if (!handled)
            {
                handled = PathOperators.ProcessOperator(op, operandStack, graphicsState, 
                                                       canvas, currentPath, page);
            }

            // Try color operators
            if (!handled)
            {
                handled = ColorOperators.ProcessOperator(op, operandStack, graphicsState, page);
            }

            // Try miscellaneous operators (includes Do operator for XObjects)
            if (!handled)
            {
                handled = InlineImageOperators.ProcessOperator(op, operandStack, ref parseContext, graphicsState, canvas, page);
            }

            // Try miscellaneous operators (includes Do operator for XObjects)
            if (!handled)
            {
                handled = MiscellaneousOperators.ProcessOperator(op, operandStack, graphicsState, canvas, page, processingXObjects);
            }

            // If no specialized class handled it, process as unknown
            if (!handled)
            {
                ProcessUnknownOperator(op, operandStack);
            }
        }

        /// <summary>
        /// Handle unknown operators by consuming operands to prevent stack buildup
        /// </summary>
        private static void ProcessUnknownOperator(string op, Stack<IPdfValue> operandStack)
        {
            // Unknown operator - consume any operands that might belong to it
            // This is a conservative approach to avoid stack buildup
            Console.WriteLine($"Warning: Unknown PDF operator '{op}' with {operandStack.Count} operands on stack");
        }
    }
}