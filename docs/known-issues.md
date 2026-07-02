# Problemas Conhecidos e Limitacoes

## Workarounds Ativos

### 1. XAML Compiler Totalmente Bypassed
- **Problema:** `XamlCompiler.exe` (net472) crasha com .NET 10 SDK
- **Solucao:** Todos `.xaml` removidos, UI 100% em codigo C#
- **Impacto:** Nao e possivel usar XAML bindings, styles, ou data templates
- **Arquivo afetado:** `App.g.cs` (stub vazio de `InitializeComponent`)

### 2. CsWinRT Source Generator Limitado
- **Problema:** `cswinrt.exe` nao gera `WinRT.ComWrappers.InitializeComWrappers()`
- **Solucao:** `RoInitialize(2)` via P/Invoke em `Program.cs`
- **Impacto:** Inicializacao COM manual, sem beneficios do CsWinRT auto-init

### 3. WindowsAppSDK Auto-Initializer Desabilitado
- **Problema:** `DeploymentManagerAutoInitializer` crasha com `REGDB_E_CLASSNOTREG`
- **Solucao:** `WindowsPackageType=None` + Bootstrap (DynamicDependency)
- **Impacto:** App roda como unpackaged, sem registro de COM server

### 4. XamlControlsResources Removido
- **Problema:** `AcrylicBackgroundFillColorDefaultBrush` nao existe no WindowsAppSDK 1.8
- **Solucao:** Removido de `SteamColors.ApplyToApplication()`
- **Impacto:** Controles WinUI usam estilo padrao, nao customizado

---

## Stubs Nao Implementados

| Servico | Metodo | Retorno Atual | Intencao |
|---------|--------|---------------|----------|
| `AchievementService` | `CheckAchievementsAsync` | `List vazia` | API Steam ISteamUserStats |
| `FpsMonitorService` | `StartMonitoring` | Vazio | Monitorar FPS de jogos |
| `FpsMonitorService` | `StopMonitoring` | Vazio | Parar monitoramento |
| `FpsMonitorService` | `GetFps` | `0` | Retornar FPS atual |
| `HardwareMonitorService` | `GetCpuUsage` | `0` | WMI para uso de CPU |
| `SensorService` | `GetCpuTemperature` | `0` | Ler temperatura CPU |
| `SensorService` | `GetGpuTemperature` | `0` | Ler temperatura GPU |
| `StellarBladeParser` | `HasSave` | `false` | Verificar save file |
| `StellarBladeParser` | `ParseSaveAsync` | `List vazia` | Parse do save |

**Nenhum stub esta sendo chamado pela UI.** Implementacao futura requer:
- API key da Steam Web API (para achievements)
- Library de monitoramento (para FPS/sensores)
- Conhecimento do formato de save do Stellar Blade

---

## Limitacoes de UI

### Sem XAML Bindings
- Todas as atualizacoes de UI sao feitas via chamadas diretas de metodos
- Nao ha data binding entre ViewModel e Views
- `INotifyPropertyChanged` e usado mas nao via binding

### Sem Animacoes
- Nao ha transicoes ou animacoes entre paginas
- Mudanca de page e instantanea (visibility toggle)

### Sem Imagens de Capa
- `GameCard` mostra um placeholder com icone Unicode
- `CoverImage` e salvo no banco mas nao carregado na UI
- Implementacao requereria `BitmapImage` + download/cache de imagens

### Sem Lazy Loading
- Todos os jogos sao carregados na memoria de uma vez
- Para bibliotecas grandes, pode causar lentidao

---

## Limitacoes de scanning

### Steam Scanner
- Apenas 4 paths fixos sao verificados
- Nao le `libraryfolders.vdf` corretamente (caminhos customizados ignorados)
- Apenas `.acf` files sao parseados

### Common Dirs Scanner
- Apenas 3 diretorios fixos (`C:\Games`, `D:\Games`, `E:\Games`)
- Qualquer .exe e tratado como jogo (falsos positivos)
- Sem verificacao de se o executavel e realmente um jogo

---

## Duplicacoes de Codigo

### ColorFromHex / ParseColor
- `SteamColors.ColorFromHex()` e `MainWindow.ParseColor()` sao funcoes identicas
- Deveria ser consolidado em um local so

### Play Time Formatting
- Formatacao de tempo aparece em 3 lugares:
  - `FormatHelpers.FormatMinutes()` (Core)
  - `GameCard.SetGame()` (inline)
  - `GameDetailPage.LoadGame()` (inline)
- Deveria usar `FormatHelpers` em todos

---

## Riscos

| Risco | Impacto | Mitigacao |
|-------|---------|-----------|
| Atualizacao WindowsAppSDK quebra compatibilidade | Alto | Fixar versao no .csproj |
| .NET 9/10 LTS incompativel com CsWinRT | Alto | `global.json` fixa SDK |
| SQLite corrompido por crash | Medio | WAL mode + backup |
| Steam API muda formato | Medio | Tratamento de erros silencioso |
| Memory leak por nao-dispose | Baixo | `DatabaseService.CloseAsync()` nao chamado no exit |
