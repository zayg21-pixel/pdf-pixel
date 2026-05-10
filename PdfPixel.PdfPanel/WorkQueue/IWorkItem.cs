namespace PdfPixel.PdfPanel.WorkQueue;

public interface IWorkItem
{
    public bool IsSkippable { get; }

    public void Process();
}
