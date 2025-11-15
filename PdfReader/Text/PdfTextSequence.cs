using PdfReader.Models;
using System.Collections.Generic;

namespace PdfReader.Text
{
    /// <summary>
    /// High-level representation of a PDF TJ array: a sequence of text fragments and positioning adjustments.
    /// Each item holds either a text fragment or an adjustment value.
    /// </summary>
    public sealed class PdfTextSequence
    {
        /// <summary>
        /// Gets the ordered list of items in the sequence.
        /// </summary>
        public IReadOnlyList<PdfTextSequenceItem> Items { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfTextSequence"/> class.
        /// </summary>
        /// <param name="items">The items in the sequence.</param>
        public PdfTextSequence(IReadOnlyList<PdfTextSequenceItem> items)
        {
            Items = items;
        }

        public static PdfTextSequence FromText(IPdfValue operand)
        {
            var items = new List<PdfTextSequenceItem>(1)
            {
                new PdfTextSequenceItem(PdfText.FromOperand(operand))
            };

            return new PdfTextSequence(items);
        }

        /// <summary>
        /// Parses a TJ array operand into a high-level sequence of text and adjustment items.
        /// </summary>
        /// <param name="arrayOperand">The PDF array operand.</param>
        /// <returns>A <see cref="PdfTextSequence"/> representing the array.</returns>
        public static PdfTextSequence FromArray(IPdfValue arrayOperand)
        {
            var items = new List<PdfTextSequenceItem>();
            if (arrayOperand.Type != PdfValueType.Array)
            {
                return new PdfTextSequence(items);
            }

            var array = arrayOperand.AsArray();
            if (array == null)
            {
                return new PdfTextSequence(items);
            }

            for (int i = 0; i < array.Count; i++)
            {
                var item = array.GetValue(i);

                if (item.Type == PdfValueType.String)
                {
                    var pdfText = PdfText.FromOperand(item);
                    items.Add(new PdfTextSequenceItem(pdfText));
                }
                else
                {
                    var adjustment = item.AsFloat();
                    items.Add(new PdfTextSequenceItem(adjustment));
                }
            }

            return new PdfTextSequence(items);
        }
    }
}
