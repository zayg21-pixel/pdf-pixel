using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace PdfReader.TextExtraction
{
    public class PdfTextChunker
    {
        public PdfTextChunker()
        {
        }

        /// <summary>
        /// Chunks a sequence of PdfCharacter into PdfWord objects.
        /// Punctuation is treated as a separate word if it is a stop, comma, or quote.
        /// Dash and connector punctuation (e.g., hyphens, underscores) are allowed within words.
        /// Whitespace splits words but is not emitted as a word.
        /// Adds X offset heuristic: if the gap between characters is more than 2x previous char width, chunk.
        /// </summary>
        /// <param name="characters">Sequence of characters to chunk.</param>
        /// <returns>Sequence of PdfWord objects.</returns>
        public IEnumerable<PdfWord> ChunkCharacters(IList<PdfCharacter> characters)
        {
            if (characters == null)
            {
                yield break;
            }

            List<PdfCharacter> currentWord = new List<PdfCharacter>();
            PdfWordType currentType = PdfWordType.Normal;
            int lineIndex = 0;
            float? lastY = null;
            float? prevCharRight = null;
            float? prevCharWidth = null;

            for (int i = 0; i < characters.Count; i++)
            {
                PdfCharacter ch = characters[i];
                if (string.IsNullOrEmpty(ch.Text))
                {
                    continue;
                }

                char c = ch.Text[0];
                UnicodeCategory category = char.GetUnicodeCategory(c);
                bool isWhiteSpace = char.IsWhiteSpace(c);
                bool isWordBreakingPunctuation =
                    category == UnicodeCategory.InitialQuotePunctuation ||
                    category == UnicodeCategory.FinalQuotePunctuation ||
                    category == UnicodeCategory.OtherPunctuation;
                // DashPunctuation and ConnectorPunctuation are allowed within words
                float y = ch.BoundingBox.Top;
                float xMin = ch.BoundingBox.Left;
                float charWidth = ch.BoundingBox.Width;

                // New line detection
                if (lastY.HasValue && Math.Abs(y - lastY.Value) > 0.1f)
                {
                    if (currentWord.Count > 0)
                    {
                        yield return CreateWord(currentWord, currentType, lineIndex);
                        currentWord.Clear();
                    }
                    lineIndex++;
                    prevCharRight = null;
                    prevCharWidth = null;
                }
                else if (prevCharRight.HasValue && prevCharWidth.HasValue)
                {
                    // X offset heuristic: if gap is more than 2x previous char width, chunk
                    float gap = Math.Abs(xMin - prevCharRight.Value);
                    if (gap > prevCharWidth.Value)
                    {
                        if (currentWord.Count > 0)
                        {
                            yield return CreateWord(currentWord, currentType, lineIndex);
                            currentWord.Clear();
                        }
                        currentType = PdfWordType.Normal;
                    }
                }

                lastY = y;
                prevCharWidth = charWidth;
                prevCharRight = ch.BoundingBox.Right;

                if (isWhiteSpace)
                {
                    // Whitespace splits words but is not emitted as a word
                    if (currentWord.Count > 0)
                    {
                        yield return CreateWord(currentWord, currentType, lineIndex);
                        currentWord.Clear();
                    }
                    currentType = PdfWordType.Normal;
                    continue;
                }

                if (isWordBreakingPunctuation)
                {
                    // Emit current word if any
                    if (currentWord.Count > 0)
                    {
                        yield return CreateWord(currentWord, currentType, lineIndex);
                        currentWord.Clear();
                    }
                    // Emit punctuation as its own word
                    yield return new PdfWord(ch.BoundingBox, PdfWordType.Punctuation, lineIndex, new[] { ch });
                    currentType = PdfWordType.Normal;
                }
                else
                {
                    if (currentType == PdfWordType.Punctuation && currentWord.Count > 0)
                    {
                        yield return CreateWord(currentWord, currentType, lineIndex);
                        currentWord.Clear();
                    }
                    currentWord.Add(ch);
                    currentType = PdfWordType.Normal;
                }
            }

            if (currentWord.Count > 0)
            {
                lastY = null;
                prevCharRight = null;
                prevCharWidth = null;

                yield return CreateWord(currentWord, currentType, lineIndex);
            }
        }

        private static PdfWord CreateWord(List<PdfCharacter> chars, PdfWordType type, int lineIndex)
        {
            if (chars == null || chars.Count == 0)
            {
                throw new ArgumentException("No characters to create word.", nameof(chars));
            }
            SKRect bbox = chars[0].BoundingBox;
            for (int i = 1; i < chars.Count; i++)
            {
                bbox = SKRect.Union(bbox, chars[i].BoundingBox);
            }
            return new PdfWord(bbox, type, lineIndex, chars.ToArray());
        }
    }
}
