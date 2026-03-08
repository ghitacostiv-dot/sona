using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SONA.Services
{
    public static class UIHelper
    {
        public static Grid WrapWithScrollArrows(ScrollViewer scrollViewer)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Ensure smooth scrolling behaviors if possible
            scrollViewer.PanningMode = PanningMode.HorizontalOnly;

            Grid.SetColumn(scrollViewer, 1);
            grid.Children.Add(scrollViewer);

            var leftBtn = new Button
            {
                Content = "←",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Width = 40, Height = 40,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 20,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Scroll Left",
                Opacity = 0.8
            };
            leftBtn.MouseEnter += (_,_) => leftBtn.Opacity = 1.0;
            leftBtn.MouseLeave += (_,_) => leftBtn.Opacity = 0.8;
            leftBtn.Click += (_, _) => {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - 400);
            };
            Grid.SetColumn(leftBtn, 0);
            grid.Children.Add(leftBtn);

            var rightBtn = new Button
            {
                Content = "→",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Width = 40, Height = 40,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontWeight = FontWeights.Bold,
                FontSize = 20,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Scroll Right",
                Opacity = 0.8
            };
            rightBtn.MouseEnter += (_,_) => rightBtn.Opacity = 1.0;
            rightBtn.MouseLeave += (_,_) => rightBtn.Opacity = 0.8;
            rightBtn.Click += (_, _) => {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + 400);
            };
            Grid.SetColumn(rightBtn, 2);
            grid.Children.Add(rightBtn);

            return grid;
        }
    }
}
