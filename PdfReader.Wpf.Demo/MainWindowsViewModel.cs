using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfReader.Wpf.PdfPanel;
using System.Windows.Input;

namespace PdfReader.Wpf.Demo
{
    public class MainWindowsViewModel : ObservableObject
    {
        private int pageNumber;
        private int rotation;
        private int currentPageRotation;
        private PdfPanelAutoScaleMode autoScaleMode;

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

        private void RotatePage()
        {
            CurrentPageRotation = GetNextRotation(CurrentPageRotation);
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
