# Banco de Dados SQLite

## Visao Geral

Banco local usando SQLite via `Microsoft.Data.Sqlite`. Localizacao: `%LocalAppData%\EMECore\eme_core.db`

**Inicializacao:** `DatabaseService.InitializeAsync(dbPath)` cria o arquivo, abre a conexao, ativa WAL e foreign keys, e cria as tabelas.

---

## Schema

### Tabela `games`

```sql
CREATE TABLE IF NOT EXISTS games (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    executable_path TEXT NOT NULL,
    cover_image TEXT DEFAULT '',
    platform TEXT DEFAULT 'other',
    last_played TEXT,
    play_time INTEGER DEFAULT 0,
    last_session_start TEXT,
    steam_app_id TEXT DEFAULT '',
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
)
```

| Coluna | Tipo | Constraints | Descricao |
|--------|------|-------------|-----------|
| `id` | TEXT | PRIMARY KEY | GUID como string |
| `name` | TEXT | NOT NULL | Nome do jogo |
| `executable_path` | TEXT | NOT NULL | Caminho do .exe |
| `cover_image` | TEXT | DEFAULT `''` | URL/caminho da capa |
| `platform` | TEXT | DEFAULT `'other'` | steam/epic/gog/xbox/other |
| `last_played` | TEXT | nullable | ISO 8601 datetime |
| `play_time` | INTEGER | DEFAULT 0 | Minutos totais |
| `last_session_start` | TEXT | nullable | ISO 8601 datetime |
| `steam_app_id` | TEXT | DEFAULT `''` | AppID do Steam |
| `created_at` | TEXT | NOT NULL | ISO 8601 datetime |
| `updated_at` | TEXT | NOT NULL | ISO 8601 datetime |

### Tabela `achievements`

```sql
CREATE TABLE IF NOT EXISTS achievements (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id TEXT NOT NULL,
    apiname TEXT NOT NULL,
    achieved INTEGER DEFAULT 0,
    unlocktime INTEGER DEFAULT 0,
    name TEXT DEFAULT '',
    description TEXT DEFAULT '',
    icon TEXT DEFAULT '',
    icongray TEXT DEFAULT '',
    updated_at TEXT NOT NULL,
    UNIQUE(game_id, apiname),
    FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
)
```

| Coluna | Tipo | Constraints | Descricao |
|--------|------|-------------|-----------|
| `id` | INTEGER | PK AUTOINCREMENT | ID auto-incremento |
| `game_id` | TEXT | NOT NULL, FK→games(id) CASCADE | Jogo associado |
| `apiname` | TEXT | NOT NULL, UNIQUE(game_id, apiname) | Nome na API Steam |
| `achieved` | INTEGER | DEFAULT 0 | 0=falso, 1=verdadeiro |
| `unlocktime` | INTEGER | DEFAULT 0 | Unix timestamp do desbloqueio |
| `name` | TEXT | DEFAULT `''` | Nome de exibicao |
| `description` | TEXT | DEFAULT `''` | Descricao |
| `icon` | TEXT | DEFAULT `''` | URL icone colorido |
| `icongray` | TEXT | DEFAULT `''` | URL icone cinza |
| `updated_at` | TEXT | NOT NULL | ISO 8601 datetime |

### Tabela `play_sessions`

```sql
CREATE TABLE IF NOT EXISTS play_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    game_id TEXT NOT NULL,
    start_time TEXT NOT NULL,
    end_time TEXT,
    duration_minutes INTEGER DEFAULT 0,
    FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
)
```

| Coluna | Tipo | Constraints | Descricao |
|--------|------|-------------|-----------|
| `id` | INTEGER | PK AUTOINCREMENT | ID auto-incremento |
| `game_id` | TEXT | NOT NULL, FK→games(id) CASCADE | Jogo associado |
| `start_time` | TEXT | NOT NULL | ISO 8601 datetime |
| `end_time` | TEXT | nullable | ISO 8601 datetime (null = ativa) |
| `duration_minutes` | INTEGER | DEFAULT 0 | Duracao em minutos |

---

## Operacoes

### CRUD de Jogos

| Metodo | SQL | Observacao |
|--------|-----|------------|
| `GetGamesAsync()` | `SELECT * FROM games ORDER BY name` | Retorna todos |
| `GetGameAsync(id)` | `SELECT * FROM games WHERE id = @id` | Retorna nullable |
| `UpsertGameAsync(game)` | `INSERT OR REPLACE INTO games ...` | Insere ou substitui |
| `DeleteGameAsync(id)` | `DELETE FROM games WHERE id = @id` | CASCADE remove achievements e sessions |

### Play Time

| Metodo | SQL |
|--------|-----|
| `UpdateGamePlayTimeAsync(id, playTime, lastSessionStart)` | `UPDATE games SET play_time=@playTime, last_session_start=@lastSessionStart WHERE id=@id` |
| `GetTotalPlayTimeAsync()` | `SELECT SUM(play_time) FROM games` |

### Sessoes

| Metodo | SQL |
|--------|-----|
| `RecordPlaySessionAsync(id, startTime, duration)` | `INSERT INTO play_sessions ...` |
| `GetPlaySessionsAsync(gameId)` | `SELECT * FROM play_sessions WHERE game_id=@gameId ORDER BY start_time DESC` |

### Conquistas

| Metodo | Estrategia |
|--------|-----------|
| `SaveAchievementsAsync(gameId, achievements)` | DELETE all + INSERT all (full replace) |
| `GetAchievementsAsync(gameId)` | `SELECT * WHERE game_id=@gameId ORDER BY achieved, name` |

### Agregacoes

| Metodo | SQL |
|--------|-----|
| `GetTotalPlayTimeAsync()` | `SELECT SUM(play_time) FROM games` |
| `GetGameCountAsync()` | `SELECT platform, COUNT(*) FROM games GROUP BY platform` |

---

## Configuracoes

- **Journal Mode:** WAL (Write-Ahead Logging) - melhor performance para leitura concorrente
- **Foreign Keys:** Habilitadas - CASCADE delete em achievements e play_sessions
- **Conexao:** Unica `SqliteConnection` por instancia de `DatabaseService`
