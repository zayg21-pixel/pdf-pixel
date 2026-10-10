using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// Destination written as an array: the target in its first element, followed by how the target page
/// is displayed (ISO 32000-2 Table 149).
/// </summary>
public abstract class PdfExplicitDestination : PdfDestination
{
    private protected PdfExplicitDestination(PdfDestinationFitType fitType, float? zoom, float? left, float? bottom, float? right, float? top)
    {
        FitType = fitType;
        Zoom = zoom;
        Left = left;
        Bottom = bottom;
        Right = right;
        Top = top;
    }

    private protected PdfExplicitDestination(PdfArray destinationArray)
    {
        if (destinationArray.Count > 1)
        {
            FitType = destinationArray.GetNameOrDefault(1).AsEnum<PdfDestinationFitType>();
        }

        switch (FitType)
        {
            case PdfDestinationFitType.XYZ:
            {
                if (destinationArray.Count >= 5)
                {
                    Left = destinationArray.GetFloat(2);
                    Top = destinationArray.GetFloat(3);
                    Zoom = destinationArray.GetFloat(4);

                    if (Zoom == 0)
                    {
                        Zoom = null;
                    }
                }

                break;
            }
            case PdfDestinationFitType.FitH:
            case PdfDestinationFitType.FitBH:
            {
                if (destinationArray.Count >= 3)
                {
                    Top = destinationArray.GetFloat(2);
                }

                break;
            }
            case PdfDestinationFitType.FitV:
            case PdfDestinationFitType.FitBV:
            {
                if (destinationArray.Count >= 3)
                {
                    Left = destinationArray.GetFloat(2);
                }

                break;
            }
            case PdfDestinationFitType.FitR:
            {
                if (destinationArray.Count >= 6)
                {
                    Left = destinationArray.GetFloat(2);
                    Bottom = destinationArray.GetFloat(3);
                    Right = destinationArray.GetFloat(4);
                    Top = destinationArray.GetFloat(5);
                }

                break;
            }
        }
    }

    /// <summary>
    /// How the target page is displayed.
    /// </summary>
    public PdfDestinationFitType FitType { get; }

    /// <summary>
    /// Zoom factor for the XYZ fit type. Null retains the current zoom.
    /// </summary>
    public float? Zoom { get; }

    /// <summary>
    /// Left coordinate for the XYZ, FitV, FitBV and FitR fit types, or null when not given.
    /// </summary>
    public float? Left { get; }

    /// <summary>
    /// Bottom coordinate for the FitR fit type, or null when not given.
    /// </summary>
    public float? Bottom { get; }

    /// <summary>
    /// Right coordinate for the FitR fit type, or null when not given.
    /// </summary>
    public float? Right { get; }

    /// <summary>
    /// Top coordinate for the XYZ, FitH, FitBH and FitR fit types, or null when not given.
    /// </summary>
    public float? Top { get; }
}
