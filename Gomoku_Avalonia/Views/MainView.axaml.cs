using Avalonia;
using Avalonia.Controls;

namespace Gomoku_Avalonia.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var insetsManager = TopLevel.GetTopLevel(this)?.InsetsManager;
            if (insetsManager != null)
            {
                insetsManager.SafeAreaChanged += (s, args) => UpdateSafeArea(args.SafeAreaPadding);
                UpdateSafeArea(insetsManager.SafeAreaPadding);
            }
        }

        private void UpdateSafeArea(Thickness padding)
        {
            var contentWrapper = this.FindControl<Grid>("ContentWrapper");
            if (contentWrapper != null)
            {
                // Optionally add a bottom safe area if the nav bar is transparent
                contentWrapper.Margin = padding;
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            var portrait = this.FindControl<Grid>("PortraitLayout");
            var landscape = this.FindControl<Grid>("LandscapeLayout");
            var portraitCard = this.FindControl<Border>("PortraitSettingsCard");
            var landscapeCard = this.FindControl<Border>("LandscapeSettingsCard");

            bool isLandscape = e.NewSize.Width > e.NewSize.Height;

            if (portrait != null && landscape != null)
            {
                portrait.IsVisible = !isLandscape;
                landscape.IsVisible = isLandscape;
            }

            if (portraitCard != null && landscapeCard != null)
            {
                portraitCard.IsVisible = !isLandscape;
                landscapeCard.IsVisible = isLandscape;
            }
        }
    }
}