# 4. Hardware, conquistas e saves

## Monitoramento de hardware

Arquivos relevantes: `HardwareMonitorService.cs`, `LhmHardwareService.cs`, `MappingService.cs`, `CpuSensorMappingService.cs` e `HardwareStats.cs`.

O projeto usa arquitetura de duas camadas:

| Fonte | Dados | Regra de uso |
|---|---|---|
| LibreHardwareMonitor (LHM) | Carga/temperatura/tensão/potência de CPU e GPU, fans e sensores dinâmicos | Usar nas coletas frequentes. Pode requerer elevação para SuperIO/placa-mãe. |
| WMI | Modelo de CPU/GPU/RAM, BIOS, disco, rede e informações pouco mutáveis | Usar de forma pontual ou com cache; nunca como polling em tempo real. |

No servidor mobile, `CollectFast()` entrega dados dinâmicos e `Collect()` renova WMI no máximo a cada 5 s. Não mover WMI para a atualização de 1 s do aplicativo móvel.

Os mapeamentos de sensores ficam em `config/hardware-mapping.json` e `config/cpu-sensors-mapping.json`, copiados para a saída. Para adaptar suporte a uma placa/sensor, priorizar esses arquivos antes de condicional específica no código.

## FPS, gamepad e periféricos

`FpsMonitorService` fornece FPS e métricas como lows e frame time; `FpsOverlayWindow` apresenta o overlay. `GamepadService`, `GamepadLayoutService`, `GamepadCalibrationWindow` e `PeripheralBatteryService` tratam controles e bateria. O layout calibrado do controle é persistido em `%LocalAppData%\EMECore\config\gamepad-layout.json`, evitando gravação na pasta de instalação. A UI deve lidar com hardware ausente e valores não disponíveis sem exceção.

Ao alterar coleta de hardware, verificar impacto no monitor desktop e no objeto `MapHardwareStats` do servidor mobile.

## Conquistas

Arquivos-chave: `AchievementService.cs`, `AchievementCheckerService.cs`, `AchievementImageService.cs`, `SaveBasedAchievementProvider.cs` e `AchievementNotificationWindow.cs`.

As conquistas podem vir da Steam ou de parsers de save. O modelo contém identificador (`Apiname`), nome, descrição, ícones, estado, progresso e máximo. A persistência é por jogo no SQLite.

Na página de detalhes, o desktop pode buscar um `SteamAppId` ausente, obter requisitos da Steam, carregar conquistas e comparar o resultado anterior para gerar notificações novas. Não assumir que todo jogo possui SteamAppId, parser ou ícone disponível.

## Saves e parsers

Arquivos: `SaveParserService.cs`, `SaveMonitorService.cs`, `LocalizedPaths.cs` e parsers específicos como `StellarBladeParser`, `EldenRingSaveParser`, `CyberpunkSaveParser`, `Witcher3SaveParser`, `SkyrimSaveParser`, `RDR2SaveParser`, `GodOfWarSaveParser` e outros.

`SaveMonitorService` é iniciado somente quando o jogo selecionado tem parser registrado. Ao trocar/sair da tela de detalhe, o monitor anterior é interrompido. Desbloqueios são enviados para a janela principal e exibidos em `AchievementNotificationWindow`.

### Checklist para novo parser

1. Identificar formato, caminho e condições seguras de leitura do save.
2. Criar parser seguindo as interfaces/serviços existentes.
3. Registrar parser no orquestrador de conquistas.
4. Usar `LocalizedPaths` quando o caminho depender do idioma/localização do Windows.
5. Testar arquivo ausente, arquivo em escrita, dados inválidos e save válido.
6. Confirmar persistência, notificação e resposta ao mobile.
7. Adicionar configuração em `EMECore.WinUI/config/achievements/` quando aplicável.

## Servidor para o aplicativo móvel

`MobileServerService` fica nesta camada e é iniciado pela janela principal. O desktop é a fonte de verdade; o Flutter não acessa SQLite diretamente.

| Canal | Porta | Responsabilidade |
|---|---:|---|
| WebSocket | `8181` configurável | `get_hardware`, `get_games`, `launch_game`, `get_achievements` e `ping`. |
| UDP | `8182` | Beacon a cada 2 s para descoberta automática. |
| HTTP | `8183` | Capas em `%LocalAppData%\EMECore\CoverCache`. |

Mensagens usam JSON com `type` em camelCase. Respostas principais: `welcome`, `hardware_stats`, `gamepad_state`, `game_list`, `achievements`, `game_launched`, `pong` e `error`. `gamepad_state` é enviado somente quando o `PacketNumber` muda, com intervalo alvo de 33 ms e pausa de 500 ms sem clientes. Ao mudar campos, atualizar simultaneamente `MobileServerService`, os modelos Dart e `WebSocketService` do projeto mobile; preferir campos opcionais para manter compatibilidade entre versões.
