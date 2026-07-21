# 3. Dados, biblioteca e scanner

## Banco SQLite

Localização: `%LocalAppData%\EMECore\eme_core.db`.

`DatabaseService` abre uma conexão sem pooling, ativa `foreign_keys=ON` e configura `journal_mode=DELETE`. Apesar de referências antigas a WAL, o comportamento atual é DELETE; não mudar sem revisar o fechamento da aplicação e os riscos de lock.

| Tabela | Conteúdo e regras |
|---|---|
| `games` | Identidade, executável obrigatório, plataforma, capa, Steam App ID, gênero, tempo de jogo e datas. |
| `achievements` | Conquistas com chave única `(game_id, apiname)`, estado, ícones e progresso. |
| `play_sessions` | Sessões de jogo, início/fim e duração. |

Exclusão de um jogo remove conquistas e sessões por `ON DELETE CASCADE`. Datas são gravadas em ISO (`DateTime.ToString("o")`). O banco possui migrações leves com `ALTER TABLE` protegido por `try/catch`; qualquer coluna nova deve funcionar tanto em instalações novas quanto existentes.

## Fluxo da biblioteca

Arquivos: `MainViewModel.cs`, `LibraryPage.xaml.cs`, `GameDetailPage.xaml.cs`, `DatabaseService.cs`.

1. `InitializeAsync` chama `DatabaseService.InitializeAsync` e carrega os jogos em `ObservableCollection<Game>`.
2. Nomes duplicados são identificados sem diferenciar maiúsculas/minúsculas; duplicatas são excluídas do banco.
3. A biblioteca apresenta lista, busca e categorias `game`, `tool` e `training`.
4. Inclusão manual cria um `Game` com `Guid`, persiste e volta à biblioteca.
5. Iniciar jogo atualiza `LastPlayed` e `LastSessionStart`, salva e inicia `PlayTimeTrackerService`.

## Scanner

Arquivo: `EMECore.Hardware/Services/GameScannerService.cs`.

Fontes pesquisadas incluem Steam, Xbox/Game Pass, diretórios comuns e instalações de Riot, Ubisoft, EA/Origin, Battle.net, Rockstar, Bethesda e Amazon Games. O scanner evita diretórios e executáveis que não são jogos, como uninstallers, redistribuíveis e crash handlers.

O fluxo normal só adiciona um resultado quando o nome ainda não existir na coleção. O fluxo de reset limpa jogos e recria a lista inteira; usar com cautela porque remove a biblioteca existente.

## Capas e gêneros

Após scan, a ViewModel tenta enriquecer jogos sem bloquear a operação principal:

1. Para Steam com `SteamAppId`, consulta dados da loja e usa `HeaderImage`.
2. Sem ID, pesquisa a Steam pelo nome e associa o ID/capa quando encontra correspondência.
3. O scanner procura imagens dentro da instalação (`Splash`, `Logo`, `cover` etc.).
4. Como último fallback, usa URL de box art Twitch.
5. Gêneros são buscados por `GenreService`; falhas não impedem o scan.

Chamadas de Steam usam limites de concorrência. Ao mudar buscas externas, manter timeout, tolerância a falhas e evitar bloquear a UI.

## Alterar modelos persistidos

1. Alterar o modelo em `EMECore.Core/Models`.
2. Incluir migração segura em `DatabaseService.InitializeAsync` quando for coluna nova.
3. Atualizar `SELECT`, mapeamento de `SqliteDataReader`, `INSERT OR REPLACE` e comandos de atualização.
4. Revisar ViewModel, páginas WinUI e o payload do servidor mobile.
5. Atualizar os modelos Dart e o documento do protocolo, se o campo for exposto.
