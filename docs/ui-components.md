# Componentes de UI - Views

## Visao Geral

Todas as views sao `UserControl` construidos 100% em codigo C#. Nao existem arquivos XAML. Cada view constrói sua UI no construtor e expoe metodos/public event para comunicacao com o pai.

---

## Sidebar (`Views/Sidebar.xaml.cs` - 124 linhas)

Sidebar de navegacao fixa no lado esquerdo.

### Layout
```
┌──────────────────┐
│ [Logo] E.M.E Core│  ← Logo area
│           v1.0.0 │
├──────────────────┤
│ [Livro] Biblioteca│  ← Nav buttons
│ [+] Adicionar    │
│ [Lupa] Procurar  │
│                  │
│ ┌──────────────┐ │
│ │ Status       │ │  ← Status card
│ │ Pronto       │ │
│ └──────────────┘ │
├──────────────────┤
│ 0 jogos          │  ← Stats footer
│ 0m jogado        │
└──────────────────┘
```

### Eventos
| Evento | Tipo | Parametro | Descricao |
|--------|------|-----------|-----------|
| `NavigationRequested` | `EventHandler<string>` | `"library"` ou `"addgame"` | Navegacao clicada |
| `ScanRequested` | `EventHandler` | - | Scan de jogos iniciado |

### Metodos Publicos
| Metodo | Descricao |
|--------|-----------|
| `UpdateStats(string stats, string playTime, string status)` | Atualiza footer e status |
| `SetScanning(bool scanning)` | Altera texto/botao durante scan |

### Referencias Internas
- `_scanBtn` - Botao de scan (para desabilitar durante scanning)
- `_scanText` - TextBlock do botao (muda para "Procurando...")
- `_statusTextBlock` - Texto de status
- `_statsText` - Contagem de jogos
- `_playTimeText` - Tempo total jogado

---

## LibraryPage (`Views/LibraryPage.xaml.cs` - 138 linhas)

Pagina principal mostrando a biblioteca de jogos.

### Layout
```
┌─────────────────────────────────────────────────┐
│ [Livro] Biblioteca (N)     [Pesquisar jogos...] │  ← Header
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐  │
│  │ Game   │ │ Game   │ │ Game   │ │ Game   │  │
│  │ Card   │ │ Card   │ │ Card   │ │ Card   │  │
│  └────────┘ └────────┘ └────────┘ └────────┘  │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Eventos
| Evento | Tipo | Parametro | Descricao |
|--------|------|-----------|-----------|
| `GameSelected` | `EventHandler<Game>` | Game selecionado | Clique no card |
| `GameLaunchRequested` | `EventHandler<Game>` | Game para lancar | Clique no "Jogar" |

### Metodos Publicos
| Metodo | Descricao |
|--------|-----------|
| `LoadGames(IList<Game> games)` | Carrega lista de jogos no grid |

### Funcionalidades
- **GridView** com `GameCard` para cada jogo
- **AutoSuggestBox** para filtrar jogos por nome
- **Empty state** quando nao ha jogos (icone + mensagem)
- Filtragem em tempo real via `TextChanged`

---

## GameCard (`Views/GameCard.xaml.cs` - 113 linhas)

Card individual de jogo para o grid da biblioteca.

### Layout
```
┌──────────────────┐
│    [Capa/Jogador] │  ← Cover area (160px)
│            [STEAM]│  ← Platform badge
├──────────────────┤
│ Nome do Jogo     │  ← Game name
│ 2h 30m           │  ← Play time
│ Jogado: 01/07/26 │  ← Last played
│                  │
│ [    Jogar     ] │  ← Play button
└──────────────────┘
```

**Dimensoes:** 200x280 pixels

### Eventos
| Evento | Tipo | Parametro | Descricao |
|--------|------|-----------|-----------|
| `GameClicked` | `EventHandler<Game>` | Game | Clique no card inteiro |
| `PlayRequested` | `EventHandler<Game>` | Game | Clique no "Jogar" |

### Metodos Publicos
| Metodo | Descricao |
|--------|-----------|
| `SetGame(Game game)` | Configura o card com dados do jogo |

### Formatacao
- Play time: `"2h 30m"` ou `"30m"` ou `"Nunca jogado"`
- Last played: `"Jogado por ultimo: dd/MM/yyyy"`
- Platform badge: texto uppercase (ex: `"STEAM"`)

---

## GameDetailPage (`Views/GameDetailPage.xaml.cs` - 165 linhas)

Pagina de detalhes de um jogo selecionado.

### Layout
```
┌─────────────────────────────────────────────────┐
│ [←] [Livro] Nome do Jogo   [Jogar] [Remover]   │  ← Header
├─────────────────────────────────────────────────┤
│                                                 │
│ ┌──────────────┐┌──────────────┐┌──────────────┐│
│ │ Plataforma   ││ Tempo Jogo   ││ Ultima Vez   ││  ← Info cards
│ │ STEAM        ││ 2h 30m       ││ 01/07/26     ││
│ └──────────────┘└──────────────┘└──────────────┘│
│                                                 │
│ ┌──────────────────────────────────────────────┐│
│ │ Caminho                                       ││  ← Path card
│ │ C:\Games\game.exe                            ││
│ └──────────────────────────────────────────────┘│
│                                                 │
│ ┌──────────────────────────────────────────────┐│
│ │ Sessoes de Jogo                               ││  ← Sessions card
│ │ Nenhuma sessao registrada                    ││
│ └──────────────────────────────────────────────┘│
│                                                 │
└─────────────────────────────────────────────────┘
```

### Eventos
| Evento | Tipo | Parametro | Descricao |
|--------|------|-----------|-----------|
| `BackRequested` | `EventHandler` | - | Botao voltar clicado |
| `LaunchRequested` | `EventHandler<Game>` | Game | Jogar clicado |
| `DeleteRequested` | `EventHandler<Game>` | Game | Remover clicado |

### Metodos Publicos
| Metodo | Descricao |
|--------|-----------|
| `LoadGame(Game game)` | Preenche detalhes do jogo |

---

## AddGamePage (`Views/AddGamePage.xaml.cs` - 180 linhas)

Formulario para adicionar jogo manualmente.

### Layout
```
┌─────────────────────────────────────────────────┐
│ [←] [+] Adicionar Jogo                          │  ← Header
├─────────────────────────────────────────────────┤
│                                                 │
│ Nome do Jogo                                    │
│ ┌────────────────────────────────────────────┐  │
│ │ Ex: Stellar Blade                          │  │
│ └────────────────────────────────────────────┘  │
│                                                 │
│ Caminho do Executavel                           │
│ ┌──────────────────────────┐ ┌──────────┐      │
│ │ C:\Games\game.exe        │ │ Procurar │      │
│ └──────────────────────────┘ └──────────┘      │
│                                                 │
│ Plataforma                                      │
│ ┌────────────────┐                              │
│ │ other      ▼   │                              │
│ └────────────────┘                              │
│                                                 │
│ [Adicionar Jogo]  [Cancelar]                    │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Eventos
| Evento | Tipo | Descricao |
|--------|------|-----------|
| `GameAdded` | `EventHandler` | Formulario submetido |
| `CancelRequested` | `EventHandler` | Cancelar clicado |

### Metodos Publicos
| Metodo | Retorno | Descricao |
|--------|---------|-----------|
| `GetFormData()` | `(string name, string exePath, string platform)` | Obtem dados do form |
| `ClearForm()` | `void` | Limpa todos os campos |

### File Picker
- Usa `Windows.Storage.Pickers.FileOpenPicker`
- Filtra por `*.exe`
- Obtem HWND via `WindowNative.GetWindowHandle(((App)App.Current).m_window!)`
- Preenche automaticamente o nome do jogo se estiver vazio

### Opcoes de Plataforma
- `other` (padrao)
- `steam`
- `epic`
- `gog`
- `xbox`

---

## Converters (`Converters/Converters.cs` - 74 linhas)

Conversores de valor (restam de implementacao XAML original, nao estao em uso na UI em codigo).

| Conversor | Funcao |
|-----------|--------|
| `PageVisibilityConverter` | String page → Visibility |
| `InvertedBoolToVisibilityConverter` | bool → Visibility invertida |
| `BoolToVisibilityConverter` | bool → Visibility |
| `PlayTimeConverter` | int minutos → `"Xh Ym"` |
| `PlatformIconConverter` | string platform → Glyph Unicode |
