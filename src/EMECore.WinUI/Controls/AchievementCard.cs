using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.Core.Models;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Controls;

public sealed class AchievementCard : ContentControl
{
    public AchievementCard(Achievement ach)
    {
        var done = ach.Achieved;

        var card = new Border
        {
            Background = done ? Design.C.Pri5B : Design.C.InsetB,
            CornerRadius = Design.R.XL,
            Padding = new Thickness(Design.S.MD),
            BorderThickness = new Thickness(1),
            BorderBrush = done ? new SolidColorBrush(Design.C.PriRing) : Design.C.BorB
        };

        var grid = new Grid { ColumnSpacing = Design.S.MD };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Icon — 40x40
        var icon = new Border
        {
            Width = 40, Height = 40,
            CornerRadius = Design.R.LG,
            Background = done ? Design.C.Pri10B : Design.C.SecB,
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            BorderBrush = done ? new SolidColorBrush(Design.C.PriRing) : Design.C.BorB,
            Child = new FontIcon { Glyph = done ? "\uE8FB" : "\uE8FB", FontSize = 20, Foreground = done ? Design.C.PriB : Design.C.MutedB, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        grid.Children.Add(icon);

        // Text
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = Design.S.XS };
        text.Children.Add(new TextBlock { Text = ach.Name, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Medium, Foreground = done ? Design.C.FgB : Design.C.Muted70B, TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis });
        if (!string.IsNullOrEmpty(ach.Description))
            text.Children.Add(new TextBlock { Text = ach.Description, FontSize = 12, Foreground = Design.C.MutedB, MaxLines = 2, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap });
        // Status
        var status = Design.T.Label();
        status.Text = done ? "Desbloqueado" : "Bloqueado";
        status.Foreground = done ? Design.C.PriB : Design.C.Muted70B;
        status.CharacterSpacing = 50;
        text.Children.Add(status);

        // Progress bar
        if (ach.HasProgress && !done)
        {
            var pct = ach.MaxProgress > 0 ? (double)ach.Progress / ach.MaxProgress : 0;
            var bar = new Grid { Margin = new Thickness(0, Design.S.XS, 0, 0) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Clamp(pct*100, 0, 100), GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Clamp(100-pct*100, 0, 100), GridUnitType.Star) });
            bar.Children.Add(new Border { Background = Design.C.PriB, CornerRadius = new CornerRadius(2), Height = 4 });
            bar.Children.Add(new Border { Background = Design.C.SecB, CornerRadius = new CornerRadius(2), Height = 4 });
            text.Children.Add(bar);
        }

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        card.Child = grid;
        Content = card;
    }
}
