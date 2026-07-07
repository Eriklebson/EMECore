# EMECore.Core - Camada de Dominio

## Visao Geral

Projeto class library puro (`net8.0`) contendo apenas modelos de dados, interfaces de servicos e helpers. Nao tem dependencias de infraestrutura - e a camada mais limpa da arquitetura.

**Localizacao:** `src/EMECore.Core/`
**Dependencias:** Apenas `CommunityToolkit.Mvvm 8.4.2`

---

## Modelos

### Game (`Models/Game.cs`)

Entidade principal do sistema. Representa um jogo na biblioteca.

| Propriedade | Tipo | Default | Descricao |
|------------|------|---------|-----------|
| `Id` | `string` | `""` | Identificador unico (GUID como string) |
| `Name` | `string` | `""` | Nome de exibicao |
| `ExecutablePath` | `string` | `""` | Caminho completo para o .exe |
| `CoverImage` | `string` | `""` | URL ou caminho da imagem de capa |
| `Platform` | `string` | `"other"` | `"steam"`, `"epic"`, `"gog"`, `"xbox"`, `"other"` |
| `LastPlayed` | `DateTime?` | `null` | Timestamp da ultima execucao |
| `PlayTime` | `int` | `0` | Tempo total jogado (em minutos) |
| `LastSessionStart` | `DateTime?` | `null` | Inicio da sessao atual |
| `SteamAppId` | `string` | `""` | AppID do Steam (se aplicavel) |
| `CreatedAt` | `DateTime` | `UTC now` | Data de criacao no banco |
| `UpdatedAt` | `DateTime` | `UTC now` | Data da ultima atualizacao |

### Achievement (`Models/Achievement.cs`)

Conquista de um jogo (espelha a API Steam).

| Propriedade | Tipo | Default | Descricao |
|------------|------|---------|-----------|
| `Id` | `int` | - | Auto-incremento |
| `GameId` | `string` | `""` | FK para Game |
| `Apiname` | `string` | `""` | Nome na API Steam |
| `Achieved` | `bool` | `false` | Se foi desbloqueada |
| `Unlocktime` | `long` | `0` | Timestamp Unix do desbloqueio |
| `Name` | `string` | `""` | Nome de exibicao |
| `Description` | `string` | `""` | Descricao da conquista |
| `Icon` | `string` | `""` | URL do icone colorido |
| `Icongray` | `string` | `""` | URL do icone cinza |
| `UpdatedAt` | `DateTime` | `UTC now` | Ultima atualizacao |

### PlaySession (`Models/PlaySession.cs`)

Sessao de jogo individual.

| Propriedade | Tipo | Default | Descricao |
|------------|------|---------|-----------|
| `Id` | `int` | - | Auto-incremento |
| `GameId` | `string` | `""` | FK para Game |
| `StartTime` | `DateTime` | - | Inicio da sessao |
| `EndTime` | `DateTime?` | `null` | Fim da sessao (null = ativa) |
| `DurationMinutes` | `int` | `0` | Duracao em minutos |

### ScannedGame (`Models/ScannedGame.cs`)

Modelo leve para jogos descobertos pelo scanner (antes de persistir).

| Propriedade | Tipo | Default | Descricao |
|------------|------|---------|-----------|
| `Name` | `string` | `""` | Nome encontrado |
| `ExecutablePath` | `string` | `""` | Caminho do .exe |
| `Platform` | `string` | `"other"` | Plataforma detectada |
| `SteamAppId` | `string` | `""` | AppID (se Steam) |
| `CoverImage` | `string` | `""` | URL da capa |

### SteamStoreInfo (`Models/SteamStoreInfo.cs`)

DTO para dados da Steam Store API.

| Propriedade | Tipo | Default | Descricao |
|------------|------|---------|-----------|
| `Name` | `string` | `""` | Nome do jogo |
| `HeaderImage` | `string` | `""` | URL da imagem de cabecalho |
| `Description` | `string` | `""` | Descricao curta |

---

## Interfaces de Servicos

### IDatabaseService (`Services/IDatabaseService.cs`)

Contrato para persistencia de dados.

| Metodo | Retorno | Descricao |
|--------|---------|-----------|
| `InitializeAsync(string dbPath)` | `Task` | Abre/cria o banco SQLite |
| `GetGamesAsync()` | `Task<List<Game>>` | Lista todos os jogos |
| `GetGameAsync(string id)` | `Task<Game?>` | Busca jogo por ID |
| `UpsertGameAsync(Game game)` | `Task` | Insere ou substitui jogo |
| `DeleteGameAsync(string id)` | `Task` | Remove jogo por ID |
| `UpdateGamePlayTimeAsync(string id, int playTime, DateTime?)` | `Task` | Atualiza play time |
| `RecordPlaySessionAsync(string id, DateTime start, int duration)` | `Task` | Registra sessao |
| `GetPlaySessionsAsync(string gameId)` | `Task<List<PlaySession>>` | Sessoes de um jogo |
| `SaveAchievementsAsync(string gameId, List<Achievement>)` | `Task` | Salva conquistas (full replace) |
| `GetAchievementsAsync(string gameId)` | `Task<List<Achievement>>` | Conquistas de um jogo |
| `GetTotalPlayTimeAsync()` | `Task<int>` | Soma total de play time |
| `GetGameCountAsync()` | `Task<Dictionary<string,int>>` | Contagem por plataforma |
| `CloseAsync()` | `Task` | Fecha conexao |

### ISteamStoreService (`Services/ISteamStoreService.cs`)

Contrato para busca de metadados na Steam Store.

| Metodo | Retorno | Descricao |
|--------|---------|-----------|
| `GetStoreInfoAsync(string appId)` | `Task<SteamStoreInfo?>` | Busca info por AppID |

### IGameScannerService (`Services/IGameScannerService.cs`)

Contrato para scanning de jogos no filesystem.

| Metodo | Retorno | Descricao |
|--------|---------|-----------|
| `ScanAllGamesAsync()` | `Task<List<ScannedGame>>` | Escaneia todos os jogos |

---

## Helpers

### FormatHelpers (`Helpers/FormatHelpers.cs`)

Classe estatica com metodos de formatacao.

| Metodo | Exemplo | Descricao |
|--------|---------|-----------|
| `FormatBytes(1024)` | `"1 KB"` | Bytes para formato legivel |
| `FormatSpeed(1048576)` | `"1 MB/s"` | Velocidade em bytes/s |
| `FormatMinutes(125)` | `"2h 5m"` | Minutos para "Xh Ym" |
