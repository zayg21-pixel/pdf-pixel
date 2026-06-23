using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Parsing;

/// <summary>
/// Parses optional content groups from the document catalog's /OCProperties entry.
/// Resolves each OCG's name, default visibility from the /D configuration, and
/// presentation order from the /Order array.
/// </summary>
internal sealed class PdfOptionalContentGroupParser
{
    private readonly PdfObject _rootObject;
    private readonly ILogger<PdfOptionalContentGroupParser> _logger;

    /// <summary>
    /// Initializes a new <see cref="PdfOptionalContentGroupParser"/> for the given catalog root object.
    /// </summary>
    public PdfOptionalContentGroupParser(PdfObject rootObject, ILogger<PdfOptionalContentGroupParser> logger)
    {
        _rootObject = rootObject ?? throw new ArgumentNullException(nameof(rootObject));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses the optional content groups from the catalog.
    /// Returns an empty dictionary if no /OCProperties entry is found.
    /// </summary>
    public Dictionary<PdfReference, PdfOptionalContentGroup> Parse()
    {
        PdfDictionary? ocProperties = _rootObject.Dictionary.GetDictionary(PdfTokens.OCPropertiesKey);
        if (ocProperties == null)
        {
            return new Dictionary<PdfReference, PdfOptionalContentGroup>();
        }

        // Parse all OCG entries from /OCGs array.
        List<PdfObject>? ocgObjects = ocProperties.GetObjects(PdfTokens.OptionalContentGroupsKey);
        if (ocgObjects == null || ocgObjects.Count == 0)
        {
            return new Dictionary<PdfReference, PdfOptionalContentGroup>();
        }

        Dictionary<PdfReference, PdfOptionalContentGroup> groupsByReference = new(ocgObjects.Count);

        foreach (PdfObject ocgObject in ocgObjects)
        {
            if (!ocgObject.Reference.IsValid)
            {
                _logger.LogWarning("OCG entry is not an indirect reference; skipping");
                continue;
            }

            PdfString name = ocgObject.Dictionary.GetString(PdfTokens.NameKey);
            if (name.IsEmpty)
            {
                _logger.LogWarning("OCG {Reference} has no /Name entry; skipping", ocgObject.Reference);
                continue;
            }

            PdfOptionalContentGroup group = new(ocgObject.Reference, name);
            groupsByReference[ocgObject.Reference] = group;
        }

        if (groupsByReference.Count == 0)
        {
            return groupsByReference;
        }

        // Apply default visibility from /D configuration dictionary.
        PdfDictionary? defaultConfig = ocProperties.GetDictionary(PdfTokens.DefaultConfigKey);
        if (defaultConfig != null)
        {
            ApplyDefaultVisibility(defaultConfig, groupsByReference);
            ApplyOrder(defaultConfig, groupsByReference);
        }

        return groupsByReference;
    }

    private void ApplyDefaultVisibility(PdfDictionary defaultConfig, Dictionary<PdfReference, PdfOptionalContentGroup> groupsByReference)
    {
        // /ON array — groups that are visible by default.
        List<PdfObject>? onObjects = defaultConfig.GetObjects(PdfTokens.OnKey);
        if (onObjects != null)
        {
            foreach (PdfObject onObject in onObjects)
            {
                if (groupsByReference.TryGetValue(onObject.Reference, out PdfOptionalContentGroup? group))
                {
                    group.DefaultVisible = true;
                }
            }
        }

        // /OFF array — groups that are hidden by default.
        List<PdfObject>? offObjects = defaultConfig.GetObjects(PdfTokens.OffKey);
        if (offObjects != null)
        {
            foreach (PdfObject offObject in offObjects)
            {
                if (groupsByReference.TryGetValue(offObject.Reference, out PdfOptionalContentGroup? group))
                {
                    group.DefaultVisible = false;
                }
            }
        }
    }

    private void ApplyOrder(PdfDictionary defaultConfig, Dictionary<PdfReference, PdfOptionalContentGroup> groupsByReference)
    {
        PdfArray? orderArray = defaultConfig.GetArray(PdfTokens.OrderKey);
        if (orderArray == null)
        {
            return;
        }

        int orderIndex = 0;
        FlattenOrderArray(orderArray, groupsByReference, ref orderIndex);
    }

    private void FlattenOrderArray(PdfArray orderArray, Dictionary<PdfReference, PdfOptionalContentGroup> groupsByReference, ref int orderIndex)
    {
        for (int index = 0; index < orderArray.Count; index++)
        {
            // Nested arrays define sub-groups in the UI hierarchy.
            PdfArray? nestedArray = orderArray.GetArray(index);
            if (nestedArray != null)
            {
                FlattenOrderArray(nestedArray, groupsByReference, ref orderIndex);
                continue;
            }

            PdfObject? orderObject = orderArray.GetObject(index);
            if (orderObject != null && groupsByReference.TryGetValue(orderObject.Reference, out PdfOptionalContentGroup? group))
            {
                group.Order = orderIndex;
                orderIndex++;
            }
        }
    }
}
