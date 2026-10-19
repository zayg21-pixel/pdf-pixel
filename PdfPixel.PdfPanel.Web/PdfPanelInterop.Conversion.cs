using PdfPixel.Color;
using PdfPixel.Encryption.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Navigation.Model;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Thumbnails;
using PdfPixel.PdfPanel.Web.Dto.Converters;
using System;
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
        JSObject target = CreateObject();
        target.SetProperty("ok", false);
        target.SetProperty("reason", reason);
        target.SetProperty("message", message);

        if (credentialRequest == null)
        {
            target.SetProperty("credentialRequest", (JSObject?)null);
            return target;
        }

        using JSObject requestObject = CreateObject();
        requestObject.SetProperty("reason", DocumentDtoConverter.ToString(credentialRequest.Reason));
        requestObject.SetProperty("authEvent", DocumentDtoConverter.ToString(credentialRequest.AuthEvent));
        target.SetProperty("credentialRequest", requestObject);

        return target;
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

    [JSImport("createObject", "canvasInterop.js")]
    private static partial JSObject CreateObject();
}
