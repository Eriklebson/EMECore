# Problemas Conhecidos e Limitacoes

## Workarounds Ativos

### 1. XAML Compiler Funcional (Corrigido - Julho 2026)
- **Problema original:** `XamlCompiler.exe` (net472) crashava com .NET 10 SDK
- **Solucao atual:** App.xaml restaurado, XAML compiler funciona com .NET 8 SDK (8.0.422)
- **Arquivos corrigidos:** `App.xaml` criado, `App.g.cs` stub removido (gerado pelo compiler)
- **Program.cs** removido (Main agora e gerado pelo XAML compiler em `App.g.i.cs`)

### 2. CsWinRT COM Wrappers (Corrigido - Julho 2026)
- **Problema original:** `WinRT.ComWrappersSupport.InitializeComWrappers()` nao era chamado
- **Solucao:** XAML compiler gera automaticamente o Main com `InitializeComWrappers()`
- **Impacto:** COM wrappers do WinRT inicializados corretamente, resolvendo crash 0xc000027b

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
| .NET 9/10 LTS incompativel com CsWinRT | Medio | `global.json` fixa SDK em 8.0.422 |
| SQLite corrompido por crash | Medio | WAL mode + backup |
| Steam API muda formato | Medio | Tratamento de erros silencioso |
| Memory leak por nao-dispose | Baixo | `DatabaseService.CloseAsync()` nao chamado no exit |
