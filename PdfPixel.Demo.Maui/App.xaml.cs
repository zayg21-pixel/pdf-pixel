using Microsoft.Extensions.Logging;

namespace PdfPixel.Demo.Maui;

public partial class App : Application
{
    private readonly ILoggerFactory _loggerFactory;

    public App(ILoggerFactory loggerFactory)
    {
        InitializeComponent();
        _loggerFactory = loggerFactory;
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(new MainPage(_loggerFactory));
}
