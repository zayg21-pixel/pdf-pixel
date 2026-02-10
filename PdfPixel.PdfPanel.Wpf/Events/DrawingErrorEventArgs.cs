using System;
using System.Windows;

namespace PdfPixel.PdfPanel.Wpf.Events
{
    /// <summary>
    /// Event arguments for PDF drawing errors
    /// </summary>
    public class DrawingErrorEventArgs : RoutedEventArgs
    {
        public DrawingErrorEventArgs(RoutedEvent routedEvent, object source, Exception exception, string context)
            : base(routedEvent, source)
        {
            Exception = exception;
            Context = context;
            Timestamp = DateTime.Now;
        }

        public Exception Exception { get; }
        public string Context { get; }
        public DateTime Timestamp { get; }
    }

    /// <summary>
    /// Delegate for handling drawing error events
    /// </summary>
    public delegate void DrawingErrorEventHandler(object sender, DrawingErrorEventArgs e);
}
