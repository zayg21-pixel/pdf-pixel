using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a Launch action that launches an application or opens or prints a document.
/// </summary>
public class PdfLaunchAction : PdfFileAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLaunchAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the Launch action.</param>
    internal PdfLaunchAction(PdfDictionary actionDictionary)
        : base(PdfActionType.Launch, actionDictionary)
    {
        Windows = PdfWindowsLaunchParameters.FromDictionary(actionDictionary.GetDictionary(PdfTokens.WindowsLaunchKey));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLaunchAction"/> class that opens <paramref name="fileSpecification"/>.
    /// </summary>
    /// <param name="fileSpecification">The file to open.</param>
    internal PdfLaunchAction(PdfFileSpecification fileSpecification)
        : base(PdfActionType.Launch, fileSpecification)
    {
    }

    /// <summary>
    /// Windows-specific launch parameters (/Win), or <see langword="null"/> when absent.
    /// </summary>
    public PdfWindowsLaunchParameters? Windows { get; }

    /// <summary>
    /// Returns a string representation of this Launch action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "Launch Action";
}
