using PdfPixel.Color;
using PdfPixel.Encryption.Model;
using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Mvvm;
using PdfPixel.PdfPanel.Web.Dto.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PdfPixel.PdfPanel.Web.Dto.Converters;

/// <summary>
/// Converts the loaded document and its results to DTOs.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class DocumentDtoConverter
{
    /// <summary>
    /// Builds the JSON result of the document loaded into <paramref name="resources"/>: <c>ok: true</c>, its properties,
    /// pages, outline, layer configurations and attached files, each item JS passes back with its handle.
    /// </summary>
    public static string CreateDocumentResult(PdfPanelResources resources)
    {
        if (resources.Document == null
            || resources.Pages == null
            || resources.Outlines == null
            || resources.Layers == null
            || resources.Attachments == null)
        {
            return CreateDocumentError("error", "No document is loaded.");
        }

        IPdfDocument document = resources.Document;

        DocumentResultDto result = new()
        {
            PageCount = resources.Pages.Count,
            Version = document.Version?.ToString(),
            PageMode = ToString(document.PageMode),
            PageLayout = ToString(document.PageLayout),
            Lang = document.Lang?.DecodePdfString(),
            Information = (document.Information != null) ? CreateDocumentInformation(document.Information) : null,
            Permissions = (document.Permissions != null) ? CreatePermissions(document.Permissions.Value) : null,
            Pages = CreatePages(resources.Pages),
            Outlines = CreateOutline(resources.Outlines.Items, resources.OutlineItems),
            LayerConfigurations = CreateLayerConfigurations(resources.Layers.Configurations, resources),
            Attachments = CreateAttachments(resources.Attachments.Items, resources)
        };

        return JsonSerializer.Serialize(result, DtoJsonContext.Default.DocumentResultDto);
    }

    /// <summary>
    /// Builds the JSON result of a document that failed to load, <c>{ ok: false, reason, message }</c>.
    /// </summary>
    public static string CreateDocumentError(string reason, string message)
    {
        DocumentErrorDto result = new()
        {
            Reason = reason,
            Message = message
        };

        return JsonSerializer.Serialize(result, DtoJsonContext.Default.DocumentErrorDto);
    }

    /// <summary>
    /// Builds the JSON result of a document that is not opened without a credential, <c>{ ok: false, reason,
    /// message, credentialRequest }</c>, with the last credential request of the document, or null when the document
    /// requested none.
    /// </summary>
    public static string CreateCredentialRequestedResult(
        string reason,
        string message,
        PdfCredentialRequest? credentialRequest)
    {
        CredentialRequestedResultDto result = new()
        {
            Reason = reason,
            Message = message,
            CredentialRequest = (credentialRequest != null) ? CreateCredentialRequest(credentialRequest) : null
        };

        return JsonSerializer.Serialize(result, DtoJsonContext.Default.CredentialRequestedResultDto);
    }

    /// <summary>
    /// Returns the JS name of <paramref name="reason"/>.
    /// </summary>
    public static string ToString(PdfCredentialRequestReason reason)
        => (reason == PdfCredentialRequestReason.CredentialRejected) ? "rejected" : "required";

    /// <summary>
    /// Returns the JS name of <paramref name="authEvent"/>.
    /// </summary>
    public static string ToString(PdfAuthEvent authEvent)
        => (authEvent == PdfAuthEvent.EmbeddedFileOpen) ? "embeddedFileOpen" : "documentOpen";

    /// <summary>
    /// Builds the file <c>{ handle, name, description }</c> of <paramref name="fileSpecification"/>, adding an embedded
    /// file to the files of <paramref name="resources"/> under its handle the first time; the handle is null for a file
    /// that is not embedded.
    /// </summary>
    public static FileDto CreateFile(PdfFileSpecification fileSpecification, PdfPanelResources resources)
    {
        PdfString? name = fileSpecification.UnicodeFile ?? fileSpecification.File;

        FileDto file = new()
        {
            Handle = GetFileHandle(fileSpecification, resources),
            Name = name?.DecodePdfString(),
            Description = fileSpecification.Description?.DecodePdfString()
        };

        return file;
    }

    /// <summary>
    /// Builds the credential request <c>{ reason, authEvent }</c> of <paramref name="credentialRequest"/>.
    /// </summary>
    private static CredentialRequestDto CreateCredentialRequest(PdfCredentialRequest credentialRequest)
    {
        CredentialRequestDto request = new()
        {
            Reason = ToString(credentialRequest.Reason),
            AuthEvent = ToString(credentialRequest.AuthEvent)
        };

        return request;
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
    /// Builds the document information <c>{ title, author, subject, keywords, creator, producer, creationDate,
    /// modificationDate, isTrapped, customEntries }</c>, the dates in ISO 8601.
    /// </summary>
    private static DocumentInformationDto CreateDocumentInformation(PdfDocumentInformation information)
    {
        Dictionary<string, string> customEntries = [];

        foreach (KeyValuePair<PdfString, PdfString> entry in information.CustomEntries)
        {
            customEntries[entry.Key.DecodePdfString()] = entry.Value.DecodePdfString();
        }

        DocumentInformationDto informationDto = new()
        {
            Title = information.Title?.DecodePdfString(),
            Author = information.Author?.DecodePdfString(),
            Subject = information.Subject?.DecodePdfString(),
            Keywords = information.Keywords?.DecodePdfString(),
            Creator = information.Creator?.DecodePdfString(),
            Producer = information.Producer?.DecodePdfString(),
            CreationDate = information.CreationDate?.ToString("o", CultureInfo.InvariantCulture),
            ModificationDate = information.ModificationDate?.ToString("o", CultureInfo.InvariantCulture),
            IsTrapped = information.IsTrapped,
            CustomEntries = customEntries
        };

        return informationDto;
    }

    /// <summary>
    /// Builds <c>{ print, modify, copy, annotate, fillForms, assemble, highQualityPrint }</c> of <paramref name="permissions"/>.
    /// </summary>
    private static PermissionsDto CreatePermissions(PdfPermissions permissions)
    {
        PermissionsDto permissionsDto = new()
        {
            Print = (permissions & PdfPermissions.Print) != 0,
            Modify = (permissions & PdfPermissions.Modify) != 0,
            Copy = (permissions & PdfPermissions.Copy) != 0,
            Annotate = (permissions & PdfPermissions.Annotate) != 0,
            FillForms = (permissions & PdfPermissions.FillForms) != 0,
            Assemble = (permissions & PdfPermissions.Assemble) != 0,
            HighQualityPrint = (permissions & PdfPermissions.HighQualityPrint) != 0
        };

        return permissionsDto;
    }

    /// <summary>
    /// Builds the static info <c>{ label, cropBox, rotation }</c> of <paramref name="pages"/>, in page order.
    /// </summary>
    private static List<PageDto> CreatePages(PdfPanelPageCollection pages)
    {
        List<PageDto> pageList = new(pages.Count);

        for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            PdfPanelPageInfo info = pages[pageIndex].Info;

            PageDto page = new()
            {
                Label = info.Label,
                CropBox = new RectangleDto
                {
                    Left = info.CropBox.Left,
                    Top = info.CropBox.Top,
                    Right = info.CropBox.Right,
                    Bottom = info.CropBox.Bottom
                },
                Rotation = info.Rotation
            };

            pageList.Add(page);
        }

        return pageList;
    }

    /// <summary>
    /// Builds <paramref name="items"/>, <c>{ handle, title, isBold, isItalic, color, isExpanded, children }</c>,
    /// and adds every item to <paramref name="outlineItems"/> under its handle.
    /// </summary>
    private static List<OutlineItemDto> CreateOutline(IReadOnlyList<PdfOutlineItemViewModel> items, List<PdfOutlineItemViewModel> outlineItems)
    {
        List<OutlineItemDto> itemList = new(items.Count);

        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            PdfOutlineItemViewModel item = items[itemIndex];
            int handle = outlineItems.Count;
            outlineItems.Add(item);

            OutlineItemDto itemDto = new()
            {
                Handle = handle,
                Title = item.Title,
                IsBold = item.IsBold,
                IsItalic = item.IsItalic,
                Color = (item.Color != null) ? ToHexColor(item.Color.Value) : null,
                IsExpanded = item.IsExpanded,
                Children = CreateOutline(item.Children, outlineItems)
            };

            itemList.Add(itemDto);
        }

        return itemList;
    }

    /// <summary>
    /// Builds <paramref name="configurations"/>, <c>{ handle, name, isDefault, layers }</c>, the handle of each being its index.
    /// </summary>
    private static List<LayerConfigurationDto> CreateLayerConfigurations(
        IReadOnlyList<PdfLayerConfigurationViewModel> configurations,
        PdfPanelResources resources)
    {
        List<LayerConfigurationDto> configurationList = new(configurations.Count);

        for (int configurationIndex = 0; configurationIndex < configurations.Count; configurationIndex++)
        {
            PdfLayerConfigurationViewModel configuration = configurations[configurationIndex];

            LayerConfigurationDto configurationDto = new()
            {
                Handle = configurationIndex,
                Name = configuration.Name,
                IsDefault = configuration.IsDefault,
                Layers = CreateLayers(configuration.Layers, resources)
            };

            configurationList.Add(configurationDto);
        }

        return configurationList;
    }

    /// <summary>
    /// Builds <paramref name="layers"/>, <c>{ handle, name, isLayer, isLocked, children }</c>, adds every node
    /// to the layer items of <paramref name="resources"/> under its handle and subscribes the panel to its visibility.
    /// </summary>
    private static List<LayerDto> CreateLayers(IReadOnlyList<PdfLayerViewModel> layers, PdfPanelResources resources)
    {
        List<LayerDto> layerList = new(layers.Count);

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            PdfLayerViewModel layer = layers[layerIndex];
            int handle = resources.LayerItems.Count;
            resources.LayerItems.Add(layer);
            layer.PropertyChanged += resources.OnLayersPropertyChanged;

            LayerDto layerDto = new()
            {
                Handle = handle,
                Name = layer.Name,
                IsLayer = layer.IsLayer,
                IsLocked = layer.IsLocked,
                Children = CreateLayers(layer.Children, resources)
            };

            layerList.Add(layerDto);
        }

        return layerList;
    }

    /// <summary>
    /// Builds <paramref name="attachments"/>, <c>{ file, pageNumber }</c>.
    /// </summary>
    private static List<AttachmentDto> CreateAttachments(IReadOnlyList<PdfAttachmentViewModel> attachments, PdfPanelResources resources)
    {
        List<AttachmentDto> attachmentList = new(attachments.Count);

        for (int attachmentIndex = 0; attachmentIndex < attachments.Count; attachmentIndex++)
        {
            PdfAttachmentViewModel attachment = attachments[attachmentIndex];

            AttachmentDto attachmentDto = new()
            {
                File = CreateFile(attachment.FileSpecification, resources),
                PageNumber = attachment.PageNumber
            };

            attachmentList.Add(attachmentDto);
        }

        return attachmentList;
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
}
