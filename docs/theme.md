# Sistema de Tema Steam

## Visao Geral

Definicoes de cores e brushes inspirados no Steam. Centralizado em `Theme/SteamColors.cs` (77 linhas). Usado por todos os componentes de UI.

**Localizacao:** `src/EMECore.WinUI/Theme/SteamColors.cs`

---

## Paleta de Cores

### Cores Base

| Nome | Hex | Uso |
|------|-----|-----|
| `Dark` | `#1b2838` | Background principal, botoes de title bar |
| `Darker` | `#171a21` | Background da sidebar |
| `Darkest` | `#0e1621` | Title bar, headers de pagina, footer |
| `Card` | `#1e2a3a` | Background de cards e info cards |
| `CardHover` | `#2a475e` | Hover de cards, botoes secundarios |
| `Hover` | `#3d6c8e` | Hover geral |

### Cores de Destaque

| Nome | Hex | Uso |
|------|-----|-----|
| `Blue` | `#66c0f4` | Botoes primarios, icones, links |
| `Green` | `#a4d007` | Botao "Jogar", tempo de jogo |
| `Orange` | `#cf6a32` | Alertas, avisos |
| `Red` | `#d94126` | Botao "Remover", erros |

### Cores de Texto

| Nome | Hex | Uso |
|------|-----|-----|
| `Text` | `#c7d5e0` | Texto principal |
| `TextSecondary` | `#8b9bb4` | Texto secundario, labels, contadores |
| `Light` | `#ffffff` | Texto em backgrounds escuros |

---

## Uso no Codigo

### Constantes Estaticas (cores e brushes)

Cada cor esta disponivel como:
1. `Windows.UI.Color` - para `SolidColorBrush(color)`
2. `SolidColorBrush` - pronto para uso direto

```csharp
// Cores
SteamColors.Dark           // Windows.UI.Color
SteamColors.Blue           // Windows.UI.Color

// Brushes
SteamColors.DarkBrush      // SolidColorBrush
SteamColors.BlueBrush      // SolidColorBrush
```

### Application Resources

`ApplyToApplication()` injeta todas as cores e brushes no resource dictionary da aplicacao:

```csharp
// Cores como resources
app.Resources["SteamDarkColor"] = Dark;
app.Resources["SteamBlueColor"] = Blue;

// Brushes como resources
app.Resources["SteamDarkBrush"] = DarkBrush;
app.Resources["SteamBlueBrush"] = BlueBrush;
```

**Nota:** Atualmente os resources nao estao sendo usados diretamente (as views acessam `SteamColors.BlueBrush` diretamente). Os resources estao disponiveis para uso futuro em XAML.

---

## Uso nos Componentes

| Componente | Cores Utilizadas |
|------------|-----------------|
| **Sidebar** | `DarkerBrush` (bg), `DarkestBrush` (footer), `CardBrush` (status) |
| **Title Bar** | `DarkestBrush` (bg), `BlueBrush` (logo), `TextBrush` (text) |
| **LibraryPage** | `BlueBrush` (icon), `CardBrush` (search bg), `CardHoverBrush` (border) |
| **GameCard** | `CardHoverBrush` (cover bg), `BlueBrush` (play btn), `TextSecondaryBrush` |
| **GameDetailPage** | `DarkestBrush` (header), `CardBrush` (cards), `GreenBrush` (play), `RedBrush` (delete) |
| **AddGamePage** | `DarkestBrush` (header), `CardBrush` (inputs), `BlueBrush` (add btn) |

---

## Helper

### ColorFromHex(string hex)

Converte string hex para `Windows.UI.Color`:
```csharp
private static Windows.UI.Color ColorFromHex(string hex)
{
    hex = hex.TrimStart('#');
    return Windows.UI.Color.FromArgb(255,
        byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
        byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
        byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
}
```

### ParseColor (MainWindow)

Funcao identica duplicada em `MainWindow` para uso com title bar:
```csharp
private static Windows.UI.Color ParseColor(string hex) { ... }
```

**Nota:** Existe duplicacao de `ColorFromHex`/`ParseColor`. Pode ser consolidado em `SteamColors`.
