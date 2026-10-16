using System;
using System.Windows;

namespace PdfPixel.Demo.Wpf.Views;

/// <summary>
/// Interaction logic for ErrorDialog.xaml.
/// </summary>
public partial class ErrorDialog : DemoWindow
{
    public ErrorDialog(Exception exception)
    {
        InitializeComponent();
        MessageTextBlock.Text = exception.Message;
        DetailsTextBox.Text = exception.ToString();
    }

    /// <summary>
    /// Shows <paramref name="exception"/> and returns whether the user chose to continue.
    /// </summary>
    public static bool ShowContinue(Window owner, Exception exception)
    {
        ErrorDialog dialog = new(exception);

        if (owner != null && owner.IsLoaded)
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(DetailsTextBox.Text);

    private void ExitButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ContinueButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
