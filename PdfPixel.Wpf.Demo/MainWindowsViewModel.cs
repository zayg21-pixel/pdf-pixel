using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfPixel.Canvas;
using PdfPixel.Wpf.PdfPanel;
using System.Windows.Input;

namespace PdfPixel.Wpf.Demo
{
    public class MainWindowsViewModel : ObservableObject
    {
        private int pageNumber;
        private int rotation;
        private int currentPageRotation;
        private PdfPanelAutoScaleMode autoScaleMode;
        private PdfViewerPageCollection pages;

        public MainWindowsViewModel()
        {
            RotatePageCommand = new RelayCommand(RotatePage);
            RotateAllPagesCommand = new RelayCommand(RotateAllPages);
        }

        public int PageNumber
        {
            get => pageNumber;
            set => SetProperty(ref pageNumber, value);
        }

        public int Rotation
        {
            get => rotation;
            set => SetProperty(ref rotation, value);
        }

        public int CurrentPageRotation
        {
            get => currentPageRotation;
            set => SetProperty(ref currentPageRotation, value);
        }

        public PdfPanelAutoScaleMode AutoScaleMode
        {
            get => autoScaleMode;
            set => SetProperty(ref autoScaleMode, value);
        }

        public ICommand RotatePageCommand { get; }

        public ICommand RotateAllPagesCommand { get; }

        public PdfViewerPageCollection Pages
        {
            get => pages;
            set
            {
                SetProperty(ref pages, value);

                //if (value != null)
                //{
                //    pages.OnAfterDraw = AfterDrawDelegate;
                //}
            }
        }

        //private void AfterDrawDelegate(SKCanvas canvas, VisiblePageInfo[] visiblePages, double scale)
        //{
        //    canvas.Scale((float)scale, (float)scale);

        //    foreach (var visiblePage in visiblePages)
        //    {
        //        var canvasPosition = new System.Windows.Point(30, 30);
        //        var pagePosition = visiblePage.ToCanvasPosition(canvasPosition, 1);
        //        var devicePosition = new SKPoint((float)pagePosition.X, (float)pagePosition.Y);
        //        //canvas.DrawText($"Page {visiblePage.PageNumber}", devicePosition.X, devicePosition.Y);
        //    }
        //}


        private void RotatePage()
        {
            CurrentPageRotation = GetNextRotation(CurrentPageRotation);
            Pages[PageNumber - 1].UserRotation += 90;
        }

        private void RotateAllPages()
        {
            Rotation = GetNextRotation(Rotation);
        }

        private int GetNextRotation(int userRotation)
        {
            return (userRotation + 90) % 360;
        }
    }
}
