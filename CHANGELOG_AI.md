# CHANGELOG AI — E.M.E Core

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
