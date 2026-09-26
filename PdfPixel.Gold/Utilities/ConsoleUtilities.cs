namespace PdfPixel.Gold.Utilities;

/// <summary>
/// Console output shared by every command.
/// </summary>
internal static class ConsoleUtilities
{
    /// <summary>
    /// Writes one line in the given color.
    /// </summary>
    public static void Write(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
