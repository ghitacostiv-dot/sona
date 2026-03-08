using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SONA.Services
{
    public class AppTheme
    {
        public string Name { get; set; } = "";
        public Color AccentColor { get; set; }
        public Color BackgroundColor { get; set; }
        public Color CardColor { get; set; }
    }

    public static class ThemeManager
    {
        public static List<AppTheme> BuiltInThemes = new()
        {
            new AppTheme { Name = "Hydra Classic", AccentColor = Color.FromRgb(225, 29, 72), BackgroundColor = Color.FromRgb(10, 10, 10), CardColor = Color.FromRgb(24, 24, 24) },
            new AppTheme { Name = "Spotify Green", AccentColor = Color.FromRgb(30, 215, 96), BackgroundColor = Color.FromRgb(12, 12, 12), CardColor = Color.FromRgb(28, 28, 28) },
            new AppTheme { Name = "Midnight Indigo", AccentColor = Color.FromRgb(99, 102, 241), BackgroundColor = Color.FromRgb(8, 8, 20), CardColor = Color.FromRgb(15, 15, 30) },
            new AppTheme { Name = "Cyberpunk Gold", AccentColor = Color.FromRgb(254, 242, 0), BackgroundColor = Color.FromRgb(20, 20, 20), CardColor = Color.FromRgb(35, 35, 35) }
        };

        private static AppTheme _currentTheme = BuiltInThemes[0];
        public static AppTheme CurrentTheme => _currentTheme;

        public static void ApplyTheme(string themeName)
        {
            var theme = BuiltInThemes.Find(t => t.Name.Equals(themeName, StringComparison.OrdinalIgnoreCase));
            if (theme != null) _currentTheme = theme;
        }

        public static void ApplyAccentColor(string colorHex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                _currentTheme.AccentColor = color;
            }
            catch { }
        }
    }
}
