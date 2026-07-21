using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.Core.Models;
using EMECore.Core.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed class AchievementsPage : UserControl
{
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _mainPanel;
    private readonly TextBlock _titleText;
    private readonly TextBlock _statsText;
    private readonly TextBlock _loadingText;
    private readonly StackPanel _contentPanel;
    private readonly StackPanel _emptyPanel;

    private readonly IAchievementCheckerService _checkerService;
    private List<Achievement> _allAchievements = new();
    private bool _isLoading;

    public AchievementsPage(IAchievementCheckerService checkerService)
    {
        _checkerService = checkerService;

        var root = new Grid { Background = new SolidColorBrush(SteamColors.Dark) };

        _scroll = new ScrollViewer();
        _mainPanel = new StackPanel { Margin = new Thickness(Design.S.XXL) };

        var header = new Grid { Margin = new Thickness(0, 0, 0, Design.S.XL) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerLeft = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.MD };
        var icon = new FontIcon { Glyph = "\uE7C1", FontSize = 28, Foreground = Design.C.PriB };
        headerLeft.Children.Add(icon);
        _titleText = new TextBlock
        {
            Text = "Conquistas",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerLeft.Children.Add(_titleText);
        Grid.SetColumn(headerLeft, 0);
        header.Children.Add(headerLeft);

        _statsText = new TextBlock
        {
            Text = "",
            FontSize = 14,
            Foreground = SteamColors.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(_statsText, 2);
        header.Children.Add(_statsText);

        _mainPanel.Children.Add(header);

        _loadingText = new TextBlock
        {
            Text = "Carregando...",
            FontSize = 14,
            Foreground = SteamColors.TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, Design.S.XXL, 0, 0)
        };
        _mainPanel.Children.Add(_loadingText);

        _contentPanel = new StackPanel { Spacing = Design.S.SM };
        _mainPanel.Children.Add(_contentPanel);

        _emptyPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, Design.S.XXL * 2, 0, 0)
        };
        _emptyPanel.Children.Add(new FontIcon
        {
            Glyph = "\uE7C1",
            FontSize = 48,
            Foreground = SteamColors.TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _emptyPanel.Children.Add(new TextBlock
        {
            Text = "Nenhuma conquista encontrada",
            FontSize = 16,
            Foreground = SteamColors.TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, Design.S.MD, 0, 0)
        });
        _emptyPanel.Children.Add(new TextBlock
        {
            Text = "Jogue um jogo para desbloquear conquistas",
            FontSize = 13,
            Foreground = SteamColors.TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = 0.6
        });
        _emptyPanel.Visibility = Visibility.Collapsed;
        _mainPanel.Children.Add(_emptyPanel);

        _scroll.Content = _mainPanel;
        root.Children.Add(_scroll);

        Content = root;
    }

    public async void LoadAchievements(List<Game> games)
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            _loadingText.Visibility = Visibility.Visible;
            _contentPanel.Children.Clear();
            _emptyPanel.Visibility = Visibility.Collapsed;
            _allAchievements.Clear();

            var results = await Task.Run(async () =>
            {
                var allResults = new List<(Game game, List<Achievement> achievements)>();
                foreach (var game in games)
                {
                    try
                    {
                        var achievements = await _checkerService.CheckAllAchievementsAsync(game);
                        if (achievements.Count > 0)
                            allResults.Add((game, achievements));
                    }
                    catch { }
                }
                return allResults;
            });

            var totalAchieved = 0;
            var totalAchievements = 0;

            foreach (var (game, achievements) in results)
            {
                _allAchievements.AddRange(achievements);
                totalAchievements += achievements.Count;
                totalAchieved += achievements.Count(a => a.Achieved);

                var section = CreateGameSection(game, achievements);
                _contentPanel.Children.Add(section);
            }

            if (_allAchievements.Count == 0)
            {
                _emptyPanel.Visibility = Visibility.Visible;
            }
            else
            {
                _statsText.Text = $"{totalAchieved}/{totalAchievements} desbloqueadas";
            }
        }
        catch { }
        finally
        {
            _loadingText.Visibility = Visibility.Collapsed;
            _isLoading = false;
        }
    }

    private StackPanel CreateGameSection(Game game, List<Achievement> achievements)
    {
        var section = new StackPanel { Spacing = Design.S.SM };

        var header = new Grid { Margin = new Thickness(0, Design.S.MD, 0, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.SM };
        var gameName = new TextBlock
        {
            Text = game.Name,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = SteamColors.TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        namePanel.Children.Add(gameName);

        var achieved = achievements.Count(a => a.Achieved);
        var badge = new Border
        {
            Background = new SolidColorBrush(ThemeManager.WithAlpha(ThemeManager.Current.Accent, 0x20)),
            CornerRadius = Design.R.SM,
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        badge.Child = new TextBlock
        {
            Text = $"{achieved}/{achievements.Count}",
            FontSize = 12,
            Foreground = new SolidColorBrush(SteamColors.Green),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        namePanel.Children.Add(badge);

        Grid.SetColumn(namePanel, 0);
        header.Children.Add(namePanel);

        var pctText = new TextBlock
        {
            Text = achievements.Count > 0 ? $"{achieved * 100 / achievements.Count}%" : "0%",
            FontSize = 13,
            Foreground = SteamColors.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(pctText, 1);
        header.Children.Add(pctText);

        section.Children.Add(header);

        var sorted = achievements.OrderByDescending(a => a.Achieved).ThenBy(a => a.Name).ToList();
        foreach (var achievement in sorted)
        {
            section.Children.Add(CreateAchievementItem(achievement));
        }

        return section;
    }

    private Border CreateAchievementItem(Achievement achievement)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(achievement.Achieved
                ? ThemeManager.WithAlpha(ThemeManager.Current.Accent, 0x15)
                : Design.C.Card),
            CornerRadius = Design.R.MD,
            Padding = new Thickness(Design.S.MD),
            Margin = new Thickness(0, 2, 0, 2),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(achievement.Achieved
                ? ThemeManager.WithAlpha(ThemeManager.Current.Accent, 0x40)
                : Design.C.Bor),
            Opacity = achievement.Achieved ? 1.0 : 0.7
        };

        var grid = new Grid { ColumnSpacing = Design.S.MD };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconPanel = new Grid { Width = 40, Height = 40 };
        var iconBg = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(achievement.Achieved
                ? ThemeManager.WithAlpha(ThemeManager.Current.Accent, 0x30)
                : Design.C.Card),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(achievement.Achieved
                ? SteamColors.Green
                : Design.C.Bor)
        };
        var iconText = new TextBlock
        {
            Text = achievement.Achieved ? "\uE73E" : "\uE7C1",
            FontSize = 16,
            Foreground = new SolidColorBrush(achievement.Achieved
                ? SteamColors.Green
                : Design.C.Muted),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBg.Child = iconText;
        iconPanel.Children.Add(iconBg);
        Grid.SetColumn(iconPanel, 0);
        grid.Children.Add(iconPanel);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var nameText = new TextBlock
        {
            Text = achievement.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(achievement.Achieved
                ? SteamColors.Text
                : SteamColors.TextSecondary)
        };
        info.Children.Add(nameText);

        if (!string.IsNullOrEmpty(achievement.Description))
        {
            var descText = new TextBlock
            {
                Text = achievement.Description,
                FontSize = 12,
                Foreground = SteamColors.TextSecondaryBrush,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 500
            };
            info.Children.Add(descText);
        }

        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        if (achievement.HasProgress)
        {
            var progressText = new TextBlock
            {
                Text = $"{achievement.Progress}/{achievement.MaxProgress}",
                FontSize = 12,
                Foreground = SteamColors.TextSecondaryBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            Grid.SetColumn(progressText, 2);
            grid.Children.Add(progressText);
        }
        else if (achievement.Achieved && achievement.Unlocktime > 0)
        {
            var dateText = new TextBlock
            {
                Text = DateTimeOffset.FromUnixTimeSeconds(achievement.Unlocktime).LocalDateTime.ToString("dd/MM/yyyy"),
                FontSize = 11,
                Foreground = SteamColors.TextSecondaryBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(dateText, 2);
            grid.Children.Add(dateText);
        }

        card.Child = grid;
        return card;
    }
}
