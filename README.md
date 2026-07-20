<div align="center">

# E.M.E Core

**Game Launcher nativo para Windows — organize, descubra e jogue seus jogos favoritos.**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4?logo=windows)](https://learn.microsoft.com/en-us/windows/apps/winui/)
[![SQLite](https://img.shields.io/badge/SQLite-local-003B57?logo=sqlite)](https://www.sqlite.org/)
[![License](https://img.shields.io/badge/Licenca-MIT-green)](LICENSE)

</div>

---

## Funcionalidades

| Recurso | Descricao | Status |
|---------|-----------|--------|
| **Scanner Automatico** | Detecta jogos Steam, Xbox/Game Pass e pastas comuns (`C:\Games`, etc.) | ✅ |
| **Biblioteca Visual** | Grid com capas reais, busca por nome e filtros | ✅ |
| **Capas Automaticas** | Steam Store API para jogos Steam + fallback de imagens locais para Xbox | ✅ |
| **Badge de Plataforma** | Identifica Steam, Xbox e Outros com cores especificas | ✅ |
| **Detalhe do Jogo** | Informacoes completas: plataforma, tempo, ultimo acesso, caminho | ✅ |
| **Conquistas** | Parser de conquistas do Stellar Blade, cards estilo Steam, notificacao com som | ✅ |
| **Monitor de Hardware** | CPU, GPU, RAM, temperaturas, fans, FPS em tempo real | ✅ |
| **Filtro Inteligente** | Ignora automaticamente uninstallers, redistributiveis, crash handlers e outros nao-jogos | ✅ |
| **100% Offline** | Banco SQLite local, sem dependencia de nuvem | ✅ |
| **Nativo Windows** | Aplicacao leve em C# WinUI 3, baixo consumo de RAM/CPU | ✅ |
| **Servidor Mobile** | WebSocket para controle remoto via app Flutter | ✅ |

---

## Instalacao

### Pre-requisitos
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.422)
- Windows 10 (build 17763+) ou Windows 11
- Visual Studio 2022 (opcional)

### Build

```bash
# 1. Clone o repositorio
git clone https://github.com/Eriklebson/EMECore.git
cd EMECore

# 2. Build
dotnet build EMECore.sln -c Debug -p:Platform=x64

# 3. Executar
src/EMECore.WinUI/bin/x64/Debug/net8.0-windows10.0.26100.0/EMECore.WinUI.exe
```

---

## Estrutura do Projeto

```
EMECore/
├── src/
│   ├── EMECore.Core/           # Modelos e interfaces (Camada de Dominio)
│   │   ├── Models/             # Game, ScannedGame, Achievement, PlaySession
│   │   ├── Services/           # IDatabaseService, IGameScannerService, ISteamStoreService
│   │   └── Helpers/            # FormatHelpers
│   ├── EMECore.Hardware/       # Implementacoes (Camada de Infraestrutura)
│   │   └── Services/           # DatabaseService, GameScannerService, SteamStoreService
│   └── EMECore.WinUI/          # Aplicacao Desktop (Camada de Apresentacao)
│       ├── ViewModels/         # MainViewModel (MVVM)
│       ├── Views/              # LibraryPage, GameCard, GameDetailPage, AddGamePage, Sidebar
│       ├── Theme/              # SteamColors (tema escuro)
│       └── MainWindow.xaml.cs  # Janela principal
└── docs/                       # Documentacao tecnica
```

---

## Banco de Dados

SQLite local com WAL mode ativado.

### Tabelas

- **games** — Jogos da biblioteca (nome, plataforma, capa, SteamAppId, tempo de jogo)
- **achievements** — Conquistas salvas por jogo
- **play_sessions** — Historico de sessoes de jogo

Localizacao: `%LocalAppData%\EMECore\eme_core.db`

---

## Scanner de Jogos

| Plataforma | Metodo de Deteccao |
|------------|-------------------|
| **Steam** | `libraryfolders.vdf` + `appmanifest_*.acf` |
| **Xbox / Game Pass** | Scan de `C:\XboxGames\` |
| **Pastas Comuns** | `C:\Games`, `D:\Games`, `E:\Games` |

Capas buscadas via **Steam Store API** (gratuita, sem autenticacao). Para jogos nao-Steam, busca na Steam Store Search pelo nome. Fallback para imagens locais (SplashScreen, Logo) em jogos Xbox.

---

## Arquitetura

Clean Architecture com 3 camadas + MVVM + SOLID.

```
EMECore.WinUI (WinExe)
  ├── EMECore.Core (Class Library)
  └── EMECore.Hardware (Class Library)
        └── EMECore.Core
```

UI 100% em C# (sem XAML devido a incompatibilidade com .NET 10 SDK).

---

## App Mobile (Controle Remoto)

Servidor WebSocket integrado no desktop para comunicacao com app Flutter.

| Configuracao | Valor Padrao |
|-------------|--------------|
| Porta | `8181` |
| Habilitado | `True` |
| Settings | `mobile_server_enabled`, `mobile_server_port` |

**Comandos suportados:** `get_hardware`, `get_games`, `launch_game`, `get_achievements`, `ping`

O app Flutter (EMECoreMobile) sera desenvolvido separadamente e se conecta ao IP do PC na porta 8181.

---

## Versao

| Versao | Data | Mudanca |
|--------|------|---------|
| 2.22.1.0 | 20/07/2026 | Fix jogos no mobile: IndexedStack preserva widgets na troca de aba, HandleGetGames envia lista imediatamente (capas em background), cache de game_list, loading indicator no GamesPage |
| 2.22.0.0 | 18/07/2026 | Correcao hardware monitor para mobile: instancia persistente de HardwareMonitorService, cache WMI para RAM/rede/disco, correcao de divisao dupla por 1024 em RAM |
| 2.21.0.0 | 18/07/2026 | HTTP Image Server para capas de jogos (porta 8183), cache local de capas no PC, coleta automatica de conquistas via mobile, normalizacao de nomes para busca de capas, fallback Steam Store page scraping, deteccao de placeholders do Twitch |
| 2.20.0.0 | 17/07/2026 | Servidor WebSocket para app mobile (controle remoto) — Fleck v1.2.0, porta 8181, broadcast hardware 1s, comandos get_games/launch_game/get_achievements, start/stop automatico com o app |
| 2.19.2.0 | 17/07/2026 | Pagina promocional do E.M.E Core na sidebar (substitui placeholder AdSense) - layout com logo, stats, feature cards e botao de download; carregamento via file URI para URLs relativas funcionarem |
| 2.19.1.0 | 17/07/2026 | Melhoria no instalador - verificacao de dependencias (.NET 8 Desktop Runtime + WebView2 Runtime) antes da instalacao com opcao de download automatico |
| 2.19.0.0 | 17/07/2026 | AdSense na sidebar (WebView2 + HTML responsivo) — banner 160x300 entre navegacao e Adicionar Jogo, encolhe junto com sidebar (56x100 quando recolhida), placeholder HTML com JS responsivo via resize observer |
| 2.18.3.0 | 16/07/2026 | Fix layout dos detalhes do monitor (2 colunas) - reescrita do sistema de grid responsivo com AddDetailRow/AddSectionLabel; remocao do contador estatico _detailRow; deteccao de full-span para evitar sobreposicao de section labels |
| 2.18.2.0 | 14/07/2026 | Fix crash MSIX Store (0xc000027b) - deteccao de contexto MSIX e bypass de elevacao UAC; fix popup de conquistas nao aparecia (GC da AchievementNotificationWindow); logo real na sidebar ao inves de texto; remocao da aba Conquistas da sidebar |
| 2.18.0.0 | 12/07/2026 | FPS overlay transparente (Win32 P/Invoke), stats 1% low/0.1% low/frame time, contorno preto para visibilidade, submenu de toggles no card FPS (controla overlay), correcao de crash GC delegate (WndProc estatico), correcao de RegisterClass duplicado |
| 2.17.0.0 | 10/07/2026 | Gamepad: mini curvas de trigger no header (LT/RT com dot animado), curvas removidas do canvas; card gamepad opcional (visivel/oculto), layout responsivo 2col/1col (<700px), janela salva posicao X/Y, gamepad compacto em tela estreita, armazenamento persistente de conquistas (SQLite), save parsers: God of War 2018, Days Gone, Cyberpunk 2077 (JSON), Hogwarts Legacy, Witcher 3 |
| 2.16.0.0 | 09/07/2026 | Imagens de conquistas via Steam API (GetSchemaForGame), SettingsService para persistencia de preferencias, sidebar salva/restaura estado (categoria + colapsada), monitor sidebar redesenhada (estilo principal, collapse persistente), cards do monitor salvam estado de colapso, parser Stellar Blade usa nomes Steam, popup de notificaao restaurado |
| 2.15.1.0 | 08/07/2026 | Perifericos: calibracao de gamepad funcional (arrastar circulos), dots analogicos na imagem, curvas de trigger com bezier 90graus, polling rate preciso (diff dwPacketNumber), auto-deteccao de novos controles, timer de alta precisao 1ms, correcao de flicker e reset de posicoes |
| 2.15.0.0 | 08/07/2026 | Cards colapsaveis (minimize/expand), toggle FPS com auto-deteccao, fallback WMI para rede com cache, timer de background (System.Threading.Timer) para scroll fluido, correcao de sobreposicao de voltage nos headers |
| 2.14.3.0 | 08/07/2026 | Remocao do macro de pesca (FishingMacroService, FishingMacroWindow), limpeza de codigo, timer de processo removido |
| 2.14.2.0 | 07/07/2026 | Correcao de persistencia de jogos no banco: journal_mode=DELETE, Environment.Exit(0), indices de colunas corrigidos, SHM/WAL limpos no fechamento, Closed handler movido pro construtor |
| 2.14.1.0 | 07/07/2026 | Scroll da biblioteca (StackPanel→Grid), correcao do botao Procurar Jogos (ScanGamesAsync), requisitos do sistema formatados, WAL checkpoint |
| 2.14.0.0 | 07/07/2026 | Sidebar colapsavel com toggle <, limpeza do layout (removido badge e status card), correcao de titulo Ferramentas/Treinamento |
| 2.13.0.0 | 07/07/2026 | Reorganizacao da navegacao: abas Jogos/Ferramentas/Treinamento, filtragem por categoria, remocao de ToolPatterns, Adicionar Jogo no footer |
| 2.12.0.0 | 07/07/2026 | Monitoramento em tempo real de conquistas, macro de pesca auto-detecta jogo, capas via Twitch CDN, correcao de game detection, multi-drive scanner, scanner Riot/Ubisoft/EA/Battle.net/Rockstar/Bethesda/Amazon |
| 2.11.0.0 | 04/07/2026 | Sistema de mapeamento JSON para ventoinhas, correcao de tensao AMD, correcao de rede WMI |
| 2.10.0.0 | 03/07/2026 | Funcionalidade: graficos de rede, tensao/wattage, GPU via nvidia-smi, monitoramento de disco e rede |
| 2.9.0.0 | 03/07/2026 | Funcionalidade: monitoramento de disco e rede, itens nao utilizados removidos da sidebar |
| 2.8.2.0 | 03/07/2026 | Correcao: fallback de uso de RAM, encoding PowerShell, script admin para temperaturas/ventoinhas |
| 2.8.1.0 | 03/07/2026 | Correcao: fallback WMI via PowerShell para deteccao de hardware |
| 2.8.0.0 | 03/07/2026 | Funcionalidade: card de placa-mae, ventoinhas por componente, informacoes de modulos de RAM |
| 2.7.1.0 | 03/07/2026 | Interface: redesign do monitor de hardware com sidebar, graficos, tema cyber dark |
| 2.7.0.0 | 03/07/2026 | Funcionalidade: barras de progresso de conquistas com valores corretos do save |
| 2.6.0.0 | 03/07/2026 | Funcionalidade: rastreador de tempo de jogo, imagens de conquistas, requisitos por jogo, melhorias na sidebar |
| 2.5.5.0 | 03/07/2026 | Interface: layout redesenhado (Sidebar, Library, GameCard, GameDetail) |
| 2.5.4.0 | 03/07/2026 | Funcionalidade: logo do projeto, icone ICO, layout melhorado |
| 2.5.3.0 | 03/07/2026 | Correcao: erro de compilacao LearnButton_Click (macro pesca funcional) |
| 2.5.2.0 | 03/07/2026 | Pausa: macro de pesca desabilitado (beta, aguardando revisao) |
| 2.5.1.0 | 02/07/2026 | BETA: macro pesca com deteccao de audio (WASAPI) |
| 2.5.0.0 | 02/07/2026 | Funcionalidade: notificacao de conquista com som estilo Steam |
| 2.4.0.0 | 02/07/2026 | Funcionalidade: macro de pesca automatica para Stellar Blade |
| 2.3.4.0 | 02/07/2026 | Correcao: popup conquista usa opacidade em vez de Translation |
| 2.3.3.0 | 02/07/2026 | Interface: botao teste popup conquista |
| 2.3.2.0 | 02/07/2026 | Interface: barra de progresso corrigida, popup conquista estilo Steam |
| 2.3.1.0 | 02/07/2026 | Interface: barra de progresso corrigida, ScrollViewer, traducao conquistas PT-BR |
| 2.3.0.0 | 02/07/2026 | Interface: redesign cards de conquistas estilo Steam |
| 2.2.4.0 | 02/07/2026 | Correcao: deteccao de conquistas do Stellar Blade por nome |
| 2.2.3.0 | 02/07/2026 | Performance: timer 2s, cache de hardware, pausa ao mover janela, fans otimizados |
| 2.2.2.0 | 02/07/2026 | Monitor: Tabler Icons, fans girando, ventoinhas funcional via LHM, grid 2 colunas |
| 2.2.1.0 | 02/07/2026 | Monitor: layout reorganizado, temps inline nos cards, barras proporcionais, cores por nivel |
| 2.2.0.0 | 02/07/2026 | Monitor de Hardware (CPU/GPU/RAM/temps/fans), conquistas Stellar Blade via save parser, pagina de detalhe redesenhada com glass cards, scan movido para biblioteca |
| 2.1.0.0 | 02/07/2026 | Scanner movido para Biblioteca, parser Stellar Blade (conquistas via save), display de conquistas no detalhe do jogo |
| 2.0.2.0 | 02/07/2026 | Correcao: icones da barra de titulo e voltar (FontIcon), versao em 4 partes |
| 2.0.1.0 | 02/07/2026 | Correcao: versao no app.manifest e sidebar |
| 2.0.0.0 | 02/07/2026 | Lancamento inicial — rewrite completo de Electron/React para C# WinUI 3. Scanner Steam + Xbox + pastas, capas via Steam Store API, imagens locais fallback, filtro avancado de nao-jogos, tema Steam escuro, banco SQLite local |

---

**Autor:** Eriklebson — [GitHub](https://github.com/Eriklebson)
