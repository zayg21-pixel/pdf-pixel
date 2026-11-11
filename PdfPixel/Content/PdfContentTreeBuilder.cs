using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Tagging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Content;

/// <summary>
/// Builds the content tree of scopes and content commands.
/// </summary>
public sealed class PdfContentTreeBuilder
{
    private readonly Stack<PdfContentNode> _openNodes = [];

    /// <summary>
    /// Initializes a new <see cref="PdfContentTreeBuilder"/> with an empty content tree.
    /// </summary>
    public PdfContentTreeBuilder()
    {
        Root = new PdfContentNode(parent: null, scope: null);
        _openNodes.Push(Root);
    }

    /// <summary>
    /// Root of the content tree, holding the elements drawn outside of any scope.
    /// </summary>
    public PdfContentNode Root { get; private set; }

    /// <summary>
    /// Starts a new, empty content tree.
    /// </summary>
    public void Reset()
    {
        _openNodes.Clear();
        Root = new PdfContentNode(parent: null, scope: null);
        _openNodes.Push(Root);
    }

    /// <summary>
    /// Opens a child node of the current node for <paramref name="scope"/>.
    /// </summary>
    public void OpenNode(PdfScope scope)
    {
        if (scope == null)
        {
            throw new ArgumentNullException(nameof(scope));
        }

        PdfContentNode parent = _openNodes.Peek();
        PdfContentNode node = new(parent, scope);
        parent.Add(node);
        _openNodes.Push(node);
    }

    /// <summary>
    /// Closes the current node when it is the node of <paramref name="scope"/>.
    /// </summary>
    public void CloseNode(PdfScope scope)
    {
        if (_openNodes.Count > 1 && ReferenceEquals(_openNodes.Peek().Scope, scope))
        {
            _openNodes.Pop();
        }
    }

    /// <summary>
    /// Appends <paramref name="command"/> to the current node of the content tree, placed under the total
    /// transformation matrix <paramref name="matrix"/>.
    /// </summary>
    public void AddContent(IPdfContentCommand command, in PdfMatrix matrix)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        PdfContentNode parent = _openNodes.Peek();
        PdfContentItem item = new(parent, command, matrix);
        parent.Add(item);
    }
}
