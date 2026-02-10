using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents PDF action types.
/// </summary>
[PdfEnum]
public enum PdfActionType
{
    /// <summary>
    /// Unknown or unsupported action type.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Go to a destination in the current document.
    /// </summary>
    [PdfEnumValue("GoTo")]
    GoTo,

    /// <summary>
    /// Go to a destination in another document.
    /// </summary>
    [PdfEnumValue("GoToR")]
    GoToRemote,

    /// <summary>
    /// Go to a destination in an embedded document.
    /// </summary>
    [PdfEnumValue("GoToE")]
    GoToEmbedded,

    /// <summary>
    /// Launch an application or open a document.
    /// </summary>
    [PdfEnumValue("Launch")]
    Launch,

    /// <summary>
    /// Begin reading an article thread.
    /// </summary>
    [PdfEnumValue("Thread")]
    Thread,

    /// <summary>
    /// Resolve a URI.
    /// </summary>
    [PdfEnumValue("URI")]
    Uri,

    /// <summary>
    /// Play a sound.
    /// </summary>
    [PdfEnumValue("Sound")]
    Sound,

    /// <summary>
    /// Play a movie.
    /// </summary>
    [PdfEnumValue("Movie")]
    Movie,

    /// <summary>
    /// Hide or show one or more annotations.
    /// </summary>
    [PdfEnumValue("Hide")]
    Hide,

    /// <summary>
    /// Set the name field in a form.
    /// </summary>
    [PdfEnumValue("Named")]
    Named,

    /// <summary>
    /// Submit a form.
    /// </summary>
    [PdfEnumValue("SubmitForm")]
    SubmitForm,

    /// <summary>
    /// Reset a form.
    /// </summary>
    [PdfEnumValue("ResetForm")]
    ResetForm,

    /// <summary>
    /// Import form data.
    /// </summary>
    [PdfEnumValue("ImportData")]
    ImportData,

    /// <summary>
    /// Execute a JavaScript script.
    /// </summary>
    [PdfEnumValue("JavaScript")]
    JavaScript,

    /// <summary>
    /// Set the states of optional content groups.
    /// </summary>
    [PdfEnumValue("SetOCGState")]
    SetOcgState,

    /// <summary>
    /// Control the playing of multimedia content.
    /// </summary>
    [PdfEnumValue("Rendition")]
    Rendition,

    /// <summary>
    /// Update the display of a document based on a transition dictionary.
    /// </summary>
    [PdfEnumValue("Trans")]
    Trans,

    /// <summary>
    /// Go to a 3D view.
    /// </summary>
    [PdfEnumValue("GoTo3DView")]
    GoTo3DView
}
