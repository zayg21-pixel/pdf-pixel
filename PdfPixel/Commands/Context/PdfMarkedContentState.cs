using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.TextExtraction;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands.Context;

/// <summary>
/// Tracks marked content scope during command execution and evaluates
/// whether content should be rendered based on optional content group visibility.
/// Visibility is recalculated on push/pop and cached for fast per-command checks.
/// Also maintains a text block tree for structured text extraction.
/// </summary>
public sealed class PdfMarkedContentState
{
    private readonly IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> _optionalContentGroups;
    private readonly Stack<PdfMarkedContent> _stack = [];
    private readonly List<PdfCharacter> _characterBuffer = [];
    private readonly PdfTextBlock _rootTextBlock;
    private bool _isContentVisible = true;
    private PdfTextBlock _currentTextBlock;

    /// <summary>
    /// Initializes a new <see cref="PdfMarkedContentState"/> with the document's optional content groups.
    /// </summary>
    public PdfMarkedContentState(IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> optionalContentGroups)
    {
        _optionalContentGroups = optionalContentGroups;
        _rootTextBlock = new PdfTextBlock();
        _currentTextBlock = _rootTextBlock;
    }

    /// <summary>
    /// Whether content at the current scope should be rendered.
    /// Recalculated when the stack changes.
    /// </summary>
    public bool IsContentVisible => _isContentVisible;

    /// <summary>
    /// Returns the root of the text block tree with every character appended so far.
    /// Child blocks are created on push, characters are appended to the current block via <see cref="AppendCharacters"/>.
    /// </summary>
    public PdfTextBlock GetRootTextBlock()
    {
        FlushCharacters();

        return _rootTextBlock;
    }

    /// <summary>
    /// Pushes a marked content scope and recalculates visibility.
    /// Opens a child text block only for scopes with text markup.
    /// </summary>
    public void Push(PdfMarkedContent markedContent)
    {
        if (markedContent == null)
        {
            throw new ArgumentNullException(nameof(markedContent));
        }

        _stack.Push(markedContent);
        RecalculateVisibility();

        if (markedContent.TextMarkup != null)
        {
            FlushCharacters();

            PdfTextBlock childBlock = new(markedContent.TextMarkup, _currentTextBlock);
            _currentTextBlock.Children.Add(childBlock);
            _currentTextBlock = childBlock;
        }
    }

    /// <summary>
    /// Pops the most recent marked content scope and recalculates visibility.
    /// Returns to the parent text block if the popped scope had text markup.
    /// </summary>
    public void Pop()
    {
        if (_stack.Count > 0)
        {
            PdfMarkedContent popped = _stack.Pop();
            RecalculateVisibility();

            if (popped.TextMarkup != null && _currentTextBlock.Parent != null)
            {
                FlushCharacters();

                _currentTextBlock = _currentTextBlock.Parent;
            }
        }
    }

    /// <summary>
    /// Appends characters to the current text block, mapping each bounding box through <paramref name="matrix"/>.
    /// Skipped when content is hidden by optional content visibility.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void AppendCharacters(in PdfMatrix matrix, PdfCharacter[] characters)
    {
        if (characters == null)
        {
            throw new ArgumentNullException(nameof(characters));
        }

        if (!_isContentVisible)
        {
            return;
        }

        foreach (PdfCharacter character in characters)
        {
            _characterBuffer.Add(new PdfCharacter(character.Text, matrix.MapRect(character.BoundingBox)));
        }
    }

    /// <summary>
    /// Returns true if the given command should be executed.
    /// Begin/end marked content commands always execute to maintain stack balance.
    /// </summary>
    public bool ShouldExecute(IPdfCommand command)
    {
        if (command is BeginMarkedContentCommand || command is EndMarkedContentCommand)
        {
            return true;
        }

        return _isContentVisible;
    }

    private void FlushCharacters()
    {
        if (_characterBuffer.Count == 0)
        {
            return;
        }

        PdfCharacter[] existingCharacters = _currentTextBlock.Characters;
        var combinedCharacters = new PdfCharacter[existingCharacters.Length + _characterBuffer.Count];

        Array.Copy(existingCharacters, combinedCharacters, existingCharacters.Length);
        _characterBuffer.CopyTo(combinedCharacters, existingCharacters.Length);

        _currentTextBlock.Characters = combinedCharacters;
        _characterBuffer.Clear();
    }

    private void RecalculateVisibility()
    {
        if (_stack.Count == 0)
        {
            _isContentVisible = true;
            return;
        }

        foreach (PdfMarkedContent markedContent in _stack)
        {
            if (markedContent.OptionalContent != null && !IsOptionalContentVisible(markedContent.OptionalContent))
            {
                _isContentVisible = false;
                return;
            }
        }

        _isContentVisible = true;
    }

    private bool IsOptionalContentVisible(PdfOptionalContentMembership membership)
    {
        if (membership.VisibilityExpression != null)
        {
            return EvaluateVisibilityExpression(membership.VisibilityExpression);
        }

        IReadOnlyList<PdfReference> groups = membership.Groups;
        if (groups.Count == 0)
        {
            return true;
        }

        return membership.VisibilityPolicy switch
        {
            PdfOptionalContentVisibilityPolicy.AllOn => AreAllVisible(groups),
            PdfOptionalContentVisibilityPolicy.AnyOn => IsAnyVisible(groups),
            PdfOptionalContentVisibilityPolicy.AnyOff => IsAnyHidden(groups),
            PdfOptionalContentVisibilityPolicy.AllOff => AreAllHidden(groups),
            _ => true
        };
    }

    private bool EvaluateVisibilityExpression(PdfVisibilityExpression expression)
    {
        if (expression.Type == PdfVisibilityExpressionType.Group)
        {
            return GetGroupVisibility(expression.Group);
        }

        IReadOnlyList<PdfVisibilityExpression> operands = expression.Operands;

        switch (expression.Operator)
        {
            case PdfVisibilityExpressionOperator.And:
            {
                foreach (PdfVisibilityExpression operand in operands)
                {
                    if (!EvaluateVisibilityExpression(operand))
                    {
                        return false;
                    }
                }

                return true;
            }
            case PdfVisibilityExpressionOperator.Or:
            {
                foreach (PdfVisibilityExpression operand in operands)
                {
                    if (EvaluateVisibilityExpression(operand))
                    {
                        return true;
                    }
                }

                return false;
            }
            case PdfVisibilityExpressionOperator.Not:
            {
                return operands.Count > 0 && !EvaluateVisibilityExpression(operands[0]);
            }
            default:
            {
                return true;
            }
        }
    }

    private bool GetGroupVisibility(in PdfReference reference)
    {
        if (_optionalContentGroups.TryGetValue(reference, out PdfOptionalContentGroup? group))
        {
            return group.Visible ?? group.DefaultVisible ?? true;
        }

        return true;
    }

    private bool AreAllVisible(IReadOnlyList<PdfReference> groups)
    {
        for (int index = 0; index < groups.Count; index++)
        {
            if (!GetGroupVisibility(groups[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsAnyVisible(IReadOnlyList<PdfReference> groups)
    {
        for (int index = 0; index < groups.Count; index++)
        {
            if (GetGroupVisibility(groups[index]))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAnyHidden(IReadOnlyList<PdfReference> groups)
    {
        for (int index = 0; index < groups.Count; index++)
        {
            if (!GetGroupVisibility(groups[index]))
            {
                return true;
            }
        }

        return false;
    }

    private bool AreAllHidden(IReadOnlyList<PdfReference> groups)
    {
        for (int index = 0; index < groups.Count; index++)
        {
            if (GetGroupVisibility(groups[index]))
            {
                return false;
            }
        }

        return true;
    }
}
