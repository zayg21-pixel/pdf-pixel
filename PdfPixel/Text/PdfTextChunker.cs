using PdfPixel.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfPixel.Text;

/// <summary>
/// Groups a sequence of <see cref="PdfCharacter"/> instances into <see cref="PdfWord"/> tokens.
/// </summary>
public sealed class PdfTextChunker
{
    private const string Space = " ";
    private const string LineBreak = "\n";
    private const char SoftHyphen = '­';

    private readonly List<PdfCharacter> _characters = [];
    private readonly List<PartRange> _parts = [];
    private readonly List<WordRange> _words = [];
    private PdfWordType _wordType;
    private int _lineIndex;
    private int _partStart;
    private int _wordFirstPart;
    private float _wordGapRatio;
    private float _lineOffsetRatio;

    /// <summary>
    /// Chunks a sequence of characters into words in reading order.
    /// Quote and other punctuation is a separate word. Whitespace is a <see cref="PdfWordType.Space"/> word, generated as a space
    /// between two words separated by a gap and as a line break at the end of a line when the characters have none.
    /// A word hyphenated at the end of a line continues in a new part on the next line.
    /// </summary>
    /// <param name="characters">Characters in reading order.</param>
    /// <param name="properties">Properties providing the word gap and line offset ratios.</param>
    public PdfWord[] ChunkCharacters(PdfCharacter[] characters, PdfTextExtractionProperties properties)
    {
        if (characters == null)
        {
            throw new ArgumentNullException(nameof(characters));
        }

        if (properties == null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        _wordGapRatio = properties.WordGapRatio;
        _lineOffsetRatio = properties.LineOffsetRatio;
        _characters.Clear();
        _parts.Clear();
        _words.Clear();
        _lineIndex = 0;
        _partStart = 0;
        _wordFirstPart = 0;

        PdfRectangle? previousContentBox = null;
        var previousIsSpace = false;

        foreach (PdfCharacter character in characters)
        {
            PdfWordType type = GetWordType(character.Text);

            if (type != PdfWordType.Space)
            {
                PdfRectangle box = character.BoundingBox;

                if (previousContentBox != null && IsNewLine(previousContentBox.Value, box))
                {
                    PdfRectangle previousBox = previousContentBox.Value;
                    PdfCharacter lineBreak = new(LineBreak, new PdfRectangle(previousBox.Right, previousBox.Top, previousBox.Right, previousBox.Bottom));

                    if (!previousIsSpace && IsHyphenation(character))
                    {
                        _characters.Add(lineBreak);
                        FlushPart();
                    }
                    else
                    {
                        if (!previousIsSpace)
                        {
                            AddSeparator(lineBreak);
                        }

                        FlushWord();
                    }

                    _lineIndex++;
                }
                else if (previousContentBox != null && !previousIsSpace && IsWordGap(previousContentBox.Value, box))
                {
                    AddSeparator(new PdfCharacter(Space, GetGapBox(previousContentBox.Value, box)));
                }

                previousContentBox = box;
            }

            AppendCharacter(character, type);
            previousIsSpace = type == PdfWordType.Space;
        }

        FlushWord();

        return CreateWords();
    }

    private void AppendCharacter(in PdfCharacter character, PdfWordType type)
    {
        bool hasWord = _characters.Count > _partStart || _parts.Count > _wordFirstPart;

        if (hasWord && (type != _wordType || type == PdfWordType.Punctuation))
        {
            FlushWord();
        }

        _wordType = type;
        _characters.Add(character);
    }

    private void AddSeparator(in PdfCharacter separator)
    {
        FlushWord();
        _wordType = PdfWordType.Space;
        _characters.Add(separator);
        FlushWord();
    }

    /// <summary>
    /// Whether <paramref name="current"/>, the first character of a new line, continues a word that ends
    /// in a letter and a hyphen on the previous line.
    /// </summary>
    private bool IsHyphenation(in PdfCharacter current)
    {
        int count = _characters.Count;

        if (_wordType != PdfWordType.Normal || count - _partStart < 2)
        {
            return false;
        }

        string? hyphenText = _characters[count - 1].Text;
        string? letterText = _characters[count - 2].Text;

        return IsHyphen(hyphenText)
            && IsLetter(letterText)
            && IsLetter(current.Text);
    }

    private void FlushPart()
    {
        int count = _characters.Count - _partStart;

        if (count == 0)
        {
            return;
        }

        _parts.Add(new PartRange(_lineIndex, _partStart, count));
        _partStart = _characters.Count;
    }

    private void FlushWord()
    {
        FlushPart();

        int partCount = _parts.Count - _wordFirstPart;

        if (partCount == 0)
        {
            return;
        }

        _words.Add(new WordRange(_wordType, _wordFirstPart, partCount));
        _wordFirstPart = _parts.Count;
    }

    /// <summary>
    /// Creates the page's words over one array of its characters and one array of its parts, each part a slice of the
    /// characters and each word a slice of the parts.
    /// </summary>
    private PdfWord[] CreateWords()
    {
        PdfCharacter[] characters = _characters.ToArray();
        var parts = new PdfWordPart[_parts.Count];

        for (int index = 0; index < parts.Length; index++)
        {
            PartRange part = _parts[index];
            parts[index] = new PdfWordPart(part.LineIndex, part.Start, characters.AsMemory(part.Start, part.Count));
        }

        var words = new PdfWord[_words.Count];

        for (int index = 0; index < words.Length; index++)
        {
            WordRange word = _words[index];
            words[index] = new PdfWord(word.Type, parts.AsMemory(word.FirstPart, word.PartCount));
        }

        return words;
    }

    private static PdfWordType GetWordType(string? text)
    {
        if (text == null || text.Length == 0)
        {
            return PdfWordType.Normal;
        }

        if (IsWhiteSpace(text))
        {
            return PdfWordType.Space;
        }

        return (IsWordBreakingPunctuation(text[0])) ? PdfWordType.Punctuation : PdfWordType.Normal;
    }

    private bool IsNewLine(in PdfRectangle previousBox, in PdfRectangle currentBox)
    {
        float height = Math.Max(previousBox.Height, currentBox.Height);

        return height > 0
            && Math.Abs(currentBox.MidY - previousBox.MidY) > height * _lineOffsetRatio;
    }

    private bool IsWordGap(in PdfRectangle previousBox, in PdfRectangle currentBox)
    {
        float height = Math.Max(previousBox.Height, currentBox.Height);

        return height > 0
            && (currentBox.Left - previousBox.Right > height * _wordGapRatio || currentBox.Right < previousBox.Left);
    }

    private static PdfRectangle GetGapBox(in PdfRectangle previousBox, in PdfRectangle currentBox)
    {
        return new(
            previousBox.Right,
            Math.Min(previousBox.Top, currentBox.Top),
            Math.Max(previousBox.Right, currentBox.Left),
            Math.Max(previousBox.Bottom, currentBox.Bottom));
    }

    private static bool IsWordBreakingPunctuation(char character)
    {
        UnicodeCategory category = char.GetUnicodeCategory(character);

        return category == UnicodeCategory.InitialQuotePunctuation
            || category == UnicodeCategory.FinalQuotePunctuation
            || category == UnicodeCategory.OtherPunctuation;
    }

    private static bool IsHyphen(string? text)
    {
        if (text == null || text.Length != 1)
        {
            return false;
        }

        return text[0] == SoftHyphen
            || char.GetUnicodeCategory(text[0]) == UnicodeCategory.DashPunctuation;
    }

    private static bool IsLetter(string? text)
        => text != null && text.Length > 0 && char.IsLetter(text[0]);

    private static bool IsWhiteSpace(string text)
    {
        foreach (char character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A word part found while chunking: its characters are the <see cref="Count"/> from <see cref="Start"/> among the
    /// page's characters.
    /// </summary>
    private readonly struct PartRange
    {
        public PartRange(int lineIndex, int start, int count)
        {
            LineIndex = lineIndex;
            Start = start;
            Count = count;
        }

        public int LineIndex { get; }

        public int Start { get; }

        public int Count { get; }
    }

    /// <summary>
    /// A word found while chunking: its parts are the <see cref="PartCount"/> from <see cref="FirstPart"/> among the
    /// page's parts.
    /// </summary>
    private readonly struct WordRange
    {
        public WordRange(PdfWordType type, int firstPart, int partCount)
        {
            Type = type;
            FirstPart = firstPart;
            PartCount = partCount;
        }

        public PdfWordType Type { get; }

        public int FirstPart { get; }

        public int PartCount { get; }
    }
}
