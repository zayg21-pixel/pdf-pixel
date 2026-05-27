using PdfPixel.Models;
using SkiaSharp;
using System;
using PdfPixel.Text;
using PdfPixel.Rendering.Operators;
using PdfPixel.Transparency.Utilities;
using PdfPixel.Transparency.Model;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;

namespace PdfPixel.Rendering.State
{
    /// <summary>
    /// Parses and applies PDF graphics state parameters from ExtGState dictionaries.
    /// Responsible for parsing PDF graphics state dictionaries and producing <see cref="PdfGraphicsStateParameters"/> instances
    /// which can then be applied to a target <see cref="PdfGraphicsState"/>.
    /// </summary>
    public static class PdfGraphicsStateParser
    {
        /// <summary>
        /// Parse graphics state parameters into a parameter container without mutating the target state.
        /// </summary>
        /// <param name="gsDict">Graphics state dictionary.</param>
        /// <param name="page">Owning page (used for soft mask parsing).</param>
        /// <returns>Populated parameters container.</returns>
        internal static PdfGraphicsStateParameters ParseGraphicsStateParametersFromDictionary(PdfDictionary gsDict, IPdfPageInternal page)
        {
            PdfGraphicsStateParameters parameters = new();
            if (gsDict == null)
            {
                return parameters;
            }

            if (gsDict.HasKey(PdfTokens.AlphaIsShapeKey))
            {
                parameters.AlphaIsShape = gsDict.GetBooleanOrDefault(PdfTokens.AlphaIsShapeKey);
            }

            if (gsDict.HasKey(PdfTokens.LineWidthKey))
            {
                parameters.LineWidth = gsDict.GetFloatOrDefault(PdfTokens.LineWidthKey);
            }

            if (gsDict.HasKey(PdfTokens.LineCapKey))
            {
                float capStyle = gsDict.GetFloatOrDefault(PdfTokens.LineCapKey);
                parameters.LineCap = capStyle switch
                {
                    0 => SKStrokeCap.Butt,
                    1 => SKStrokeCap.Round,
                    2 => SKStrokeCap.Square,
                    _ => SKStrokeCap.Butt
                };
            }

            if (gsDict.HasKey(PdfTokens.LineJoinKey))
            {
                float joinStyle = gsDict.GetFloatOrDefault(PdfTokens.LineJoinKey);
                parameters.LineJoin = joinStyle switch
                {
                    0 => SKStrokeJoin.Miter,
                    1 => SKStrokeJoin.Round,
                    2 => SKStrokeJoin.Bevel,
                    _ => SKStrokeJoin.Miter
                };
            }

            if (gsDict.HasKey(PdfTokens.MiterLimitKey))
            {
                parameters.MiterLimit = gsDict.GetFloatOrDefault(PdfTokens.MiterLimitKey);
            }

            if (gsDict.HasKey(PdfTokens.DashPatternKey))
            {
                PdfArray? dashArray = gsDict.GetArray(PdfTokens.DashPatternKey);
                if (dashArray?.Count >= 2)
                {
                    float[]? patternArray = dashArray.GetArray(0)?.GetFloatArray();
                    float phase = dashArray.GetFloatOrDefault(1);

                    if (patternArray?.Length > 0)
                    {
                        parameters.DashPattern = patternArray;
                        parameters.DashPhase = phase;
                    }
                    else
                    {
                        // Empty array means solid line
                        parameters.DashPattern = null;
                        parameters.DashPhase = 0f;
                    }
                }
            }

            if (gsDict.HasKey(PdfTokens.StrokeAlphaKey)) // Stroke alpha (/CA)
            {
                float alpha = gsDict.GetFloatOrDefault(PdfTokens.StrokeAlphaKey);
                // Clamp alpha to valid range [0.0, 1.0] as per PDF specification
                parameters.StrokeAlpha = Math.Max(0f, Math.Min(1f, alpha));
            }

            if (gsDict.HasKey(PdfTokens.FillAlphaKey))   // Fill alpha (/ca)
            {
                float alpha = gsDict.GetFloatOrDefault(PdfTokens.FillAlphaKey);
                // Clamp alpha to valid range [0.0, 1.0] as per PDF specification
                parameters.FillAlpha = Math.Max(0f, Math.Min(1f, alpha));
            }

            if (gsDict.HasKey(PdfTokens.BlendModeKey))
            {
                // First try to get as name
                PdfString blendModeName = gsDict.GetName(PdfTokens.BlendModeKey);
                if (!blendModeName.IsEmpty)
                {
                    PdfBlendMode mode = blendModeName.AsEnum<PdfBlendMode>();
                    if (mode != PdfBlendMode.Unknown)
                    {
                        parameters.BlendMode = mode;
                    }
                }
                else
                {
                    // Handle blend mode arrays - PDF viewers should use the first supported blend mode
                    PdfArray? blendModeArray = gsDict.GetArray(PdfTokens.BlendModeKey);
                    if (blendModeArray?.Count > 0)
                    {
                        // Try each blend mode in the array until we find a supported one
                        for (int index = 0; index < blendModeArray.Count; index++)
                        {
                            PdfBlendMode mode = blendModeArray.GetName(index).AsEnum<PdfBlendMode>();

                            if (mode != PdfBlendMode.Unknown)
                            {
                                parameters.BlendMode = mode;
                                break;
                            }
                        }
                    }
                }
            }

            PdfArray? matrixArray = null;
            if (gsDict.HasKey(PdfTokens.MatrixKey)) // Custom transformation matrix
            {
                matrixArray = gsDict.GetArray(PdfTokens.MatrixKey);
            }
            else if (gsDict.HasKey(PdfTokens.CTMKey)) // Alternative key name
            {
                matrixArray = gsDict.GetArray(PdfTokens.CTMKey);
            }

            parameters.TransformMatrix = PdfLocationUtilities.CreateMatrix(matrixArray);

            // Soft Mask (/SMask)
            if (gsDict.HasKey(PdfTokens.SoftMaskKey))
            {
                PdfString maskName = gsDict.GetName(PdfTokens.SoftMaskKey);
                if (maskName == PdfTokens.NoneValue)
                {
                    parameters.SoftMask = null; // explicit removal
                }
                else
                {
                    PdfDictionary? softMaskDict = gsDict.GetDictionary(PdfTokens.SoftMaskKey);
                    parameters.SoftMask = PdfSoftMaskParser.ParseSoftMaskDictionary(softMaskDict, page);
                }
            }

            // Transfer Function (/TR)
            if (gsDict.HasKey(PdfTokens.TransferFunctionKey))
            {
                PdfObject? trObject = gsDict.GetObject(PdfTokens.TransferFunctionKey);
                parameters.TransferFunction = TransferFunctionTransform.FromPdfObject(trObject);
            }

            // Knockout (/TK)
            if (gsDict.HasKey(PdfTokens.KnockoutKey))
            {
                parameters.Knockout = gsDict.GetBooleanOrDefault(PdfTokens.KnockoutKey);
            }

            // Overprint Mode (/OPM)
            if (gsDict.HasKey(PdfTokens.OverprintModeKey))
            {
                parameters.OverprintMode = gsDict.GetIntegerOrDefault(PdfTokens.OverprintModeKey);
            }

            // Overprint Stroke (/OP)
            if (gsDict.HasKey(PdfTokens.OverprintStrokeKey))
            {
                parameters.OverprintStroke = gsDict.GetBooleanOrDefault(PdfTokens.OverprintStrokeKey);
            }

            // Overprint Fill (/op)
            if (gsDict.HasKey(PdfTokens.OverprintFillKey))
            {
                parameters.OverprintFill = gsDict.GetBooleanOrDefault(PdfTokens.OverprintFillKey);
            }

            // Font (/Font)
            if (gsDict.HasKey(PdfTokens.FontKey))
            {
                PdfArray? fontArray = gsDict.GetArray(PdfTokens.FontKey);
                if (fontArray?.Count == 2)
                {
                    PdfObject? fontObject = fontArray.GetObject(0);
                    float fontSize = fontArray.GetFloatOrDefault(1);

                    parameters.Font = page.Cache.GetFont(fontObject);
                    parameters.FontSize = fontSize;
                }
            }

            // Rendering intent (/RI)
            if (gsDict.HasKey(PdfTokens.StateIntentKey))
            {
                PdfString intentName = gsDict.GetName(PdfTokens.StateIntentKey);
                parameters.PdfRenderingIntent = intentName.AsEnum<PdfRenderingIntent>();
            }

            return parameters;
        }
    }
}
