using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Sfnt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Indexes installed Windows font files by family name and style (bold/italic), reading only the
/// "name" and "OS/2" tables of each file - via <see cref="SfntContainerProcessor.ReadFiltered"/> - rather
/// than parsing the whole file.
/// </summary>
internal sealed class WindowsFontDirectoryIndex
{
    private const ushort WindowsPlatformId = 3;
    private const ushort WindowsUnicodeBmpEncodingId = 1;
    private const ushort FamilyNameId = 1;
    private const ushort BoldSelectionBit = 0x0020;
    private const ushort ItalicSelectionBit = 0x0001;

    private static readonly string[] FontFileExtensions = [".ttf", ".ttc", ".otf"];

    private readonly Dictionary<PdfSubstitutionInfo, string> _fontFilesByKey = [];

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsFontDirectoryIndex"/>, scanning every installed
    /// font file under the Windows Fonts folder.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public WindowsFontDirectoryIndex(ILoggerFactory loggerFactory)
    {
        SfntContainerProcessor containerProcessor = new(loggerFactory.CreateLogger<SfntContainerProcessor>());
        SfntNameProcessor nameProcessor = new(loggerFactory.CreateLogger<SfntNameProcessor>());
        SfntOs2Processor os2Processor = new(loggerFactory.CreateLogger<SfntOs2Processor>());

        string fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!Directory.Exists(fontsDirectory))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(fontsDirectory))
        {
            if (Array.IndexOf(FontFileExtensions, Path.GetExtension(filePath).ToLowerInvariant()) < 0)
            {
                continue;
            }

            IndexFontFile(filePath, containerProcessor, nameProcessor, os2Processor);
        }
    }

    /// <summary>
    /// Finds the font file path for the given family name and style, or <see langword="null"/> if no
    /// installed font matches.
    /// </summary>
    /// <param name="familyName">The font family name to look up.</param>
    /// <param name="isBold">Whether the bold variant is requested.</param>
    /// <param name="isItalic">Whether the italic variant is requested.</param>
    public string? FindFontFile(string familyName, bool isBold, bool isItalic) =>
        _fontFilesByKey.TryGetValue(new PdfSubstitutionInfo(familyName, isBold, isItalic), out string? filePath) ? filePath : null;

    private void IndexFontFile(string filePath, SfntContainerProcessor containerProcessor, SfntNameProcessor nameProcessor, SfntOs2Processor os2Processor)
    {
        try
        {
            using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            SfntContainer? container = containerProcessor.ReadFiltered(fileStream, IsNameOrOs2Table);
            SfntTableRecord? nameRecord = container?.FindTable(SfntTableTags.Name);
            if (nameRecord == null)
            {
                return;
            }

            SfntName? name = nameProcessor.Read(nameRecord.Value.Data);
            string? familyName = FindFamilyName(name);
            if (string.IsNullOrEmpty(familyName))
            {
                return;
            }

            var isBold = false;
            var isItalic = false;
            SfntTableRecord? os2Record = container?.FindTable(SfntTableTags.Os2);
            if (os2Record != null)
            {
                SfntOs2? os2 = os2Processor.Read(os2Record.Value.Data);
                if (os2 != null)
                {
                    isBold = (os2.FsSelection & BoldSelectionBit) != 0;
                    isItalic = (os2.FsSelection & ItalicSelectionBit) != 0;
                }
            }

            _fontFilesByKey[new PdfSubstitutionInfo(familyName, isBold, isItalic)] = filePath;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsNameOrOs2Table(SfntTableTag tag) => tag == SfntTableTags.Name || tag == SfntTableTags.Os2;

    private static string? FindFamilyName(SfntName? name)
    {
        if (name == null)
        {
            return null;
        }

        foreach (SfntNameRecord record in name.Records)
        {
            if (record.NameId == FamilyNameId && record.PlatformId == WindowsPlatformId && record.EncodingId == WindowsUnicodeBmpEncodingId)
            {
                return Encoding.BigEndianUnicode.GetString(record.Value.ToArray());
            }
        }

        return null;
    }
}
