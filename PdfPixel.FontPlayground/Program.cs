using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Management;
using PdfPixel.Fonts.Model;
using System;
using System.Diagnostics;

namespace PdfPixel.FontPlayground;

internal static class Program
{
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();

    private static void Main()
    {
        (PdfSubstitutionInfo SubstitutionInfo, string Unicode, string Description)[] cases =
        [
            (new PdfSubstitutionInfo(PdfStandardFontName.Times, false, false), "a", "Times Regular"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Times, true, false), "a", "Times Bold"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Times, false, true), "a", "Times Italic"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Times, true, true), "a", "Times BoldItalic"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Helvetica, false, false), "a", "Helvetica Regular"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Helvetica, true, false), "a", "Helvetica Bold"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Helvetica, false, true), "a", "Helvetica Italic"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Helvetica, true, true), "a", "Helvetica BoldItalic"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Courier, false, false), "a", "Courier Regular"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Courier, true, false), "a", "Courier Bold"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Courier, false, true), "a", "Courier Italic"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Courier, true, true), "a", "Courier BoldItalic"),
            (new PdfSubstitutionInfo(PdfStandardFontName.Symbol, false, false), "a", "Symbol"),
            (new PdfSubstitutionInfo(PdfStandardFontName.ZapfDingbats, false, false), "a", "ZapfDingbats"),
            (new PdfSubstitutionInfo("Verdana", false, false), "a", "non-standard14 char ('a')"),
            (new PdfSubstitutionInfo("Courier New", false, false), char.ConvertFromUtf32(0x20000), "obscure CJK char (U+20000)")
        ];

        MemorySnapshot memoryBefore = MeasureMemory();
        MemorySnapshot previousMemory = memoryBefore;

        Stopwatch stopwatch = Stopwatch.StartNew();
        WindowsFontProvider fontProvider = new(NullLoggerFactory.Instance);
        previousMemory = ReportStep("WindowsFontProvider construction", previousMemory);

        foreach ((PdfSubstitutionInfo substitutionInfo, string unicode, string description) in cases)
        {
            IPdfTypeface typeface = fontProvider.GetTypeface(substitutionInfo, unicode, width: null);
            ushort? gid = typeface.GetGid(unicode);
            bool gidExists = gid.HasValue && typeface.IsGidExists(gid.Value);

            Console.WriteLine($"{description}");
            Console.WriteLine($"  Font resolved: {typeface.Metrics.FontName}");
            Console.WriteLine($"  GID: {(gid.HasValue ? gid.Value : "none")}");
            Console.WriteLine($"  GID exists in font: {gidExists}");

            previousMemory = ReportStep($"  after resolving '{description}'", previousMemory);
        }

        stopwatch.Stop();
        MemorySnapshot memoryAfter = MeasureMemory();

        Console.WriteLine();
        Console.WriteLine($"Overall time (provider creation to last char resolved): {stopwatch.Elapsed.TotalMilliseconds} ms");
        Console.WriteLine($"Managed memory before: {memoryBefore.ManagedBytes / 1024} kbytes, after: {memoryAfter.ManagedBytes / 1024} kbytes, used: {(memoryAfter.ManagedBytes - memoryBefore.ManagedBytes) / 1024} kbytes");
        Console.WriteLine($"Process working set before: {memoryBefore.WorkingSetBytes / 1024} kbytes, after: {memoryAfter.WorkingSetBytes / 1024} kbytes, used: {(memoryAfter.WorkingSetBytes - memoryBefore.WorkingSetBytes) / 1024} kbytes");
        Console.WriteLine($"Process private bytes before: {memoryBefore.PrivateBytes / 1024} kbytes, after: {memoryAfter.PrivateBytes / 1024} kbytes, used: {(memoryAfter.PrivateBytes - memoryBefore.PrivateBytes) / 1024} kbytes");

        fontProvider.Dispose();
    }

    private static MemorySnapshot MeasureMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long managedBytes = GC.GetTotalMemory(true);

        CurrentProcess.Refresh();
        return new MemorySnapshot(managedBytes, CurrentProcess.WorkingSet64, CurrentProcess.PrivateMemorySize64);
    }

    private static MemorySnapshot ReportStep(string label, MemorySnapshot previousMemory)
    {
        MemorySnapshot currentMemory = MeasureMemory();
        Console.WriteLine(
            $"[memory] {label}: managed {currentMemory.ManagedBytes / 1024} kbytes (delta {(currentMemory.ManagedBytes - previousMemory.ManagedBytes) / 1024} kbytes), " +
            $"working set {currentMemory.WorkingSetBytes / 1024} kbytes (delta {(currentMemory.WorkingSetBytes - previousMemory.WorkingSetBytes) / 1024} kbytes), " +
            $"private bytes {currentMemory.PrivateBytes / 1024} kbytes (delta {(currentMemory.PrivateBytes - previousMemory.PrivateBytes) / 1024} kbytes)");
        return currentMemory;
    }

    private readonly struct MemorySnapshot
    {
        public MemorySnapshot(long managedBytes, long workingSetBytes, long privateBytes)
        {
            ManagedBytes = managedBytes;
            WorkingSetBytes = workingSetBytes;
            PrivateBytes = privateBytes;
        }

        public long ManagedBytes { get; }

        public long WorkingSetBytes { get; }

        public long PrivateBytes { get; }
    }
}
