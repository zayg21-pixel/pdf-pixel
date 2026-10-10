using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a named action that executes an action predefined by the viewer.
/// </summary>
public class PdfNamedAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfNamedAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the named action.</param>
    internal PdfNamedAction(PdfDictionary actionDictionary)
        : base(PdfActionType.Named)
    {
        PdfString? name = actionDictionary.GetName(PdfTokens.NamedActionNameKey);
        Name = name?.AsEnum<PdfNamedActionName>();
        RawName = (Name == PdfNamedActionName.Raw) ? name : null;
    }

    /// <summary>
    /// Name of the action to perform (/N), or <see langword="null"/> when absent.
    /// </summary>
    public PdfNamedActionName? Name { get; }

    /// <summary>
    /// Name as written when <see cref="Name"/> is <see cref="PdfNamedActionName.Raw"/>, otherwise <see langword="null"/>.
    /// </summary>
    public PdfString? RawName { get; }

    /// <summary>
    /// Returns a string representation of this named action.
    /// </summary>
    /// <returns>A string containing the action name.</returns>
    public override string ToString() => $"Named Action: {RawName?.ToString() ?? Name?.ToString()}";
}
