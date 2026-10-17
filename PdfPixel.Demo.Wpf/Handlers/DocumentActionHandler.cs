using Microsoft.Extensions.Logging;
using PdfPixel.Actions.Model;
using PdfPixel.Demo.Wpf.ViewModels;
using PdfPixel.Demo.Wpf.Views;
using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Wpf;
using PdfPixel.Text;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PdfPixel.Demo.Wpf.Handlers;

/// <summary>
/// Performs the actions of a tab's document that leave the panel: links, launched and remote files, embedded
/// documents and attachments.
/// </summary>
public sealed class DocumentActionHandler
{
    private const string PdfExtension = ".pdf";

    private readonly DocumentTabViewModel _tab;
    private readonly Func<DocumentTabRequest, DocumentTabViewModel> _openTab;
    private readonly ILogger<DocumentActionHandler> _logger;

    /// <summary>
    /// Initializes a handler of the actions of <paramref name="tab"/> that opens new tabs through <paramref name="openTab"/>.
    /// </summary>
    public DocumentActionHandler(DocumentTabViewModel tab, Func<DocumentTabRequest, DocumentTabViewModel> openTab, ILogger<DocumentActionHandler> logger)
    {
        _tab = tab ?? throw new ArgumentNullException(nameof(tab));
        _openTab = openTab ?? throw new ArgumentNullException(nameof(openTab));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs an action the panel asks the host to perform, when a link, a file attachment or an outline item
    /// triggered it.
    /// </summary>
    public void PerformAction(WpfPdfPanelActionEventArgs args)
    {
        if (args.ActionSource != PdfPanelActionSource.LinkAnnotationClick
            && args.ActionSource != PdfPanelActionSource.FileAttachmentAnnotationClick
            && args.ActionSource != PdfPanelActionSource.Outline)
        {
            _logger.LogInformation("{Action} from {Source} is not performed.", args.Action, args.ActionSource);
            return;
        }

        switch (args.Action)
        {
            case PdfUriAction uriAction:
            {
                OpenUri(uriAction);
                break;
            }
            case PdfLaunchAction launchAction:
            {
                Launch(launchAction);
                break;
            }
            case PdfGoToRemoteAction goToRemoteAction:
            {
                GoToRemote(goToRemoteAction);
                break;
            }
            case PdfGoToEmbeddedAction goToEmbeddedAction:
            {
                GoToEmbedded(goToEmbeddedAction);
                break;
            }
            default:
            {
                _logger.LogInformation("{Action} is not supported.", args.Action);
                break;
            }
        }
    }

    /// <summary>
    /// Opens a PDF attachment in a new tab, or saves any other attachment to a file.
    /// </summary>
    public void OpenAttachment(PdfAttachmentViewModel attachment)
    {
        if (attachment == null)
        {
            return;
        }

        OpenEmbeddedFile(attachment.FileSpecification, null);
    }

    private void OpenUri(PdfUriAction action)
    {
        string uri = action.Uri?.DecodePdfString();
        if (string.IsNullOrEmpty(uri))
        {
            return;
        }

        if (ConfirmDialog.ShowConfirm(Application.Current.MainWindow, "Open Link", "The document wants to open a link in your browser.", uri, "Open"))
        {
            ShellOpen(uri, null, null);
        }
    }

    private void Launch(PdfLaunchAction action)
    {
        PdfFileSpecification fileSpecification = action.FileSpecification;
        if (fileSpecification != null && GetEmbeddedFile(fileSpecification) != null)
        {
            OpenEmbeddedFile(fileSpecification, action.NewWindow);
            return;
        }

        string fileName = action.Windows?.FileName.DecodePdfString() ?? GetFileName(fileSpecification);
        string filePath = ResolveFilePath(fileName);
        if (filePath == null)
        {
            return;
        }

        if (IsPdfFile(filePath))
        {
            OpenRequest(DocumentTabRequest.FromFile(filePath), action.NewWindow);
            return;
        }

        if (ConfirmDialog.ShowConfirm(Application.Current.MainWindow, "Launch File", "The document wants to open a file with its default application.", filePath, "Launch"))
        {
            ShellOpen(filePath, action.Windows?.Parameters?.DecodePdfString(), action.Windows?.DefaultDirectory?.DecodePdfString());
        }
    }

    private void GoToRemote(PdfGoToRemoteAction action)
    {
        string filePath = ResolveFilePath(GetFileName(action.FileSpecification));
        if (filePath == null)
        {
            return;
        }

        OpenRequest(DocumentTabRequest.FromFile(filePath, action.Destination), action.NewWindow);
    }

    /// <summary>
    /// Follows the path of <paramref name="action"/> from the tab's document, reading each intermediate document,
    /// and opens the target document at the action's destination.
    /// </summary>
    private void GoToEmbedded(PdfGoToEmbeddedAction action)
    {
        if (action.Path.Count == 0)
        {
            if (action.Destination != null)
            {
                _tab.PanelInterface.ScrollToDestination(action.Destination);
            }

            return;
        }

        DocumentTabRequest target = _tab.Request;
        IPdfDocument targetDocument = _tab.Document;

        try
        {
            for (int index = 0; index < action.Path.Count; index++)
            {
                PdfEmbeddedPathStep step = action.Path[index];

                if (step.Direction == PdfEmbeddedPathDirection.Parent)
                {
                    target = target.Parent;
                }
                else
                {
                    if (targetDocument == null)
                    {
                        targetDocument = _tab.ReadDocumentWithPasswordPrompt(target.Data);
                        if (targetDocument == null)
                        {
                            return;
                        }
                    }

                    target = GetEmbeddedChild(target, targetDocument, step);
                }

                if (target == null)
                {
                    _logger.LogWarning("Target of step {Step} of {Action} is not found.", index + 1, action);
                    return;
                }

                if (targetDocument != _tab.Document)
                {
                    targetDocument?.Dispose();
                }

                targetDocument = null;
            }
        }
        finally
        {
            if (targetDocument != _tab.Document)
            {
                targetDocument?.Dispose();
            }
        }

        OpenRequest(target.WithDestination(action.Destination), action.NewWindow);
    }

    /// <summary>
    /// The embedded file a child step names in <paramref name="parentDocument"/>, as a document embedded in
    /// <paramref name="parent"/>; null when the file is not found.
    /// </summary>
    private DocumentTabRequest GetEmbeddedChild(DocumentTabRequest parent, IPdfDocument parentDocument, PdfEmbeddedPathStep step)
    {
        PdfFileSpecification fileSpecification;
        byte[] data;

        if (parentDocument == _tab.Document)
        {
            lock (_tab.Pages.DocumentLocker)
            {
                fileSpecification = parentDocument.EmbeddedFiles.ResolveFile(step);
                data = ReadEmbeddedFile(fileSpecification);
            }
        }
        else
        {
            fileSpecification = parentDocument.EmbeddedFiles.ResolveFile(step);
            data = ReadEmbeddedFile(fileSpecification);
        }

        if (data == null)
        {
            return null;
        }

        return new DocumentTabRequest(Path.GetFileNameWithoutExtension(GetFileName(fileSpecification) ?? string.Empty), data, null, null, parent);
    }

    /// <summary>
    /// Opens an embedded PDF file as a document embedded in the tab's document, or saves any other embedded file.
    /// </summary>
    private void OpenEmbeddedFile(PdfFileSpecification fileSpecification, bool? newWindow)
    {
        byte[] data;

        lock (_tab.Pages.DocumentLocker)
        {
            data = ReadEmbeddedFile(fileSpecification);
        }

        if (data == null)
        {
            _logger.LogWarning("Embedded file {FileName} has no content.", GetFileName(fileSpecification));
            return;
        }

        string fileName = GetFileName(fileSpecification) ?? string.Empty;

        if (IsPdfFile(fileName))
        {
            OpenRequest(new DocumentTabRequest(Path.GetFileNameWithoutExtension(fileName), data, null, null, _tab.Request), newWindow);
            return;
        }

        SaveFile(fileName, data);
    }

    /// <summary>
    /// Opens <paramref name="request"/> in the tab when <paramref name="newWindow"/> is false, otherwise in a new tab.
    /// </summary>
    private void OpenRequest(DocumentTabRequest request, bool? newWindow)
    {
        if (newWindow == false)
        {
            _tab.Load(request);
        }
        else
        {
            _openTab(request);
        }
    }

    /// <summary>
    /// Path of a file a document refers to: rooted as is, relative to the tab's file; null when it cannot be resolved.
    /// </summary>
    private string ResolveFilePath(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            _logger.LogWarning("Referenced file has no name.");
            return null;
        }

        string filePath = fileName;
        if (!Path.IsPathRooted(fileName))
        {
            string documentPath = _tab.Request?.FilePath;
            if (documentPath == null)
            {
                _logger.LogWarning("Relative file {FileName} cannot be resolved from an embedded document.", fileName);
                return null;
            }

            filePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(documentPath)), fileName);
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Referenced file {FilePath} is not found.", filePath);
            return null;
        }

        return filePath;
    }

    private static PdfEmbeddedFile GetEmbeddedFile(PdfFileSpecification fileSpecification)
        => fileSpecification.UnicodeEmbeddedFile ?? fileSpecification.EmbeddedFile;

    private static byte[] ReadEmbeddedFile(PdfFileSpecification fileSpecification)
    {
        if (fileSpecification == null)
        {
            return null;
        }

        return GetEmbeddedFile(fileSpecification)?.Stream.DecodeAsMemory().ToArray();
    }

    private static string GetFileName(PdfFileSpecification fileSpecification)
    {
        if (fileSpecification == null)
        {
            return null;
        }

        return (fileSpecification.UnicodeFile ?? fileSpecification.File)?.DecodePdfString();
    }

    private static bool IsPdfFile(string fileName) => string.Equals(Path.GetExtension(fileName), PdfExtension, StringComparison.OrdinalIgnoreCase);

    private static void ShellOpen(string fileName, string arguments, string workingDirectory)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            UseShellExecute = true,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty
        };

        Process.Start(startInfo)?.Dispose();
    }

    private static void SaveFile(string fileName, byte[] data)
    {
        var saveFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = fileName,
            Title = "Save Attachment"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            File.WriteAllBytes(saveFileDialog.FileName, data);
        }
    }
}
