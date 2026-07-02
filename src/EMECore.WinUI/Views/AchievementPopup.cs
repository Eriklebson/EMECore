using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using EMECore.Core.Models;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed class AchievementPopup
{
    private readonly Border _container;
    private readonly TextBlock _titleText;
    private readonly TextBlock _achievementName;
    private readonly TextBlock _achievementDesc;
    private readonly FontIcon _icon;
    private readonly DispatcherTimer _hideTimer;
    private readonly Storyboard _showAnimation;
    private readonly Storyboard _hideAnimation;

    public AchievementPopup()
    {
        _container = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xF0, 0x1a, 0x2a, 0x3a), Offset = 0 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xF0, 0x0e, 0x16, 0x21), Offset = 1 }
                }
            },
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x66, 0xC0, 0xF4)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 20, 20),
            Visibility = Visibility.Collapsed,
            Translation = new System.Numerics.Vector3(0, 100, 0)
        };

        var content = new Grid { ColumnSpacing = 12 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Ícone
        var iconBorder = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(24),
            Background = SteamColors.BlueBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        _icon = new FontIcon
        {
            Glyph = "\uE8FB",
            FontSize = 22,
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBorder.Child = _icon;
        content.Children.Add(iconBorder);

        // Textos
        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        _titleText = new TextBlock
        {
            Text = "CONQUISTA DESBLOQUEADA",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.BlueBrush,
            CharacterSpacing = 100
        };
        textStack.Children.Add(_titleText);

        _achievementName = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.TextBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        textStack.Children.Add(_achievementName);

        _achievementDesc = new TextBlock
        {
            FontSize = 11,
            Foreground = SteamColors.TextSecondaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            TextWrapping = TextWrapping.Wrap
        };
        textStack.Children.Add(_achievementDesc);

        Grid.SetColumn(textStack, 1);
        content.Children.Add(textStack);
        _container.Child = content;

        // Timer para esconder
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _hideTimer.Tick += (_, _) => Hide();

        // Animações
        _showAnimation = new Storyboard();
        var showAnim = new DoubleAnimation
        {
            From = 100,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showAnim, _container);
        Storyboard.SetTargetProperty(showAnim, "Translation.Y");
        _showAnimation.Children.Add(showAnim);

        _hideAnimation = new Storyboard();
        var hideAnim = new DoubleAnimation
        {
            From = 0,
            To = 100,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideAnim, _container);
        Storyboard.SetTargetProperty(hideAnim, "Translation.Y");
        _hideAnimation.Children.Add(hideAnim);
        _hideAnimation.Completed += (_, _) => _container.Visibility = Visibility.Collapsed;
    }

    public UIElement Element => _container;

    public void Show(Achievement achievement)
    {
        _achievementName.Text = achievement.Name;
        _achievementDesc.Text = achievement.Description;
        _icon.Glyph = achievement.Achieved ? "\uE8FB" : "\uE739";

        if (achievement.Achieved)
        {
            _container.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x66, 0xC0, 0xF4));
            ((Border)((Grid)_container.Child).Children[0]).Background = SteamColors.BlueBrush;
        }
        else
        {
            _container.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0x66, 0x66, 0x66));
            ((Border)((Grid)_container.Child).Children[0]).Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x44, 0x66, 0x66, 0x66));
        }

        _container.Visibility = Visibility.Visible;
        _showAnimation.Begin();
        _hideTimer.Start();
    }

    public void Hide()
    {
        _hideTimer.Stop();
        _hideAnimation.Begin();
    }
}
