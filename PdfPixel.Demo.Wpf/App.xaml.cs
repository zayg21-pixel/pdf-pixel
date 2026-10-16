using PdfPixel.Demo.Wpf.ViewModels;
using PdfPixel.Demo.Wpf.Views;
using System.Windows;
using System.Windows.Threading;

namespace PdfPixel.Demo.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        MainWindowViewModel viewModel = new();
        MainWindow window = new() { DataContext = viewModel };
        MainWindow = window;
        window.Show();

        if (e.Args.Length == 1)
        {
            viewModel.SelectedTab.Load(DocumentTabRequest.FromFile(e.Args[0]));
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        => e.Handled = ErrorDialog.ShowContinue(MainWindow, e.Exception);
}
