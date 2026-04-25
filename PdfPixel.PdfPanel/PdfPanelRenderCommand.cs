using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Represents a single render command that can be executed by the render loop.
/// Commands are granular units of work that allow yielding between frames.
/// </summary>
public sealed class PdfPanelRenderCommand
{
    /// <summary>
    /// Creates a new render command.
    /// </summary>
    /// <param name="type">The type of command.</param>
    /// <param name="request">Optional drawing request.</param>
    /// <param name="pageNumber">Optional page number for page-specific commands.</param>
    /// <param name="content">Optional content for commands that generate or draw content.</param>
    public PdfPanelRenderCommand(PdfPanelRenderCommandType type, PagesDrawingRequest request = null, int? pageNumber = null, ContentLocker<SKPicture> content = null)
    {
        Type = type;
        DrawingRequest = request;
        PageNumber = pageNumber;
        Content = content;
    }

    /// <summary>
    /// The type of command to execute.
    /// </summary>
    public PdfPanelRenderCommandType Type { get; }

    /// <summary>
    /// The original drawing request this command belongs to, can be null for non-drawing commands like Dispose.
    /// </summary>
    public PagesDrawingRequest DrawingRequest { get; }

    /// <summary>
    /// The page number this command operates on. Null for commands that affect all pages.
    /// </summary>
    public int? PageNumber { get; }

    public ContentLocker<SKPicture> Content { get; }

    public ContentLocker<SKPicture> Annotations { get; }

    public static List<PdfPanelRenderCommand> GenerateCommandsFromRequest(DrawingRequest request)
    {
        if (request is PagesDrawingRequest pagesRequest)
        {
            return GenerateCommandsFromRequest(pagesRequest);
        }
        else if (request is DisposeRequest)
        {
            return [new PdfPanelRenderCommand(PdfPanelRenderCommandType.Dispose)];
        }
        else if (request is ResetDrawingRequest)
        {
            return [new PdfPanelRenderCommand(PdfPanelRenderCommandType.Reset)];

        }
        else if (request is RefreshGraphicsDrawingRequest)
        {
            return [new PdfPanelRenderCommand(PdfPanelRenderCommandType.Render)];
        }
        else
        {
            return new List<PdfPanelRenderCommand>();
        }
    }

    public static List<PdfPanelRenderCommand> GenerateCommandsFromRequestNew(DrawingRequest request, IPdfPageContentProvider contentProvider)
    {
        if (request is PagesDrawingRequest pagesRequest)
        {
            return GenerateCommandsFromRequestNew(pagesRequest, contentProvider);
        }
        else if (request is DisposeRequest)
        {
            return [new PdfPanelRenderCommand(PdfPanelRenderCommandType.Dispose)];
        }
        else if (request is ResetDrawingRequest)
        {
            return [new PdfPanelRenderCommand(PdfPanelRenderCommandType.Reset)];

        }
        else if (request is RefreshGraphicsDrawingRequest)
        {
            return [new PdfPanelRenderCommand(PdfPanelRenderCommandType.Render)];
        }
        else
        {
            return new List<PdfPanelRenderCommand>();
        }
    }

    private static List<PdfPanelRenderCommand> GenerateCommandsFromRequestNew(PagesDrawingRequest request, IPdfPageContentProvider contentProvider)
    {
        var commands = new List<PdfPanelRenderCommand>();

        // 1. Draw background and shadows for all pages
        commands.Add(new PdfPanelRenderCommand(PdfPanelRenderCommandType.DrawBackground, request));

        // 3. Generate and draw full content for each visible page
        foreach (var page in request.VisiblePages)
        {
            commands.Add(new PdfPanelRenderCommand(
                PdfPanelRenderCommandType.DrawContent,
                request,
                page.PageNumber,
                contentProvider.GetExistingContent(page.PageNumber)));


            commands.Add(new PdfPanelRenderCommand(PdfPanelRenderCommandType.Render, request));
        }

        return commands;
    }

    /// <summary>
    /// Generates a list of render commands from a drawing request.
    /// Commands are ordered for progressive rendering: background, thumbnails, then content.
    /// </summary>
    /// <param name="request">The drawing request to generate commands from.</param>
    /// <returns>A list of commands to execute in order.</returns>
    private static List<PdfPanelRenderCommand> GenerateCommandsFromRequest(PagesDrawingRequest request)
    {
        var commands = new List<PdfPanelRenderCommand>();

        // 1. Draw background and shadows for all pages
        commands.Add(new PdfPanelRenderCommand(PdfPanelRenderCommandType.DrawBackground, request));
        commands.Add(new PdfPanelRenderCommand(PdfPanelRenderCommandType.Render, request));

        // 2. Generate and draw thumbnails for each visible page
        foreach (var page in request.VisiblePages)
        {
            commands.Add(new PdfPanelRenderCommand(
                PdfPanelRenderCommandType.InitializePage,
                request,
                page.PageNumber));

            commands.Add(new PdfPanelRenderCommand(
                PdfPanelRenderCommandType.DrawThumbnail,
                request,
                page.PageNumber));

            // Present after each thumbnail so user sees progress
            commands.Add(new PdfPanelRenderCommand(PdfPanelRenderCommandType.Render, request));
        }

        // 3. Generate and draw full content for each visible page
        foreach (var page in request.VisiblePages)
        {
            commands.Add(new PdfPanelRenderCommand(
                PdfPanelRenderCommandType.GenerateContent,
                request,
                page.PageNumber));

            commands.Add(new PdfPanelRenderCommand(
                PdfPanelRenderCommandType.DrawContent,
                request,
                page.PageNumber));

            // Present after each page content so user sees progress
            commands.Add(new PdfPanelRenderCommand(PdfPanelRenderCommandType.Render, request));
        }

        commands.Add(new PdfPanelRenderCommand(PdfPanelRenderCommandType.Finalize, request));

        return commands;
    }
}
