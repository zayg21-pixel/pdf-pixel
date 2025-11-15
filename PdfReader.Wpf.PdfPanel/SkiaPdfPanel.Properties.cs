using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PdfReader.Wpf.PdfPanel
{
    public enum PdfPanelAutoScaleMode
    {
        /// <summary>
        /// No automatic scaling.
        /// </summary>
        NoAutoScale,

        /// <summary>
        /// Scale to visible pages.
        /// </summary>
        ScaleToVisible,

        /// <summary>
        /// Scale to all pages.
        /// </summary>
        ScaleToWidth
    }

    public partial class SkiaPdfPanel
    {
        public static readonly DependencyProperty PagesProperty = DependencyProperty.Register(nameof(Pages), typeof(PdfViewerPageCollection), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, PagesProperty_Changed));

        public static readonly DependencyProperty ScrollTickProperty = DependencyProperty.Register(nameof(ScrollTick), typeof(int), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(100, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, ScaleProperty_Changed));

        public static readonly DependencyProperty ScaleFactorProperty = DependencyProperty.Register(nameof(ScaleFactor), typeof(double), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(0.1d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinScaleProperty = DependencyProperty.Register(nameof(MinScale), typeof(double), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(0.1d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxScaleProperty = DependencyProperty.Register(nameof(MaxScale), typeof(double), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PageGapProperty = DependencyProperty.Register(nameof(PageGap), typeof(double), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(20d, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PagesPaddingProperty = DependencyProperty.Register(nameof(PagesPadding), typeof(Thickness), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(new Thickness(20), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, CurrentPageProperty_Changed));

        public static readonly DependencyProperty MaxThumbnailSizeProperty = DependencyProperty.Register(nameof(MaxThumbnailSize), typeof(int), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(512, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BackgroundColorProperty = DependencyProperty.Register(nameof(BackgroundColor), typeof(System.Windows.Media.Color), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(Colors.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AutoScaleModeProperty = DependencyProperty.Register(nameof(AutoScaleMode), typeof(PdfPanelAutoScaleMode), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(PdfPanelAutoScaleMode.NoAutoScale, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty AnnotationPopupProperty = DependencyProperty.Register(nameof(AnnotationPopup), typeof(AnnotationPopup), typeof(SkiaPdfPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Gets or sets the collection of pages.
        /// </summary>
        public PdfViewerPageCollection Pages
        {
            get => (PdfViewerPageCollection)GetValue(PagesProperty);
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
        public AnnotationPopup AnnotationPopup
        {
            get => (AnnotationPopup)GetValue(AnnotationPopupProperty);
            set => SetValue(AnnotationPopupProperty, value);
        }

        /// <summary>
        /// Annotation tooltip template.
        /// </summary>
        public ToolTip AnnotationToolTip { get; set; }

        private static void PagesProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (SkiaPdfPanel)d;

            if (e.NewValue is null)
            {
                source.ResetContent();
            }
        }

        private static void ScaleProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (SkiaPdfPanel)d;
            var oldValue = (e.OldValue as double?) ?? 1;

            if (source.Scale < source.MinScale)
            {
                source.Scale = source.MinScale;
            }

            if (source.Scale > source.MaxScale)
            {
                source.Scale = source.MaxScale;
            }

            if (!source.autoScaling)
            {
                source.OnScaleChanged(oldValue);
                source.AutoScaleMode = PdfPanelAutoScaleMode.NoAutoScale;
            }
        }

        private static void CurrentPageProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (SkiaPdfPanel)d;

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

            if (!source.pageChangedLocally)
            {
                source.ScrollToPage(source.CurrentPage);
            }
        }
    }
}