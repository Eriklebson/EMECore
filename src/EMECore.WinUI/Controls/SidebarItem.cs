using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Controls;

public sealed class SidebarItem : Button
{
    public bool IsActive
    {
        get => _active;
        set { _active = value; UpdateVisual(); }
    }

    private bool _active;
    private readonly FontIcon _icon;
    private readonly TextBlock _label;

    public SidebarItem(string glyph, string label)
    {
        _icon = new FontIcon { Glyph = glyph, FontSize = 18 };
        _label = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.MD };
        panel.Children.Add(_icon);
        panel.Children.Add(_label);

        Content = panel;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        BorderThickness = new Thickness(0);
        HorizontalContentAlignment = HorizontalAlignment.Left;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        CornerRadius = Design.R.LG;
        Padding = new Thickness(Design.S.MD, Design.S.SM, Design.S.MD, Design.S.SM);

        PointerEntered += (_, _) => { if (!_active) Background = Design.C.SecB; };
        PointerExited += (_, _) => { if (!_active) Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); };

        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_active)
        {
            Background = Design.C.Pri10B;
            Foreground = Design.C.PriB;
            _icon.Foreground = Design.C.PriB;
            BorderThickness = new Thickness(1);
            BorderBrush = new SolidColorBrush(Design.C.PriRing20);
        }
        else
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            Foreground = Design.C.FgB;
            _icon.Foreground = Design.C.MutedB;
            BorderThickness = new Thickness(0);
        }
    }

    public void SetCollapsed(bool collapsed)
    {
        HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        Padding = collapsed ? new Thickness(0, Design.S.SM, 0, Design.S.SM) : new Thickness(Design.S.MD, Design.S.SM, Design.S.MD, Design.S.SM);
        _label.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
    }
}
