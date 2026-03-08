using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using SONA.Services;

namespace SONA.Pages
{
    public class SystemToolsPage : UserControl
    {
        private readonly MainWindow _mainWindow;

        public SystemToolsPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var root = new Grid();
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

            var icon = IconHelper.Img("nav/programs", 64);
            icon.Margin = new Thickness(0, 0, 0, 24);
            stack.Children.Add(icon);

            var title = new TextBlock 
            { 
                Text = "SYSTEM TOOLS", 
                Foreground = Brushes.White, 
                FontSize = 32, 
                FontWeight = FontWeights.Bold, 
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 40)
            };
            stack.Children.Add(title);

            var itemsGrid = new UniformGrid { Columns = 3, HorizontalAlignment = HorizontalAlignment.Center };
            
            itemsGrid.Children.Add(CreateToolCard("Apollo", "nav/programs", "apollo"));
            itemsGrid.Children.Add(CreateToolCard("Moonlight", "nav/programs", "moonlight"));
            itemsGrid.Children.Add(CreateToolCard("Tailscale", "nav/programs", "tailscale"));

            stack.Children.Add(itemsGrid);
            root.Children.Add(stack);
            Content = root;
        }

        private Border CreateToolCard(string name, string iconPath, string appId)
        {
            var card = new Border
            {
                Width = 180, Height = 180,
                Margin = new Thickness(12),
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var icon = IconHelper.Img(iconPath, 48);
            icon.Margin = new Thickness(0, 0, 0, 16);
            stack.Children.Add(icon);

            stack.Children.Add(new TextBlock 
            { 
                Text = name, 
                Foreground = Brushes.White, 
                FontSize = 16, 
                FontWeight = FontWeights.SemiBold, 
                HorizontalAlignment = HorizontalAlignment.Center 
            });

            card.Child = stack;

            card.MouseEnter += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                card.BorderBrush = Brushes.White;
            };
            card.MouseLeave += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            };

            card.MouseLeftButtonDown += (_, _) => {
                _mainWindow.PageHost.Content = new EmbeddedAppPage(appId, _mainWindow);
            };

            return card;
        }
    }
}
