using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using EMECore.Core.Models;
using EMECore.Core.Services;
using Fleck;

namespace EMECore.Hardware.Services;

public class MobileServerService : IDisposable
{
    private WebSocketServer? _server;
    private IDatabaseService? _database;
    private readonly ConcurrentDictionary<Guid, IWebSocketConnection> _clients = new();
    private bool _disposed;

    public int Port { get; private set; }
    public bool IsRunning { get; private set; }
    public int ConnectedClients => _clients.Count;
    public string? LocalIp { get; private set; }

    public event Action<string>? Log;
    public event Action<int>? ClientCountChanged;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MobileServerService(int port = 8181)
    {
        Port = port;
        LocalIp = GetLocalIpAddress();
    }

    public void Start(IDatabaseService database)
    {
        if (IsRunning) return;

        _database = database;

        var listenUrl = $"ws://0.0.0.0:{Port}";
        _server = new WebSocketServer(listenUrl);

        _server.Start(socket =>
        {
            socket.OnOpen = () => OnClientConnected(socket);
            socket.OnClose = () => OnClientDisconnected(socket);
            socket.OnMessage = message => OnMessageReceived(socket, message);
            socket.OnError = ex => OnClientError(socket, ex);
        });

        IsRunning = true;

        Log?.Invoke($"[MobileServer] Iniciado em {listenUrl}");
        if (LocalIp != null)
            Log?.Invoke($"[MobileServer] Conecte pelo IP: {LocalIp}:{Port}");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        foreach (var client in _clients.Values)
        {
            try { client.Close(); } catch { }
        }
        _clients.Clear();

        _server?.Dispose();
        _server = null;

        IsRunning = false;
        Log?.Invoke("[MobileServer] Servidor parado");
    }

    private void OnClientConnected(IWebSocketConnection socket)
    {
        _clients.TryAdd(socket.ConnectionInfo.Id, socket);
        ClientCountChanged?.Invoke(_clients.Count);
        Log?.Invoke($"[MobileServer] Cliente conectado: {socket.ConnectionInfo.ClientIpAddress} (Total: {_clients.Count})");

        SendToClient(socket, new
        {
            type = "welcome",
            message = "Conectado ao E.M.E Core",
            serverVersion = "1.0.0",
            port = Port
        });
    }

    private void OnClientDisconnected(IWebSocketConnection socket)
    {
        _clients.TryRemove(socket.ConnectionInfo.Id, out _);
        ClientCountChanged?.Invoke(_clients.Count);
        Log?.Invoke($"[MobileServer] Cliente desconectado (Total: {_clients.Count})");
    }

    private void OnClientError(IWebSocketConnection socket, Exception ex)
    {
        Log?.Invoke($"[MobileServer] Erro no cliente: {ex.Message}");
        _clients.TryRemove(socket.ConnectionInfo.Id, out _);
        ClientCountChanged?.Invoke(_clients.Count);
    }

    private async void OnMessageReceived(IWebSocketConnection socket, string message)
    {
        try
        {
            var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
            {
                SendError(socket, "Campo 'type' obrigatório");
                return;
            }

            var type = typeProp.GetString()?.ToLower();

            switch (type)
            {
                case "get_hardware":
                    await HandleGetHardware(socket);
                    break;
                case "get_games":
                    await HandleGetGames(socket);
                    break;
                case "launch_game":
                    if (root.TryGetProperty("gameId", out var gameIdProp))
                        await HandleLaunchGame(socket, gameIdProp.GetString() ?? "");
                    else
                        SendError(socket, "Campo 'gameId' obrigatório");
                    break;
                case "get_achievements":
                    if (root.TryGetProperty("gameId", out var achGameIdProp))
                        await HandleGetAchievements(socket, achGameIdProp.GetString() ?? "");
                    else
                        SendError(socket, "Campo 'gameId' obrigatório");
                    break;
                case "ping":
                    SendToClient(socket, new { type = "pong", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                    break;
                default:
                    SendError(socket, $"Tipo desconhecido: {type}");
                    break;
            }
        }
        catch (JsonException)
        {
            SendError(socket, "JSON inválido");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[MobileServer] Erro ao processar mensagem: {ex.Message}");
            SendError(socket, "Erro interno");
        }
    }

    private async Task HandleGetHardware(IWebSocketConnection socket)
    {
        try
        {
            var monitor = new HardwareMonitorService();
            var stats = monitor.CollectFast();
            var json = JsonSerializer.Serialize(new
            {
                type = "hardware_stats",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                data = MapHardwareStats(stats)
            }, _jsonOptions);

            await socket.Send(json);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[MobileServer] Erro ao coletar hardware: {ex.Message}");
            SendError(socket, "Monitor de hardware indisponivel");
        }
    }

    private async Task HandleGetGames(IWebSocketConnection socket)
    {
        if (_database == null)
        {
            SendError(socket, "Banco de dados não disponível");
            return;
        }

        var games = await _database.GetGamesAsync();
        var gameList = games.Select(g => new
        {
            id = g.Id,
            name = g.Name,
            platform = g.Platform,
            coverImage = g.CoverImage,
            genre = g.Genre,
            playTime = g.PlayTime,
            lastPlayed = g.LastPlayed?.ToString("o"),
            steamAppId = g.SteamAppId
        }).ToList();

        var json = JsonSerializer.Serialize(new
        {
            type = "game_list",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data = gameList
        }, _jsonOptions);

        await socket.Send(json);
    }

    private async Task HandleLaunchGame(IWebSocketConnection socket, string gameId)
    {
        if (_database == null)
        {
            SendError(socket, "Banco de dados não disponível");
            return;
        }

        var game = await _database.GetGameAsync(gameId);
        if (game == null)
        {
            SendError(socket, $"Jogo não encontrado: {gameId}");
            return;
        }

        if (string.IsNullOrEmpty(game.ExecutablePath) || !System.IO.File.Exists(game.ExecutablePath))
        {
            SendError(socket, $"Executável não encontrado: {game.ExecutablePath}");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = game.ExecutablePath,
                UseShellExecute = true
            };
            Process.Start(psi);

            game.LastPlayed = DateTime.UtcNow;
            game.LastSessionStart = DateTime.UtcNow;
            await _database.UpsertGameAsync(game);

            SendToClient(socket, new
            {
                type = "game_launched",
                gameId = game.Id,
                name = game.Name,
                success = true
            });

            Log?.Invoke($"[MobileServer] Jogo iniciado remotamente: {game.Name}");
        }
        catch (Exception ex)
        {
            SendError(socket, $"Erro ao iniciar jogo: {ex.Message}");
        }
    }

    private async Task HandleGetAchievements(IWebSocketConnection socket, string gameId)
    {
        if (_database == null)
        {
            SendError(socket, "Banco de dados não disponível");
            return;
        }

        var achievements = await _database.GetAchievementsAsync(gameId);
        var achList = achievements.Select(a => new
        {
            apiname = a.Apiname,
            name = a.Name,
            description = a.Description,
            achieved = a.Achieved,
            icon = a.Icon,
            iconGray = a.Icongray,
            progress = a.Progress,
            maxProgress = a.MaxProgress,
            hasProgress = a.HasProgress,
            progressPercentage = a.ProgressPercentage
        }).ToList();

        var json = JsonSerializer.Serialize(new
        {
            type = "achievements",
            gameId = gameId,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data = achList
        }, _jsonOptions);

        await socket.Send(json);
    }

    private static object MapHardwareStats(HardwareStats s)
    {
        return new
        {
            cpu = new
            {
                usage = s.CpuUsage,
                temp = s.CpuPackageTemp,
                voltage = s.CpuVoltage,
                power = s.CpuPower,
                model = s.CpuModel,
                cores = s.CpuCoresPhysical,
                threads = s.CpuThreads,
                clockMhz = s.CpuCoreClocks.FirstOrDefault()?.Value ?? 0
            },
            gpu = new
            {
                usage = s.GpuUsage,
                temp = s.GpuTemp,
                hotspotTemp = s.GpuHotspotTemp,
                voltage = s.GpuVoltage,
                power = s.GpuPower,
                model = s.GpuModel,
                coreClockMhz = s.GpuCoreClockMhz,
                memoryClockMhz = s.GpuMemoryClockMhz,
                memoryTotalMb = s.GpuMemoryTotalMb,
                memoryUsedMb = s.GpuMemoryUsedMb,
                driverVersion = s.GpuDriverVersion
            },
            ram = new
            {
                usedGb = Math.Round(s.UsedRam / 1024.0, 1),
                totalGb = Math.Round(s.TotalRam / 1024.0, 1),
                freeGb = Math.Round(s.RamFree / 1024.0, 1),
                percent = s.TotalRam > 0 ? Math.Round(s.UsedRam * 100.0 / s.TotalRam, 1) : 0,
                speed = s.RamSpeed,
                model = s.RamModel,
                type = s.RamType
            },
            fps = new
            {
                current = s.FpsCurrent,
                min = s.FpsMin,
                max = s.FpsMax,
                avg = s.FpsAvg,
                low1 = s.FpsLow1,
                low01 = s.FpsLow01,
                frameTimeMs = s.FpsFrameTimeMs,
                source = s.FpsSource
            },
            disk = new
            {
                readKbps = s.DiskReadKbps,
                writeKbps = s.DiskWriteKbps,
                usagePercent = s.DiskUsagePercent
            },
            network = new
            {
                downloadSpeed = s.NetworkDownloadSpeed,
                uploadSpeed = s.NetworkUploadSpeed,
                name = s.NetworkName
            },
            motherboard = new
            {
                model = s.MotherboardModel,
                temp = s.MotherboardTemp,
                biosVersion = s.MbBiosVersion,
                biosDate = s.MbBiosDate
            },
            fans = s.Fans?.Select(f => (object)new
            {
                name = f.Name,
                rpm = f.Rpm,
                dutyPercent = f.DutyPercent
            }).ToList() ?? new List<object>()
        };
    }

    private void SendToClient(IWebSocketConnection socket, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            socket.Send(json).Wait(500);
        }
        catch { }
    }

    private void SendError(IWebSocketConnection socket, string message)
    {
        SendToClient(socket, new { type = "error", message });
    }

    private static string? GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            var endpoint = socket.LocalEndPoint as IPEndPoint;
            return endpoint?.Address.ToString();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
