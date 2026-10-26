using PdfPixel.Actions;
using PdfPixel.Navigation;
using PdfPixel.Objects;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Text;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PdfPixel.PdfPanel.Web.Dto.Converters;

/// <summary>
/// Converts the redraw state between DTOs and the panel.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class RedrawDtoConverter
{
    /// <summary>
    /// Reads the JSON view state JS passes to a redraw, or returns <see langword="null"/> for a JSON null.
    /// </summary>
    public static RedrawInputDto? ReadInput(string json)
        => JsonSerializer.Deserialize(json, DtoJsonContext.Default.RedrawInputDto);

    /// <summary>
    /// Builds the JSON view state a redraw returns to JS from <paramref name="context"/>; the popup, active content,
    /// search results, layers and action requests are set only when they changed since they were last sent to JS.
    /// </summary>
    public static string CreateResult(PdfPanelResources resources, PdfPanelContext context)
    {
        RedrawResultDto result = new()
        {
            CursorStyle = ToCursorStyle(context.Input.Cursor),
            IsTextExtracted = context.Text.IsTextExtracted,
            ScrollWidth = context.ExtentWidth,
            ScrollHeight = context.ExtentHeight,
            VerticalOffset = context.VerticalOffset,
            HorizontalOffset = context.HorizontalOffset,
            Scale = context.Scale,
            AutoScaleMode = ToString(context.AutoScaleMode),
            CurrentPage = context.GetCurrentPage()
        };

        PdfAnnotationPopup? activeAnnotation = context.Annotations.ActiveAnnotation;

        if (activeAnnotation != resources.AnnotationPopup)
        {
            resources.AnnotationPopup = activeAnnotation;
            result.AnnotationPopupChanged = true;
            result.AnnotationPopup = (activeAnnotation != null) ? CreateAnnotationPopup(activeAnnotation) : null;
        }

        string? activeContentName = context.Content.ActiveContentName;

        if (activeContentName != resources.ActiveContentName)
        {
            resources.ActiveContentName = activeContentName;
            result.ActiveContentNameChanged = true;
            result.ActiveContentName = activeContentName;
        }

        if (resources.SearchResultsChanged)
        {
            resources.SearchResultsChanged = false;
            result.SearchResultsChanged = true;
            result.SearchResults = CreateSearchResults(context.Search.Matches);
        }

        if (resources.LayersChanged)
        {
            resources.LayersChanged = false;
            result.LayersChanged = true;
            result.LayerConfiguration = GetLayerConfigurationHandle(resources.Layers);
            result.VisibleLayers = CreateVisibleLayers(resources);
        }

        if (resources.ActionRequests.Count > 0)
        {
            result.ActionRequests = CreateActionRequests(resources);
            resources.ActionRequests.Clear();
        }

        return JsonSerializer.Serialize(result, DtoJsonContext.Default.RedrawResultDto);
    }

    /// <summary>
    /// Reads an automatic scaling mode name, or returns <see langword="null"/> when the name is unknown.
    /// </summary>
    public static PdfPanelAutoScaleMode? ReadAutoScaleMode(string? value)
    {
        return value switch
        {
            "noAutoScale" => PdfPanelAutoScaleMode.NoAutoScale,
            "scaleToWidth" => PdfPanelAutoScaleMode.ScaleToWidth,
            "scaleToHeight" => PdfPanelAutoScaleMode.ScaleToHeight,
            _ => null
        };
    }

    /// <summary>
    /// Reads a text range <c>{ pageNumber, startIndex, length }</c>, or returns <see langword="null"/> when
    /// <paramref name="source"/> is <see langword="null"/>.
    /// </summary>
    public static PdfPanelTextRange? ReadTextRange(TextRangeDto? source)
    {
        if (source == null)
        {
            return null;
        }

        return new PdfPanelTextRange(source.PageNumber, source.StartIndex, source.Length);
    }

    /// <summary>
    /// Returns the JS name of <paramref name="mode"/>.
    /// </summary>
    private static string ToString(PdfPanelAutoScaleMode mode)
    {
        return mode switch
        {
            PdfPanelAutoScaleMode.ScaleToWidth => "scaleToWidth",
            PdfPanelAutoScaleMode.ScaleToHeight => "scaleToHeight",
            _ => "noAutoScale"
        };
    }

    /// <summary>
    /// Returns the CSS cursor of <paramref name="cursor"/>, or <see langword="null"/> when the panel has no preference.
    /// </summary>
    private static string? ToCursorStyle(PdfPanelCursor cursor)
    {
        return cursor switch
        {
            PdfPanelCursor.Hand => "pointer",
            PdfPanelCursor.IBeam => "text",
            _ => null
        };
    }

    /// <summary>
    /// Builds the popup <c>{ isInteractive, messages }</c> of <paramref name="popup"/>.
    /// </summary>
    private static AnnotationPopupDto CreateAnnotationPopup(PdfAnnotationPopup popup)
    {
        AnnotationPopupDto popupDto = new()
        {
            IsInteractive = popup.IsInteractive,
            Messages = CreateMessages(popup.Messages)
        };

        return popupDto;
    }

    /// <summary>
    /// Builds the search results <c>{ pageNumber, startIndex, length, bounds }</c> of <paramref name="matches"/>.
    /// </summary>
    private static List<SearchResultDto> CreateSearchResults(IReadOnlyList<PdfPanelSearchMatch> matches)
    {
        List<SearchResultDto> results = new(matches.Count);

        for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
        {
            PdfPanelSearchMatch match = matches[matchIndex];

            SearchResultDto result = new()
            {
                PageNumber = match.Range.PageNumber,
                StartIndex = match.Range.StartIndex,
                Length = match.Range.Length,
                Bounds = new RectangleDto
                {
                    Left = match.Bounds.Left,
                    Top = match.Bounds.Top,
                    Right = match.Bounds.Right,
                    Bottom = match.Bounds.Bottom
                }
            };

            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Returns the handle of the layer configuration content is rendered with, or <see langword="null"/> when there is none.
    /// </summary>
    private static int? GetLayerConfigurationHandle(PdfLayersViewModel? layers)
    {
        if (layers == null || layers.Configuration == null)
        {
            return null;
        }

        for (int configurationIndex = 0; configurationIndex < layers.Configurations.Count; configurationIndex++)
        {
            if (layers.Configurations[configurationIndex] == layers.Configuration)
            {
                return configurationIndex;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the handles of the visible layers of every configuration of <paramref name="resources"/>.
    /// </summary>
    private static List<int> CreateVisibleLayers(PdfPanelResources resources)
    {
        List<int> visibleLayers = [];

        for (int handle = 0; handle < resources.LayerItems.Count; handle++)
        {
            if (resources.LayerItems[handle].IsVisible)
            {
                visibleLayers.Add(handle);
            }
        }

        return visibleLayers;
    }

    /// <summary>
    /// Builds the requested actions of <paramref name="resources"/>, <c>{ source, action }</c> in request order.
    /// </summary>
    private static List<ActionRequestDto> CreateActionRequests(PdfPanelResources resources)
    {
        List<ActionRequestDto> requests = new(resources.ActionRequests.Count);

        for (int requestIndex = 0; requestIndex < resources.ActionRequests.Count; requestIndex++)
        {
            PdfPanelActionEventArgs args = resources.ActionRequests[requestIndex];

            ActionRequestDto request = new()
            {
                Source = ToString(args.Source),
                Action = CreateAction(args.Action, resources)
            };

            requests.Add(request);
        }

        return requests;
    }

    /// <summary>
    /// Builds <paramref name="messages"/>, each with its replies nested.
    /// </summary>
    private static List<AnnotationMessageDto> CreateMessages(IReadOnlyList<PdfAnnotationMessage> messages)
    {
        List<AnnotationMessageDto> messageList = new(messages.Count);

        for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            PdfAnnotationMessage message = messages[messageIndex];

            AnnotationMessageDto messageDto = new()
            {
                Title = message.Title,
                Contents = message.Contents,
                CreationDate = message.CreationDate?.ToString("o"),
                Replies = CreateMessages(message.Replies)
            };

            messageList.Add(messageDto);
        }

        return messageList;
    }

    /// <summary>
    /// Builds the action of <paramref name="action"/>: <c>uri</c>, <c>launch</c>, <c>goToRemote</c> and <c>named</c>
    /// with their values, any other action as <c>{ type: 'unsupported', actionType }</c> with its PDF name.
    /// </summary>
    private static ActionDto CreateAction(PdfAction action, PdfPanelResources resources)
    {
        switch (action)
        {
            case PdfUriAction uriAction:
            {
                UriActionDto uriActionDto = new() { Uri = uriAction.Uri?.DecodePdfString() };

                return uriActionDto;
            }
            case PdfLaunchAction launchAction:
            {
                LaunchActionDto launchActionDto = new()
                {
                    File = CreateFile(launchAction, resources),
                    NewWindow = launchAction.NewWindow
                };

                return launchActionDto;
            }
            case PdfGoToRemoteAction goToRemoteAction:
            {
                GoToRemoteActionDto goToRemoteActionDto = new()
                {
                    File = CreateFile(goToRemoteAction, resources),
                    NewWindow = goToRemoteAction.NewWindow,
                    Destination = CreateRemoteDestination(goToRemoteAction.Destination)
                };

                return goToRemoteActionDto;
            }
            case PdfNamedAction namedAction:
            {
                NamedActionDto namedActionDto = new() { Name = namedAction.RawName?.DecodePdfString() };

                return namedActionDto;
            }
            default:
            {
                UnsupportedActionDto unsupportedActionDto = new() { ActionType = ToString(action.ActionType) };

                return unsupportedActionDto;
            }
        }
    }

    /// <summary>
    /// Builds the file of <paramref name="action"/>, or returns <see langword="null"/> when the action names none.
    /// </summary>
    private static FileDto? CreateFile(PdfFileAction action, PdfPanelResources resources)
    {
        if (action.FileSpecification == null)
        {
            return null;
        }

        return DocumentDtoConverter.CreateFile(action.FileSpecification, resources);
    }

    /// <summary>
    /// Builds the destination in another document of <paramref name="destination"/>: a page with its view,
    /// <c>{ pageNumber, fitType, left, top, right, bottom, zoom }</c> with a 1-based page number, or a named destination
    /// <c>{ name }</c>; <see langword="null"/> for none or another destination.
    /// </summary>
    private static object? CreateRemoteDestination(PdfDestination? destination)
    {
        switch (destination)
        {
            case PdfPageNumberDestination pageDestination:
            {
                PageDestinationDto destinationDto = new()
                {
                    PageNumber = pageDestination.PageNumber + 1,
                    FitType = ToString(pageDestination.FitType),
                    Left = pageDestination.Left,
                    Top = pageDestination.Top,
                    Right = pageDestination.Right,
                    Bottom = pageDestination.Bottom,
                    Zoom = pageDestination.Zoom
                };

                return destinationDto;
            }
            case PdfNamedDestination namedDestination:
            {
                NamedDestinationDto destinationDto = new() { Name = namedDestination.Name.DecodePdfString() };

                return destinationDto;
            }
            default:
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Returns the JS name of <paramref name="source"/>.
    /// </summary>
    private static string ToString(PdfPanelActionSource source)
    {
        return source switch
        {
            PdfPanelActionSource.DocumentOpen => "documentOpen",
            PdfPanelActionSource.PageOpen => "pageOpen",
            PdfPanelActionSource.PageClose => "pageClose",
            PdfPanelActionSource.LinkAnnotationClick => "linkAnnotationClick",
            PdfPanelActionSource.FileAttachmentAnnotationClick => "fileAttachmentAnnotationClick",
            _ => "outline"
        };
    }

    /// <summary>
    /// Returns the fit type name of <paramref name="fitType"/>, the one <see cref="PdfPanelInterop.ReadDestination"/> reads.
    /// </summary>
    private static string ToString(PdfDestinationFitType fitType)
    {
        return fitType switch
        {
            PdfDestinationFitType.XYZ => "XYZ",
            PdfDestinationFitType.Fit => "Fit",
            PdfDestinationFitType.FitH => "FitH",
            PdfDestinationFitType.FitV => "FitV",
            PdfDestinationFitType.FitR => "FitR",
            PdfDestinationFitType.FitB => "FitB",
            PdfDestinationFitType.FitBH => "FitBH",
            _ => "FitBV"
        };
    }

    /// <summary>
    /// Returns the PDF name (<c>/S</c>) of <paramref name="actionType"/>, or <c>Unknown</c> for an unrecognized type.
    /// </summary>
    private static string ToString(PdfActionType actionType)
    {
        return actionType switch
        {
            PdfActionType.GoTo => "GoTo",
            PdfActionType.GoToRemote => "GoToR",
            PdfActionType.GoToEmbedded => "GoToE",
            PdfActionType.GoToDocumentPart => "GoToDp",
            PdfActionType.Launch => "Launch",
            PdfActionType.Thread => "Thread",
            PdfActionType.Uri => "URI",
            PdfActionType.Sound => "Sound",
            PdfActionType.Movie => "Movie",
            PdfActionType.Hide => "Hide",
            PdfActionType.Named => "Named",
            PdfActionType.SubmitForm => "SubmitForm",
            PdfActionType.ResetForm => "ResetForm",
            PdfActionType.ImportData => "ImportData",
            PdfActionType.JavaScript => "JavaScript",
            PdfActionType.SetOcgState => "SetOCGState",
            PdfActionType.Rendition => "Rendition",
            PdfActionType.Trans => "Trans",
            PdfActionType.GoTo3DView => "GoTo3DView",
            PdfActionType.RichMediaExecute => "RichMediaExecute",
            _ => "Unknown"
        };
    }
}
