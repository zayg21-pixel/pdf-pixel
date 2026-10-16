using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Windows-specific parameters of a launch action (/Win, deprecated in PDF 2.0).
/// </summary>
public sealed class PdfWindowsLaunchParameters
{
    private PdfWindowsLaunchParameters(PdfDictionary parametersDictionary, in PdfString fileName)
    {
        FileName = fileName;
        DefaultDirectory = parametersDictionary.GetString(PdfTokens.LaunchDirectoryKey);
        Operation = parametersDictionary.GetString(PdfTokens.LaunchOperationKey)?.AsEnum<PdfLaunchOperation>();
        Parameters = parametersDictionary.GetString(PdfTokens.LaunchParametersKey);
    }

    /// <summary>
    /// Application to launch or document to open or print (/F), as a Windows path.
    /// </summary>
    public PdfString FileName { get; }

    /// <summary>
    /// Default directory (/D), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? DefaultDirectory { get; }

    /// <summary>
    /// Operation on the document (/O), or <see langword="null"/> when absent; the document is then opened.
    /// </summary>
    public PdfLaunchOperation? Operation { get; }

    /// <summary>
    /// Parameters passed to the application (/P), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Parameters { get; }

    /// <summary>
    /// The parameters held by <paramref name="parametersDictionary"/>, or <see langword="null"/> when it is
    /// absent or has no file name.
    /// </summary>
    internal static PdfWindowsLaunchParameters? FromDictionary(PdfDictionary? parametersDictionary)
    {
        PdfString? fileName = parametersDictionary?.GetString(PdfTokens.FKey);
        if (parametersDictionary == null || fileName == null)
        {
            return null;
        }

        return new PdfWindowsLaunchParameters(parametersDictionary, fileName.Value);
    }
}
