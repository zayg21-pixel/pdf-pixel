using System;

namespace PdfPixel.Examples;

/// <summary>
/// Runs every example in turn. Each one reads from <c>Input</c> and writes a PNG to <c>Output</c>,
/// both next to the executable.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("PdfPixel examples");
        Console.WriteLine($"  input:  {ExamplePaths.InputRoot}");
        Console.WriteLine($"  output: {ExamplePaths.OutputRoot}");
        Console.WriteLine();

        PdfExamples.Run();
        JpgExamples.Run();
        JpxExamples.Run();
        Jbig2Examples.Run();
        CcittExamples.Run();

        Console.WriteLine();
        Console.WriteLine("Done.");
    }
}
