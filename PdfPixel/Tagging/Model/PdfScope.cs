namespace PdfPixel.Tagging.Model;

/// <summary>
/// A scope enclosing part of the content drawn during command execution.
/// </summary>
public abstract class PdfScope
{
    /// <summary>
    /// Identifies the concrete type of this scope.
    /// </summary>
    public abstract PdfScopeType ScopeType { get; }
}
