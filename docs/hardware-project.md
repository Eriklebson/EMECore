# EMECore.Hardware - Implementacoes de Servicos

## Visao Geral

Projeto class library (`net8.0`) contendo as implementacoes concretas dos servicos definidos no Core. Referencia `EMECore.Core` para acessar modelos e interfaces.

**Localizacao:** `src/EMECore.Hardware/`
**Dependencias:** `EMECore.Core`, `Microsoft.Data.Sqlite 8.0.0`, `System.Text.Json 8.0.5`, `System.Management 8.0.0`

---

## Servicos Implementados

### DatabaseService (`Services/DatabaseService.cs`)

Implementacao completa de `IDatabaseService` usando SQLite.

**Detalhes:**
- Conexao: `SqliteConnection` com caminho customizavel
- Journal mode: WAL (Write-Ahead Logging) para performance
- Foreign keys habilitadas
- Estrategia de upsert: `INSERT OR REPLACE`
- Estrategia de achievements: full replace (deleta todos, insere novos)

**Tabelas criadas em `InitializeAsync`:**
- `games` - Tabela principal de jogos
- `achievements` - Conquistas (FK para games com CASCADE)
- `play_sessions` - Sessoes de jogo (FK para games com CASCADE)

Ver [database.md](database.md) para schema completo.

---

### SteamStoreService (`Services/SteamStoreService.cs`)

Implementacao de `ISteamStoreService` usando a API publica da Steam Store.

**Detalhes:**
- HTTP client estatico (compartilhado entre chamadas)
- Cache em memoria com TTL de 6 horas
- Endpoint: `https://store.steampowered.com/api/appdetails?appids={appId}`
- Parsing com `System.Text.Json.JsonDocument`
- Falha silenciosa: retorna `null` em qualquer erro

**Fluxo:**
1. Verifica cache (se existe e nao expirou, retorna)
2. Faz requisicao HTTP GET
3. Parseia JSON: `root[appId].data.{name, header_image, short_description}`
4. Armazena no cache e retorna
5. Em erro: retorna `null`

---

### GameScannerService (`Services/GameScannerService.cs`)

Implementacao de `IGameScannerService` que descobre jogos instalados.

**Estrategias de scanning:**

#### 1. ScanSteamAsync()
- Percorre paths conhecidos de Steam:
  - `C:\Program Files (x86)\Steam\steamapps`
  - `D:\Steam\steamapps`
  - `E:\Steam\steamapps`
  - `C:\SteamLibrary\steamapps`
- Busca arquivos `appmanifest_*.acf`
- Extrai `name`, `appid`, `installdir` via regex (VDF parsing)
- Constroi caminho de instalacao

#### 2. ScanCommonDirsAsync()
- Busca recursiva por `*.exe` em:
  - `C:\Games`
  - `D:\Games`
  - `E:\Games`
- Cada .exe encontrado e tratado como potencial jogo

#### Helper: ExtractVdfValue()
- Parsing regex de pares chave-valor no formato Valve Data Format (VDF)

---

## Servicos Stub (Nao Implementados)

### AchievementService (`Services/AchievementService.cs`)
- `CheckAchievementsAsync(gameId)` → retorna `List<Achievement>` vazia
- **Intencao:** Chamar API Steam `ISteamUserStats` para buscar conquistas

### FpsMonitorService (`Services/FpsMonitorService.cs`)
- `StartMonitoring()` → vazio
- `StopMonitoring()` → vazio
- `GetFps()` → retorna `0`
- **Intencao:** Monitorar FPS de jogos em execucao

### HardwareMonitorService (`Services/HardwareMonitorService.cs`)
- `GetCpuUsage()` → retorna `0`
- **Intencao:** Usar WMI (`System.Management`) para medir uso de CPU

### SensorService (`Services/SensorService.cs`)
- `GetCpuTemperature()` → retorna `0`
- `GetGpuTemperature()` → retorna `0`
- **Intencao:** Ler temperaturas de hardware via sensores

### StellarBladeParser (`Services/StellarBladeParser.cs`)
- `HasSave()` → retorna `false`
- `ParseSaveAsync()` → retorna `List<Achievement>` vazia
- **Intencao:** Parse do save file local do Stellar Blade para extrair conquistas

---

## Observacoes

- Todos os stubs estao com implementacao minima (retornam valores padrao)
- Nenhum stub esta sendo chamado pela UI atualmente
- A implementacao futura dos stubs requererá novos pacotes NuGet (ex: `OpenHardwareMonitor` para sensores)
- `System.Management` ja esta referenciado para WMI mas nao esta em uso
