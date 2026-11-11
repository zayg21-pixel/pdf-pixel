using PdfPixel.Commands;
using PdfPixel.OptionalContent;
using PdfPixel.Tagging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Content;

/// <summary>
/// Tracks scopes during command execution and evaluates whether content should be rendered based on optional
/// content group visibility.
/// Visibility is recalculated on push/pop and cached for fast per-command checks.
/// </summary>
public sealed class PdfContentScopeState
{
    private readonly PdfOptionalContentStates _groupStates;
    private readonly Stack<PdfScope> _stack = [];
    private bool _isContentVisible = true;

    /// <summary>
    /// Initializes a new <see cref="PdfContentScopeState"/> with the ON/OFF states of the optional content groups that
    /// take part in visibility; groups not listed have no effect.
    /// </summary>
    public PdfContentScopeState(PdfOptionalContentStates groupStates) => _groupStates = groupStates;

    /// <summary>
    /// Whether content at the current scope should be rendered.
    /// Recalculated when the stack changes.
    /// </summary>
    public bool IsContentVisible => _isContentVisible;

    /// <summary>
    /// The open scopes, the innermost first.
    /// </summary>
    public IReadOnlyCollection<PdfScope> Scopes => _stack;

    /// <summary>
    /// Clears the open scopes.
    /// </summary>
    public void Reset()
    {
        _stack.Clear();
        _isContentVisible = true;
    }

    /// <summary>
    /// Pushes a scope and recalculates visibility.
    /// </summary>
    public void Push(PdfScope scope)
    {
        if (scope == null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        _stack.Push(scope);
        RecalculateVisibility();
    }

    /// <summary>
    /// Pops the most recent scope and recalculates visibility; returns the scope, or <see langword="null"/> when none
    /// is open.
    /// </summary>
    public PdfScope? Pop()
    {
        if (_stack.Count == 0)
        {
            return null;
        }

        PdfScope scope = _stack.Pop();
        RecalculateVisibility();

        return scope;
    }

    /// <summary>
    /// Returns true if the given command should be executed.
    /// Begin/end scope commands always execute to maintain stack balance.
    /// </summary>
    public bool ShouldExecute(IPdfCommand command)
    {
        if (command is BeginScopeCommand || command is EndScopeCommand)
        {
            return true;
        }

        return _isContentVisible;
    }

    private void RecalculateVisibility()
    {
        _isContentVisible = true;

        foreach (PdfScope scope in _stack)
        {
            if (scope is PdfContentScope contentScope
                && contentScope.OptionalContent != null
                && !_groupStates.IsVisible(contentScope.OptionalContent))
            {
                _isContentVisible = false;
            }
        }
    }
}
