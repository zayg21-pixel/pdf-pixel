using PdfPixel.PdfPanel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfPixel.PdfPanel.Wpf
{
    public partial class WpfPdfPanel
    {
        public static readonly DependencyProperty PagesProperty = DependencyProperty.Register(nameof(Pages), typeof(PdfPanelPageCollection), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, PagesProperty_Changed));

        public static readonly DependencyProperty ScrollTickProperty = DependencyProperty.Register(nameof(ScrollTick), typeof(int), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(100, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ScaleProperty_Changed));

        public static readonly DependencyProperty ScaleFactorProperty = DependencyProperty.Register(nameof(ScaleFactor), typeof(double), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(0.1d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinScaleProperty = DependencyProperty.Register(nameof(MinScale), typeof(double), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(0.1d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxScaleProperty = DependencyProperty.Register(nameof(MaxScale), typeof(double), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PageGapProperty = DependencyProperty.Register(nameof(PageGap), typeof(double), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(20d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PagesPaddingProperty = DependencyProperty.Register(nameof(PagesPadding), typeof(Thickness), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(new Thickness(20), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, CurrentPageProperty_Changed));

        public static readonly DependencyProperty MaxThumbnailSizeProperty = DependencyProperty.Register(nameof(MaxThumbnailSize), typeof(int), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(256, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BackgroundColorProperty = DependencyProperty.Register(nameof(BackgroundColor), typeof(System.Windows.Media.Color), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(Colors.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AutoScaleModeProperty = DependencyProperty.Register(nameof(AutoScaleMode), typeof(PdfPanelAutoScaleMode), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(PdfPanelAutoScaleMode.NoAutoScale, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty AnnotationPopupProperty = DependencyProperty.Register(nameof(AnnotationPopup), typeof(PdfAnnotationPopup), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PanelInterfaceProperty = DependencyProperty.Register(nameof(PanelInterface), typeof(WpfPdfPanelInterface), typeof(WpfPdfPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.None, PanelInterfaceProperty_Changed));

        /// <summary>
        /// Gets or sets the collection of pages.
        /// </summary>
        public PdfPanelPageCollection Pages
        {
            get => (PdfPanelPageCollection)GetValue(PagesProperty);
            set => SetValue(PagesProperty, value);
        }

        /// <summary>
        /// Gets or sets the scroll tick.
        /// New scroll position = Old scroll position + ScrollTick.
        /// </summary>
        public int ScrollTick
        {
            get => (int)GetValue(ScrollTickProperty);
            set => SetValue(ScrollTickProperty, value);
        }

        /// <summary>
        /// Gets or sets the scale.
        /// </summary>
        public double Scale
        {
            get => (double)GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        /// <summary>
        /// Gets or sets the scale factor for zooming.
        /// New scale = Old scale * ScaleFactor.
        /// </summary>
        public double ScaleFactor
        {
            get => (double)GetValue(ScaleFactorProperty);
            set => SetValue(ScaleFactorProperty, value);
        }

        /// <summary>
        /// Gets or sets the minimum scale.
        /// </summary>
        public double MinScale
        {
            get => (double)GetValue(MinScaleProperty);
            set => SetValue(MinScaleProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum scale.
        /// </summary>
        public double MaxScale
        {
            get => (double)GetValue(MaxScaleProperty);
            set => SetValue(MaxScaleProperty, value);
        }

        /// <summary>
        /// Gets or sets the gap between pages.
        /// </summary>
        public double PageGap
        {
            get => (double)GetValue(PageGapProperty);
            set => SetValue(PageGapProperty, value);
        }

        /// <summary>
        /// Gets or sets the padding between pages and element border.
        /// </summary>
        public Thickness PagesPadding
        {
            get => (Thickness)GetValue(PagesPaddingProperty);
            set => SetValue(PagesPaddingProperty, value);
        }

        /// <summary>
        /// Gets or sets the current page number.
        /// </summary>
        public int CurrentPage
        {
            get => (int)GetValue(CurrentPageProperty);
            set => SetValue(CurrentPageProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum size of the thumbnail in pixels.
        /// </summary>
        public int MaxThumbnailSize
        {
            get => (int)GetValue(MaxThumbnailSizeProperty);
            set => SetValue(MaxThumbnailSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public System.Windows.Media.Color BackgroundColor
        {
            get => (System.Windows.Media.Color)GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the automatic scaling mode.
        /// </summary>
        public PdfPanelAutoScaleMode AutoScaleMode
        {
            get => (PdfPanelAutoScaleMode)GetValue(AutoScaleModeProperty);
            set => SetValue(AutoScaleModeProperty, value);
        }

        /// <summary>
        /// Annotation popup under the mouse cursor.
        /// </summary>
        public PdfAnnotationPopup AnnotationPopup
        {
            get => (PdfAnnotationPopup)GetValue(AnnotationPopupProperty);
            set => SetValue(AnnotationPopupProperty, value);
        }

        /// <summary>
        /// Annotation tooltip template.
        /// </summary>
        public ToolTip AnnotationToolTip { get; set; }

        /// <summary>
        /// Gets or sets the panel interface for controlling panel operations via MVVM.
        /// </summary>
        public WpfPdfPanelInterface PanelInterface
        {
            get => (WpfPdfPanelInterface)GetValue(PanelInterfaceProperty);
            set => SetValue(PanelInterfaceProperty, value);
        }

        private static void PagesProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (WpfPdfPanel)d;

            if (e.NewValue is null)
            {
                source.ResetContent();
            }
        }

        private static void ScaleProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (WpfPdfPanel)d;
            var oldValue = (e.OldValue as double?) ?? 1;

            if (source.Scale < source.MinScale)
            {
                source.Scale = source.MinScale;
            }

            if (source.Scale > source.MaxScale)
            {
                source.Scale = source.MaxScale;
            }

            if (!source._updatingScale)
            {
                source.AutoScaleMode = PdfPanelAutoScaleMode.NoAutoScale;
                source.OnScaleChanged();
            }
        }

        private static void CurrentPageProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (WpfPdfPanel)d;

            if (source.Pages == null)
            {
                return;
            }

            if (source.Pages.Count == 0)
            {
                return;
            }

            if (source.CurrentPage < 1)
            {
                source.CurrentPage = 1;
            }

            if (source.CurrentPage > source.Pages.Count)
            {
                source.CurrentPage = source.Pages.Count;
            }

            if (!source._updatingPages)
            {
                source.ScrollToPage(source.CurrentPage);
            }
        }

        private static void PanelInterfaceProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (WpfPdfPanel)d;

            if (e.OldValue is WpfPdfPanelInterface oldInterface)
            {
                oldInterface.OnRequest = null;
            }

            if (e.NewValue is WpfPdfPanelInterface newInterface)
            {
                newInterface.OnRequest = source.HandleInterfaceRequest;
            }
        }
    }
}