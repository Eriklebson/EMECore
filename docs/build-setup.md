# Configuracao de Build e Ambiente

## Requisitos

| Componente | Versao | Observacao |
|------------|--------|------------|
| .NET SDK | 8.0.422 | Fixada via `global.json` com `rollForward: latestPatch` |
| Windows SDK | 10.0.26100.0 | Necessario para `TargetFramework` `net8.0-windows10.0.26100.0` |
| WindowsAppSDK | 1.8.250907003 | Via NuGet `Microsoft.WindowsAppSDK` |
| Visual Studio | 2022+ | Para IDE (build funciona via CLI) |

## Comando de Build

```powershell
# Da raiz do projeto (EMECore/)
dotnet build EMECore.sln -c Debug -p:Platform=x64

# Ou com cultura en-US (workaround para bugs de localizacao)
powershell -ExecutionPolicy Bypass -File build-en.ps1
```

## Configuracao do Projeto WinUI

| Propriedade | Valor | Observacao |
|------------|-------|------------|
| `TargetFramework` | `net8.0-windows10.0.26100.0` | Usa SDK 26100 (nao 22621) |
| `WindowsPackageType` | `None` | App DESPACOTADO (unpackaged) |
| `EnableMsixTooling` | `false` | Nao usa MSIX |
| `CsWinRTEnabled` | `true` | Habilita projecoes CsWinRT |
| `UseWinUI` | `true` | Habilita componentes WinUI 3 |
| `Nullable` | `enable` | Referencias nullable habilitadas |
| `ImplicitUsings` | `enable` | Using global implicitos |
| `Platforms` | `x86;x64;ARM64` | Multiplas plataformas |
| `RuntimeIdentifiers` | `win-x86;win-x64;win-arm64` | RIDs para publicacao |

### Publicacao

| Config | Propriedade | Valor |
|--------|------------|-------|
| Debug | `PublishReadyToRun` | `False` |
| Release | `PublishReadyToRun` | `True` |
| Debug | `PublishTrimmed` | `False` |
| Release | `PublishTrimmed` | `True` |

## Pacotes NuGet

### EMECore.Core
| Pacote | Versao | Uso |
|--------|--------|-----|
| `CommunityToolkit.Mvvm` | 8.4.2 | MVVM source generators |

### EMECore.Hardware
| Pacote | Versao | Uso |
|--------|--------|-----|
| `Microsoft.Data.Sqlite` | 8.0.0 | Acesso ao banco SQLite |
| `System.Text.Json` | 8.0.5 | Parsing JSON (Steam API) |
| `System.Management` | 8.0.0 | WMI (monitoramento hardware) |

### EMECore.WinUI
| Pacote | Versao | Uso |
|--------|--------|-----|
| `Microsoft.Windows.SDK.BuildTools` | 10.0.28000.2270 | Build tools Windows SDK |
| `Microsoft.WindowsAppSDK` | 1.8.250907003 | Runtime WinUI 3 |
| `CommunityToolkit.Mvvm` | 8.4.2 | MVVM no ViewModel |
| `Microsoft.Windows.CsWinRT` | 2.1.3 | Projecoes WinRT |

## Workarounds Aplicados

### 1. XAML Compiler Crash
**Problema:** `XamlCompiler.exe` (net472) do WindowsAppSDK crasha com exit code 1 quando processa XAML com .NET 10 SDK.

**Solucao:** Todos os arquivos `.xaml` foram removidos. UI construida 100% em codigo C#. Stub vazio de `InitializeComponent()` em `App.g.cs`.

### 2. CsWinRT cswinrt.exe Crash
**Problema:** `cswinrt.exe` e uma ferramenta net472 que nao funciona com .NET 10 SDK.

**Solucao:** `global.json` fixa SDK para 8.0.422. Projeto targeta `net8.0-windows10.0.26100.0`.

### 3. WindowsAppSDK Auto-Initializer Crash
**Problema:** `DeploymentManagerAutoInitializer` (module initializer) tenta criar objetos WinRT antes do COM estar registrado, causando `REGDB_E_CLASSNOTREG`.

**Solucao:** `WindowsPackageType=None` faz o Bootstrap (DynamicDependency) carregar o runtime, em vez do DeploymentManager.

### 4. XamlControlsResources Key Not Found
**Problema:** `AcrylicBackgroundFillColorDefaultBrush` nao existe no WindowsAppSDK 1.8.

**Solucao:** `XamlControlsResources` removido do `SteamColors.ApplyToApplication()`. Nao e necessario para UI pura em C#.

### 5. Platform.xml Missing
**Problema:** `cswinrt.exe` precisa de `Platform.xml` em `UAP\10.0.22621.0`, mas apenas 10.0.26100.0 estava instalado.

**Solucao:** `fix_sdk.cmd` copia o arquivo via PowerShell elevado. Target alterado para 10.0.26100.0.

## Arquivos de Build

| Arquivo | Funcao |
|---------|--------|
| `global.json` | Fixa .NET SDK 8.0.422 |
| `Directory.Build.props` | Define `UICulture=en-US` para todos os projetos |
| `build-en.ps1` | Build com cultura en-US forçada |
| `fix_sdk.cmd` | Copia Platform.xml entre versoes do SDK |
| `Directory.Build.targets` | Vazio (overrides do XAML compiler removidos) |
| `app.manifest` | DPI awareness PerMonitorV2, compat Windows 10 |
