using Microsoft.Data.Sqlite;
using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class DatabaseService : IDatabaseService
{
    private SqliteConnection? _connection;

    public async Task InitializeAsync(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection($"Data Source={dbPath};Pooling=false");
        await _connection.OpenAsync();

        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=DELETE";
            await pragma.ExecuteNonQueryAsync();
        }
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys=ON";
            await pragma.ExecuteNonQueryAsync();
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS games (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, executable_path TEXT NOT NULL,
                cover_image TEXT, platform TEXT NOT NULL DEFAULT 'other',
                last_played TEXT, play_time INTEGER NOT NULL DEFAULT 0,
                last_session_start TEXT, steam_app_id TEXT,
                genre TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS achievements (
                id INTEGER PRIMARY KEY AUTOINCREMENT, game_id TEXT NOT NULL,
                apiname TEXT NOT NULL, achieved INTEGER NOT NULL DEFAULT 0,
                unlocktime INTEGER NOT NULL DEFAULT 0, name TEXT NOT NULL,
                description TEXT, icon TEXT, icongray TEXT,
                updated_at TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE,
                UNIQUE(game_id, apiname)
            );
            CREATE TABLE IF NOT EXISTS play_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT, game_id TEXT NOT NULL,
                start_time TEXT NOT NULL, end_time TEXT,
                duration_minutes INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
            );";
        await cmd.ExecuteNonQueryAsync();

        try { using var m = _connection!.CreateCommand(); m.CommandText = "ALTER TABLE games ADD COLUMN genre TEXT NOT NULL DEFAULT ''"; await m.ExecuteNonQueryAsync(); } catch { }
        try { using var m = _connection!.CreateCommand(); m.CommandText = "ALTER TABLE achievements ADD COLUMN progress INTEGER NOT NULL DEFAULT 0"; await m.ExecuteNonQueryAsync(); } catch { }
        try { using var m = _connection!.CreateCommand(); m.CommandText = "ALTER TABLE achievements ADD COLUMN max_progress INTEGER NOT NULL DEFAULT 0"; await m.ExecuteNonQueryAsync(); } catch { }
    }

    public async Task<List<Game>> GetGamesAsync()
    {
        var games = new List<Game>();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT * FROM games ORDER BY name";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            games.Add(new Game
            {
                Id = reader.GetString(0), Name = reader.GetString(1),
                ExecutablePath = reader.GetString(2),
                CoverImage = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Platform = reader.GetString(4),
                LastPlayed = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                PlayTime = reader.GetInt32(6),
                LastSessionStart = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                SteamAppId = reader.IsDBNull(8) ? "" : reader.GetString(8),
                Genre = reader.IsDBNull(9) ? "" : reader.GetString(9),
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                UpdatedAt = DateTime.Parse(reader.GetString(11))
            });
        }
        return games;
    }

    public async Task<Game?> GetGameAsync(string id)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT * FROM games WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Game
            {
                Id = reader.GetString(0), Name = reader.GetString(1),
                ExecutablePath = reader.GetString(2),
                CoverImage = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Platform = reader.GetString(4),
                LastPlayed = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
                PlayTime = reader.GetInt32(6),
                LastSessionStart = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                SteamAppId = reader.IsDBNull(8) ? "" : reader.GetString(8),
                Genre = reader.IsDBNull(9) ? "" : reader.GetString(9),
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                UpdatedAt = DateTime.Parse(reader.GetString(11))
            };
        }
        return null;
    }

    public async Task UpsertGameAsync(Game game)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO games
            (id, name, executable_path, cover_image, platform, last_played, play_time, last_session_start, steam_app_id, genre, created_at, updated_at)
            VALUES (@id, @name, @exe, @cover, @platform, @lp, @pt, @lss, @steam, @genre, @ca, @ua)";
        cmd.Parameters.AddWithValue("@id", game.Id);
        cmd.Parameters.AddWithValue("@name", game.Name);
        cmd.Parameters.AddWithValue("@exe", game.ExecutablePath);
        cmd.Parameters.AddWithValue("@cover", game.CoverImage);
        cmd.Parameters.AddWithValue("@platform", game.Platform);
        cmd.Parameters.AddWithValue("@lp", (object?)game.LastPlayed?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pt", game.PlayTime);
        cmd.Parameters.AddWithValue("@lss", (object?)game.LastSessionStart?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@steam", game.SteamAppId);
        cmd.Parameters.AddWithValue("@genre", game.Genre);
        cmd.Parameters.AddWithValue("@ca", game.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@ua", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteGameAsync(string id)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "DELETE FROM games WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAllGamesAsync()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "DELETE FROM games";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateGamePlayTimeAsync(string id, int playTime, DateTime? lastSessionStart)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "UPDATE games SET play_time=@pt, last_session_start=@lss, updated_at=@ua WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@pt", playTime);
        cmd.Parameters.AddWithValue("@lss", (object?)lastSessionStart?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ua", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RecordPlaySessionAsync(string id, DateTime startTime, int durationMinutes)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "INSERT INTO play_sessions (game_id, start_time, duration_minutes) VALUES (@id, @st, @dm)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@st", startTime.ToString("o"));
        cmd.Parameters.AddWithValue("@dm", durationMinutes);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<PlaySession>> GetPlaySessionsAsync(string gameId)
    {
        var sessions = new List<PlaySession>();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT * FROM play_sessions WHERE game_id=@id ORDER BY start_time DESC";
        cmd.Parameters.AddWithValue("@id", gameId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new PlaySession
            {
                Id = reader.GetInt32(0), GameId = reader.GetString(1),
                StartTime = DateTime.Parse(reader.GetString(2)),
                EndTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                DurationMinutes = reader.GetInt32(4)
            });
        }
        return sessions;
    }

    public async Task SaveAchievementsAsync(string gameId, List<Achievement> achievements)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "DELETE FROM achievements WHERE game_id=@id";
        cmd.Parameters.AddWithValue("@id", gameId);
        await cmd.ExecuteNonQueryAsync();

        foreach (var a in achievements)
        {
            using var ins = _connection!.CreateCommand();
            ins.CommandText = @"INSERT INTO achievements (game_id, apiname, achieved, unlocktime, name, description, icon, icongray, progress, max_progress, updated_at)
                VALUES (@gid, @ap, @ach, @ut, @nm, @desc, @ic, @ig, @prog, @maxprog, @ua)";
            ins.Parameters.AddWithValue("@gid", gameId);
            ins.Parameters.AddWithValue("@ap", a.Apiname);
            ins.Parameters.AddWithValue("@ach", a.Achieved ? 1 : 0);
            ins.Parameters.AddWithValue("@ut", a.Unlocktime);
            ins.Parameters.AddWithValue("@nm", a.Name);
            ins.Parameters.AddWithValue("@desc", a.Description);
            ins.Parameters.AddWithValue("@ic", a.Icon);
            ins.Parameters.AddWithValue("@ig", a.Icongray);
            ins.Parameters.AddWithValue("@prog", a.Progress);
            ins.Parameters.AddWithValue("@maxprog", a.MaxProgress);
            ins.Parameters.AddWithValue("@ua", DateTime.UtcNow.ToString("o"));
            await ins.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<Achievement>> GetAchievementsAsync(string gameId)
    {
        var list = new List<Achievement>();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT * FROM achievements WHERE game_id=@id ORDER BY achieved DESC, name ASC";
        cmd.Parameters.AddWithValue("@id", gameId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var a = new Achievement
            {
                Id = reader.GetInt32(0), GameId = reader.GetString(1),
                Apiname = reader.GetString(2), Achieved = reader.GetInt32(3) == 1,
                Unlocktime = reader.GetInt64(4), Name = reader.GetString(5),
                Description = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Icon = reader.IsDBNull(7) ? "" : reader.GetString(7),
                Icongray = reader.IsDBNull(8) ? "" : reader.GetString(8),
                UpdatedAt = DateTime.Parse(reader.GetString(9))
            };
            // columns 10, 11 are progress/max_progress (may not exist in old DBs)
            try { a.Progress = reader.GetInt32(10); } catch { }
            try { a.MaxProgress = reader.GetInt32(11); } catch { }
            list.Add(a);
        }
        return list;
    }

    public async Task<int> GetTotalPlayTimeAsync()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(play_time), 0) FROM games";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<Dictionary<string, int>> GetGameCountAsync()
    {
        var dict = new Dictionary<string, int>();
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT platform, COUNT(*) FROM games GROUP BY platform";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            dict[reader.GetString(0)] = reader.GetInt32(1);
        return dict;
    }

    public void Checkpoint()
    {
        if (_connection != null)
        {
            try { using var cmd = _connection.CreateCommand(); cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)"; cmd.ExecuteNonQuery(); } catch { }
        }
    }

    public void CloseSync()
    {
        if (_connection != null)
        {
            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                cmd.ExecuteNonQuery();
            }
            catch { }
            _connection.Close();
            _connection.Dispose();
            _connection = null;
        }
    }

    public async Task CloseAsync()
    {
        if (_connection != null)
        {
            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                cmd.ExecuteNonQuery();
            }
            catch { }
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
