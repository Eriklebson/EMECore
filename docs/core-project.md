# EMECore.Core — domínio e contratos

`src/EMECore.Core` é uma biblioteca `net8.0` sem dependência de WinUI ou infraestrutura. Ela contém modelos, interfaces e helpers; `EMECore.Hardware` implementa seus contratos e `EMECore.WinUI` os consome.

## Modelos relevantes

| Modelo | Responsabilidade |
|---|---|
| `Game` | Biblioteca: executável, plataforma, capa, Steam App ID, gênero e tempo de jogo. |
| `Achievement` / `AchievementDefinition` | Conquistas, estado, ícones e progresso. |
| `HardwareStats` | Snapshot de CPU, GPU, RAM, FPS, disco, rede, placa-mãe, fans e gamepads. |
| `ScannedGame` | Resultado temporário do scanner. |
| `PlaySession` | Histórico de execução. |
| `GameSaveInfo`, `StellarBladeSaveData` | Dados de saves e parsers. |
| `SteamStoreInfo` | Metadados da Steam Store (requisitos, descricao). |

`Game` usa `Id` como GUID string e `PlayTime` em minutos. Campos novos que precisem ir ao banco devem ser adicionados também à migração e aos mapeamentos de `DatabaseService`.

## Interfaces

| Interface | Implementação principal |
|---|---|
| `IDatabaseService` | `DatabaseService` |
| `IGameScannerService` | `GameScannerService` |
| `ISteamStoreService` | `SteamStoreService` |
| `IAchievementProvider` | Providers de conquista/saves |
| `ISaveDiscoveryService` | `SaveDiscoveryService` |

## Convenções

- Não adicionar dependência de Windows, SQLite ou rede em `Core`.
- Usar `FormatHelpers` para formatos reutilizáveis.
- Interfaces representam contrato público: alteração exige revisar implementações, WinUI, testes e protocolo mobile quando aplicável.

