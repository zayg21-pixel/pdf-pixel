using Microsoft.Extensions.Logging;
using PdfPixel.Objects;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfPixel.Fonts.Substitution;

/// <summary>
/// Loads fonts from the font files found in a set of directories, such as the installed system fonts. Every face is
/// loaded as a typeface on first use and matched by its metrics; the typeface returned is a new instance of the face
/// chosen.
/// </summary>
public sealed class PdfFontSubstitutor : IFontSubstitutor
{
    private const int CollectionHeaderLength = 12;
    private const int CollectionFontCountOffset = 8;
    private const uint CollectionTag = 0x74746366;
    private const int ItalicMismatchPenalty = 10000;
    private const int WeightPenaltyFactor = 10;

    private static readonly string[] FontFileExtensions = [".ttf", ".otf", ".ttc", ".otc"];

    private readonly string[] _fontDirectories;
    private readonly string _fallbackFamily;
    private readonly ILoggerFactory _loggerFactory;
    private readonly object _locker = new();
    private readonly Lazy<FontFace[]> _faces;

    /// <summary>
    /// Initializes a substitutor loading the fonts found in <paramref name="fontDirectories"/> and their
    /// subdirectories.
    /// </summary>
    /// <param name="fontDirectories">The directories to look for font files in; earlier directories win ties.</param>
    /// <param name="fallbackFamily">The family loaded as the last resort; it must be among the fonts found.</param>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public PdfFontSubstitutor(string[] fontDirectories, string fallbackFamily, ILoggerFactory loggerFactory)
    {
        _fontDirectories = fontDirectories ?? throw new ArgumentNullException(nameof(fontDirectories));
        _fallbackFamily = fallbackFamily ?? throw new ArgumentNullException(nameof(fallbackFamily));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _faces = new Lazy<FontFace[]>(LoadFaces);
    }

    /// <inheritdoc />
    public SfntPdfTypeface? ResolveByFamilyName(in PdfSubstitutionInfo substitutionInfo)
    {
        string stem = substitutionInfo.NormalizedStem;
        FontFace? bestFace = null;
        int bestDistance = int.MaxValue;

        foreach (FontFace face in _faces.Value)
        {
            if (!face.IsNamed(stem))
            {
                continue;
            }

            int distance = GetStyleDistance(face.Typeface.Metrics, substitutionInfo);

            if (distance < bestDistance)
            {
                bestFace = face;
                bestDistance = distance;
            }
        }

        if (bestFace == null)
        {
            return null;
        }

        return CreateTypeface(bestFace);
    }

    /// <inheritdoc />
    public SfntPdfTypeface? ResolveByCharacter(in PdfSubstitutionInfo substitutionInfo, string unicode)
    {
        if (unicode == null)
        {
            throw new ArgumentNullException(nameof(unicode));
        }

        string stem = substitutionInfo.NormalizedStem;
        PdfSubstitutionInfo style = substitutionInfo;
        IEnumerable<FontFace> candidates = _faces.Value
            .OrderBy(face => (face.IsNamed(stem)) ? 0 : 1)
            .ThenBy(face => GetStyleDistance(face.Typeface.Metrics, style));

        lock (_locker)
        {
            foreach (FontFace face in candidates)
            {
                if (face.Typeface.GetGid(unicode) != null)
                {
                    return CreateTypeface(face);
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public SfntPdfTypeface GetFallbackTypeface()
    {
        PdfSubstitutionInfo fallbackInfo = new(_fallbackFamily, PdfSubstitutionInfo.NormalWeight, PdfSubstitutionInfo.NormalWidth, italicAngle: 0f);

        return ResolveByFamilyName(fallbackInfo) ?? throw new InvalidOperationException($"The fallback family {_fallbackFamily} is not among the fonts found.");
    }

    /// <summary>
    /// Returns how far the style of <paramref name="metrics"/> is from the requested one: italic first, then weight,
    /// then width.
    /// </summary>
    private static int GetStyleDistance(PdfFontMetrics metrics, in PdfSubstitutionInfo substitutionInfo)
    {
        int italicMismatch = (metrics.IsItalic != substitutionInfo.IsItalic) ? ItalicMismatchPenalty : 0;
        int weightDistance = Math.Abs(metrics.Weight - substitutionInfo.Weight) * WeightPenaltyFactor;
        int widthDistance = Math.Abs(metrics.Width - substitutionInfo.Width);

        return italicMismatch + weightDistance + widthDistance;
    }

    private static int GetFaceCount(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = new byte[CollectionHeaderLength];

        if (stream.Read(header, 0, header.Length) < CollectionHeaderLength || BinaryPrimitives.ReadUInt32BigEndian(header) != CollectionTag)
        {
            return 1;
        }

        return (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(CollectionFontCountOffset));
    }

    private static IEnumerable<string> GetFontFiles(string directory)
    {
        if (Directory.Exists(directory))
        {
            return Directory
                .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => FontFileExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Loads every face of the font files in the directories as a typeface.
    /// </summary>
    private FontFace[] LoadFaces()
    {
        List<FontFace> faces = [];

        foreach (string path in _fontDirectories.SelectMany(GetFontFiles))
        {
            int faceCount;

            try
            {
                faceCount = GetFaceCount(path);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
            {
                SfntPdfTypeface? typeface = LoadTypeface(path, faceIndex);

                if (typeface != null)
                {
                    faces.Add(new FontFace(path, faceIndex, typeface));
                }
            }
        }

        return faces.ToArray();
    }

    /// <summary>
    /// Creates a new typeface of <paramref name="face"/>, owned by the caller.
    /// </summary>
    private SfntPdfTypeface? CreateTypeface(FontFace face) => LoadTypeface(face.Path, face.FaceIndex);

    /// <summary>
    /// Loads the face at <paramref name="faceIndex"/> of the font file at <paramref name="path"/>, or returns
    /// <see langword="null"/> when it cannot be read as a typeface.
    /// </summary>
    private SfntPdfTypeface? LoadTypeface(string path, int faceIndex)
    {
        SfntPdfTypefaceParameters parameters = new() { RepackTypeface = false, TtcIndex = faceIndex, IsSystemFont = true };
        FileStream? stream = null;

        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            return new SfntPdfTypeface(stream, _loggerFactory, parameters);
        }
        catch (ArgumentException)
        {
            stream?.Dispose();
            return null;
        }
        catch (IOException)
        {
            stream?.Dispose();
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            stream?.Dispose();
            return null;
        }
    }

    /// <summary>
    /// One face of a font file, loaded as a typeface.
    /// </summary>
    private sealed class FontFace
    {
        public FontFace(string path, int faceIndex, SfntPdfTypeface typeface)
        {
            Path = path;
            FaceIndex = faceIndex;
            Typeface = typeface;
            FamilyName = typeface.Metrics.FamilyName.ToString();
            PostScriptStem = PdfSubstitutionInfo.Parse((PdfString)typeface.Metrics.FontName.ToString(), descriptor: null).NormalizedStem;
        }

        public string Path { get; }

        public int FaceIndex { get; }

        public SfntPdfTypeface Typeface { get; }

        public string FamilyName { get; }

        public string PostScriptStem { get; }

        /// <summary>
        /// Returns whether <paramref name="stem"/> names this face's family or, normalized, its font name.
        /// </summary>
        public bool IsNamed(string stem)
        {
            return string.Equals(FamilyName, stem, StringComparison.OrdinalIgnoreCase)
                || string.Equals(PostScriptStem, stem, StringComparison.OrdinalIgnoreCase);
        }
    }
}
