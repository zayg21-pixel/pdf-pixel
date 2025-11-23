using SkiaSharp;

namespace PdfReader.TextExtraction
{
    public enum PdfWordType
    {
        Normal,
        Punctuation
    }

    public class PdfWord
    {
        public PdfWord(SKRect boundingBox, PdfWordType type, int lineIndex, PdfCharacter[] characters)
        {
            BoundingBox = boundingBox;
            Type = type;
            LineIndex = lineIndex;
            Characters = characters;
        }

        public SKRect BoundingBox { get; }

        public PdfWordType Type { get; }

        public int LineIndex { get; }

        public PdfCharacter[] Characters { get; }
    }
}
