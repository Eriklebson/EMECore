# MainViewModel — estado e operações da biblioteca

Arquivo: `src/EMECore.WinUI/ViewModels/MainViewModel.cs`. Usa `CommunityToolkit.Mvvm` e mantém o estado principal de biblioteca.

## Estado observável

`CurrentPage`, `SelectedGame`, `StatusText`, `TotalGames`, `TotalPlayTime`, `IsScanning` e `IsSidebarVisible`; a coleção é `ObservableCollection<Game> Games`.

## Fluxos

- `InitializeAsync`: inicializa o banco e carrega jogos.
- `ScanGamesAsync`: impede scan concorrente, procura jogos, evita duplicação por **nome** sem diferenciar maiúsculas, persiste e tenta enriquecer gênero/capa sem bloquear o resultado.
- `ResetAndScanAsync`: apaga jogos e recria a biblioteca; é operação destrutiva e deve ser exposta com cuidado.
- `LaunchGameAsync`: inicia executável, registra data/sessão e aciona `PlayTimeTrackerService`.
- `DeleteGameAsync` e `AddGameManualAsync`: persistem e mantêm coleção/contadores coerentes.

## Navegação

Comandos `NavigateTo`, `SelectGame`, `GoBack` e `AddGame` mudam `CurrentPage`; `MainWindow` reage ao `PropertyChanged` e altera a visibilidade das telas. Páginas atuais incluem biblioteca, detalhe, adição, conquistas, configurações, loja e detalhe da loja.

Não mover lógica de banco, scanner, Steam ou processo para Views; adicionar comando/serviço na VM quando a ação fizer parte deste fluxo.
