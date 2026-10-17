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
    internal PdfThreads(List<PdfThread>? items) => Items = items;

    /// <summary>
    /// Threads in order, or <see langword="null"/> when the document has no /Threads.
    /// </summary>
    public IReadOnlyList<PdfThread>? Items { get; }

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

        if (Items == null)
        {
            return null;
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

        HashSet<PdfBead> visitedBeads = [];
        int beadIndex = 0;

        for (PdfBead? bead = thread.GetFirstBead(); bead != null && visitedBeads.Add(bead); bead = bead.GetNext())
        {
            bool isRequestedBead = (action.BeadReference != null)
                ? bead.Reference.Equals(action.BeadReference.Value)
                : beadIndex == (action.BeadIndex ?? 0);

            if (isRequestedBead)
            {
                return bead;
            }

            beadIndex++;
        }

        return null;
    }

    /// <summary>
    /// The threads of the catalog's /Threads entry, without items when the catalog has none.
    /// </summary>
    internal static PdfThreads FromCatalog(PdfDictionary catalog)
    {
        PdfArray? threads = catalog.GetArray(PdfTokens.ThreadsKey);
        if (threads == null)
        {
            return new PdfThreads(null);
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
