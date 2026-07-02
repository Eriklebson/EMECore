# EMECore.WinUI - Camada de Aplicacao

## Visao Geral

Aplicacao desktop WinUI 3 que consome os servicos do Core/Hardware e apresenta a interface grafica. Toda a UI e construida em codigo C# (sem XAML).

**Localizacao:** `src/EMECore.WinUI/`
**Target:** `net8.0-windows10.0.26100.0`
**Tipo:** `WinExe` (despackaged)

---

## Entry Point

### Program.cs (23 linhas)

Ponto de entrada da aplicacao. Inicializa o runtime COM/WinRT e inicia o loop da aplicacao WinUI.

**Fluxo:**
1. `[STAThread]` - Thread de apartamento unico (necessario para UI)
2. `RoInitialize(2)` - Inicializa COM como MTA (Multi-Threaded Apartment)
3. `Application.Start(callback)` - Inicia o loop de mensagens WinUI
4. No callback:
   - Obtem `DispatcherQueue` da thread atual
   - Cria `SynchronizationContext` para async/await
   - Instancia `new App()`

**Importante:** `RoInitialize(2)` e chamado via P/Invoke do `combase.dll`, nao via `WinRT.ComWrappers.InitializeComWrappers()` (que nao e gerado pelo CsWinRT neste contexto).

---

### App.xaml.cs (21 linhas)

Subclasse de `Microsoft.UI.Xaml.Application`.

**OnLaunched:**
1. `SteamColors.ApplyToApplication(this)` - Carrega cores/brushes no resource dictionary
2. `new MainWindow()` - Cria a janela principal
3. `m_window.Activate()` - Ativa a janela

### App.g.cs (9 linhas)

Stub vazio de `InitializeComponent()`. Necessario porque o compilador XAML nao esta sendo usado.

---

## MainWindow (`MainWindow.xaml.cs` - 276 linhas)

Janela principal da aplicacao. Constroi toda a UI programaticamente.

### Layout

```
┌─────────────────────────────────────────────────────────┐
│ Title Bar (40px)                                        │
│ [Logo] [E.M.E Core]          [Drag Region] [_][□][X]    │
├────────────┬────────────────────────────────────────────┤
│            │                                            │
│  Sidebar   │         Content Area                       │
│  (220px)   │    ┌──────────────────────┐                │
│            │    │  LibraryPage          │ (visivel)     │
│  [Biblio]  │    │  GameDetailPage       │ (collapsed)   │
│  [Add]     │    │  AddGamePage          │ (collapsed)   │
│  [Scan]    │    └──────────────────────┘                │
│            │                                            │
│ [Status]   │                                            │
│            │                                            │
├────────────┤                                            │
│ [Stats]    │                                            │
└────────────┴────────────────────────────────────────────┘
```

### Construtor - Servicos

Cria instancias dos servicos e do ViewModel:
```csharp
var databaseService = new DatabaseService();
var steamStoreService = new SteamStoreService();
var gameScannerService = new GameScannerService(steamStoreService);
ViewModel = new MainViewModel(databaseService, gameScannerService);
```

### Construtor - UI

1. **Root Grid** - 2 rows: Title Bar (40px) + Content (star)
2. **Title Bar** - Grid com 3 colunas: Logo | Drag Region | Window Buttons
3. **Content Grid** - 2 colunas: Sidebar (220px) + Page Container (star)
4. **Pages** - 3 UserControl empilhados, visibility controlada

### MainWindow_Activated

Executado quando a janela e ativada pela primeira vez (`CodeActivated`):
1. Remove handler para nao executar novamente
2. Obtem `AppWindow` via Win32Interop
3. Redimensiona para 1400x900
4. Estiliza title bar com cores Steam
5. Define `_dragRegion` como title bar customizada
6. Inicializa database via `ViewModel.InitializeAsync(dbPath)`
7. Carrega jogos na LibraryPage
8. Configura listeners de `PropertyChanged`

### Event Handlers

| Handler | Origem | Acao |
|---------|--------|------|
| `Sidebar_NavigationRequested` | Sidebar | Navega via ViewModel |
| `Sidebar_ScanRequested` | Sidebar | Executa scan de jogos |
| `LibraryPage_GameSelected` | LibraryPage | Abre detalhes do jogo |
| `LibraryPage_GameLaunchRequested` | LibraryPage | Lanca o jogo |
| `DetailPage_BackRequested` | GameDetailPage | Volta para biblioteca |
| `DetailPage_LaunchRequested` | GameDetailPage | Lanca o jogo |
| `DetailPage_DeleteRequested` | GameDetailPage | Remove o jogo |
| `AddGamePage_GameAdded` | AddGamePage | Adiciona jogo manual |
| `AddGamePage_CancelRequested` | AddGamePage | Volta para biblioteca |

### Title Bar Custom Buttons

| Botao | Glyph | Acao |
|-------|-------|------|
| Minimizar | `\uE921` | `ShowWindow(hwnd, 6)` (SW_MINIMIZE) |
| Maximizar | `\uE922` | Toggle maximize/restore via `OverlappedPresenter` |
| Fechar | `\uE8BB` | `this.Close()` |
