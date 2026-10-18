using PdfPixel.Color;
using PdfPixel.Encryption.Model;
using PdfPixel.Geometry;
using PdfPixel.Navigation.Model;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Text;
using PdfPixel.PdfPanel.Thumbnails;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Reads panel values from JS objects and writes panel values to the properties of JS objects.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class PdfPanelJsConverter
{
    /// <summary>
    /// Reads the configuration keys present in <paramref name="source"/> into <paramref name="target"/>;
    /// keys absent from it keep their current values.
    /// </summary>
    public static void ReadConfiguration(JSObject source, PdfPanelConfiguration target)
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
        target.RenderPageThumbnails = ReadBoolean(source, "renderPageThumbnails", target.RenderPageThumbnails);
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

        using JSObject thumbnailRenderMaximumSize = source.GetPropertyAsJSObject("thumbnailRenderMaximumSize");

        if (thumbnailRenderMaximumSize != null)
        {
            PdfIntegerSize size = target.ThumbnailRenderMaximumSize;
            target.ThumbnailRenderMaximumSize = new PdfIntegerSize(
                ReadInt32(thumbnailRenderMaximumSize, "width", size.Width),
                ReadInt32(thumbnailRenderMaximumSize, "height", size.Height));
        }

        using JSObject layoutPadding = source.GetPropertyAsJSObject("layoutPadding");

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
    public static PdfPageNumberDestination ReadDestination(JSObject source)
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
    public static PdfPanelTextRange? ReadTextRange(JSObject source)
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
    public static PdfPanelAutoScaleMode? ReadAutoScaleMode(string value)
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
    /// Writes <c>pageNumber</c>, <c>startIndex</c> and <c>length</c> of <paramref name="match"/> to <paramref name="target"/>.
    /// </summary>
    public static void WriteSearchMatch(JSObject target, in PdfPanelSearchMatch match)
    {
        target.SetProperty("pageNumber", match.Range.PageNumber);
        target.SetProperty("startIndex", match.Range.StartIndex);
        target.SetProperty("length", match.Range.Length);
    }

    /// <summary>
    /// Writes <c>left</c>, <c>top</c>, <c>right</c> and <c>bottom</c> of <paramref name="rectangle"/> to <paramref name="target"/>.
    /// </summary>
    public static void WriteRectangle(JSObject target, in PdfRectangle rectangle)
    {
        target.SetProperty("left", rectangle.Left);
        target.SetProperty("top", rectangle.Top);
        target.SetProperty("right", rectangle.Right);
        target.SetProperty("bottom", rectangle.Bottom);
    }

    /// <summary>
    /// Writes <c>title</c>, <c>contents</c> and <c>creationDate</c> (ISO 8601) of <paramref name="message"/> to <paramref name="target"/>.
    /// </summary>
    public static void WriteAnnotationMessage(JSObject target, PdfAnnotationMessage message)
    {
        target.SetProperty("title", message.Title);
        target.SetProperty("contents", message.Contents);
        target.SetProperty("creationDate", message.CreationDate?.ToString("o"));
    }

    /// <summary>
    /// Writes the result of a loaded document, <c>{ ok: true, pageCount }</c>, to <paramref name="target"/>.
    /// </summary>
    public static void WriteDocumentResult(JSObject target, int pageCount)
    {
        target.SetProperty("ok", true);
        target.SetProperty("pageCount", pageCount);
    }

    /// <summary>
    /// Writes the result of a document that failed to load, <c>{ ok: false, reason, message }</c>, to <paramref name="target"/>.
    /// </summary>
    public static void WriteDocumentResult(JSObject target, string reason, string message)
    {
        target.SetProperty("ok", false);
        target.SetProperty("reason", reason);
        target.SetProperty("message", message);
    }

    /// <summary>
    /// Returns the reason reported to JS for an exception thrown while reading a document:
    /// <c>encrypted</c>, <c>unsupported</c>, <c>invalid</c> or <c>error</c>.
    /// </summary>
    public static string GetDocumentErrorReason(Exception exception)
    {
        if (exception is PdfAuthenticationException)
        {
            return "encrypted";
        }

        for (Exception current = exception; current != null; current = current.InnerException)
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
    public static string ToString(PdfPanelAutoScaleMode mode)
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
    public static string ToCursorStyle(PdfPanelCursor cursor)
    {
        return cursor switch
        {
            PdfPanelCursor.Hand => "pointer",
            PdfPanelCursor.IBeam => "text",
            _ => null
        };
    }

    private static float ReadFloat(JSObject source, string name, float currentValue)
        => source.HasProperty(name) ? (float)source.GetPropertyAsDouble(name) : currentValue;

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
        => source.HasProperty(name) ? source.GetPropertyAsBoolean(name) : currentValue;

    private static PdfColor ReadColor(JSObject source, string name, PdfColor currentValue)
        => source.HasProperty(name) ? PdfColor.ParseHexColor(source.GetPropertyAsString(name)) : currentValue;

    private static float? ReadOptionalFloat(JSObject source, string name)
        => (source.GetTypeOfProperty(name) == "number") ? (float)source.GetPropertyAsDouble(name) : null;
}
