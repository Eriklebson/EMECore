using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Controls;

public sealed class InfoCard : ContentControl
{
    private readonly StackPanel _content;

    public InfoCard(string icon, string title)
    {
        var header = SectionHeader(icon, title);

        _content = new StackPanel { Spacing = Design.S.SM };
        _content.Children.Add(header);

        var body = new StackPanel { Spacing = Design.S.SM };

        var card = new Border
        {
            Background = Design.C.Card60B,
            CornerRadius = Design.R.XXL,
            Padding = new Thickness(Design.S.XL),
            BorderThickness = new Thickness(1),
            BorderBrush = Design.C.BorB,
            Child = body
        };

        _content.Children.Add(body);
        Content = card;
    }

    public void AddField(string label, string value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.LG };
        row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Design.C.MutedB, Width = 80 });
        row.Children.Add(new TextBlock { Text = value, FontSize = 12, Foreground = Design.C.FgB, TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis });
        ((StackPanel)((Border)Content).Child).Children.Add(row);
    }

    public void SetContent(UIElement el)
    {
        var stack = (StackPanel)((Border)Content).Child;
        // Remove all but header
        while (stack.Children.Count > 1) stack.Children.RemoveAt(1);
        stack.Children.Add(el);
    }

    private static StackPanel SectionHeader(string icon, string title)
    {
        var h = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.SM };
        h.Children.Add(new FontIcon { Glyph = icon, FontSize = 14, Foreground = Design.C.PriB, VerticalAlignment = VerticalAlignment.Center });
        var t = Design.T.Label();
        t.Text = title;
        t.Foreground = Design.C.Muted70B;
        h.Children.Add(t);
        return h;
    }
}
