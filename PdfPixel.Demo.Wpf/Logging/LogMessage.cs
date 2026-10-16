using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.Demo.Wpf.Logging;

/// <summary>
/// Represents a log message with details for display or analysis.
/// </summary>
public class LogMessage
{
    /// <summary>
    /// Gets or sets the date and time of the log message.
    /// </summary>
    public DateTime DateTime { get; set; }

    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public LogLevel LogLevel { get; set; }

    /// <summary>
    /// Gets or sets the logger category name.
    /// </summary>
    public string CategoryName { get; set; }

    /// <summary>
    /// Gets or sets the formatted log message.
    /// </summary>
    public string FormattedMessage { get; set; }
}
