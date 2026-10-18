using PdfPixel.Commands.Model;
using PdfPixel.Content.Model;
using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Tagging.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Context;

/// <summary>
/// Tracks scopes during command execution, evaluates
/// whether content should be rendered based on optional content group visibility
/// and builds the content tree of the visible scopes and content commands.
/// Visibility is recalculated on push/pop and cached for fast per-command checks.
/// </summary>
public sealed class PdfContentTreeBuilder
{
    private readonly PdfOptionalContentStates _groupStates;
    private readonly Stack<PdfScope> _stack = [];
    private readonly Stack<bool> _scopeOpenedNode = [];
    private readonly Stack<PdfContentNode> _openNodes = [];
    private bool _isContentVisible = true;

    /// <summary>
    /// Initializes a new <see cref="PdfContentTreeBuilder"/> with the ON/OFF states of the optional content
    /// groups that take part in visibility; groups not listed have no effect.
    /// </summary>
    public PdfContentTreeBuilder(PdfOptionalContentStates groupStates)
    {
        _groupStates = groupStates;
        Root = new PdfContentNode(parent: null, scope: null);
        _openNodes.Push(Root);
    }

    /// <summary>
    /// Whether content at the current scope should be rendered.
    /// Recalculated when the stack changes.
    /// </summary>
    public bool IsContentVisible => _isContentVisible;

    /// <summary>
    /// Root of the content tree, holding the elements drawn outside of any scope.
    /// </summary>
    public PdfContentNode Root { get; }

    /// <summary>
    /// Pushes a scope and recalculates visibility.
    /// Opens a child node of the content tree when the scope is visible.
    /// </summary>
    public void Push(PdfScope scope)
    {
        if (scope == null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        _stack.Push(scope);
        RecalculateVisibility();

        _scopeOpenedNode.Push(_isContentVisible);

        if (_isContentVisible)
        {
            PdfContentNode parent = _openNodes.Peek();
            PdfContentNode node = new(parent, scope);
            parent.Add(node);
            _openNodes.Push(node);
        }
    }

    /// <summary>
    /// Pops the most recent scope and recalculates visibility.
    /// Closes the node of the content tree the scope opened.
    /// </summary>
    public void Pop()
    {
        if (_stack.Count > 0)
        {
            _stack.Pop();
            RecalculateVisibility();

            if (_scopeOpenedNode.Pop())
            {
                _openNodes.Pop();
            }
        }
    }

    /// <summary>
    /// Appends <paramref name="command"/> to the current node of the content tree, placed under the total
    /// transformation matrix and clip of <paramref name="frames"/>.
    /// </summary>
    public void AddContent(IPdfContentCommand command, PdfCommandExecutionFrames frames)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (frames == null)
        {
            throw new ArgumentNullException(nameof(frames));
        }

        PdfContentNode parent = _openNodes.Peek();
        PdfContentItem item = new(parent, command, frames.TotalMatrix, frames.CurrentClip);
        parent.Add(item);
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
        if (_stack.Count == 0)
        {
            _isContentVisible = true;
            return;
        }

        foreach (PdfScope scope in _stack)
        {
            if (scope is PdfContentScope contentScope
                && contentScope.OptionalContent != null
                && !IsOptionalContentVisible(contentScope.OptionalContent))
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

        int onCount = 0;
        int offCount = 0;

        foreach (PdfReference group in membership.Groups)
        {
            bool? state = _groupStates.GetState(group);

            if (state == null)
            {
                continue;
            }

            if (state.Value)
            {
                onCount++;
            }
            else
            {
                offCount++;
            }
        }

        if (onCount + offCount == 0)
        {
            return true;
        }

        return membership.VisibilityPolicy switch
        {
            PdfOptionalContentVisibilityPolicy.AllOn => offCount == 0,
            PdfOptionalContentVisibilityPolicy.AnyOn => onCount > 0,
            PdfOptionalContentVisibilityPolicy.AnyOff => offCount > 0,
            PdfOptionalContentVisibilityPolicy.AllOff => onCount == 0,
            _ => true
        };
    }

    private bool EvaluateVisibilityExpression(PdfVisibilityExpression expression)
    {
        if (expression.GroupReference != null)
        {
            return GetGroupVisibility(expression.GroupReference.Value);
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
        bool? state = _groupStates.GetState(reference);

        if (state == null)
        {
            return true;
        }

        return state.Value;
    }
}
