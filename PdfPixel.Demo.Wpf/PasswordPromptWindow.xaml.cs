using System.Windows;

namespace PdfPixel.Demo.Wpf;

/// <summary>
/// Interaction logic for PasswordPromptWindow.xaml.
/// </summary>
public partial class PasswordPromptWindow : Window
{
    public PasswordPromptWindow(string errorMessage)
    {
        InitializeComponent();

        if (!string.IsNullOrEmpty(errorMessage))
        {
            ErrorTextBlock.Text = errorMessage;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        Loaded += PasswordPromptWindow_Loaded;
    }

    public string Password { get; private set; } = string.Empty;

    private void PasswordPromptWindow_Loaded(object sender, RoutedEventArgs e) => PasswordBox.Focus();

    /// <summary>
    /// Shows the password prompt modally, returning the entered password, or null if the user cancelled.
    /// </summary>
    public static string TryPromptForPassword(Window owner, string errorMessage)
    {
        PasswordPromptWindow window = new(errorMessage);
        if (owner != null)
        {
            window.Owner = owner;
        }

        bool? result = window.ShowDialog();
        if (result != true)
        {
            return null;
        }

        return window.Password;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Password = PasswordBox.Password;
        DialogResult = true;
    }
}
