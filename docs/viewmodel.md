# MainViewModel - Logica MVVM

## Visao Geral

ViewModel principal usando `CommunityToolkit.Mvvm`. Gerencia estado da aplicacao, navegacao, operacoes CRUD e scanning de jogos.

**Localizacao:** `src/EMECore.WinUI/ViewModels/MainViewModel.cs` (179 linhas)

---

## Propriedades Observaveis

| Propriedade | Tipo | Default | Descricao |
|------------|------|---------|-----------|
| `CurrentPage` | `string` | `"library"` | Pagina ativa (`"library"`, `"detail"`, `"addgame"`) |
| `SelectedGame` | `Game?` | `null` | Jogo selecionado para detalhes |
| `IsSidebarVisible` | `bool` | `true` | Visibilidade da sidebar |
| `StatusText` | `string` | `"Pronto"` | Texto de status na sidebar |
| `TotalGames` | `int` | `0` | Contagem total de jogos |
| `TotalPlayTime` | `string` | `"0m"` | Tempo total formatado |
| `IsScanning` | `bool` | `false` | Se um scan esta em andamento |

### Colecao

| Propriedade | Tipo | Descricao |
|------------|------|-----------|
| `Games` | `ObservableCollection<Game>` | Lista de jogos da biblioteca |

---

## Comandos

### NavigateTo(string page)
- **Trigger:** Sidebar (Biblioteca, Adicionar Jogo)
- **Acao:** `CurrentPage = page`

### SelectGame(Game? game)
- **Trigger:** Clique no GameCard
- **Acao:** `SelectedGame = game; CurrentPage = "detail"`

### GoBack()
- **Trigger:** Botao voltar na GameDetailPage
- **Acao:** `SelectedGame = null; CurrentPage = "library"`

### AddGame()
- **Trigger:** Botao "Adicionar Jogo" na sidebar
- **Acao:** `CurrentPage = "addgame"`

### ScanGamesAsync()
- **Trigger:** Botao "Procurar Jogos" na sidebar
- **Fluxo:**
  1. `IsScanning = true` (desabilita botao)
  2. `StatusText = "Procurando jogos..."`
  3. Chama `_gameScannerService.ScanAllGamesAsync()`
  4. Para cada jogo escaneado:
     - Verifica duplicata por `ExecutablePath`
     - Cria `Game` com GUID
     - Salva no banco via `UpsertGameAsync`
     - Adiciona na colecao
  5. Atualiza `TotalGames` e `StatusText`
  6. `IsScanning = false` (reabilita botao)

### DeleteGameAsync(Game? game)
- **Trigger:** Botao "Remover" na GameDetailPage
- **Fluxo:**
  1. Deleta do banco via `DeleteGameAsync`
  2. Remove da colecao `Games`
  3. Atualiza `TotalGames`
  4. Se era o jogo selecionado, volta para biblioteca
  5. `StatusText = "Nome removido"`

### LaunchGameAsync(Game? game)
- **Trigger:** Botao "Jogar" (GameCard ou GameDetailPage)
- **Fluxo:**
  1. `StatusText = "Abrindo Nome..."`
  2. `Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true })`
  3. Atualiza `LastPlayed` e salva no banco
  4. `StatusText = "Nome iniciado"`
  5. Em erro: `StatusText = "Erro ao abrir: msg"`

### AddGameManualAsync(string name, string exePath, string platform)
- **Trigger:** Submit do AddGamePage
- **Fluxo:**
  1. Cria `Game` com GUID
  2. Salva no banco
  3. Adiciona na colecao
  4. `CurrentPage = "library"`
  5. `StatusText = "Nome adicionado"`

---

## Inicializacao

### InitializeAsync(string dbPath)
1. `_databaseService.InitializeAsync(dbPath)` - Abre/cria banco
2. `LoadGamesAsync()` - Carrega jogos

### LoadGamesAsync()
1. Busca todos os jogos do banco
2. Limpa e repopula `Games`
3. Atualiza `TotalGames`
4. Calcula `TotalPlayTime` via `GetTotalPlayTimeAsync()`
5. `StatusText = "N jogos carregados"`

---

## Fluxo de Dados

```
DatabaseService ←→ SQLite DB
       ↑
MainViewModel
       ↑
MainWindow (via property binding e eventos)
       ↑
Views (Sidebar, LibraryPage, GameDetailPage, AddGamePage)
```

### Atualizacao de Status

```
ViewModel.StatusText (set)
  ↓
PropertyChanged dispara
  ↓
MainWindow.ViewModel_PropertyChanged
  ↓
Sidebar.UpdateStats(...) via DispatcherQueue
```

### Navegacao

```
Sidebar.NavigationRequested("library")
  ↓
ViewModel.NavigateToCommand.Execute("library")
  ↓
CurrentPage = "library"
  ↓
PropertyChanged dispara
  ↓
MainWindow.UpdatePageVisibility()
  ↓
LibraryPage.Visibility = Visible
GameDetailPage.Visibility = Collapsed
AddGamePage.Visibility = Collapsed
```
