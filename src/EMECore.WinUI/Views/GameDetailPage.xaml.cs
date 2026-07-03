using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using EMECore.Core.Models;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class GameDetailPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler<Game>? LaunchRequested;
    public event EventHandler<Game>? DeleteRequested;
    public event EventHandler? TestAchievementRequested;

    private Game? _game;
    private readonly TextBlock _gameTitle;
    private readonly TextBlock _developerText;
    private readonly StellarBladeAchievementImageService _achievementImageService;
    private readonly TextBlock _platformBadge;
    private readonly TextBlock _playTimeValue;
    private readonly TextBlock _lastPlayedValue;
    private readonly TextBlock _pathValue;
    private readonly TextBlock _achievementsTitle;
    private readonly StackPanel _achievementsContainer;
    private readonly StackPanel _sessionsContainer;
    private readonly StackPanel _requirementsContainer;
    private readonly Image _heroImage;
    private readonly Border _coverThumb;

    public GameDetailPage()
    {
        _achievementImageService = new StellarBladeAchievementImageService();
        
        var root = new Grid();

        // Hero Background Image
        _heroImage = new Image 
        { 
            Stretch = Stretch.UniformToFill, 
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        root.Children.Add(_heroImage);

        // Gradient overlay for hero
        var overlay = new Grid 
        { 
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xDD, 0x0e, 0x16, 0x21), Offset = 0 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0x99, 0x0e, 0x16, 0x21), Offset = 0.4 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xBB, 0x0e, 0x16, 0x21), Offset = 0.7 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xEE, 0x0e, 0x16, 0x21), Offset = 1 }
                }
            }
        };
        root.Children.Add(overlay);

        // Content
        var scrollViewer = new ScrollViewer();
        var contentRoot = new StackPanel();

        // Hero Header Section
        var heroHeader = new Grid 
        { 
            Margin = new Thickness(24, 24, 24, 16),
            MinHeight = 180
        };
        heroHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heroHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heroHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Back Button
        var backBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE72B", FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Biblioteca", VerticalAlignment = VerticalAlignment.Center, FontSize = 13 }
                }
            },
            Height = 40,
            Padding = new Thickness(12, 0, 16, 0),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x00, 0x00, 0x00)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 16, 0)
        };
        backBtn.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);

        // Cover Thumbnail
        _coverThumb = new Border
        {
            Width = 120,
            Height = 168,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
            Child = new Image { Stretch = Stretch.UniformToFill },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0)
        };

        // Title and Info Section
        var titleBlock = new StackPanel 
        { 
            VerticalAlignment = VerticalAlignment.Center, 
            Spacing = 8 
        };
        
        _gameTitle = new TextBlock 
        { 
            FontSize = 32, 
            FontWeight = Microsoft.UI.Text.FontWeights.ExtraBlack, 
            Foreground = new SolidColorBrush(Colors.White), 
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 36
        };
        titleBlock.Children.Add(_gameTitle);
        
        _developerText = new TextBlock 
        { 
            FontSize = 14, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xBB, 0xFF, 0xFF, 0xFF)) 
        };
        titleBlock.Children.Add(_developerText);
        
        var badgeRow = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Spacing = 10, 
            Margin = new Thickness(0, 8, 0, 0) 
        };
        
        var badgeBorder = new Border 
        { 
            Background = SteamColors.BlueBrush, 
            CornerRadius = new CornerRadius(6), 
            Padding = new Thickness(12, 6, 12, 6) 
        };
        _platformBadge = new TextBlock 
        { 
            FontSize = 11, 
            FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
            Foreground = new SolidColorBrush(Colors.White) 
        };
        badgeBorder.Child = _platformBadge;
        badgeRow.Children.Add(badgeBorder);
        titleBlock.Children.Add(badgeRow);

        // Left header layout
        var leftHeader = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            VerticalAlignment = VerticalAlignment.Center 
        };
        leftHeader.Children.Add(backBtn);
        leftHeader.Children.Add(_coverThumb);
        leftHeader.Children.Add(titleBlock);
        Grid.SetColumn(leftHeader, 0);
        heroHeader.Children.Add(leftHeader);

        // Action Buttons
        var actions = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Spacing = 12, 
            VerticalAlignment = VerticalAlignment.Center 
        };
        
        var playBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    new FontIcon { Glyph = "\uE768", FontSize = 18, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Jogar", VerticalAlignment = VerticalAlignment.Center, FontSize = 16 }
                }
            },
            Background = SteamColors.GreenBrush,
            Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(28, 14, 28, 14),
            Height = 48
        };
        playBtn.Click += (_, _) => { if (_game != null) LaunchRequested?.Invoke(this, _game); };
        actions.Children.Add(playBtn);

        var delBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE74D", FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Remover", VerticalAlignment = VerticalAlignment.Center, FontSize = 13 }
                }
            },
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0xD9, 0x41, 0x26)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x66, 0x66)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 12, 20, 12),
            Height = 48
        };
        delBtn.Click += (_, _) => { if (_game != null) DeleteRequested?.Invoke(this, _game); };
        actions.Children.Add(delBtn);
        
        Grid.SetColumn(actions, 2);
        heroHeader.Children.Add(actions);

        contentRoot.Children.Add(heroHeader);

        // Info Strip
        var infoStrip = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Spacing = 24, 
            Margin = new Thickness(24, 8, 24, 12) 
        };
        
        var playTimeStack = new StackPanel { Spacing = 2 };
        playTimeStack.Children.Add(new TextBlock 
        { 
            Text = "TEMPO DE JOGO", 
            FontSize = 9, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CharacterSpacing = 50
        });
        _playTimeValue = new TextBlock 
        { 
            FontSize = 14, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        playTimeStack.Children.Add(_playTimeValue);
        infoStrip.Children.Add(playTimeStack);
        
        var pathStack = new StackPanel { Spacing = 2 };
        pathStack.Children.Add(new TextBlock 
        { 
            Text = "CAMINHO", 
            FontSize = 9, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CharacterSpacing = 50
        });
        _pathValue = new TextBlock 
        { 
            FontSize = 12, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)), 
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontFamily = new FontFamily("Consolas")
        };
        pathStack.Children.Add(_pathValue);
        infoStrip.Children.Add(pathStack);
        
        contentRoot.Children.Add(infoStrip);

        // Content Cards Section
        var cardsGrid = new Grid 
        { 
            Margin = new Thickness(24, 8, 24, 24),
            ColumnSpacing = 16
        };
        cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Column - Achievements
        var leftColumn = new StackPanel { Spacing = 16 };

        var achCard = new StackPanel { Spacing = 12 };
        _achievementsTitle = new TextBlock 
        { 
            Text = "CONQUISTAS", 
            FontSize = 12, 
            FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)),
            CharacterSpacing = 50
        };
        achCard.Children.Add(_achievementsTitle);
        _achievementsContainer = new StackPanel { Spacing = 8 };
        achCard.Children.Add(_achievementsContainer);
        leftColumn.Children.Add(GlassCard(achCard));

        // Right Column - Info and Sessions
        var rightColumn = new StackPanel { Spacing = 16 };

        // Info Card
        var infoCard = new StackPanel { Spacing = 12 };
        infoCard.Children.Add(SidebarTitle("INFORMAÇÕES"));
        
        var infoContent = new StackPanel { Spacing = 16 };
        
        var lastPlayedStack = new StackPanel { Spacing = 4 };
        lastPlayedStack.Children.Add(new TextBlock 
        { 
            Text = "Última Vez", 
            FontSize = 11, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)) 
        });
        _lastPlayedValue = new TextBlock 
        { 
            Text = "Nunca", 
            FontSize = 14, 
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
            Foreground = SteamColors.TextBrush 
        };
        lastPlayedStack.Children.Add(_lastPlayedValue);
        infoContent.Children.Add(lastPlayedStack);
        
        infoCard.Children.Add(infoContent);
        rightColumn.Children.Add(GlassCard(infoCard));

        // Sessions Card
        var sessCard = new StackPanel { Spacing = 12 };
        sessCard.Children.Add(SidebarTitle("SESSÕES RECENTES"));
        _sessionsContainer = new StackPanel { Spacing = 8 };
        _sessionsContainer.Children.Add(new TextBlock 
        { 
            Text = "Nenhuma sessão registrada", 
            FontSize = 12, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)) 
        });
        sessCard.Children.Add(_sessionsContainer);
        rightColumn.Children.Add(GlassCard(sessCard));

        // Requirements Card
        var reqCard = new StackPanel { Spacing = 12 };
        reqCard.Children.Add(SidebarTitle("REQUISITOS DO SISTEMA"));
        
        _requirementsContainer = new StackPanel { Spacing = 16 };
        _requirementsContainer.Children.Add(new TextBlock 
        { 
            Text = "Carregando requisitos...", 
            FontSize = 12, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)) 
        });
        
        reqCard.Children.Add(_requirementsContainer);
        rightColumn.Children.Add(GlassCard(reqCard));

        // Add columns to grid
        Grid.SetColumn(leftColumn, 0);
        cardsGrid.Children.Add(leftColumn);
        Grid.SetColumn(rightColumn, 1);
        cardsGrid.Children.Add(rightColumn);

        contentRoot.Children.Add(cardsGrid);
        scrollViewer.Content = contentRoot;
        root.Children.Add(scrollViewer);
        Content = root;
    }

    private static StackPanel BtnContent(string g, string t) => new()
    {
        Orientation = Orientation.Horizontal, Spacing = 8,
        Children = 
        { 
            new FontIcon { Glyph = g, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }, 
            new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 } 
        }
    };

    private static Border GlassCard(UIElement child) => new()
    {
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x17, 0x2A, 0x40)),
        CornerRadius = new CornerRadius(12), 
        Padding = new Thickness(20),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)),
        Child = child
    };

    private static TextBlock SidebarTitle(string text) => new()
    {
        Text = text, 
        FontSize = 11, 
        FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)),
        CharacterSpacing = 50
    };

    private static StackPanel CreateRequirementItem(string label, string value)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new TextBlock 
        { 
            Text = label, 
            FontSize = 11, 
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)),
            Width = 100
        });
        panel.Children.Add(new TextBlock 
        { 
            Text = value, 
            FontSize = 11, 
            Foreground = SteamColors.TextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static TextBlock InfoText(StackPanel parent, string label, string value)
    {
        var row = new StackPanel { Spacing = 4 };
        row.Children.Add(new TextBlock 
        { 
            Text = label, 
            FontSize = 11, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)) 
        });
        var val = new TextBlock 
        { 
            Text = value, 
            FontSize = 14, 
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, 
            Foreground = SteamColors.TextBrush 
        };
        row.Children.Add(val);
        parent.Children.Add(row);
        return val;
    }

    public void LoadGame(Game game)
    {
        _game = game;
        _gameTitle.Text = game.Name;
        _platformBadge.Text = game.Platform.ToUpper();
        _developerText.Text = game.Platform == "steam" ? "Steam" : game.Platform == "xbox" ? "Xbox / Game Pass" : "Outro";

        var ts = TimeSpan.FromMinutes(game.PlayTime);
        _playTimeValue.Text = game.PlayTime > 0 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : "Nunca jogado";

        _lastPlayedValue.Text = game.LastPlayed.HasValue ? game.LastPlayed.Value.ToString("dd/MM/yyyy HH:mm") : "Nunca";

        _pathValue.Text = game.ExecutablePath.Length > 80 ? "..." + game.ExecutablePath[^77..] : game.ExecutablePath;

        if (!string.IsNullOrWhiteSpace(game.CoverImage) &&
            Uri.TryCreate(game.CoverImage, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "file"))
        {
            _heroImage.Source = new BitmapImage(uri);
            _heroImage.Visibility = Visibility.Visible;
            ((Image)_coverThumb.Child).Source = new BitmapImage(uri);
        }
    }

    public void SetRequirements(SteamRequirements? requirements, string platform = "")
    {
        _requirementsContainer.Children.Clear();

        // Para jogos Xbox, mostrar mensagem específica
        if (platform.Equals("xbox", StringComparison.OrdinalIgnoreCase))
        {
            _requirementsContainer.Children.Add(new TextBlock 
            { 
                Text = "Requisitos do sistema não disponíveis para jogos Xbox/Game Pass", 
                FontSize = 12, 
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)),
                TextWrapping = TextWrapping.Wrap
            });
            _requirementsContainer.Children.Add(new TextBlock 
            { 
                Text = "Os requisitos são gerenciados automaticamente pelo Xbox App", 
                FontSize = 11, 
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x8B, 0x9B, 0xB4)),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        if (requirements == null || (string.IsNullOrEmpty(requirements.Minimum) && string.IsNullOrEmpty(requirements.Recommended)))
        {
            _requirementsContainer.Children.Add(new TextBlock 
            { 
                Text = "Requisitos não disponíveis", 
                FontSize = 12, 
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)) 
            });
            return;
        }

        // Minimum Requirements
        if (!string.IsNullOrEmpty(requirements.Minimum))
        {
            var minReqStack = new StackPanel { Spacing = 8 };
            minReqStack.Children.Add(new TextBlock 
            { 
                Text = "Mínimos", 
                FontSize = 12, 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                Foreground = SteamColors.TextBrush 
            });
            
            var minReqText = new TextBlock 
            { 
                Text = CleanHtml(requirements.Minimum), 
                FontSize = 11, 
                Foreground = SteamColors.TextBrush,
                TextWrapping = TextWrapping.Wrap
            };
            minReqStack.Children.Add(minReqText);
            _requirementsContainer.Children.Add(minReqStack);
        }

        // Recommended Requirements
        if (!string.IsNullOrEmpty(requirements.Recommended))
        {
            var recReqStack = new StackPanel { Spacing = 8 };
            recReqStack.Children.Add(new TextBlock 
            { 
                Text = "Recomendados", 
                FontSize = 12, 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                Foreground = SteamColors.TextBrush 
            });
            
            var recReqText = new TextBlock 
            { 
                Text = CleanHtml(requirements.Recommended), 
                FontSize = 11, 
                Foreground = SteamColors.TextBrush,
                TextWrapping = TextWrapping.Wrap
            };
            recReqStack.Children.Add(recReqText);
            _requirementsContainer.Children.Add(recReqStack);
        }
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        
        // Remove tags HTML
        var clean = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
        
        // Limpar espaços extras e quebras de linha
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ");
        clean = clean.Trim();
        
        // Adicionar quebras de linha após dois pontos para melhor formatação
        clean = clean.Replace(": ", ":\n");
        
        return clean;
    }

    public async Task SetAchievements(List<Achievement> achievements)
    {
        _achievementsContainer.Children.Clear();

        if (achievements.Count == 0)
        {
            _achievementsContainer.Children.Add(new TextBlock 
            { 
                Text = "Nenhuma conquista", 
                FontSize = 12, 
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)) 
            });
            return;
        }

        var achieved = achievements.Count(a => a.Achieved);
        _achievementsTitle.Text = $"CONQUISTAS ({achieved}/{achievements.Count})";

        // Progress header
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var progressContainer = new StackPanel { Spacing = 8 };
        var pctText = new TextBlock
        {
            Text = $"{achieved} de {achievements.Count} conquistas desbloqueadas",
            FontSize = 12,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4))
        };
        progressContainer.Children.Add(pctText);

        // Progress bar
        var barHeight = 8;
        var pct = achievements.Count > 0 ? (double)achieved / achievements.Count : 0;
        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(pct, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - pct, GridUnitType.Star) });
        
        var barFill = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0.5),
                EndPoint = new Windows.Foundation.Point(1, 0.5),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xFF, 0x66, 0xC0, 0xF4), Offset = 0 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xCC, 0x66, 0xC0, 0xF4), Offset = 1 }
                }
            },
            CornerRadius = new CornerRadius(4, 0, 0, 4),
            Height = barHeight
        };
        
        var barBg = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0x66, 0xC0, 0xF4)),
            CornerRadius = new CornerRadius(0, 4, 4, 0),
            Height = barHeight
        };
        
        Grid.SetColumn(barFill, 0);
        Grid.SetColumn(barBg, 1);
        bar.Children.Add(barFill);
        bar.Children.Add(barBg);
        progressContainer.Children.Add(bar);
        headerGrid.Children.Add(progressContainer);

        var pctBadge = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0x66, 0xC0, 0xF4)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6, 12, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        pctBadge.Child = new TextBlock
        {
            Text = $"{Math.Round(achievements.Count > 0 ? 100.0 * achieved / achievements.Count : 0, 0)}%",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.BlueBrush
        };
        Grid.SetColumn(pctBadge, 1);
        headerGrid.Children.Add(pctBadge);
        _achievementsContainer.Children.Add(headerGrid);

        // Separator
        _achievementsContainer.Children.Add(new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)),
            Height = 1,
            Margin = new Thickness(0, 0, 0, 12)
        });

        // Achievements grid
        var achievementsScroll = new ScrollViewer
        {
            MaxHeight = 400,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var achievementsGrid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        achievementsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        achievementsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < achievements.Count; i++)
        {
            var ach = achievements[i];
            var done = ach.Achieved;
            var col = i % 2;
            var row = i / 2;

            var achCard = new Border
            {
                Background = done
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x18, 0x66, 0xC0, 0xF4))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                BorderThickness = new Thickness(1),
                BorderBrush = done
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x25, 0x66, 0xC0, 0xF4))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF))
            };

            var achContent = new Grid { ColumnSpacing = 14 };
            achContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            achContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Achievement icon
            var iconBorder = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(8),
                Background = done
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x30, 0x66, 0xC0, 0xF4))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                BorderBrush = done
                    ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x66, 0xC0, 0xF4))
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF))
            };
            
            // Carregar imagem da conquista
            var achievementImage = new Image
            {
                Width = 40,
                Height = 40,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Tentar carregar imagem específica do Stellar Blade
            var stellarBladeImagePath = await _achievementImageService.GetAchievementImageAsync(ach.Apiname);
            if (!string.IsNullOrEmpty(stellarBladeImagePath))
            {
                achievementImage.Source = new BitmapImage(new Uri($"file:///{stellarBladeImagePath}"));
            }
            else
            {
                // Fallback para imagens padrão
                var defaultImage = done 
                    ? new BitmapImage(new Uri("ms-appx:///Assets/Achievements/achieved.png"))
                    : new BitmapImage(new Uri("ms-appx:///Assets/Achievements/locked.png"));
                achievementImage.Source = defaultImage;
            }
            
            iconBorder.Child = achievementImage;
            achContent.Children.Add(iconBorder);

            // Achievement text
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4 };
            textStack.Children.Add(new TextBlock
            {
                Text = ach.Name,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = done
                    ? SteamColors.TextBrush
                    : new SolidColorBrush(Windows.UI.Color.FromArgb(0x70, 0xC6, 0xD4, 0xDF)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (!string.IsNullOrEmpty(ach.Description))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = ach.Description,
                    FontSize = 11,
                    Foreground = done
                        ? SteamColors.TextSecondaryBrush
                        : new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x88, 0x88, 0x88)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 2,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // Barra de progresso para conquistas com progresso
            if (ach.HasProgress && !done)
            {
                var achProgressContainer = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
                
                var progressText = new TextBlock
                {
                    Text = $"{ach.Progress} / {ach.MaxProgress}",
                    FontSize = 10,
                    Foreground = SteamColors.TextSecondaryBrush
                };
                achProgressContainer.Children.Add(progressText);
                
                var progressPercent = ach.MaxProgress > 0 ? Math.Min(100, (double)ach.Progress / ach.MaxProgress * 100) : 0;
                var remainingPercent = 100 - progressPercent;
                
                var progressBar = new Grid();
                progressBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(progressPercent, GridUnitType.Star) });
                progressBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(remainingPercent, GridUnitType.Star) });
                
                var progressFill = new Border
                {
                    Background = SteamColors.BlueBrush,
                    CornerRadius = new CornerRadius(2, 0, 0, 2),
                    Height = 4
                };
                
                var progressBg = new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0x66, 0xC0, 0xF4)),
                    CornerRadius = new CornerRadius(0, 2, 2, 0),
                    Height = 4
                };
                
                Grid.SetColumn(progressFill, 0);
                Grid.SetColumn(progressBg, 1);
                progressBar.Children.Add(progressFill);
                progressBar.Children.Add(progressBg);
                achProgressContainer.Children.Add(progressBar);
                
                textStack.Children.Add(achProgressContainer);
            }
            else if (done)
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = "Desbloqueado",
                    FontSize = 10,
                    Foreground = SteamColors.GreenBrush,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold
                });
            }

            Grid.SetColumn(textStack, 1);
            achContent.Children.Add(textStack);
            achCard.Child = achContent;

            Grid.SetColumn(achCard, col);
            Grid.SetRow(achCard, row);
            achievementsGrid.Children.Add(achCard);
        }

        for (int i = 0; i < (achievements.Count + 1) / 2; i++)
            achievementsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        achievementsScroll.Content = achievementsGrid;
        _achievementsContainer.Children.Add(achievementsScroll);
    }
}
