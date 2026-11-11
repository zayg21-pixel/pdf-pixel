using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Objects;
using System.Diagnostics;

namespace PdfPixel.Diagnostics;

// TODO: Temporary measurement of PdfFontSubstitutor alone (face loading, memory, lookups); remove together with its hook in Program.Main.
internal static class FontResolutionCheck
{
    private static readonly string[] FamilyNames = ["Arial", "TimesNewRomanPS-BoldMT", "NoSuchFontAtAll"];

    private static readonly string[] SampleTexts =
    [
        "A",
        "Ж",
        "中",
        "ع",
        "अ",
        "한",
        "ก",
        "\U0001F600",
        "\U0001D11E",
        "",
        "\U0010FFFD"
    ];

    public static int Run()
    {
        PdfFontSubstitutor substitutor = PdfFontSubstitutorFactory.Create(NullLoggerFactory.Instance);
        long managedBefore = GC.GetTotalMemory(forceFullCollection: true);

        Console.WriteLine($"{"family name",-26} {"first",12} {"repeat",12} {"stem",-20} match");

        foreach (string familyName in FamilyNames)
        {
            PdfSubstitutionInfo info = PdfSubstitutionInfo.Parse((PdfString)familyName, descriptor: null);
            Stopwatch stopwatch = Stopwatch.StartNew();
            SfntPdfTypeface? first = substitutor.ResolveByFamilyName(info);
            stopwatch.Stop();
            double firstMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            SfntPdfTypeface? repeat = substitutor.ResolveByFamilyName(info);
            stopwatch.Stop();

            string match = (first != null) ? first.Metrics.FontName.ToString() : "-";
            Console.WriteLine($"{familyName,-26} {firstMilliseconds,9:F3} ms {stopwatch.Elapsed.TotalMilliseconds,9:F3} ms {info.NormalizedStem,-20} {match}");

            first?.Dispose();
            repeat?.Dispose();
        }

        long managedAfter = GC.GetTotalMemory(forceFullCollection: true);
        Console.WriteLine($"managed heap after loading the faces +{(managedAfter - managedBefore) / 1048576.0:F1} MB");
        Console.WriteLine();
        Console.WriteLine($"{"codepoint",-10} {"first",12} {"repeat",12} match");

        PdfSubstitutionInfo arial = PdfSubstitutionInfo.Parse((PdfString)"Arial", descriptor: null);

        foreach (string text in SampleTexts)
        {
            int codepoint = text.EnumerateRunes().First().Value;
            Stopwatch stopwatch = Stopwatch.StartNew();
            SfntPdfTypeface? first = substitutor.ResolveByCharacter(arial, text);
            stopwatch.Stop();
            double firstMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            SfntPdfTypeface? repeat = substitutor.ResolveByCharacter(arial, text);
            stopwatch.Stop();

            string match = (first != null) ? first.Metrics.FamilyName.ToString() : "-";
            Console.WriteLine($"U+{codepoint:X5}    {firstMilliseconds,9:F3} ms {stopwatch.Elapsed.TotalMilliseconds,9:F3} ms {match}");

            first?.Dispose();
            repeat?.Dispose();
        }

        long managedFinal = GC.GetTotalMemory(forceFullCollection: true);
        Console.WriteLine($"managed heap after the character lookups +{(managedFinal - managedBefore) / 1048576.0:F1} MB");
        GC.KeepAlive(substitutor);

        return 0;
    }
}
