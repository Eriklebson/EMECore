using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace EMECore.WinUI.Theme;

/// <summary>
/// Atualiza brushes que foram criados pelas Views a partir da paleta anterior.
/// Brushes semânticos de hardware que não pertencem à paleta são preservados.
/// </summary>
public static class ThemeVisualTree
{
    public const string PreviewTag = "theme-preview";

    public static void Refresh(DependencyObject? root, AppTheme previous, AppTheme current)
    {
        Refresh(root, previous, current, "raiz", 0);
    }

    private static void Refresh(DependencyObject? root, AppTheme previous, AppTheme current, string path, int depth)
    {
        if (root == null) return;
        var elementName = root is FrameworkElement element && !string.IsNullOrWhiteSpace(element.Name)
            ? $"#{element.Name}"
            : string.Empty;
        var currentPath = $"{path}/{root.GetType().Name}{elementName}";
        ThemeChangeDiagnostics.Write($"TREE ENTER d={depth} {currentPath}");
        if (root is FrameworkElement { Tag: string tag } && tag == PreviewTag)
        {
            ThemeChangeDiagnostics.Write($"TREE SKIP preview {currentPath}");
            return;
        }

        switch (root)
        {
            case Border border:
                RefreshProperty(() => border.Background, value => border.Background = value, previous, current, $"{currentPath}.Background");
                RefreshProperty(() => border.BorderBrush, value => border.BorderBrush = value, previous, current, $"{currentPath}.BorderBrush");
                break;
            case Panel panel:
                RefreshProperty(() => panel.Background, value => panel.Background = value, previous, current, $"{currentPath}.Background");
                break;
            case Control control:
                RefreshProperty(() => control.Background, value => control.Background = value, previous, current, $"{currentPath}.Background");
                RefreshProperty(() => control.Foreground, value => control.Foreground = value, previous, current, $"{currentPath}.Foreground");
                RefreshProperty(() => control.BorderBrush, value => control.BorderBrush = value, previous, current, $"{currentPath}.BorderBrush");
                break;
            case TextBlock text:
                RefreshProperty(() => text.Foreground, value => text.Foreground = value, previous, current, $"{currentPath}.Foreground");
                break;
            case IconElement icon:
                RefreshProperty(() => icon.Foreground, value => icon.Foreground = value, previous, current, $"{currentPath}.Foreground");
                break;
            case Shape shape:
                RefreshProperty(() => shape.Fill, value => shape.Fill = value, previous, current, $"{currentPath}.Fill");
                RefreshProperty(() => shape.Stroke, value => shape.Stroke = value, previous, current, $"{currentPath}.Stroke");
                break;
        }

        // Percorre apenas o conteúdo pertencente ao EMECore. VisualTreeHelper também
        // entra nos templates nativos de controles WinUI/WebView2; alterar brushes
        // internos desses templates durante um evento de ponteiro derruba o compositor.
        switch (root)
        {
            case Panel panel:
                foreach (var child in panel.Children)
                    Refresh(child, previous, current, currentPath, depth + 1);
                break;
            case Border { Child: DependencyObject child }:
                Refresh(child, previous, current, currentPath, depth + 1);
                break;
            case UserControl { Content: DependencyObject content }:
                Refresh(content, previous, current, currentPath, depth + 1);
                break;
            case ContentControl { Content: DependencyObject content }:
                Refresh(content, previous, current, currentPath, depth + 1);
                break;
            case Viewbox { Child: DependencyObject child }:
                Refresh(child, previous, current, currentPath, depth + 1);
                break;
            case ItemsControl itemsControl:
                foreach (var item in itemsControl.Items)
                    if (item is DependencyObject itemElement)
                        Refresh(itemElement, previous, current, currentPath, depth + 1);
                break;
        }
        ThemeChangeDiagnostics.Write($"TREE EXIT d={depth} {currentPath}");
    }

    private static void RefreshProperty(
        Func<Brush?> getter,
        Action<Brush> setter,
        AppTheme previous,
        AppTheme current,
        string propertyPath)
    {
        ThemeChangeDiagnostics.Write($"PROPERTY GET BEFORE {propertyPath}");
        var brush = getter();
        ThemeChangeDiagnostics.Write($"PROPERTY GET AFTER {propertyPath} type={brush?.GetType().Name ?? "null"}");
        switch (brush)
        {
            case SolidColorBrush solid:
                ThemeChangeDiagnostics.Write($"BRUSH SOLID READ BEFORE {propertyPath}");
                var source = solid.Color;
                var mapped = MapColor(source, previous, current);
                ThemeChangeDiagnostics.Write($"BRUSH SOLID REPLACE BEFORE {propertyPath} {source} -> {mapped}");
                setter(new SolidColorBrush(mapped) { Opacity = solid.Opacity });
                ThemeChangeDiagnostics.Write($"BRUSH SOLID REPLACE AFTER {propertyPath}");
                break;
            case LinearGradientBrush gradient:
                var replacement = new LinearGradientBrush
                {
                    StartPoint = gradient.StartPoint,
                    EndPoint = gradient.EndPoint,
                    MappingMode = gradient.MappingMode,
                    SpreadMethod = gradient.SpreadMethod,
                    ColorInterpolationMode = gradient.ColorInterpolationMode,
                    Opacity = gradient.Opacity
                };
                for (var index = 0; index < gradient.GradientStops.Count; index++)
                {
                    var stop = gradient.GradientStops[index];
                    var stopSource = stop.Color;
                    var stopMapped = MapColor(stopSource, previous, current);
                    replacement.GradientStops.Add(new GradientStop { Color = stopMapped, Offset = stop.Offset });
                }
                ThemeChangeDiagnostics.Write($"BRUSH GRADIENT REPLACE BEFORE {propertyPath}");
                setter(replacement);
                ThemeChangeDiagnostics.Write($"BRUSH GRADIENT REPLACE AFTER {propertyPath}");
                break;
            default:
                ThemeChangeDiagnostics.Write($"BRUSH SKIP {propertyPath}");
                break;
        }
    }

    private static Color MapColor(Color source, AppTheme previous, AppTheme current)
    {
        if (source.A == 0) return source;

        var mapped = Match(source, previous.Background) ? current.Background
            : Match(source, previous.Surface) ? current.Surface
            : Match(source, previous.Card) ? current.Card
            : Match(source, previous.CardHover) ? current.CardHover
            : Match(source, previous.Accent) ? current.Accent
            : Match(source, previous.AccentSecondary) ? current.AccentSecondary
            : Match(source, previous.AccentGlow) ? current.AccentGlow
            : Match(source, previous.Success) ? current.Success
            : Match(source, previous.Warning) ? current.Warning
            : Match(source, previous.Danger) ? current.Danger
            : Match(source, previous.TextPrimary) ? current.TextPrimary
            : Match(source, previous.TextSecondary) ? current.TextSecondary
            : Match(source, previous.TextMuted) ? current.TextMuted
            : Match(source, previous.Border) ? current.Border
            : Match(source, previous.BorderHover) ? current.BorderHover
            : MapLegacyColor(source, current);

        return Color.FromArgb(source.A, mapped.R, mapped.G, mapped.B);
    }

    private static Color MapLegacyColor(Color source, AppTheme current)
    {
        if (Match(source, 0x0A, 0x0B, 0x0D)) return current.Background;
        if (Match(source, 0x16, 0x17, 0x19)) return current.Surface;
        if (Match(source, 0x2A, 0x2D, 0x31)) return current.Card;
        if (Match(source, 0x1B, 0x1D, 0x22)) return Design.C.Inset;
        if (Match(source, 0x3A, 0x3D, 0x43)) return current.CardHover;
        if (Match(source, 0x4C, 0xCB, 0xA0)) return current.Accent;
        if (Match(source, 0xE8, 0xE9, 0xEB) || Match(source, 0xE4, 0xE4, 0xE4)) return current.TextPrimary;
        if (Match(source, 0xA8, 0xAB, 0xB0) || Match(source, 0x9C, 0xA3, 0xAF)) return current.TextSecondary;
        if (Match(source, 0xE6, 0xA0, 0x30)) return current.Warning;
        if (Match(source, 0xE8, 0x4D, 0x4D)) return current.Danger;
        return source;
    }

    private static bool Match(Color left, Color right) =>
        left.R == right.R && left.G == right.G && left.B == right.B;

    private static bool Match(Color color, byte red, byte green, byte blue) =>
        color.R == red && color.G == green && color.B == blue;
}
