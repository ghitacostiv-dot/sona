using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SONA.Services;

namespace SONA
{
    public class Audiobook
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("title")] public string Title { get; set; } = "";
        [JsonProperty("author")] public string Author { get; set; } = "";
        [JsonProperty("narrator")] public string Narrator { get; set; } = "";
        [JsonProperty("duration")] public string Duration { get; set; } = "";
        [JsonProperty("url")] public string Url { get; set; } = "";
        [JsonProperty("cover")] public string Cover { get; set; } = "";
        [JsonProperty("category")] public string Category { get; set; } = "";
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("rating")] public double Rating { get; set; }
    }

    public class AudiobookPage : UserControl, IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly ContentControl _contentArea = new();
        private TextBox _searchBox = new();
        private Border _loadingOverlay = new();
        private RadioButton _tabHome = new();
        private RadioButton _tabSearch = new();
        private RadioButton _tabFavorites = new();
        private RadioButton _tabCategories = new();
        private StackPanel _resultsPanel = new();
        private bool _isPlayingAudiobook = false;
        private string _currentPlayingUrl = "";

        private readonly List<Audiobook> _favorites = new();
        private readonly string _favoritesPath = Path.Combine(AppConfig.DataDir, "audiobook_favorites.json");
        private static readonly HttpClient _http = new();

        private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0x84, 0x16, 0x22)); // Deep red

        public AudiobookPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            LoadFavorites();
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftDock = new DockPanel();

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 24, 0, 0) };
            var icon = IconHelper.Img("nav/books", 44);
            icon.Margin = new Thickness(0, 0, 16, 0);
            headerStack.Children.Add(icon);

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 12, 24, 0) };
            _tabHome = CreateTabButton("📚 Browse", true);
            _tabSearch = CreateTabButton("🔍 Search", false);
            _tabFavorites = CreateTabButton("❤ Favorites", false);
            _tabCategories = CreateTabButton("📂 Categories", false);
            
            tabs.Children.Add(_tabHome);
            tabs.Children.Add(_tabSearch);
            tabs.Children.Add(_tabFavorites);
            tabs.Children.Add(_tabCategories);

            _tabHome.Checked += (_, _) => { if (IsLoaded) ShowBrowseView(); };
            _tabSearch.Checked += (_, _) => { if (IsLoaded) ShowSearchView(); };
            _tabFavorites.Checked += (_, _) => { if (IsLoaded) ShowFavoritesView(); };
            _tabCategories.Checked += (_, _) => { if (IsLoaded) ShowCategoriesView(); };

            leftDock.Children.Add(headerStack);
            DockPanel.SetDock(headerStack, Dock.Top);
            leftDock.Children.Add(tabs);
            DockPanel.SetDock(tabs, Dock.Top);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = _contentArea;
            leftDock.Children.Add(scroll);

            rootGrid.Children.Add(leftDock);

            _loadingOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Visibility = Visibility.Collapsed,
                Child = new TextBlock { Text = "Loading audiobooks...", Foreground = Brushes.White, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            rootGrid.Children.Add(_loadingOverlay);

            Content = rootGrid;

            ShowBrowseView();
        }

        private RadioButton CreateTabButton(string text, bool isActive)
        {
            return new RadioButton
            {
                Content = text,
                Style = (Style)Application.Current.FindResource("PageTab"),
                GroupName = "AudiobookTabs",
                IsChecked = isActive
            };
        }

        // --- BROWSE VIEW ---
        private void ShowBrowseView()
        {
            var mainStack = new StackPanel { Margin = new Thickness(24) };

            // Hero header
            var heroGrad = new Border { Height = 160, Padding = new Thickness(32), Margin = new Thickness(-24, -24, -24, 24) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x84, 0x16, 0x22), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x5a, 0x0e, 0x15), 0.5));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            heroGrad.Background = grad;
            var heroStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            heroStack.Children.Add(new TextBlock { Text = "📚 Audiobooks", Foreground = Brushes.White, FontSize = 42, FontWeight = FontWeights.Heavy });
            heroStack.Children.Add(new TextBlock { Text = "Immersive storytelling • Professional narration • Vast library", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 14, Margin = new Thickness(0, 4, 0, 0) });
            heroGrad.Child = heroStack;
            mainStack.Children.Add(heroGrad);

            // Featured Audiobooks
            mainStack.Children.Add(new TextBlock { Text = "Featured Audiobooks", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) });
            var featuredPanel = new StackPanel();
            mainStack.Children.Add(featuredPanel);
            _ = LoadFeaturedAudiobooksAsync(featuredPanel);

            // Recently Added
            mainStack.Children.Add(new TextBlock { Text = "Recently Added", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 32, 0, 16) });
            var recentPanel = new StackPanel();
            mainStack.Children.Add(recentPanel);
            _ = LoadRecentAudiobooksAsync(recentPanel);

            _contentArea.Content = mainStack;
        }

        private async Task LoadFeaturedAudiobooksAsync(StackPanel panel)
        {
            try
            {
                var audiobooks = await GetSampleAudiobooks();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var book in audiobooks.Take(6)) panel.Children.Add(BuildAudiobookCard(book));
                });
            }
            catch { }
        }

        private async Task LoadRecentAudiobooksAsync(StackPanel panel)
        {
            try
            {
                var audiobooks = await GetSampleAudiobooks();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var book in audiobooks.Skip(6).Take(6)) panel.Children.Add(BuildAudiobookCard(book));
                });
            }
            catch { }
        }

        private async Task<List<Audiobook>> GetSampleAudiobooks()
        {
            // Return sample audiobooks (in real implementation, this would call an API)
            return new List<Audiobook>
            {
                new Audiobook { Id = "1", Title = "The Great Adventure", Author = "John Smith", Narrator = "Sarah Johnson", Duration = "8h 45m", Category = "Fiction", Rating = 4.5, Description = "An epic tale of courage and discovery." },
                new Audiobook { Id = "2", Title = "Learning to Code", Author = "Tech Master", Narrator = "Alex Chen", Duration = "12h 30m", Category = "Education", Rating = 4.8, Description = "Comprehensive programming guide for beginners." },
                new Audiobook { Id = "3", Title = "Mystery of the Lost City", Author = "Detective Writer", Narrator = "James Morgan", Duration = "10h 15m", Category = "Mystery", Rating = 4.3, Description = "A thrilling mystery that will keep you guessing." },
                new Audiobook { Id = "4", Title = "Science Explained", Author = "Dr. Einstein", Narrator = "Prof. Wisdom", Duration = "15h 20m", Category = "Science", Rating = 4.6, Description = "Complex science made simple and accessible." },
                new Audiobook { Id = "5", Title = "The Entrepreneur's Mind", Author = "Business Guru", Narrator = "Success Voice", Duration = "6h 50m", Category = "Business", Rating = 4.4, Description = "Learn the mindset of successful entrepreneurs." },
                new Audiobook { Id = "6", Title = "History of the World", Author = "Historian Pro", Narrator = "Time Traveler", Duration = "25h 00m", Category = "History", Rating = 4.7, Description = "Journey through the annals of human history." },
                new Audiobook { Id = "7", Title = "Cooking Masterclass", Author = "Chef Deluxe", Narrator = "Food Critic", Duration = "9h 30m", Category = "Lifestyle", Rating = 4.2, Description = "Master the art of culinary excellence." },
                new Audiobook { Id = "8", Title = "Philosophy for Life", Author = "Think Deep", Narrator = "Wise Voice", Duration = "11h 45m", Category = "Philosophy", Rating = 4.5, Description = "Ancient wisdom for modern living." },
                new Audiobook { Id = "9", Title = "Fitness Revolution", Author = "Health Expert", Narrator = "Motivator", Duration = "7h 20m", Category = "Health", Rating = 4.3, Description = "Transform your body and mind." },
                new Audiobook { Id = "10", Title = "Art Appreciation", Author = "Art Critic", Narrator = "Culture Voice", Duration = "8h 10m", Category = "Arts", Rating = 4.4, Description = "Discover the beauty in the world around you." },
                new Audiobook { Id = "11", Title = "Financial Freedom", Author = "Money Wise", Narrator = "Wealth Coach", Duration = "13h 15m", Category = "Finance", Rating = 4.6, Description = "Your guide to financial independence." },
                new Audiobook { Id = "12", Title = "Space Exploration", Author = "Astro Physicist", Narrator = "Space Voice", Duration = "16h 40m", Category = "Science", Rating = 4.8, Description = "Journey to the stars and beyond." }
            };
        }

        // --- SEARCH VIEW ---
        private void ShowSearchView()
        {
            var mainStack = new StackPanel();

            var border = new Border { Height = 180, Padding = new Thickness(32) };
            var searchGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            searchGrad.GradientStops.Add(new GradientStop(Color.FromRgb(0x5a, 0x0e, 0x15), 0.0));
            searchGrad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            border.Background = searchGrad;

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            stack.Children.Add(new TextBlock { Text = "Search Audiobooks", Foreground = Brushes.White, FontSize = 36, FontWeight = FontWeights.Heavy });

            var searchRow = new DockPanel { Margin = new Thickness(0, 16, 0, 0), MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left };
            _searchBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Height = 44, Width = 400, FontSize = 16,
                Padding = new Thickness(15, 0, 15, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a))
            };
            _searchBox.KeyDown += async (s, e) => { if (e.Key == Key.Enter) await DoSearch(_searchBox.Text); };

            var goBtn = new Button { Content = "🔍", Style = (Style)Application.Current.FindResource("AccentBtn"), Width = 40, Height = 40, Margin = new Thickness(8, 0, 0, 0), Background = _accentBrush };
            goBtn.Click += async (_, _) => await DoSearch(_searchBox.Text);
            DockPanel.SetDock(goBtn, Dock.Right);
            searchRow.Children.Add(goBtn);
            searchRow.Children.Add(_searchBox);
            stack.Children.Add(searchRow);
            border.Child = stack;
            mainStack.Children.Add(border);

            _resultsPanel = new StackPanel { Margin = new Thickness(24) };
            if (_resultsPanel.Parent is Panel p) p.Children.Remove(_resultsPanel);
            mainStack.Children.Add(_resultsPanel);

            _contentArea.Content = mainStack;
        }

        private async Task DoSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            _loadingOverlay.Visibility = Visibility.Visible;
            _resultsPanel.Children.Clear();

            try
            {
                var audiobooks = await GetSampleAudiobooks();
                var filtered = audiobooks.Where(b => 
                    b.Title.ToLower().Contains(query.ToLower()) || 
                    b.Author.ToLower().Contains(query.ToLower()) ||
                    b.Category.ToLower().Contains(query.ToLower())).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (filtered.Count == 0)
                    {
                        _resultsPanel.Children.Add(new TextBlock { Text = "No audiobooks found.", Foreground = Brushes.Gray, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
                        return;
                    }
                    foreach (var book in filtered) _resultsPanel.Children.Add(BuildAudiobookCard(book));
                });
            }
            catch { }
            finally { _loadingOverlay.Visibility = Visibility.Collapsed; }
        }

        // --- CATEGORIES VIEW ---
        private void ShowCategoriesView()
        {
            var mainStack = new StackPanel { Margin = new Thickness(24) };

            mainStack.Children.Add(new TextBlock { Text = "📂 Browse by Category", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });

            var categories = new[] {
                ("Fiction", "📖", "Stories and novels"),
                ("Education", "🎓", "Learning and development"),
                ("Mystery", "🔍", "Thrilling mysteries"),
                ("Science", "🔬", "Scientific discoveries"),
                ("Business", "💼", "Professional development"),
                ("History", "📜", "Historical accounts"),
                ("Lifestyle", "🌟", "Life improvement"),
                ("Philosophy", "🤔", "Deep thinking"),
                ("Health", "💪", "Wellness and fitness"),
                ("Arts", "🎨", "Artistic expression"),
                ("Finance", "💰", "Money management"),
                ("Self-Help", "🌈", "Personal growth")
            };

            var categoryGrid = new UniformGrid { Columns = 3 };
            foreach (var (name, icon, description) in categories)
            {
                var card = CreateCategoryCard(icon, name, description);
                categoryGrid.Children.Add(card);
            }
            mainStack.Children.Add(categoryGrid);

            _contentArea.Content = mainStack;
        }

        private Border CreateCategoryCard(string icon, string name, string description)
        {
            var card = new Border
            {
                Height = 120, Margin = new Thickness(8), CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0d, 0x0d, 0x14)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                Cursor = Cursors.Hand, Padding = new Thickness(16)
            };
            
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = icon, FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) });
            stack.Children.Add(new TextBlock { Text = name, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = description, Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            
            card.Child = stack;
            
            card.MouseEnter += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x1a, 0x1a, 0x2e));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 132, 22, 34));
            };
            card.MouseLeave += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0d, 0x0d, 0x14));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255));
            };
            
            card.MouseLeftButtonDown += async (_, _) => await ShowCategoryResults(name);
            
            return card;
        }

        private async Task ShowCategoryResults(string category)
        {
            _loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                var audiobooks = await GetSampleAudiobooks();
                var filtered = audiobooks.Where(b => b.Category == category).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainStack = new StackPanel { Margin = new Thickness(24) };
                    var backBtn = new Button { Content = "← Back to Categories", Style = (Style)Application.Current.FindResource("DarkBtn"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 20) };
                    backBtn.Click += (_, _) => ShowCategoriesView();
                    mainStack.Children.Add(backBtn);
                    mainStack.Children.Add(new TextBlock { Text = $"{category} Audiobooks", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });
                    
                    if (filtered.Count == 0)
                    {
                        mainStack.Children.Add(new TextBlock { Text = "No audiobooks found in this category.", Foreground = Brushes.Gray, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
                    }
                    else
                    {
                        foreach (var book in filtered) mainStack.Children.Add(BuildAudiobookCard(book));
                    }
                    
                    _contentArea.Content = mainStack;
                });
            }
            catch { }
            finally { _loadingOverlay.Visibility = Visibility.Collapsed; }
        }

        // --- FAVORITES VIEW ---
        private void ShowFavoritesView()
        {
            var mainStack = new StackPanel { Margin = new Thickness(24) };
            mainStack.Children.Add(new TextBlock { Text = "❤ Favorite Audiobooks", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });

            if (_favorites.Count == 0)
            {
                mainStack.Children.Add(new TextBlock { Text = "No favorites yet. Click the ❤ icon on any audiobook to add it here.", Foreground = Brushes.Gray, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
            }
            else
            {
                foreach (var book in _favorites) mainStack.Children.Add(BuildAudiobookCard(book, isFavoriteView: true));
            }

            _contentArea.Content = mainStack;
        }

        // --- AUDIOBOOK CARD ---
        private Border BuildAudiobookCard(Audiobook book, bool isFavoriteView = false)
        {
            var card = new Border
            {
                Height = 120, 
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0d, 0x0d, 0x14)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                Margin = new Thickness(0, 0, 0, 12), 
                Padding = new Thickness(16)
            };

            var dp = new DockPanel();

            // Right side: buttons
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var isFav = _favorites.Any(f => f.Id == book.Id);
            var favBtn = new Button
            {
                Content = isFav ? "❤" : "🤍",
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                FontSize = 16, Cursor = Cursors.Hand, ToolTip = isFav ? "Remove from favorites" : "Add to favorites",
                Foreground = isFav ? new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)) : Brushes.White,
                Margin = new Thickness(0, 0, 8, 0)
            };
            favBtn.Click += (_, _) =>
            {
                if (_favorites.Any(f => f.Id == book.Id))
                    _favorites.RemoveAll(f => f.Id == book.Id);
                else
                    _favorites.Add(book);
                SaveFavorites();
                if (isFavoriteView) ShowFavoritesView();
            };
            rightStack.Children.Add(favBtn);

            var playBtn = new Button
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Margin = new Thickness(4, 0, 0, 0)
            };
            playBtn.Content = _currentPlayingUrl == book.Url ? IconHelper.Img("player/pause", 22) : IconHelper.Img("player/play", 22);
            playBtn.Click += (_, _) => PlayAudiobook(book);
            rightStack.Children.Add(playBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            dp.Children.Add(rightStack);

            // Left side: cover + info
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal };

            var coverBorder = new Border { Width = 60, Height = 80, CornerRadius = new CornerRadius(8), ClipToBounds = true };
            coverBorder.Background = new SolidColorBrush(Color.FromRgb(0x84, 0x16, 0x22));
            coverBorder.Child = new TextBlock { Text = "📚", FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            leftStack.Children.Add(coverBorder);

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            infoStack.Children.Add(new TextBlock { Text = book.Title, Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 500 });
            infoStack.Children.Add(new TextBlock { Text = $"by {book.Author}", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 13, Margin = new Thickness(0, 2, 0, 0) });
            infoStack.Children.Add(new TextBlock { Text = $"Narrated by {book.Narrator}", Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
            
            var subtitle = new List<string>();
            subtitle.Add(book.Category);
            subtitle.Add(book.Duration);
            subtitle.Add($"⭐ {book.Rating}");
            
            infoStack.Children.Add(new TextBlock { Text = string.Join(" • ", subtitle), Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
            
            if (!string.IsNullOrEmpty(book.Description))
            {
                infoStack.Children.Add(new TextBlock { Text = book.Description, Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 500, Margin = new Thickness(0, 4, 0, 0) });
            }
            
            leftStack.Children.Add(infoStack);
            dp.Children.Add(leftStack);

            card.Child = dp;
            card.MouseEnter += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x1a, 0x1a, 0x2e));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 132, 22, 34));
            };
            card.MouseLeave += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0d, 0x0d, 0x14));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255));
            };

            return card;
        }

        private void PlayAudiobook(Audiobook book)
        {
            if (_currentPlayingUrl == book.Url)
            {
                // Stop current playback
                _mainWindow.PlayerEngine.Pause(); // Use PlayerEngine.Pause() instead of StopRadio
                _currentPlayingUrl = "";
                _isPlayingAudiobook = false;
            }
            else
            {
                // Play new audiobook
                _mainWindow.PlayAudio(book.Url, book.Title, book.Author, book.Cover);
                _currentPlayingUrl = book.Url;
                _isPlayingAudiobook = true;
            }
            
            // Refresh the current view to update play/pause buttons
            if (_tabHome.IsChecked == true) ShowBrowseView();
            else if (_tabSearch.IsChecked == true) ShowSearchView();
            else if (_tabFavorites.IsChecked == true) ShowFavoritesView();
        }

        private void LoadFavorites()
        {
            try
            {
                if (File.Exists(_favoritesPath))
                {
                    var json = File.ReadAllText(_favoritesPath);
                    _favorites.AddRange(JsonConvert.DeserializeObject<List<Audiobook>>(json) ?? new());
                }
            }
            catch { }
        }

        private void SaveFavorites()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_favorites, Formatting.Indented);
                Directory.CreateDirectory(Path.GetDirectoryName(_favoritesPath)!);
                File.WriteAllText(_favoritesPath, json);
            }
            catch { }
        }

        public void Dispose()
        {
            try { _http?.Dispose(); } catch { }
        }
    }
}
