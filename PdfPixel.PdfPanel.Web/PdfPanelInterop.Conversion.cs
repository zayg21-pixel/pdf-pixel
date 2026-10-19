using PdfPixel.Actions.Model;
using PdfPixel.Color;
using PdfPixel.Encryption.Model;
using PdfPixel.Files;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Navigation.Model;
using PdfPixel.PdfPanel.Actions;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Thumbnails;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;

namespace PdfPixel.PdfPanel.Web;

internal static partial class PdfPanelInterop
{
    /// <summary>
    /// Reads the configuration keys present in <paramref name="source"/> into <paramref name="target"/>;
    /// keys absent from it keep their current values.
    /// </summary>
    private static void ReadConfiguration(JSObject source, PdfPanelConfiguration target)
    {
        target.MinScale = ReadFloat(source, "minScale", target.MinScale);
        target.MaxScale = ReadFloat(source, "maxScale", target.MaxScale);
        target.ZoomStep = ReadFloat(source, "zoomStep", target.ZoomStep);
        target.PageGap = ReadFloat(source, "pageGap", target.PageGap);
        target.PanelBackgroundColor = ReadColor(source, "panelBackgroundColor", target.PanelBackgroundColor);
        target.PageCornerRadius = ReadFloat(source, "pageCornerRadius", target.PageCornerRadius);
        target.ShowPageLoadingAnimation = ReadBoolean(source, "showPageLoadingAnimation", target.ShowPageLoadingAnimation);
        target.ExtractAllText = ReadBoolean(source, "extractAllText", target.ExtractAllText);
        target.TextSelectionColor = ReadColor(source, "textSelectionColor", target.TextSelectionColor);
        target.SearchMatchCase = ReadBoolean(source, "searchMatchCase", target.SearchMatchCase);
        target.SearchMatchWholeWord = ReadBoolean(source, "searchMatchWholeWord", target.SearchMatchWholeWord);
        target.SearchMatchDiacritics = ReadBoolean(source, "searchMatchDiacritics", target.SearchMatchDiacritics);
        target.SearchMatchColor = ReadColor(source, "searchMatchColor", target.SearchMatchColor);
        target.SearchCurrentMatchColor = ReadColor(source, "searchCurrentMatchColor", target.SearchCurrentMatchColor);
        target.ContentSelectionBorderThickness = ReadFloat(source, "contentSelectionBorderThickness", target.ContentSelectionBorderThickness);
        target.ContentSelectionBorderColor = ReadColor(source, "contentSelectionBorderColor", target.ContentSelectionBorderColor);
        target.AllowTextSelection = ReadBoolean(source, "allowTextSelection", target.AllowTextSelection);
        target.AllowPageContentHover = ReadBoolean(source, "allowPageContentHover", target.AllowPageContentHover);
        target.AllowImageSelection = ReadBoolean(source, "allowImageSelection", target.AllowImageSelection);
        target.ShowSearchHighlights = ReadBoolean(source, "showSearchHighlights", target.ShowSearchHighlights);
        target.UsePageThumbnails = ReadBoolean(source, "usePageThumbnails", target.UsePageThumbnails);
        target.ThumbnailMaximumCount = ReadInt32(source, "thumbnailMaximumCount", target.ThumbnailMaximumCount);
        target.ThumbnailPreloadMode = ReadThumbnailPreloadMode(source, "thumbnailPreloadMode", target.ThumbnailPreloadMode);
        target.UseThumbnailsAsPlaceholders = ReadBoolean(source, "useThumbnailsAsPlaceholders", target.UseThumbnailsAsPlaceholders);
        target.Antialias = ReadBoolean(source, "antialias", target.Antialias);
        target.SnapToDevicePixels = ReadBoolean(source, "snapToDevicePixels", target.SnapToDevicePixels);
        target.TileSize = ReadInt32(source, "tileSize", target.TileSize);
        target.AnimationFps = ReadInt32(source, "animationFps", target.AnimationFps);
        target.ScrollContentUpdateDelay = ReadMilliseconds(source, "scrollContentUpdateDelay", target.ScrollContentUpdateDelay);
        target.ZoomContentUpdateDelay = ReadMilliseconds(source, "zoomContentUpdateDelay", target.ZoomContentUpdateDelay);
        target.LookAheadPageCount = ReadInt32(source, "lookAheadPageCount", target.LookAheadPageCount);
        target.ContentCacheSize = ReadInt32(source, "contentCacheSize", target.ContentCacheSize);
        target.TextCharacterHitRadius = ReadFloat(source, "textCharacterHitRadius", target.TextCharacterHitRadius);
        target.TextLineMergeThreshold = ReadFloat(source, "textLineMergeThreshold", target.TextLineMergeThreshold);

        using JSObject? thumbnailRenderMaximumSize = source.GetPropertyAsJSObject("thumbnailRenderMaximumSize");

        if (thumbnailRenderMaximumSize != null)
        {
            PdfIntegerSize size = target.ThumbnailRenderMaximumSize;
            target.ThumbnailRenderMaximumSize = new PdfIntegerSize(
                ReadInt32(thumbnailRenderMaximumSize, "width", size.Width),
                ReadInt32(thumbnailRenderMaximumSize, "height", size.Height));
        }

        using JSObject? layoutPadding = source.GetPropertyAsJSObject("layoutPadding");

        if (layoutPadding != null)
        {
            PdfRectangle padding = target.LayoutPadding;
            target.LayoutPadding = new PdfRectangle(
                ReadFloat(layoutPadding, "left", padding.Left),
                ReadFloat(layoutPadding, "top", padding.Top),
                ReadFloat(layoutPadding, "right", padding.Right),
                ReadFloat(layoutPadding, "bottom", padding.Bottom));
        }
    }

    /// <summary>
    /// Reads a destination <c>{ pageNumber, fitType, left, top, right, bottom, zoom }</c>, or returns <see langword="null"/>
    /// when the fit type is not one of <see cref="PdfDestinationFitType"/>.
    /// </summary>
    private static PdfPageNumberDestination? ReadDestination(JSObject source)
    {
        PdfDestinationFitType? fitType = source.GetPropertyAsString("fitType") switch
        {
            "XYZ" => PdfDestinationFitType.XYZ,
            "Fit" => PdfDestinationFitType.Fit,
            "FitH" => PdfDestinationFitType.FitH,
            "FitV" => PdfDestinationFitType.FitV,
            "FitR" => PdfDestinationFitType.FitR,
            "FitB" => PdfDestinationFitType.FitB,
            "FitBH" => PdfDestinationFitType.FitBH,
            "FitBV" => PdfDestinationFitType.FitBV,
            _ => null
        };

        if (fitType == null)
        {
            return null;
        }

        return new PdfPageNumberDestination(
            source.GetPropertyAsInt32("pageNumber") - 1,
            fitType.Value,
            ReadOptionalFloat(source, "zoom"),
            ReadOptionalFloat(source, "left"),
            ReadOptionalFloat(source, "bottom"),
            ReadOptionalFloat(source, "right"),
            ReadOptionalFloat(source, "top"));
    }

    /// <summary>
    /// Reads a text range <c>{ pageNumber, startIndex, length }</c>, or returns <see langword="null"/> when
    /// <paramref name="source"/> is <see langword="null"/>.
    /// </summary>
    private static PdfPanelTextRange? ReadTextRange(JSObject? source)
    {
        if (source == null)
        {
            return null;
        }

        return new PdfPanelTextRange(
            source.GetPropertyAsInt32("pageNumber"),
            source.GetPropertyAsInt32("startIndex"),
            source.GetPropertyAsInt32("length"));
    }

    /// <summary>
    /// Reads an automatic scaling mode name, or returns <see langword="null"/> when the name is unknown.
    /// </summary>
    private static PdfPanelAutoScaleMode? ReadAutoScaleMode(string? value)
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
    /// Builds the result of the document loaded into <paramref name="resources"/>: <c>ok: true</c>, its properties,
    /// pages, outline, layer configurations and attached files, each item JS passes back with its handle.
    /// </summary>
    private static JSObject CreateDocumentResult(PdfPanelResources resources)
    {
        if (resources.Document == null
            || resources.Pages == null
            || resources.Outlines == null
            || resources.Layers == null
            || resources.Attachments == null)
        {
            return CreateDocumentResult("error", "No document is loaded.");
        }

        IPdfDocument document = resources.Document;
        JSObject target = CreateObject();

        target.SetProperty("ok", true);
        target.SetProperty("pageCount", resources.Pages.Count);
        target.SetProperty("version", document.Version?.ToString());
        target.SetProperty("pageMode", ToString(document.PageMode));
        target.SetProperty("pageLayout", ToString(document.PageLayout));
        target.SetProperty("lang", document.Lang?.DecodePdfString());

        if (document.Information == null)
        {
            target.SetProperty("information", (JSObject?)null);
        }
        else
        {
            using JSObject information = CreateDocumentInformation(document.Information);
            target.SetProperty("information", information);
        }

        if (document.Permissions == null)
        {
            target.SetProperty("permissions", (JSObject?)null);
        }
        else
        {
            using JSObject permissions = CreatePermissions(document.Permissions.Value);
            target.SetProperty("permissions", permissions);
        }

        using JSObject pages = CreatePagesArray(resources.Pages);
        target.SetProperty("pages", pages);

        using JSObject outlines = CreateOutlineArray(resources.Outlines.Items, resources.OutlineItems);
        target.SetProperty("outlines", outlines);

        using JSObject layerConfigurations = CreateLayerConfigurationsArray(resources.Layers.Configurations, resources);
        target.SetProperty("layerConfigurations", layerConfigurations);

        using JSObject attachments = CreateAttachmentsArray(resources.Attachments.Items, resources);
        target.SetProperty("attachments", attachments);

        return target;
    }

    /// <summary>
    /// Builds the result of a document that failed to load, <c>{ ok: false, reason, message }</c>.
    /// </summary>
    private static JSObject CreateDocumentResult(string reason, string message)
    {
        JSObject target = CreateObject();
        target.SetProperty("ok", false);
        target.SetProperty("reason", reason);
        target.SetProperty("message", message);

        return target;
    }

    /// <summary>
    /// Reads a credential <c>{ type: 'password', password }</c>, or returns <see langword="null"/> when
    /// <paramref name="source"/> is <see langword="null"/> or its type is unknown.
    /// </summary>
    private static PdfCredential? ReadCredential(JSObject? source)
    {
        if (source == null)
        {
            return null;
        }

        PdfCredential? credential = source.GetPropertyAsString("type") switch
        {
            "password" => new PdfPasswordCredential(source.GetPropertyAsString("password") ?? string.Empty),
            _ => null
        };

        return credential;
    }

    /// <summary>
    /// Builds the result of a read embedded file, <c>{ ok: true, bytes }</c>.
    /// </summary>
    private static JSObject CreateFileResult(byte[] bytes)
    {
        JSObject target = CreateObject();
        target.SetProperty("ok", true);
        target.SetProperty("bytes", bytes);

        return target;
    }

    /// <summary>
    /// Builds the result of a document or embedded file that is not opened without a credential, <c>{ ok: false, reason,
    /// message, credentialRequest }</c>, with the last credential request of the document as <c>{ reason, authEvent }</c>,
    /// or null when the document requested none.
    /// </summary>
    private static JSObject CreateCredentialRequestedResult(string reason, string message, PdfCredentialRequest? credentialRequest)
    {
        JSObject target = CreateDocumentResult(reason, message);

        if (credentialRequest == null)
        {
            target.SetProperty("credentialRequest", (JSObject?)null);
            return target;
        }

        using JSObject requestObject = CreateObject();
        requestObject.SetProperty("reason", ToString(credentialRequest.Reason));
        requestObject.SetProperty("authEvent", ToString(credentialRequest.AuthEvent));
        target.SetProperty("credentialRequest", requestObject);

        return target;
    }

    /// <summary>
    /// Returns the JS name of <paramref name="reason"/>.
    /// </summary>
    private static string ToString(PdfCredentialRequestReason reason)
        => (reason == PdfCredentialRequestReason.CredentialRejected) ? "rejected" : "required";

    /// <summary>
    /// Returns the JS name of <paramref name="authEvent"/>.
    /// </summary>
    private static string ToString(PdfAuthEvent authEvent)
        => (authEvent == PdfAuthEvent.EmbeddedFileOpen) ? "embeddedFileOpen" : "documentOpen";

    /// <summary>
    /// Sets the JS array built from <paramref name="matches"/> and marks the search results as changed on the redraw state.
    /// </summary>
    private static void SetSearchResults(JSObject state, IReadOnlyList<PdfPanelSearchMatch> matches)
    {
        state.SetProperty("searchResultsChanged", true);

        using JSObject results = CreateArray();

        for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
        {
            PdfPanelSearchMatch match = matches[matchIndex];

            using JSObject result = CreateObject();
            using JSObject bounds = CreateRectangle(match.Bounds);
            result.SetProperty("pageNumber", match.Range.PageNumber);
            result.SetProperty("startIndex", match.Range.StartIndex);
            result.SetProperty("length", match.Range.Length);
            result.SetProperty("bounds", bounds);

            results.SetProperty(matchIndex.ToString(CultureInfo.InvariantCulture), result);
        }

        state.SetProperty("searchResults", results);
    }

    /// <summary>
    /// Sets the JS popup object built from <paramref name="popup"/>, or null when no annotation is active,
    /// and marks the popup as changed on the redraw state.
    /// </summary>
    private static void SetAnnotationPopup(JSObject state, PdfAnnotationPopup? popup)
    {
        state.SetProperty("annotationPopupChanged", true);

        if (popup == null)
        {
            state.SetProperty("annotationPopup", (JSObject?)null);
            return;
        }

        using JSObject popupObject = CreateObject();
        using JSObject messagesArray = CreateMessagesArray(popup.Messages);
        popupObject.SetProperty("isInteractive", popup.IsInteractive);
        popupObject.SetProperty("messages", messagesArray);

        state.SetProperty("annotationPopup", popupObject);
    }

    /// <summary>
    /// Sets the handle of the layer configuration content is rendered with and the handles of the visible layers
    /// of every configuration, and marks the layers as changed on the redraw state.
    /// </summary>
    private static void SetLayers(JSObject state, PdfPanelResources resources)
    {
        state.SetProperty("layersChanged", true);

        int? configurationHandle = GetLayerConfigurationHandle(resources.Layers);

        if (configurationHandle == null)
        {
            state.SetProperty("layerConfiguration", (JSObject?)null);
        }
        else
        {
            state.SetProperty("layerConfiguration", configurationHandle.Value);
        }

        using JSObject visibleLayers = CreateArray();
        int visibleCount = 0;

        for (int handle = 0; handle < resources.LayerItems.Count; handle++)
        {
            if (resources.LayerItems[handle].IsVisible)
            {
                visibleLayers.SetProperty(visibleCount.ToString(CultureInfo.InvariantCulture), handle);
                visibleCount++;
            }
        }

        state.SetProperty("visibleLayers", visibleLayers);
    }

    /// <summary>
    /// Builds the selected content <c>{ type: 'image', png }</c> of a selected image.
    /// </summary>
    private static JSObject CreateSelectedImage(byte[] png)
    {
        JSObject content = CreateObject();
        content.SetProperty("type", "image");
        content.SetProperty("png", png);

        return content;
    }

    /// <summary>
    /// Builds the selected content <c>{ type: 'text', text }</c> of selected text.
    /// </summary>
    private static JSObject CreateSelectedText(string text)
    {
        JSObject content = CreateObject();
        content.SetProperty("type", "text");
        content.SetProperty("text", text);

        return content;
    }

    /// <summary>
    /// Builds the update <c>{ pageNumber, png }</c> of the thumbnail of a page.
    /// </summary>
    private static JSObject CreateThumbnailUpdate(int pageNumber, byte[] png)
    {
        JSObject update = CreateObject();
        update.SetProperty("pageNumber", pageNumber);
        update.SetProperty("png", png);

        return update;
    }

    /// <summary>
    /// Returns the reason reported to JS for an exception thrown while reading a document:
    /// <c>encrypted</c>, <c>unsupported</c>, <c>invalid</c> or <c>error</c>.
    /// </summary>
    private static string GetDocumentErrorReason(Exception exception)
    {
        if (exception is PdfAuthenticationException)
        {
            return "encrypted";
        }

        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is NotSupportedException)
            {
                return "unsupported";
            }
        }

        return (exception is PdfInvalidDocumentException) ? "invalid" : "error";
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
    /// Returns the PDF name of <paramref name="mode"/>, <c>Unknown</c> for an unrecognized value, or
    /// <see langword="null"/> when the document defines none.
    /// </summary>
    private static string? ToString(PdfPageMode? mode)
    {
        return mode switch
        {
            null => null,
            PdfPageMode.UseNone => "UseNone",
            PdfPageMode.UseOutlines => "UseOutlines",
            PdfPageMode.UseThumbs => "UseThumbs",
            PdfPageMode.FullScreen => "FullScreen",
            PdfPageMode.UseOC => "UseOC",
            PdfPageMode.UseAttachments => "UseAttachments",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Returns the PDF name of <paramref name="layout"/>, <c>Unknown</c> for an unrecognized value, or
    /// <see langword="null"/> when the document defines none.
    /// </summary>
    private static string? ToString(PdfPageLayout? layout)
    {
        return layout switch
        {
            null => null,
            PdfPageLayout.SinglePage => "SinglePage",
            PdfPageLayout.OneColumn => "OneColumn",
            PdfPageLayout.TwoColumnLeft => "TwoColumnLeft",
            PdfPageLayout.TwoColumnRight => "TwoColumnRight",
            PdfPageLayout.TwoPageLeft => "TwoPageLeft",
            PdfPageLayout.TwoPageRight => "TwoPageRight",
            _ => "Unknown"
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
    /// Builds <c>{ left, top, right, bottom }</c> of <paramref name="rectangle"/>.
    /// </summary>
    private static JSObject CreateRectangle(in PdfRectangle rectangle)
    {
        JSObject rectangleObject = CreateObject();
        rectangleObject.SetProperty("left", rectangle.Left);
        rectangleObject.SetProperty("top", rectangle.Top);
        rectangleObject.SetProperty("right", rectangle.Right);
        rectangleObject.SetProperty("bottom", rectangle.Bottom);

        return rectangleObject;
    }

    /// <summary>
    /// Builds the JS array of <paramref name="messages"/>, each with its replies nested.
    /// </summary>
    private static JSObject CreateMessagesArray(IReadOnlyList<PdfAnnotationMessage> messages)
    {
        JSObject messagesArray = CreateArray();

        for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            PdfAnnotationMessage message = messages[messageIndex];

            using JSObject messageObject = CreateObject();
            using JSObject repliesArray = CreateMessagesArray(message.Replies);
            messageObject.SetProperty("title", message.Title);
            messageObject.SetProperty("contents", message.Contents);
            messageObject.SetProperty("creationDate", message.CreationDate?.ToString("o"));
            messageObject.SetProperty("replies", repliesArray);

            messagesArray.SetProperty(messageIndex.ToString(CultureInfo.InvariantCulture), messageObject);
        }

        return messagesArray;
    }

    /// <summary>
    /// Builds the document information <c>{ title, author, subject, keywords, creator, producer, creationDate,
    /// modificationDate, isTrapped, customEntries }</c>, the dates in ISO 8601.
    /// </summary>
    private static JSObject CreateDocumentInformation(PdfDocumentInformation information)
    {
        JSObject informationObject = CreateObject();
        informationObject.SetProperty("title", information.Title?.DecodePdfString());
        informationObject.SetProperty("author", information.Author?.DecodePdfString());
        informationObject.SetProperty("subject", information.Subject?.DecodePdfString());
        informationObject.SetProperty("keywords", information.Keywords?.DecodePdfString());
        informationObject.SetProperty("creator", information.Creator?.DecodePdfString());
        informationObject.SetProperty("producer", information.Producer?.DecodePdfString());
        informationObject.SetProperty("creationDate", information.CreationDate?.ToString("o", CultureInfo.InvariantCulture));
        informationObject.SetProperty("modificationDate", information.ModificationDate?.ToString("o", CultureInfo.InvariantCulture));

        if (information.IsTrapped == null)
        {
            informationObject.SetProperty("isTrapped", (JSObject?)null);
        }
        else
        {
            informationObject.SetProperty("isTrapped", information.IsTrapped.Value);
        }

        using JSObject customEntries = CreateObject();

        foreach (KeyValuePair<PdfString, PdfString> entry in information.CustomEntries)
        {
            customEntries.SetProperty(entry.Key.DecodePdfString(), entry.Value.DecodePdfString());
        }

        informationObject.SetProperty("customEntries", customEntries);

        return informationObject;
    }

    /// <summary>
    /// Builds <c>{ print, modify, copy, annotate, fillForms, assemble, highQualityPrint }</c> of <paramref name="permissions"/>.
    /// </summary>
    private static JSObject CreatePermissions(PdfPermissions permissions)
    {
        JSObject permissionsObject = CreateObject();
        permissionsObject.SetProperty("print", (permissions & PdfPermissions.Print) != 0);
        permissionsObject.SetProperty("modify", (permissions & PdfPermissions.Modify) != 0);
        permissionsObject.SetProperty("copy", (permissions & PdfPermissions.Copy) != 0);
        permissionsObject.SetProperty("annotate", (permissions & PdfPermissions.Annotate) != 0);
        permissionsObject.SetProperty("fillForms", (permissions & PdfPermissions.FillForms) != 0);
        permissionsObject.SetProperty("assemble", (permissions & PdfPermissions.Assemble) != 0);
        permissionsObject.SetProperty("highQualityPrint", (permissions & PdfPermissions.HighQualityPrint) != 0);

        return permissionsObject;
    }

    /// <summary>
    /// Builds the JS array of the static info <c>{ label, cropBox, rotation }</c> of <paramref name="pages"/>, in page order.
    /// </summary>
    private static JSObject CreatePagesArray(PdfPanelPageCollection pages)
    {
        JSObject pagesArray = CreateArray();

        for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            PdfPanelPageInfo info = pages[pageIndex].Info;

            using JSObject pageObject = CreateObject();
            using JSObject cropBox = CreateRectangle(info.CropBox);
            pageObject.SetProperty("label", info.Label);
            pageObject.SetProperty("cropBox", cropBox);
            pageObject.SetProperty("rotation", info.Rotation);

            pagesArray.SetProperty(pageIndex.ToString(CultureInfo.InvariantCulture), pageObject);
        }

        return pagesArray;
    }

    /// <summary>
    /// Builds the JS array of <paramref name="items"/>, <c>{ handle, title, isBold, isItalic, color, isExpanded, children }</c>,
    /// and adds every item to <paramref name="outlineItems"/> under its handle.
    /// </summary>
    private static JSObject CreateOutlineArray(IReadOnlyList<PdfOutlineItemViewModel> items, List<PdfOutlineItemViewModel> outlineItems)
    {
        JSObject itemsArray = CreateArray();

        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            PdfOutlineItemViewModel item = items[itemIndex];
            int handle = outlineItems.Count;
            outlineItems.Add(item);

            using JSObject itemObject = CreateObject();
            using JSObject childrenArray = CreateOutlineArray(item.Children, outlineItems);
            itemObject.SetProperty("handle", handle);
            itemObject.SetProperty("title", item.Title);
            itemObject.SetProperty("isBold", item.IsBold);
            itemObject.SetProperty("isItalic", item.IsItalic);
            itemObject.SetProperty("color", (item.Color != null) ? ToHexColor(item.Color.Value) : null);
            itemObject.SetProperty("isExpanded", item.IsExpanded);
            itemObject.SetProperty("children", childrenArray);

            itemsArray.SetProperty(itemIndex.ToString(CultureInfo.InvariantCulture), itemObject);
        }

        return itemsArray;
    }

    /// <summary>
    /// Builds the JS array of <paramref name="configurations"/>, <c>{ handle, name, isDefault, layers }</c>,
    /// the handle of each being its index.
    /// </summary>
    private static JSObject CreateLayerConfigurationsArray(
        IReadOnlyList<PdfLayerConfigurationViewModel> configurations,
        PdfPanelResources resources)
    {
        JSObject configurationsArray = CreateArray();

        for (int configurationIndex = 0; configurationIndex < configurations.Count; configurationIndex++)
        {
            PdfLayerConfigurationViewModel configuration = configurations[configurationIndex];

            using JSObject configurationObject = CreateObject();
            using JSObject layersArray = CreateLayersArray(configuration.Layers, resources);
            configurationObject.SetProperty("handle", configurationIndex);
            configurationObject.SetProperty("name", configuration.Name);
            configurationObject.SetProperty("isDefault", configuration.IsDefault);
            configurationObject.SetProperty("layers", layersArray);

            configurationsArray.SetProperty(configurationIndex.ToString(CultureInfo.InvariantCulture), configurationObject);
        }

        return configurationsArray;
    }

    /// <summary>
    /// Builds the JS array of <paramref name="layers"/>, <c>{ handle, name, isLayer, isLocked, children }</c>, adds every node
    /// to the layer items of <paramref name="resources"/> under its handle and subscribes the panel to its visibility.
    /// </summary>
    private static JSObject CreateLayersArray(IReadOnlyList<PdfLayerViewModel> layers, PdfPanelResources resources)
    {
        JSObject layersArray = CreateArray();

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            PdfLayerViewModel layer = layers[layerIndex];
            int handle = resources.LayerItems.Count;
            resources.LayerItems.Add(layer);
            layer.PropertyChanged += resources.OnLayersPropertyChanged;

            using JSObject layerObject = CreateObject();
            using JSObject childrenArray = CreateLayersArray(layer.Children, resources);
            layerObject.SetProperty("handle", handle);
            layerObject.SetProperty("name", layer.Name);
            layerObject.SetProperty("isLayer", layer.IsLayer);
            layerObject.SetProperty("isLocked", layer.IsLocked);
            layerObject.SetProperty("children", childrenArray);

            layersArray.SetProperty(layerIndex.ToString(CultureInfo.InvariantCulture), layerObject);
        }

        return layersArray;
    }

    /// <summary>
    /// Builds the JS array of <paramref name="attachments"/>, <c>{ file, pageNumber }</c>.
    /// </summary>
    private static JSObject CreateAttachmentsArray(IReadOnlyList<PdfAttachmentViewModel> attachments, PdfPanelResources resources)
    {
        JSObject attachmentsArray = CreateArray();

        for (int attachmentIndex = 0; attachmentIndex < attachments.Count; attachmentIndex++)
        {
            PdfAttachmentViewModel attachment = attachments[attachmentIndex];

            using JSObject attachmentObject = CreateObject();
            using JSObject file = CreateFile(attachment.FileSpecification, resources);
            attachmentObject.SetProperty("file", file);

            if (attachment.PageNumber == null)
            {
                attachmentObject.SetProperty("pageNumber", (JSObject?)null);
            }
            else
            {
                attachmentObject.SetProperty("pageNumber", attachment.PageNumber.Value);
            }

            attachmentsArray.SetProperty(attachmentIndex.ToString(CultureInfo.InvariantCulture), attachmentObject);
        }

        return attachmentsArray;
    }

    /// <summary>
    /// Builds the file <c>{ handle, name, description }</c> of <paramref name="fileSpecification"/>, adding an embedded
    /// file to the files of <paramref name="resources"/> under its handle the first time; the handle is null for a file
    /// that is not embedded.
    /// </summary>
    private static JSObject CreateFile(PdfFileSpecification fileSpecification, PdfPanelResources resources)
    {
        JSObject file = CreateObject();
        int? handle = GetFileHandle(fileSpecification, resources);

        if (handle == null)
        {
            file.SetProperty("handle", (JSObject?)null);
        }
        else
        {
            file.SetProperty("handle", handle.Value);
        }

        PdfString? name = fileSpecification.UnicodeFile ?? fileSpecification.File;
        file.SetProperty("name", name?.DecodePdfString());
        file.SetProperty("description", fileSpecification.Description?.DecodePdfString());

        return file;
    }

    /// <summary>
    /// Returns the handle of the embedded file of <paramref name="fileSpecification"/>, adding it to the files of
    /// <paramref name="resources"/> the first time, or <see langword="null"/> when the file is not embedded.
    /// </summary>
    private static int? GetFileHandle(PdfFileSpecification fileSpecification, PdfPanelResources resources)
    {
        if (fileSpecification.EmbeddedFile == null && fileSpecification.UnicodeEmbeddedFile == null)
        {
            return null;
        }

        if (resources.FileHandles.TryGetValue(fileSpecification, out int handle))
        {
            return handle;
        }

        handle = resources.Files.Count;
        resources.Files.Add(fileSpecification);
        resources.FileHandles.Add(fileSpecification, handle);

        return handle;
    }

    /// <summary>
    /// Sets the JS array of the requested actions of <paramref name="resources"/>, <c>{ source, action }</c> in request order,
    /// on the redraw state.
    /// </summary>
    private static void SetActionRequests(JSObject state, PdfPanelResources resources)
    {
        using JSObject requests = CreateArray();

        for (int requestIndex = 0; requestIndex < resources.ActionRequests.Count; requestIndex++)
        {
            PdfPanelActionEventArgs args = resources.ActionRequests[requestIndex];

            using JSObject request = CreateObject();
            using JSObject action = CreateAction(args.Action, resources);
            request.SetProperty("source", ToString(args.Source));
            request.SetProperty("action", action);

            requests.SetProperty(requestIndex.ToString(CultureInfo.InvariantCulture), request);
        }

        state.SetProperty("actionRequests", requests);
    }

    /// <summary>
    /// Builds the JS action of <paramref name="action"/>: <c>uri</c>, <c>launch</c>, <c>goToRemote</c> and <c>named</c>
    /// with their values, any other action as <c>{ type: 'unsupported', actionType }</c> with its PDF name.
    /// </summary>
    private static JSObject CreateAction(PdfAction action, PdfPanelResources resources)
    {
        JSObject actionObject = CreateObject();

        switch (action)
        {
            case PdfUriAction uriAction:
            {
                actionObject.SetProperty("type", "uri");
                actionObject.SetProperty("uri", uriAction.Uri?.DecodePdfString());
                break;
            }
            case PdfLaunchAction launchAction:
            {
                actionObject.SetProperty("type", "launch");
                SetFileActionProperties(actionObject, launchAction, resources);
                break;
            }
            case PdfGoToRemoteAction goToRemoteAction:
            {
                actionObject.SetProperty("type", "goToRemote");
                SetFileActionProperties(actionObject, goToRemoteAction, resources);

                using JSObject? destination = CreateRemoteDestination(goToRemoteAction.Destination);
                actionObject.SetProperty("destination", destination);
                break;
            }
            case PdfNamedAction namedAction:
            {
                actionObject.SetProperty("type", "named");
                actionObject.SetProperty("name", namedAction.RawName?.DecodePdfString());
                break;
            }
            default:
            {
                actionObject.SetProperty("type", "unsupported");
                actionObject.SetProperty("actionType", ToString(action.ActionType));
                break;
            }
        }

        return actionObject;
    }

    /// <summary>
    /// Sets <c>file</c>, or null when the action names none, and <c>newWindow</c> of <paramref name="action"/>
    /// on <paramref name="target"/>.
    /// </summary>
    private static void SetFileActionProperties(JSObject target, PdfFileAction action, PdfPanelResources resources)
    {
        if (action.FileSpecification == null)
        {
            target.SetProperty("file", (JSObject?)null);
        }
        else
        {
            using JSObject file = CreateFile(action.FileSpecification, resources);
            target.SetProperty("file", file);
        }

        if (action.NewWindow == null)
        {
            target.SetProperty("newWindow", (JSObject?)null);
        }
        else
        {
            target.SetProperty("newWindow", action.NewWindow.Value);
        }
    }

    /// <summary>
    /// Builds the destination in another document of <paramref name="destination"/>: a page with its view,
    /// <c>{ pageNumber, fitType, left, top, right, bottom, zoom }</c> with a 1-based page number, or a named destination
    /// <c>{ name }</c>; <see langword="null"/> for none or another destination.
    /// </summary>
    private static JSObject? CreateRemoteDestination(PdfDestination? destination)
    {
        switch (destination)
        {
            case PdfPageNumberDestination pageDestination:
            {
                JSObject destinationObject = CreateObject();
                destinationObject.SetProperty("pageNumber", pageDestination.PageNumber + 1);
                destinationObject.SetProperty("fitType", ToString(pageDestination.FitType));
                SetOptionalNumber(destinationObject, "left", pageDestination.Left);
                SetOptionalNumber(destinationObject, "top", pageDestination.Top);
                SetOptionalNumber(destinationObject, "right", pageDestination.Right);
                SetOptionalNumber(destinationObject, "bottom", pageDestination.Bottom);
                SetOptionalNumber(destinationObject, "zoom", pageDestination.Zoom);

                return destinationObject;
            }
            case PdfNamedDestination namedDestination:
            {
                JSObject destinationObject = CreateObject();
                destinationObject.SetProperty("name", namedDestination.Name.DecodePdfString());

                return destinationObject;
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
    /// Returns the fit type name of <paramref name="fitType"/>, the one <see cref="ReadDestination"/> reads.
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

    private static void SetOptionalNumber(JSObject target, string name, float? value)
    {
        if (value == null)
        {
            target.SetProperty(name, (JSObject?)null);
        }
        else
        {
            target.SetProperty(name, value.Value);
        }
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

    private static float ReadFloat(JSObject source, string name, float currentValue)
        => (source.HasProperty(name)) ? (float)source.GetPropertyAsDouble(name) : currentValue;

    private static int ReadInt32(JSObject source, string name, int currentValue)
        => (source.HasProperty(name)) ? source.GetPropertyAsInt32(name) : currentValue;

    private static TimeSpan ReadMilliseconds(JSObject source, string name, in TimeSpan currentValue)
        => (source.HasProperty(name)) ? TimeSpan.FromMilliseconds(source.GetPropertyAsDouble(name)) : currentValue;

    private static PdfPanelThumbnailPreloadMode ReadThumbnailPreloadMode(JSObject source, string name, PdfPanelThumbnailPreloadMode currentValue)
    {
        if (!source.HasProperty(name))
        {
            return currentValue;
        }

        return source.GetPropertyAsString(name) switch
        {
            "none" => PdfPanelThumbnailPreloadMode.None,
            "visiblePages" => PdfPanelThumbnailPreloadMode.VisiblePages,
            "nearestPages" => PdfPanelThumbnailPreloadMode.NearestPages,
            _ => currentValue
        };
    }

    private static bool ReadBoolean(JSObject source, string name, bool currentValue)
        => (source.HasProperty(name)) ? source.GetPropertyAsBoolean(name) : currentValue;

    private static PdfColor ReadColor(JSObject source, string name, in PdfColor currentValue)
    {
        string? value = source.GetPropertyAsString(name);
        return (value != null) ? PdfColor.ParseHexColor(value) : currentValue;
    }

    private static float? ReadOptionalFloat(JSObject source, string name)
        => (source.GetTypeOfProperty(name) == "number") ? (float)source.GetPropertyAsDouble(name) : null;

    private static string ToHexColor(in PdfColor color)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "#{0:X2}{1:X2}{2:X2}",
            ToColorByte(color.Red),
            ToColorByte(color.Green),
            ToColorByte(color.Blue));
    }

    private static int ToColorByte(float component) => (int)Math.Round(Math.Clamp(component, 0f, 1f) * 255f);

    [JSImport("createObject", "canvasInterop.js")]
    private static partial JSObject CreateObject();

    [JSImport("createArray", "canvasInterop.js")]
    private static partial JSObject CreateArray();
}
