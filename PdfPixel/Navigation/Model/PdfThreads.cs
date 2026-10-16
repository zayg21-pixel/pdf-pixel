using PdfPixel.Actions.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// The article threads of a document (/Threads).
/// </summary>
public sealed class PdfThreads
{
    private PdfThreads(List<PdfThread> items) => Items = items;

    /// <summary>
    /// Threads in order. Empty if the array has none.
    /// </summary>
    public IReadOnlyList<PdfThread> Items { get; }

    /// <summary>
    /// Returns the thread of <paramref name="action"/>: the thread with its reference, the thread at its index, or
    /// the first thread with its title; <see langword="null"/> when not found.
    /// </summary>
    /// <param name="action">The thread action.</param>
    public PdfThread? ResolveThread(PdfThreadAction action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (action.ThreadReference != null)
        {
            return Items.FirstOrDefault(thread => thread.Reference.Equals(action.ThreadReference.Value));
        }

        if (action.ThreadIndex != null)
        {
            int index = action.ThreadIndex.Value;
            return (index >= 0 && index < Items.Count) ? Items[index] : null;
        }

        if (action.ThreadTitle != null)
        {
            return Items.FirstOrDefault(thread => thread.Information?.Title != null && thread.Information.Title.Value.Equals(action.ThreadTitle.Value));
        }

        return null;
    }

    /// <summary>
    /// Returns the bead of <paramref name="action"/> in its thread: the bead with its reference, the bead at its
    /// index, or the first bead when the action names none; <see langword="null"/> when not found.
    /// </summary>
    /// <param name="action">The thread action.</param>
    public PdfBead? ResolveBead(PdfThreadAction action)
    {
        PdfThread? thread = ResolveThread(action);
        if (thread == null)
        {
            return null;
        }

        if (action.BeadReference != null)
        {
            return thread.Beads.FirstOrDefault(bead => bead.Reference.Equals(action.BeadReference.Value));
        }

        if (action.BeadIndex != null)
        {
            int index = action.BeadIndex.Value;
            return (index >= 0 && index < thread.Beads.Count) ? thread.Beads[index] : null;
        }

        return (thread.Beads.Count > 0) ? thread.Beads[0] : null;
    }

    /// <summary>
    /// The threads of the catalog's /Threads entry, or <see langword="null"/> when the catalog has none.
    /// </summary>
    internal static PdfThreads? FromCatalog(PdfDictionary catalog)
    {
        PdfArray? threads = catalog.GetArray(PdfTokens.ThreadsKey);
        if (threads == null)
        {
            return null;
        }

        List<PdfThread> items = [];
        for (int index = 0; index < threads.Count; index++)
        {
            PdfThread? thread = PdfThread.GetThread(threads, index);
            if (thread != null)
            {
                items.Add(thread);
            }
        }

        return new PdfThreads(items);
    }
}
