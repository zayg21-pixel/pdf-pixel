using PdfPixel.Color;
using PdfPixel.Navigation.Model;
using PdfPixel.Text;
using System.Collections.Generic;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable outline item (bookmark): its text, style and child items.
/// </summary>
public sealed class PdfOutlineItemViewModel : INotifyPropertyChanged
{
    private readonly object _documentLocker;
    private List<PdfOutlineItemViewModel>? _children;
    private bool _isExpanded;

    private PdfOutlineItemViewModel(PdfOutlineItem item, object documentLocker)
    {
        Item = item;
        _documentLocker = documentLocker;
        _isExpanded = item.IsOpen;
        Title = item.Title?.DecodePdfString();
        IsBold = (item.Flags & PdfOutlineItemFlags.Bold) != 0;
        IsItalic = (item.Flags & PdfOutlineItemFlags.Italic) != 0;
        Color = item.Color;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Text displayed for the item (/Title), or <see langword="null"/> when the document defines none.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Whether the item's text is displayed in bold.
    /// </summary>
    public bool IsBold { get; }

    /// <summary>
    /// Whether the item's text is displayed in italic.
    /// </summary>
    public bool IsItalic { get; }

    /// <summary>
    /// Color of the item's text (/C), or <see langword="null"/> when the document defines none.
    /// </summary>
    public PdfColor? Color { get; }

    /// <summary>
    /// Whether the item is expanded, showing its children; initially whether the document opens it.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;

        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    /// <summary>
    /// Child items, read from the document the first time they are requested.
    /// </summary>
    public IReadOnlyList<PdfOutlineItemViewModel> Children
    {
        get
        {
            if (_children == null)
            {
                _children = CreateItems(Item.EnumerateItems(), _documentLocker);
            }

            return _children;
        }
    }

    internal PdfOutlineItem Item { get; }

    /// <summary>
    /// Creates the view models of <paramref name="items"/>, reading the document under <paramref name="documentLocker"/>.
    /// </summary>
    internal static List<PdfOutlineItemViewModel> CreateItems(IEnumerable<PdfOutlineItem> items, object documentLocker)
    {
        List<PdfOutlineItemViewModel> result = [];

        lock (documentLocker)
        {
            foreach (PdfOutlineItem item in items)
            {
                result.Add(new PdfOutlineItemViewModel(item, documentLocker));
            }
        }

        return result;
    }
}
