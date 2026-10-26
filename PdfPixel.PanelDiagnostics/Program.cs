using PdfPixel.PanelDiagnostics.Commands;
using System.CommandLine;

namespace PdfPixel.PanelDiagnostics;

internal sealed class Program
{
    private static int Main(string[] args)
    {
        Argument<FileInfo> pdfArgument = new("pdf")
        {
            Description = "Path to the PDF to open in the panel."
        };

        Argument<FileInfo> scriptArgument = new("script")
        {
            Description = "Path to the script to replay, one command per line: size <w> <h>, zoom <scale>, scroll <x> <y>, scroll_relative <dx> <dy>, page <n>, sleep <seconds>."
        };

        RootCommand rootCommand = new("Replays a script of user interactions against a PDF panel, logging frames and blocked time.")
        {
            pdfArgument,
            scriptArgument
        };

        rootCommand.SetAction(parseResult =>
        {
            FileInfo? pdfFile = parseResult.GetValue(pdfArgument);
            FileInfo? scriptFile = parseResult.GetValue(scriptArgument);

            if (pdfFile == null || scriptFile == null)
            {
                return 1;
            }

            ReplayCommand.Run(pdfFile, scriptFile);

            return 0;
        });

        return rootCommand.Parse(args).Invoke();
    }
}
