# Arquitetura do Sistema

## Estrutura de Pastas

```
C:\laragon\www\LancherGamesV2\EMECore\
├── Directory.Build.props          # UICulture: en-US, NeutralLanguage: en-US
├── build-en.ps1                   # Script PowerShell que forca cultura en-US no build
├── fix_sdk.cmd                    # Copia Platform.xml entre versoes do SDK Windows
├── global.json                    # Fixa .NET SDK 8.0.422 com rollForward: latestPatch
├── EMECore.sln                    # Solucao VS2022 com 3 projetos
│
├── docs/                          # Esta documentacao
│
└── src/
    ├── EMECore.Core/              # Camada de dominio (models + interfaces)
    │   ├── Helpers/
    │   │   └── FormatHelpers.cs
    │   ├── Models/
    │   │   ├── Game.cs
    │   │   ├── Achievement.cs
    │   │   ├── PlaySession.cs
    │   │   ├── ScannedGame.cs
    │   │   └── SteamStoreInfo.cs
    │   └── Services/
    │       ├── IDatabaseService.cs
    │       ├── IGameScannerService.cs
    │       └── ISteamStoreService.cs
    │
    ├── EMECore.Hardware/          # Camada de infraestrutura (implementacoes)
    │   └── Services/
    │       ├── DatabaseService.cs
    │       ├── GameScannerService.cs
    │       ├── SteamStoreService.cs
    │       ├── AchievementService.cs      # STUB
    │       ├── FpsMonitorService.cs       # STUB
    │       ├── HardwareMonitorService.cs  # STUB
    │       ├── SensorService.cs           # STUB
    │       └── StellarBladeParser.cs      # STUB
    │
    └── EMECore.WinUI/             # Aplicacao desktop WinUI 3
        ├── Program.cs             # Entry point (RoInitialize + Application.Start)
        ├── App.xaml.cs            # Aplicacao WinUI (OnLaunched)
        ├── App.g.cs               # Stub InitializeComponent (sem XAML)
        ├── MainWindow.xaml.cs     # Janela principal (UI toda em codigo C#)
        ├── Directory.Build.targets # Vazio (overrides removidos)
        ├── app.manifest           # DPI awareness, Windows 10 compat
        ├── Assets/                # Icones e splash screen
        ├── Converters/
        │   └── Converters.cs
        ├── Theme/
        │   └── SteamColors.cs
        ├── ViewModels/
        │   └── MainViewModel.cs
        └── Views/
            ├── Sidebar.xaml.cs
            ├── LibraryPage.xaml.cs
            ├── GameCard.xaml.cs
            ├── GameDetailPage.xaml.cs
            └── AddGamePage.xaml.cs
```

## Diagrama de Dependencias

```
┌─────────────────────────────────────────┐
│           EMECore.WinUI                 │
│  (WinExe, net8.0-windows10.0.26100.0)  │
│                                         │
│  Program.cs ──► App.xaml.cs             │
│                    └──► MainWindow      │
│                           ├── Views/    │
│                           ├── VMs/      │
│                           └── Theme/    │
│                                         │
│  Refs: EMECore.Core                     │
│        EMECore.Hardware                 │
├─────────────┬───────────────────────────┤
│             │                           │
▼             ▼                           │
┌────────────────────┐  ┌─────────────────┘
│   EMECore.Core     │◄─┤ EMECore.Hardware
│   (net8.0)         │  │   (net8.0)
│                    │  │
│ Models:            │  │ Services:
│  Game              │  │  DatabaseService
│  Achievement       │  │  GameScannerService
│  PlaySession       │  │  SteamStoreService
│  ScannedGame       │  │  (stubs...)
│  SteamStoreInfo    │  │
│                    │  │ Pkgs:
│ Interfaces:        │  │  Microsoft.Data.Sqlite
│  IDatabaseService  │  │  System.Text.Json
│  ISteamStoreSvc    │  │  System.Management
│  IGameScannerSvc   │  │
│                    │  │
│ Helpers:           │  │
│  FormatHelpers     │  │
│                    │  │
│ Pkgs:              │  │
│  CommunityToolkit  │  │
│  .Mvvm             │  │
└────────────────────┘  └─────────────────┘
```

## Padroes Arquiteturais

### Dependency Inversion
- **Core** define interfaces (`IDatabaseService`, `ISteamStoreService`, `IGameScannerService`)
- **Hardware** implementa as interfaces concretas
- **WinUI** consome interfaces via construtor do `MainViewModel`

### Clean Architecture
- Core nao tem conhecimento de Hardware
- Hardware referencia Core (nao o inverso)
- WinUI referencia ambos

### MVVM
- `MainViewModel` usa `CommunityToolkit.Mvvm` com `[ObservableProperty]` e `[RelayCommand]`
- Views se comunicam com ViewModel via eventos e bindings programaticos
- Nao ha XAML binding (tudo e feito em codigo C#)

### UI Sem XAML
- Todos os componentes de UI sao `UserControl` construidos 100% em codigo C#
- Nenhum arquivo `.xaml` existe no projeto (todos foram removidos)
- `App.g.cs` fornece um stub vazio de `InitializeComponent()`
- O compilador XAML do WindowsAppSDK (net472) e incompativel com .NET 10 SDK

## Fluxo de Inicializacao

```
1. Program.Main()
   ├── RoInitialize(2)                    # Inicializa COM/WinRT
   └── Application.Start(callback)
       └── new App()
           └── OnLaunched()
               ├── SteamColors.ApplyToApplication(this)  # Carrega resources
               ├── new MainWindow()
               │   ├── Cria services (DatabaseService, SteamStoreService, GameScannerService)
               │   ├── Cria MainViewModel
               │   ├── Constroi UI (title bar, sidebar, content area, pages)
               │   └── Registra event handlers
               └── window.Activate()
                   └── MainWindow_Activated (CodeActivated)
                       ├── Resize para 1400x900
                       ├── Estiliza title bar (cores Steam)
                       ├── SetTitleBar(_dragRegion)
                       ├── ViewModel.InitializeAsync(dbPath)  # Abre SQLite, carrega jogos
                       ├── _libraryPage.LoadGames(...)
                       └── _sidebar.UpdateStats(...)
```

## Navegacao entre Paginas

A navegacao e controlada por `MainViewModel.CurrentPage` (string):
- `"library"` → `LibraryPage` visivel
- `"detail"` → `GameDetailPage` visivel
- `"addgame"` → `AddGamePage` visivel

`MainWindow.ViewModel_PropertyChanged` observa mudancas em `CurrentPage` e ajusta `Visibility` das pages.
