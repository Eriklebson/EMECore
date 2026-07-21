# CHANGELOG AI — E.M.E Core

---

## 2026-07-20 — Documentação: revisão técnica da arquitetura atual

### Arquivos modificados
- `docs/README.md` — Índice atualizado da documentação oficial.
- `docs/architecture.md`, `core-project.md`, `database.md`, `hardware-project.md`, `winui-project.md`, `ui-components.md`, `viewmodel.md`, `theme.md`, `build-setup.md`, `known-issues.md` — Arquivos existentes revisados contra o código atual.
- `docs/stellar-blade-*.md` — Pesquisa de saves do Stellar Blade mantida como material específico.

### O que mudou
- A documentação foi confrontada com o código atual e reorganizada em arquivos por assunto.
- Foram registrados o monitor LHM/WMI, FPS via PresentMon, conquistas e parsers de save, SQLite em modo `DELETE`, servidor WebSocket, descoberta UDP e servidor HTTP de capas.
- Instruções antigas que descreviam esses recursos como stubs, ou diziam que não havia XAML, foram retiradas do índice de manutenção.

### Motivo
Evitar que futuras alterações usem documentação de uma versão inicial do projeto e introduzam regressões no banco, no monitoramento ou na integração mobile.

### Impacto
- Não há mudança de comportamento no aplicativo.
- Desenvolvedores e agentes devem usar `docs/README.md` e os arquivos atuais ligados por ele como fonte de verdade.

---

## 2026-07-17 — Feature: Servidor WebSocket para App Mobile (Controle Remoto)

### Arquivos criados
- `EMECore.Hardware/Services/MobileServerService.cs` — Servidor WebSocket para comunicação com app mobile Flutter

### Arquivos modificados
- `EMECore.Hardware/EMECore.Hardware.csproj` — Adicionado pacote NuGet `Fleck` v1.2.0
- `EMECore.WinUI/MainWindow.xaml.cs` — Integrado `MobileServerService` (iniciar/parar automaticamente)
- `EMECore.WinUI/ViewModels/MainViewModel.cs` — Adicionado método `GetDatabaseService()` para acesso do servidor

### O que mudou
- **Servidor WebSocket** escutando na porta `8181` (configurável via settings)
- Inicia automaticamente quando o app abre, para quando fecha
- **Broadcast de hardware** a cada 1 segundo para todos os clientes conectados
- **Comandos suportados:**
  - `get_hardware` — Stats de hardware em tempo real
  - `get_games` — Lista completa de jogos
  - `launch_game` — Lançar jogo remotamente por ID
  - `get_achievements` — Conquistas de um jogo específico
  - `ping/pong` — Keep-alive
- **Gerenciamento de clientes:** `ConcurrentDictionary` com limpeza automática de clientes desconectados
- **Configurações:** `mobile_server_enabled` (True/False) e `mobile_server_port` (8181)
- **Protocolo JSON** bidirecional: desktop envia `hardware_stats` a cada 1s, mobile envia comandos
- IP local detectado automaticamente para exibição no log

### Motivo
Preparar a infraestrutura de rede para o futuro app mobile Flutter que funcionará como controle remoto do PC. O servidor roda dentro do desktop (sem segundo processo) e expõe dados de hardware, jogos e conquistas via WebSocket.

### Impacto
- Nenhum impacto no desempenho do desktop (timer de 1s em background thread)
- Porta 8181 pode precisar de permissão no firewall para conexões externas
- Pode ser desativado via settings (`mobile_server_enabled = False`)
- Pronto para conexão do app Flutter (EMECoreMobile)

---

## 2026-07-17 — Atualizacao: Pagina promocional na sidebar

### Arquivos modificados
- `Assets/ad.html` — Substituido placeholder por pagina promocional completa do E.M.E Core
- `Views/Sidebar.xaml.cs` — Mudado de `NavigateToString` para `CoreWebView2.Navigate(fileUri)` para URLs relativas funcionarem
- `EMECore.WinUI.csproj` — Adicionado `logo.png` ao output

### O que mudou
- Pagina promocional com logo, stats (100% Offline, WinUI Nativo), 3 feature cards (Scanner, Monitor, Conquistas) e botao de download
- Logo carrega localmente (sem dependencia externa)
- Layout responsivo com CSS media query para sidebar recolhida (`@media max-width: 80px`)
- Carregamento via file URI em vez de NavigateToString

### Motivo
Substituir o placeholder "Espaco reservado para anuncio" pela pagina promocional real do E.M.E Core criada pelo usuario.

---

## 2026-07-17 — Melhoria: Verificacao de dependencias no instalador

### Arquivos modificados
- `installer.iss` — Reescrito script [Code] com verificacoes de dependencias

### O que mudou
- **Verificacao de .NET 8 Desktop Runtime** — Executa `dotnet --list-runtimes` antes da instalacao. Se ausente, pergunta se quer baixar
- **Verificacao de WebView2 Runtime** — Verifica registro Windows (HKLM/HKCU) para a chave do EdgeUpdate. Se ausente, pergunta se quer baixar
- Funcoes `IsDotNet8Installed` e `IsWebView2Installed` usadas em `PrepareToInstall`
- Downloads apontam para URLs oficiais da Microsoft

### Motivo
Amigos tiveram erros de runtime ao testar versoes anteriores (2.11) porque .NET 8 Runtime nao estava instalado. O instalador antigo so verificava DEPOIS da instalacao e so abria o navegador sem orientacao clara.

### Impacto
- Usuarios sem .NET 8 receberao aviso antes de instalar
- Usuarios sem WebView2 receberao aviso antes de instalar
- Menos erros de runtime pos-instalacao

---

## 2026-07-17 — Feature: AdSense na Sidebar (WebView2 + HTML)

### Arquivos criados
- `Assets/ad.html` — Página HTML com placeholder AdSense + JS responsivo (resize observer)

### Arquivos modificados
- `Views/Sidebar.xaml.cs` — Adicionado WebView2 `_adWeb` na Row 3 da sidebar, carrega ad.html via `NavigateToString`, resize automático no collapse (180x320 → 56x100)
- `EMECore.WinUI.csproj` — ad.html incluído no output via `<Content CopyToOutputDirectory>`

### O que mudou
- Nova Row 3 no grid da sidebar para o anúncio (AddButton movido para Row 4)
- WebView2 carrega HTML local do filesystem (`AppContext.BaseDirectory/Assets/ad.html`)
- Inicialização via `Loaded` event + `EnsureCoreWebView2Async()` (não `CoreWebView2Initialized`)
- HTML usa `#161719` como fundo para combinar com tema da sidebar
- JS responsivo: detecta largura do container e ajusta layout (expandido 160x300, compacto 56x100)

### Motivo
Integrar espaço para anúncios AdSense na sidebar do launcher. O ad é sempre visível e responsivo — quando a sidebar recolhe, o ad encolhe proporcionalmente.

### Decisões técnicas
- `ms-appx:///` não funciona em apps não-empacotados (`WindowsPackageType=None`) → usamos `AppContext.BaseDirectory`
- `CoreWebView2Initialized` pode não disparar → usamos `Loaded` + `EnsureCoreWebView2Async()` explícito
- `NavigateToString` em vez de `NavigateToLocalStreamUri` parasimplicidade

### Impacto
- Sidebar agora tem 5 rows (era 4)
- WebView2 adiciona ~30-50MB ao uso de memória quando carregado
- Para ativar AdSense real: descomentar bloco no ad.html e inserir publisher ID

---

## 2026-07-16 — Fix: Layout 2 colunas dos detalhes do monitor

### Arquivos modificados
- `MonitorWindow.cs` — Reescrita completa do sistema de grid dos detalhes do monitor de hardware

### O que mudou
- Removido contador estático `_detailRow` e `ResetDetailRow()` que causavam conflito entre painéis
- `AddDetailRow()` agora conta filhos reais por row usando `Grid.GetRow`/`Grid.GetColumnSpan`
- `AddSectionLabel()` usa `ColumnDefinitions.Count` para ColumnSpan (funciona com qualquer número de colunas)
- Adicionado `hasFullSpan` para detectar section labels e evitar sobreposição (label full-span + item na mesma row)
- Responsivo: 2 colunas ≥800px, 1 coluna <800px via `UpdateDetailGridColumns()`

### Motivo
O layout 2 colunas estava quebrado — informações apareciam empilhadas verticalmente. A causa raiz era dois bugs:
1. `AddSectionLabel` criava uma row com `ColumnSpan=2`, mas `AddDetailRow` não a detectava e colocava o primeiro item na mesma row
2. O contador estático `_detailRow` era compartilhado entre todos os painéis (CPU, GPU, MB, RAM)

### Impacto
- Detalhes do monitor (CPU, GPU, MB, RAM) agora exibem corretamente em grid 2 colunas
- Layout responsivo mantido (1 coluna em telas estreitas)

---

## 2026-07-08 — Remoção: Macro de Pesca

### Arquivos removidos
- `FishingMacroService.cs` — Serviço completo de macro de pesca (visão por computador, automação com teclado/mouse)
- `FishingMacroWindow.cs` — Janela de controle da macro de pesca

### Arquivos modificados
- `MainWindow.xaml.cs` — Removido: `_gameProcessTimer`, `GameProcessTimer_Tick`, `IsStellarBladeRunning`, `Sidebar_FishingMacroRequested`, lógica de visibilidade no `LibraryPage_GameSelected`
- `Sidebar.xaml.cs` — Removido: evento `FishingMacroRequested`, método `UpdateFishingMacroVisibility`

### Motivo
O macro de pesca era específico para Stellar Blade e não se encaixava na ideia central do launcher. A remoção simplifica a base de código e elimina uma funcionalidade que não é parte do escopo do projeto.

### Impacto
- Nenhum efeito em outras funcionalidades
- Botão de macro de pesca removido da sidebar (não existia visualmente, apenas o evento)
- Timer de verificação de processo removido (reduz uso de CPU)

---

## 2026-07-07 — Correção: Persistência de jogos no banco de dados

### Arquivos modificados
- `DatabaseService.cs` — journal_mode=DELETE via PRAGMA, indices de colunas corrigidos, CloseSync, Checkpoint
- `IDatabaseService.cs` — CloseSync, Checkpoint adicionados à interface
- `MainViewModel.cs` — CloseDatabaseSync, try/catch em LoadGamesAsync
- `MainWindow.xaml.cs` — Closed handler movido pro construtor, Environment.Exit(0), SHM/WAL limpos no fechamento
- `LibraryPage.xaml.cs` — Scroll corrigido (StackPanel→Grid com linha Star)
- `GameDetailPage.xaml.cs` — Requisitos formatados (Clean preserva <br> e <li>)
- `GameScannerService.cs` — (sem alterações diretas)
- `app.manifest` — Versão 2.14.2.0
- `Sidebar.xaml.cs` — Logo v2.14.2.0
- `README.md` — Entradas 2.14.1.0 e 2.14.2.0

### Causas raízes encontradas (4 problemas)

**1. Processo não encerrava ao fechar a janela**
O evento `Closed` estava inscrito dentro do handler `Activated` assíncrono, que podia não ter executado quando o usuário fechava a janela. Além disso, `CloseAsync()` causava deadlock no thread UI. Solução: `Closed` movido pro construtor, `CloseSync()` síncrono, `Environment.Exit(0)` para forçar encerramento.

**2. WAL (Write-Ahead Logging) nunca fazia checkpoint**
O SQLite em modo WAL gravava dados no arquivo `.db-wal` e só copiava pro banco principal no checkpoint. Como o processo nunca encerrava corretamente, o checkpoint nunca acontecia. Solução: `PRAGMA journal_mode=DELETE` — grava direto no banco principal, sem WAL.

**3. Índices de colunas trocados em GetGamesAsync**
`Genre` era lido da coluna 10 (created_at) e `CreatedAt` da coluna 9 (genre). `DateTime.Parse` crashava silenciosamente e nenhum jogo era carregado. Solução: Genre=9, CreatedAt=10.

**4. SHM/WAL travavam o SQLite no próximo lançamento**
Arquivos SHM e WAL deixados por processos zumbis impediam o novo processo de ler o banco. Solução: deletar SHM e WAL no handler `Closed` antes de `Environment.Exit(0)`.

---

## 2026-07-07 — Correção: Botão "Procurar Jogos" não atualizava a grade

### Arquivos modificados
- `src/EMECore.WinUI/MainWindow.xaml.cs` — Removida condição `WindowActivationState.CodeActivated` + adicionado `LoadGames` no handler de `StatusText`
- `src/EMECore.Hardware/Services/DatabaseService.cs` — Adicionado `PRAGMA wal_checkpoint(TRUNCATE)` no `InitializeAsync`
- `src/EMECore.WinUI/Views/LibraryPage.xaml.cs` — Rastros removidos
- `src/EMECore.WinUI/ViewModels/MainViewModel.cs` — Rastros removidos

### Resumo
O botão "Procurar Jogos" executava o scan e salvava os jogos no banco, mas a grade da biblioteca nunca era atualizada. O usuário só via os jogos ao fechar e reabrir o aplicativo manualmente.

### Causas encontradas (2 problemas)

**Problema 1 — `PropertyChanged` nunca atualizava a UI**

Quando o scan terminava, `StatusText` era alterado para "14 jogos encontrados" e o evento `PropertyChanged` era disparado. O handler no `MainWindow` capturava essa mudança e atualizava o Sidebar, mas **não chamava `_libraryPage.LoadGames()`**. A grade mantinha o snapshot antigo (vazio) e nunca era recarregada.

Correção: Adicionada a linha `_libraryPage.LoadGames(ViewModel.Games)` dentro do handler `ViewModel.PropertyChanged` para o evento `StatusText`.

**Problema 2 — Condição `WindowActivationState.CodeActivated`**

A inicialização do app (`InitializeAsync`, subscrições de eventos, `LoadGames` inicial) estava dentro de um bloco `if (args.WindowActivationState == WindowActivationState.CodeActivated)`. Quando o app era lançado via terminal ou outras formas, o estado de ativação podia ser `PointerActivated`, fazendo com que **nenhum código de inicialização executasse** — banco não inicializado, eventos não inscritos, UI vazia.

Correção: Removida a condição `WindowActivationState`. A inicialização agora roda no primeiro evento `Activated` independente do estado.

**Problema 3 — WAL do SQLite travava `InitializeAsync`**

Durante os testes, `Stop-Process -Force` matava o processo sem dar chance ao SQLite fazer checkpoint do WAL. O arquivo `eme_core.db-wal` acumulava dados (3+ MB) e o próximo `InitializeAsync` travava por 10+ segundos tentando processar o WAL corrompido.

Correção: Adicionado `PRAGMA wal_checkpoint(TRUNCATE)` logo após `PRAGMA journal_mode=WAL` para limpar o WAL no startup.

### Impacto
- Botão "Procurar Jogos" agora atualiza a grade imediatamente após o scan
- App inicializa corretamente independente de como é lançado (terminal, duplo clique, etc.)
- WAL do SQLite não acumula dados entre execuções

### Próximos passos recomendados
1. Adicionar filtros de gênero na LibraryPage (Todos, RPG, Ação, etc.)
2. Testar o botão "Procurar Jogos" com o app fechado normalmente (não `Stop-Process`)
3. Fazer commit das alterações
