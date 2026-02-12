using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;

namespace PdfPixel.Demo.Wpf;

/// <summary>
/// A TextBox for page input, only allows numbers, supports up/down keys, Enter, and LostFocus for binding updates.
/// </summary>
public class PageTextBox : TextBox
{
    public static readonly DependencyProperty PageProperty = DependencyProperty.Register(
        nameof(Page), typeof(int), typeof(PageTextBox),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPageChanged));

    public static readonly DependencyProperty MaxPageProperty = DependencyProperty.Register(
        nameof(MaxPage),
        typeof(int),
        typeof(PageTextBox),
        new PropertyMetadata(int.MaxValue));


    public PageTextBox()
    {
        DataObject.AddPastingHandler(this, OnPaste);
        Focusable = true;
    }

    public int Page
    {
        get { return (int)GetValue(PageProperty); }
        set { SetValue(PageProperty, value); }
    }

    public int MaxPage
    {
        get { return (int)GetValue(MaxPageProperty); }
        set { SetValue(MaxPageProperty, value); }
    }

    private static void OnPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PageTextBox)d;
        control.Text = control.Page.ToString();
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        e.Handled = !IsTextNumeric(e.Text);
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text))
        {
            string text = (string)e.DataObject.GetData(DataFormats.Text);
            if (!IsTextNumeric(text))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private static bool IsTextNumeric(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            if (Page > 1)
            {
                Page--;
                Text = Page.ToString();
                UpdateBinding();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            if (Page < MaxPage)
            {
                Page++;
                Text = Page.ToString();
                UpdateBinding();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            CommitTextToPage();
            UpdateBinding();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Text = Page.ToString();
            e.Handled = true;
        }
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        CommitTextToPage();
        UpdateBinding();
    }

    private void CommitTextToPage()
    {
        if (int.TryParse(Text, out int page))
        {
            if (page < 1)
            {
                page = 1;
            }
            else if (page > MaxPage)
            {
                page = MaxPage;
            }
            Page = page;
        }
        else
        {
            Text = Page.ToString();
        }
    }

    private void UpdateBinding()
    {
        BindingExpression binding = GetBindingExpression(PageProperty);
        binding?.UpdateSource();
        SelectionStart = Text?.Length ?? 0;
        SelectionLength = 0;
    }
}
