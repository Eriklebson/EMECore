# 2. Arquitetura do desktop (`EMECore`)

## Estrutura de solução

```text
EMECore/
├─ EMECore.sln
├─ config/
│  ├─ hardware-mapping.json
│  └─ cpu-sensors-mapping.json
└─ src/
   ├─ EMECore.Core/       # domínio: modelos, contratos e helpers
   ├─ EMECore.Hardware/   # infraestrutura: SQLite, scanner, sensores e rede
   └─ EMECore.WinUI/      # apresentação: janela, Views, ViewModel e tema
```

Dependências permitidas:

```text
EMECore.WinUI ─► EMECore.Core
       │
       └────────► EMECore.Hardware ─► EMECore.Core
```

`Core` não depende de UI ou infraestrutura. `Hardware` implementa contratos de `Core`. `WinUI` consome as duas camadas e deve apenas orquestrar e apresentar dados.

## Componentes principais

| Camada | Local | Conteúdo |
|---|---|---|
| Domínio | `EMECore.Core/Models` | `Game`, `Achievement`, `HardwareStats`, `PlaySession`, `ScannedGame` e modelos auxiliares. |
| Contratos | `EMECore.Core/Services` | Interfaces de banco, scanner, Steam, discovery e providers de conquistas. |
| Infraestrutura | `EMECore.Hardware/Services` | Implementações concretas, integrações externas, parsers e servidor mobile. |
| UI | `EMECore.WinUI/Views` | Biblioteca, detalhes, configurações, loja, sidebar e janelas auxiliares. |
| Estado de UI | `EMECore.WinUI/ViewModels/MainViewModel.cs` | Navegação, lista de jogos, scanner e operações principais. |
| Tema | `EMECore.WinUI/Theme` | `ThemeManager`, `SteamColors`, `Design` e estilos. |

Pacotes relevantes: `Microsoft.WindowsAppSDK`, `CommunityToolkit.Mvvm`, `LiveChartsCore`, `LibreHardwareMonitorLib`, `Microsoft.Data.Sqlite`, `Fleck`, `NAudio` e `System.Management`.

## Inicialização e encerramento

Arquivos-chave: `App.xaml.cs`, `MainWindow.xaml.cs` e `MainViewModel.cs`.

1. O app instancia `MainWindow`.
2. A janela monta barra de título, sidebar e instâncias das páginas; depois carrega `SettingsService` e o tema salvo.
3. No primeiro evento `Activated`, restaura posição, ajusta a janela e chama `ViewModel.InitializeAsync(dbPath)`.
4. A ViewModel inicializa o SQLite e carrega a biblioteca.
5. `StartMobileServer()` inicia `MobileServerService` se `mobile_server_enabled` for `True`; porta padrão `8181` e configurável por `mobile_server_port`.
6. No fechamento, a janela persiste posição, encerra servidor mobile, monitor de saves e janelas, fecha o banco e tenta remover arquivos SQLite residuais `-wal` e `-shm`.

## Navegação

`MainViewModel.CurrentPage` define visibilidade das páginas:

| Valor | Página |
|---|---|
| `library` | Biblioteca e filtros por categoria. |
| `detail` | Dados do jogo, requisitos e conquistas. |
| `addgame` | Inclusão manual. |
| `achievements` | Visão de conquistas. |
| `settings` | Preferências e tema. |
| `store` / `store_detail` | Loja e detalhe de oferta. |

`MonitorWindow` é uma janela separada, aberta pela sidebar. Ao criar uma página, conecte eventos à ViewModel/serviços e não a regras de persistência na View.

## Tema e preferências

Use `ThemeManager` e `SteamColors`; não crie paletas paralelas em cada View. O design é escuro e inspirado no Steam. `SettingsService` persiste, entre outros, tema, posição da janela, categoria ativa e estado colapsado da sidebar.
