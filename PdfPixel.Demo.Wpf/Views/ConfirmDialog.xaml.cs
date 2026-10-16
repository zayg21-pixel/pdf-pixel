using System.Windows;

namespace PdfPixel.Demo.Wpf.Views;

/// <summary>
/// Interaction logic for ConfirmDialog.xaml.
/// </summary>
public partial class ConfirmDialog : DemoWindow
{
    public ConfirmDialog(string title, string message, string details, string confirmText)
    {
        InitializeComponent();
        Title = title;
        MessageTextBlock.Text = message;
        DetailsTextBox.Text = details;
        ConfirmButton.Content = confirmText;
    }

    /// <summary>
    /// Asks the user to confirm <paramref name="message"/> about <paramref name="details"/>, which the user can copy,
    /// and returns whether the user confirmed.
    /// </summary>
    public static bool ShowConfirm(Window owner, string title, string message, string details, string confirmText)
    {
        ConfirmDialog dialog = new(title, message, details, confirmText);

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

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
