using System;

namespace PdfReader.Text
{
    /// <summary>
    /// Indicates the kind of item in a PDF TJ array: either text or adjustment.
    /// </summary>
    public enum PdfTextPositioningKind
    {
        Text,
        Adjustment
    }

    /// <summary>
    /// Represents a single item in a PDF TJ array: either a text fragment or a positioning adjustment.
    /// </summary>
    public readonly struct PdfTextSequenceItem
    {
        /// <summary>
        /// Creates a positioning adjustment item.
        /// </summary>
        /// <param name="adjustment">The adjustment value.</param>
        public PdfTextSequenceItem(float adjustment)
        {
            Kind = PdfTextPositioningKind.Adjustment;
            Text = default;
            Adjustment = adjustment;
        }

        /// <summary>
        /// Creates a text fragment item.
        /// </summary>
        /// <param name="text">The text fragment.</param>
        public PdfTextSequenceItem(PdfText text)
        {
            Kind = PdfTextPositioningKind.Text;
            Text = text;
            Adjustment = 0f;
        }

        /// <summary>
        /// The kind of this item (Text or Adjustment).
        /// </summary>
        public PdfTextPositioningKind Kind { get; }

        /// <summary>
        /// The text fragment for this item (valid if Kind == Text).
        /// </summary>
        public PdfText Text { get; }

        /// <summary>
        /// The adjustment value for this item (valid if Kind == Adjustment).
        /// </summary>
        public float Adjustment { get; }
    }
}
