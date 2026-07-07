using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class AddGamePage : UserControl
{
    public event EventHandler? GameAdded;
    public event EventHandler? CancelRequested;

    private readonly TextBox _nameBox;
    private readonly TextBox _exePathBox;
    private readonly ComboBox _platformCombo;

    public AddGamePage()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var header = new Grid { Padding = new Thickness(24, 16, 24, 12), Background = SteamColors.DarkestBrush };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var backButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE72B", FontSize = 14 },
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent),
            Foreground = SteamColors.TextBrush,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4)
        };
        backButton.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        header.Children.Add(backButton);

        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(12, 0, 0, 0) };
        titlePanel.Children.Add(new FontIcon { Glyph = "\uE710", FontSize = 24, Foreground = SteamColors.BlueBrush });
        titlePanel.Children.Add(new TextBlock { Text = "Adicionar Jogo", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = SteamColors.TextBrush, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(titlePanel, 1);
        header.Children.Add(titlePanel);

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Form
        var scrollViewer = new ScrollViewer { Padding = new Thickness(24) };
        var formStack = new StackPanel { MaxWidth = 500, Spacing = 20 };

        // Name field
        _nameBox = new TextBox
        {
            PlaceholderText = "Ex: Stellar Blade",
            Background = SteamColors.CardBrush,
            Foreground = SteamColors.TextBrush,
            BorderBrush = SteamColors.CardHoverBrush
        };
        formStack.Children.Add(CreateFormField("Nome do Jogo", _nameBox));

        // Exe path field
        _exePathBox = new TextBox
        {
            PlaceholderText = @"C:\Games\game.exe",
            Background = SteamColors.CardBrush,
            Foreground = SteamColors.TextBrush,
            BorderBrush = SteamColors.CardHoverBrush
        };
        var pathGrid = new Grid { ColumnSpacing = 8 };
        pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pathGrid.Children.Add(_exePathBox);
        var browseBtn = new Button
        {
            Content = "Procurar",
            Background = SteamColors.CardHoverBrush,
            Foreground = SteamColors.TextBrush,
            Padding = new Thickness(12, 0, 12, 0)
        };
        browseBtn.Click += BrowseExe_Click;
        Grid.SetColumn(browseBtn, 1);
        pathGrid.Children.Add(browseBtn);
        formStack.Children.Add(CreateFormField("Caminho do Executável", pathGrid));

        // Platform field
        _platformCombo = new ComboBox
        {
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = SteamColors.CardBrush,
            Foreground = SteamColors.TextBrush
        };
        _platformCombo.Items.Add(new ComboBoxItem { Content = "other", IsSelected = true });
        _platformCombo.Items.Add(new ComboBoxItem { Content = "steam" });
        _platformCombo.Items.Add(new ComboBoxItem { Content = "epic" });
        _platformCombo.Items.Add(new ComboBoxItem { Content = "gog" });
        _platformCombo.Items.Add(new ComboBoxItem { Content = "xbox" });
        formStack.Children.Add(CreateFormField("Plataforma", _platformCombo));

        // Buttons
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 16, 0, 0) };
        var addBtn = new Button
        {
            Content = "Adicionar Jogo",
            Background = SteamColors.BlueBrush,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Padding = new Thickness(24, 10, 24, 10),
            CornerRadius = new CornerRadius(4),
            FontSize = 14
        };
        addBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_nameBox.Text) && !string.IsNullOrWhiteSpace(_exePathBox.Text))
                GameAdded?.Invoke(this, EventArgs.Empty);
        };
        buttonPanel.Children.Add(addBtn);

        var cancelBtn = new Button
        {
            Content = "Cancelar",
            Background = SteamColors.CardHoverBrush,
            Foreground = SteamColors.TextBrush,
            Padding = new Thickness(16, 10, 16, 10),
            CornerRadius = new CornerRadius(4)
        };
        cancelBtn.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        buttonPanel.Children.Add(cancelBtn);
        formStack.Children.Add(buttonPanel);

        scrollViewer.Content = formStack;
        Grid.SetRow(scrollViewer, 1);
        root.Children.Add(scrollViewer);

        Content = root;
    }

    private static StackPanel CreateFormField(string label, UIElement input)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock { Text = label, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush });
        stack.Children.Add(input);
        return stack;
    }

    private async void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(((App)App.Current).m_window!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _exePathBox.Text = file.Path;
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
                _nameBox.Text = System.IO.Path.GetFileNameWithoutExtension(file.Name);
        }
    }

    public (string name, string exePath, string platform) GetFormData()
    {
        var platform = (_platformCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "other";
        return (_nameBox.Text.Trim(), _exePathBox.Text.Trim(), platform);
    }

    public void ClearForm()
    {
        _nameBox.Text = "";
        _exePathBox.Text = "";
        _platformCombo.SelectedIndex = 0;
    }
}
