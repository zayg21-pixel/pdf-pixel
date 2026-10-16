namespace PdfPixel.Demo.Wpf.ViewModels;

public class PdfFileLocation
{
    public PdfFileLocation(string filePath)
    {
        FilePath = filePath;
        FileName = System.IO.Path.GetFileNameWithoutExtension(FilePath);
    }

    public string FilePath { get; }

    public string FileName { get; }
}
