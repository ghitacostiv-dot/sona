using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SONA.Services
{
    /// <summary>
    /// Loads PNG icons from the embedded Resources/Icons/ folder.
    /// </summary>
    public static class IconHelper
    {
        private const string PackBase = "pack://application:,,,/Resources/Icons/";
        private static readonly Dictionary<string, BitmapImage> _cache = new();

        /// <summary>Returns a BitmapImage for the given icon path (e.g. "nav/home").</summary>
        public static BitmapImage? Get(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath)) return null;
            if (_cache.ContainsKey(iconPath)) return _cache[iconPath];

            try
            {
                // Check if it's a full pack URI or relative to GUI folder
                string fullPath = iconPath;
                if (!iconPath.StartsWith("pack://"))
                {
                    if (iconPath.Contains(".") || iconPath.Contains("/"))
                    {
                        // Assume it's a path relative to Resources or absolute
                        if (!iconPath.StartsWith("/")) fullPath = PackBase.Replace("Icons/", "") + iconPath;
                        else fullPath = "pack://application:,,," + iconPath;
                    }
                    else
                    {
                        fullPath = PackBase + iconPath + ".png";
                    }
                }

                var bmp = CreateSafe(new Uri(fullPath));
                if (bmp != null) _cache[iconPath] = bmp;
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a BitmapImage safely using BeginInit/EndInit and Freeze.
        /// </summary>
        public static BitmapImage? CreateSafe(Uri uri)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                if (bmp.CanFreeze) bmp.Freeze();
                return bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SafeBitmap Error: {ex.Message} for {uri}");
                return null;
            }
        }

        /// <summary>
        /// Returns a cropped version of the image focused on the center, making it essentially "larger" as an icon.
        /// </summary>
        public static ImageSource? GetCroppedIcon(Uri uri)
        {
            var bmp = CreateSafe(uri);
            if (bmp == null) return null;

            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;

            // To make a square icon from a wide image, we take a square from the center.
            // We want the square to be tight on the logo, but centered.
            // Let's take 'h' as the size of the square (assuming it's wider than tall).
            int side = h;
            int x = (w - side) / 2;
            int y = 0;

            if (w < h) { side = w; x = 0; y = (h - side) / 2; }

            try
            {
                // To make it "4 times larger" (zoomed in), we take a smaller square.
                int zoomedSide = (int)(side * 0.5); // 50% of the possible square (4x area)
                int zoomedX = x + (side - zoomedSide) / 2;
                int zoomedY = y + (side - zoomedSide) / 2;

                return new CroppedBitmap(bmp, new Int32Rect(zoomedX, zoomedY, zoomedSide, zoomedSide));
            }
            catch
            {
                return bmp; // Fallback
            }
        }

        /// <summary>
        /// Returns a styled WPF Image control with the given icon and desired pixel size.
        /// </summary>
        public static Image Img(string iconPath, int size = 20,
                                  HorizontalAlignment ha = HorizontalAlignment.Center,
                                  VerticalAlignment va = VerticalAlignment.Center)
        {
            var img = new Image
            {
                Source = Get(iconPath),
                Width = size,
                Height = size,
                HorizontalAlignment = ha,
                VerticalAlignment = va,
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            return img;
        }

        /// <summary>
        /// Returns a standard Segoe Fluent Icon (Symbol) as a TextBlock for chrome/small UI.
        /// </summary>
        public static TextBlock ChromeIcon(string symbol, double size = 12)
        {
            string hex = symbol switch
            {
                "minimize" => "\uE921",
                "maximize" => "\uE922",
                "restore" => "\uE923",
                "close" => "\uE8BB",
                "settings" => "\uE713",
                "back" => "\uE72B",
                _ => "?"
            };

            var font = Application.Current.TryFindResource("SegoeFluent") as FontFamily 
                       ?? new FontFamily("Segoe MDL2 Assets, Segoe UI Symbol");
            
            return new TextBlock
            {
                Text = hex,
                FontFamily = font,
                FontSize = size,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        /// <summary>
        /// Creates a small StackPanel with an icon + optional label for use in buttons/nav.
        /// </summary>
        public static StackPanel NavItem(string iconPath, string label,
                                          int iconSize = 20, double labelFontSize = 8)
        {
            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent
            };

            var img = Img(iconPath, iconSize);
            img.Margin = new Thickness(0, 0, 0, 2);
            sp.Children.Add(img);

            if (!string.IsNullOrEmpty(label))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = labelFontSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Background = Brushes.Transparent
                });
            }

            return sp;
        }
    }
}
