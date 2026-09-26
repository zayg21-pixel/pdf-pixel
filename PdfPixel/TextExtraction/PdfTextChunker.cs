using PdfPixel.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Groups a sequence of <see cref="PdfCharacter"/> instances into <see cref="PdfWord"/> tokens.
/// </summary>
public sealed class PdfTextChunker
{
    private const string Space = " ";
    private const string LineBreak = "\n";
    private const char SoftHyphen = '­';

    /// <summary>
    /// Horizontal gap between two characters on one line, as a fraction of character height, above which a space is generated.
    /// </summary>
    private const float WordGapRatio = 0.1f;

    /// <summary>
    /// Vertical distance between two character centers, as a fraction of character height, above which a line break is generated.
    /// </summary>
    private const float LineOffsetRatio = 0.5f;

    private readonly List<PdfWord> _words = [];
    private readonly List<PdfWordPart> _wordParts = [];
    private readonly List<PdfCharacter> _partCharacters = [];
    private PdfWordType _wordType;
    private int _lineIndex;
    private int _characterCount;

    /// <summary>
    /// Chunks a sequence of characters into words in reading order.
    /// Quote and other punctuation is a separate word. Whitespace is a <see cref="PdfWordType.Space"/> word, generated as a space
    /// between two words separated by a gap and as a line break at the end of a line when the characters have none.
    /// A word hyphenated at the end of a line continues in a new part on the next line.
    /// </summary>
    public PdfWord[] ChunkCharacters(PdfCharacter[] characters)
    {
        if (characters == null)
        {
            throw new ArgumentNullException(nameof(characters));
        }

        _words.Clear();
        _wordParts.Clear();
        _partCharacters.Clear();
        _lineIndex = 0;
        _characterCount = 0;

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
                        _partCharacters.Add(lineBreak);
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

        return _words.ToArray();
    }

    private void AppendCharacter(in PdfCharacter character, PdfWordType type)
    {
        bool hasWord = _partCharacters.Count > 0 || _wordParts.Count > 0;

        if (hasWord && (type != _wordType || type == PdfWordType.Punctuation))
        {
            FlushWord();
        }

        _wordType = type;
        _partCharacters.Add(character);
    }

    private void AddSeparator(in PdfCharacter separator)
    {
        FlushWord();
        _wordType = PdfWordType.Space;
        _partCharacters.Add(separator);
        FlushWord();
    }

    /// <summary>
    /// Whether <paramref name="current"/>, the first character of a new line, continues a word that ends
    /// in a letter and a hyphen on the previous line.
    /// </summary>
    private bool IsHyphenation(in PdfCharacter current)
    {
        int count = _partCharacters.Count;

        if (_wordType != PdfWordType.Normal || count < 2)
        {
            return false;
        }

        string? hyphenText = _partCharacters[count - 1].Text;
        string? letterText = _partCharacters[count - 2].Text;

        return IsHyphen(hyphenText)
            && IsLetter(letterText)
            && IsLetter(current.Text);
    }

    private void FlushPart()
    {
        if (_partCharacters.Count == 0)
        {
            return;
        }

        PdfRectangle bounds = _partCharacters[0].BoundingBox;

        for (int index = 1; index < _partCharacters.Count; index++)
        {
            bounds = PdfRectangle.Union(bounds, _partCharacters[index].BoundingBox);
        }

        _wordParts.Add(new PdfWordPart(bounds, _lineIndex, _characterCount, _partCharacters.ToArray()));
        _characterCount += _partCharacters.Count;
        _partCharacters.Clear();
    }

    private void FlushWord()
    {
        FlushPart();

        if (_wordParts.Count == 0)
        {
            return;
        }

        _words.Add(new PdfWord(_wordType, _wordParts.ToArray()));
        _wordParts.Clear();
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

    private static bool IsNewLine(in PdfRectangle previousBox, in PdfRectangle currentBox)
    {
        float height = Math.Max(previousBox.Height, currentBox.Height);

        return height > 0
            && Math.Abs(currentBox.MidY - previousBox.MidY) > height * LineOffsetRatio;
    }

    private static bool IsWordGap(in PdfRectangle previousBox, in PdfRectangle currentBox)
    {
        float height = Math.Max(previousBox.Height, currentBox.Height);

        return height > 0
            && (currentBox.Left - previousBox.Right > height * WordGapRatio || currentBox.Right < previousBox.Left);
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
}
