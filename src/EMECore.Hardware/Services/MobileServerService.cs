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
    private AchievementService? _achievementService;
    private readonly ConcurrentDictionary<Guid, IWebSocketConnection> _clients = new();
    private bool _disposed;
    private CancellationTokenSource? _beaconCts;
    private CancellationTokenSource? _httpCts;
    private CancellationTokenSource? _gamepadCts;
    private HttpListener? _httpListener;
    private readonly SteamStoreService _steamStore = new();
    private readonly HardwareMonitorService _monitor = new();
    private HardwareStats? _lastWmiStats;
    private DateTime _lastWmiRefresh = DateTime.MinValue;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public const int BeaconPort = 8182;
    public const int ImagePort = 8183;
    public int Port { get; private set; }
    public bool IsRunning { get; private set; }
    public int ConnectedClients => _clients.Count;
    public string? LocalIp { get; private set; }

    private string CoversCacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EMECore", "CoverCache");

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
        Log?.Invoke($"[MobileServer] IP Local: {LocalIp ?? "(nao detectado)"}");
    }

    public void Start(IDatabaseService database, AchievementService? achievementService = null)
    {
        if (IsRunning) return;

        _database = database;
        _achievementService = achievementService;
        LocalIp = GetLocalIpAddress() ?? LocalIp;

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

        StartBeacon();
        StartImageServer();
        StartGamepadStreaming();
        _ = PreloadCoversAsync();

        Log?.Invoke($"[MobileServer] Iniciado em {listenUrl}");
        if (LocalIp != null)
            Log?.Invoke($"[MobileServer] Conecte pelo IP: {LocalIp}:{Port}");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        StopBeacon();
        StopImageServer();
        StopGamepadStreaming();

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
            var stats = _monitor.CollectFast();

            if ((DateTime.UtcNow - _lastWmiRefresh).TotalSeconds >= 5)
            {
                _lastWmiStats = _monitor.Collect();
                _lastWmiRefresh = DateTime.UtcNow;
            }

            if (_lastWmiStats != null)
            {
                stats.TotalRam = _lastWmiStats.TotalRam;
                stats.UsedRam = _lastWmiStats.UsedRam;
                stats.RamFree = _lastWmiStats.RamFree;
                stats.RamModel = _lastWmiStats.RamModel;
                stats.RamSpeed = _lastWmiStats.RamSpeed;
                stats.RamType = _lastWmiStats.RamType;
                stats.RamModuleCount = _lastWmiStats.RamModuleCount;
                stats.RamModules = _lastWmiStats.RamModules;
                stats.DiskUsagePercent = _lastWmiStats.DiskUsagePercent;
                stats.Disks = _lastWmiStats.Disks;
                stats.GpuDriverVersion = _lastWmiStats.GpuDriverVersion;
                if (stats.GpuMemoryTotalMb <= 0 && _lastWmiStats.GpuMemoryTotalMb > 0)
                    stats.GpuMemoryTotalMb = _lastWmiStats.GpuMemoryTotalMb;
                if (_lastWmiStats.NetworkDownloadSpeed > 0 || _lastWmiStats.NetworkUploadSpeed > 0)
                {
                    stats.NetworkDownloadSpeed = _lastWmiStats.NetworkDownloadSpeed;
                    stats.NetworkUploadSpeed = _lastWmiStats.NetworkUploadSpeed;
                    stats.NetworkName = _lastWmiStats.NetworkName;
                }
            }

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

        var gameList = new List<object>();
        var unresolvedCovers = new List<(string GameId, string GameName, string CoverUrl)>();

        foreach (var g in games)
        {
            var cachedPath = Path.Combine(CoversCacheDir, $"{g.Id}.jpg");
            var coverUrl = "";

            if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 8000)
            {
                coverUrl = $"http://{LocalIp}:{ImagePort}/{g.Id}.jpg";
            }
            else if (!string.IsNullOrEmpty(g.CoverImage) && g.CoverImage.StartsWith("http"))
            {
                coverUrl = g.CoverImage;
                unresolvedCovers.Add((g.Id, g.Name, g.CoverImage));
            }

            gameList.Add(new
            {
                id = g.Id,
                name = g.Name,
                platform = g.Platform,
                coverImage = coverUrl,
                genre = g.Genre,
                playTime = g.PlayTime,
                lastPlayed = g.LastPlayed?.ToString("o"),
                steamAppId = g.SteamAppId
            });
        }

        var json = JsonSerializer.Serialize(new
        {
            type = "game_list",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data = gameList
        }, _jsonOptions);

        await socket.Send(json);
        Log?.Invoke($"[MobileServer] Enviou {gameList.Count} jogos para o celular");

        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var remaining = unresolvedCovers.Take(10).ToList();
                foreach (var cover in remaining)
                {
                    if (cts.Token.IsCancellationRequested) break;
                    await DownloadAndCacheCoverAsync(cover.GameId, cover.CoverUrl, cover.GameName);
                }
            }
            catch { }
        });
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

        if (achievements.Count == 0 && _achievementService != null)
        {
            var game = await _database.GetGameAsync(gameId);
            if (game != null)
            {
                Log?.Invoke($"[MobileServer] Coletando conquistas para '{game.Name}'...");
                achievements = await _achievementService.GetAchievementsAsync(game);
                if (achievements.Count > 0)
                    Log?.Invoke($"[MobileServer] {achievements.Count} conquistas coletadas para '{game.Name}'");
            }
        }

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
                usedGb = Math.Round(s.UsedRam, 1),
                totalGb = Math.Round(s.TotalRam, 1),
                freeGb = Math.Round(s.RamFree, 1),
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
            }).ToList() ?? new List<object>(),
            gamepads = s.Gamepads?.Where(g => g.IsConnected).Select(g => (object)new
            {
                name = g.Name,
                isConnected = g.IsConnected,
                batteryType = g.BatteryType.ToString(),
                batteryLevel = g.BatteryLevel.ToString(),
                hasBattery = g.HasBattery
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

    private void StartBeacon()
    {
        _beaconCts?.Cancel();
        _beaconCts = new CancellationTokenSource();
        var token = _beaconCts.Token;

        Task.Run(async () =>
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;

            var machineName = Environment.MachineName;
            var beacon = JsonSerializer.Serialize(new
            {
                app = "EMECore",
                ip = LocalIp,
                port = Port,
                name = machineName
            });
            var data = System.Text.Encoding.UTF8.GetBytes(beacon);

            var broadcastEndpoints = GetBroadcastEndpoints();
            Log?.Invoke($"[MobileServer] Beacon UDP ativo na porta {BeaconPort} para {broadcastEndpoints.Count} sub-redes");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (var ep in broadcastEndpoints)
                    {
                        await udp.SendAsync(data, data.Length, ep);
                    }
                    await Task.Delay(2000, token);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(5000, token); }
            }
        }, token);
    }

    private static List<IPEndPoint> GetBroadcastEndpoints()
    {
        var endpoints = new List<IPEndPoint>();
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = addr.Address.GetAddressBytes();
                    var mask = addr.IPv4Mask.GetAddressBytes();
                    var broadcast = new byte[4];
                    for (int i = 0; i < 4; i++)
                        broadcast[i] = (byte)(ip[i] | ~mask[i]);
                    endpoints.Add(new IPEndPoint(new IPAddress(broadcast), BeaconPort));
                }
            }
        }
        catch { }

        if (endpoints.Count == 0)
            endpoints.Add(new IPEndPoint(IPAddress.Broadcast, BeaconPort));

        return endpoints;
    }

    private void StopBeacon()
    {
        _beaconCts?.Cancel();
        _beaconCts?.Dispose();
        _beaconCts = null;
    }

    private void StartGamepadStreaming()
    {
        _gamepadCts?.Cancel();
        _gamepadCts = new CancellationTokenSource();
        var token = _gamepadCts.Token;
        uint lastPacket = 0;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_clients.IsEmpty)
                    {
                        await Task.Delay(500, token);
                        continue;
                    }

                    var gp = _monitor.GamepadService.GetState(0);
                    if (gp.IsConnected && gp.PacketNumber != lastPacket)
                    {
                        lastPacket = gp.PacketNumber;
                        var msg = JsonSerializer.Serialize(new
                        {
                            type = "gamepad_state",
                            buttons = gp.Buttons,
                            leftTrigger = (int)gp.LeftTrigger,
                            rightTrigger = (int)gp.RightTrigger,
                            thumbLX = (int)gp.ThumbLX,
                            thumbLY = (int)gp.ThumbLY,
                            thumbRX = (int)gp.ThumbRX,
                            thumbRY = (int)gp.ThumbRY,
                            packetNumber = gp.PacketNumber
                        }, _jsonOptions);

                        foreach (var client in _clients.Values)
                        {
                            try { client.Send(msg).Wait(100); } catch { }
                        }
                    }

                    await Task.Delay(33, token);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(1000, token); }
            }
        }, token);
    }

    private void StopGamepadStreaming()
    {
        _gamepadCts?.Cancel();
        _gamepadCts?.Dispose();
        _gamepadCts = null;
    }

    private void StartImageServer()
    {
        try
        {
            Directory.CreateDirectory(CoversCacheDir);
        }
        catch { }

        _httpCts?.Cancel();
        _httpCts = new CancellationTokenSource();
        var token = _httpCts.Token;

        Task.Run(async () =>
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://+:{ImagePort}/");
                _httpListener.Start();

                Log?.Invoke($"[MobileServer] Image server ativo na porta {ImagePort}");

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var context = await _httpListener.GetContextAsync();
                        _ = Task.Run(() => HandleImageRequest(context));
                    }
                    catch (ObjectDisposedException) { break; }
                    catch (HttpListenerException) { break; }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[MobileServer] Erro no image server: {ex.Message}");
            }
        }, token);
    }

    private void HandleImageRequest(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath?.TrimStart('/') ?? "";
            var filePath = Path.Combine(CoversCacheDir, path);

            if (File.Exists(filePath))
            {
                var ext = Path.GetExtension(filePath).ToLower();
                var mime = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                };

                context.Response.ContentType = mime;
                context.Response.Headers.Add("Cache-Control", "public, max-age=86400");
                using var fs = File.OpenRead(filePath);
                fs.CopyTo(context.Response.OutputStream);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }
        catch { context.Response.StatusCode = 500; }
        finally { context.Response.Close(); }
    }

    private void StopImageServer()
    {
        _httpCts?.Cancel();
        _httpCts?.Dispose();
        _httpCts = null;
        try { _httpListener?.Stop(); } catch { }
        try { _httpListener?.Close(); } catch { }
        _httpListener = null;
    }

    private async Task PreloadCoversAsync()
    {
        try
        {
            await Task.Delay(2000);
            if (_database == null) return;

        var games = await _database.GetGamesAsync();
        Log?.Invoke($"[MobileServer] GetGames retornou {games.Count} jogos");
            Log?.Invoke($"[MobileServer] Pre-cache: {games.Count} jogos para verificar");

            var sem = new SemaphoreSlim(4);
            var tasks = games.Select(async g =>
            {
                await sem.WaitAsync();
                try
                {
                    var ext = ".jpg";
                    var fileName = $"{g.Id}{ext}";
                    var filePath = Path.Combine(CoversCacheDir, fileName);

                    if (File.Exists(filePath)) return;

                    var coverUrl = await ResolveCoverForMobileAsync(g);
                    if (!string.IsNullOrEmpty(coverUrl))
                        await DownloadAndCacheCoverAsync(g.Id, coverUrl, g.Name);
                }
                catch { }
                finally { sem.Release(); }
            });

            await Task.WhenAll(tasks);
            var cached = Directory.GetFiles(CoversCacheDir, "*.jpg").Length;
            Log?.Invoke($"[MobileServer] Pre-cache concluido: {cached} capas em cache");
        }
        catch { }
    }

    private async Task<string> DownloadAndCacheCoverAsync(string gameId, string url, string gameName = "")
    {
        var ext = ".jpg";
        var fileName = $"{gameId}{ext}";
        var filePath = Path.Combine(CoversCacheDir, fileName);

        if (File.Exists(filePath))
        {
            var fi = new FileInfo(filePath);
            if (fi.Length > 8000)
                return $"http://{LocalIp}:{ImagePort}/{fileName}";
            else
            {
                try { File.Delete(filePath); } catch { }
            }
        }

        if (string.IsNullOrEmpty(url))
        {
            Log?.Invoke($"[MobileServer] Sem capa: '{gameName}'");
            return "";
        }

        var urlsToTry = new List<string> { url };

        var steamMatch = System.Text.RegularExpressions.Regex.Match(url, @"/apps/(\d+)/");
        if (steamMatch.Success)
        {
            var appId = steamMatch.Groups[1].Value;
            var info = await _steamStore.GetStoreInfoAsync(appId);
            if (info != null && !string.IsNullOrEmpty(info.HeaderImage) && info.HeaderImage != url)
                urlsToTry.Add(info.HeaderImage);
        }

        foreach (var tryUrl in urlsToTry)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var response = await _http.GetAsync(tryUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);
                    if (bytes.Length < 8000) continue;

                    await File.WriteAllBytesAsync(filePath, bytes);
                    Log?.Invoke($"[MobileServer] Capa: '{gameName}' -> {fileName} ({bytes.Length}b)");
                    return $"http://{LocalIp}:{ImagePort}/{fileName}";
                }
            }
            catch (TaskCanceledException) { }
            catch (HttpRequestException) { }
        }

        Log?.Invoke($"[MobileServer] Sem capa: '{gameName}'");
        return "";
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

    private async Task<string> ResolveCoverForMobileAsync(Game g)
    {
        var cover = g.CoverImage ?? "";

        if (!string.IsNullOrEmpty(cover) && cover.StartsWith("http"))
            return cover;

        if (!string.IsNullOrEmpty(cover) && cover.StartsWith("file:///"))
        {
            var localPath = cover.Substring(8).Replace('/', '\\');
            if (File.Exists(localPath))
            {
                try
                {
                    var fi = new FileInfo(localPath);
                    if (fi.Length > 10240)
                    {
                        var ext = fi.Extension.ToLower();
                        var fileName = $"{g.Id}{ext}";
                        var destPath = Path.Combine(CoversCacheDir, fileName);
                        File.Copy(localPath, destPath, true);
                        return $"http://{LocalIp}:{ImagePort}/{fileName}";
                    }
                }
                catch { }
            }
        }

        if (!string.IsNullOrEmpty(g.SteamAppId))
        {
            var info = await _steamStore.GetStoreInfoAsync(g.SteamAppId);
            if (info != null && !string.IsNullOrEmpty(info.HeaderImage))
                return info.HeaderImage;
        }

        if (!string.IsNullOrEmpty(g.Name))
        {
            var appId = await _steamStore.SearchAppIdAsync(g.Name);
            if (!string.IsNullOrEmpty(appId))
            {
                var info = await _steamStore.GetStoreInfoAsync(appId);
                if (info != null && !string.IsNullOrEmpty(info.HeaderImage))
                    return info.HeaderImage;
            }

            var encoded = Uri.EscapeDataString(g.Name);
            return $"https://static-cdn.jtvnw.net/ttv-boxart/{encoded}-285x380.jpg";
        }

        return "";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
