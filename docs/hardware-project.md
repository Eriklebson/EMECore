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

Para periféricos Logitech, `PeripheralBatteryService` combina a API de bateria do Windows com HID++. O PRO X Superlight 2 (`046D:C54D`) usa a coleção longa (`FF00:0002`, relatório `0x11`), o dispositivo `0x01` e o recurso Unified Battery `0x1004`. O MX Ergo S (`046D:C548`) usa a mesma família de transporte pelo receptor Bolt, no dispositivo `0x02`. As consultas possuem timeout, cache e serialização para não travar o Hardware Monitor.

As definições de modelos ficam em `config/peripheral-devices.json`. Cada entrada informa VID/PID, nome, tipo, provedor, coleção HID, endereço, comando de bateria, polling e calibração. `PeripheralDeviceCatalog` carrega o arquivo uma única vez e `PeripheralBatteryService` agrupa as definições por coleção antes de enumerar o Windows, evitando uma varredura separada por modelo.

Provedores ativos nesta versão:

| Provedor | Uso |
|---|---|
| `windows-battery` | Fallback universal para dispositivos que publicam bateria pela API do Windows/Bluetooth. |
| `logitech-feature-index` | Dispositivos HID++ 2 que descobrem o recurso Unified Battery, incluindo os receptores LIGHTSPEED e Bolt validados. |
| `logitech-voltage` | Headsets Logitech que retornam tensão por comando direto; aceita polinômio ou pontos de calibração definidos no JSON. |

O catálogo contém 38 definições de família, 84 identificadores VID/PID e oito fabricantes USB: Logitech, SteelSeries, Corsair, HyperX/HP, Kingston, Audeze, Sony e Lenovo. Dezesseis identificadores Logitech estão ativos; os outros ficam com `enabled: false` e `supportStatus: cataloged-parser-pending` até que seu transporte seja implementado no E.M.E Core. Cada grupo pesquisado registra também sua fonte e a data da base consultada. Um modelo só deve ser ativado quando coleção, comando e interpretação de resposta forem conhecidos; adicionar somente VID/PID não produz uma leitura confiável.

O MX Ergo S é coletado separadamente pelo receptor Logi Bolt `046D:C548`, coleção longa, dispositivo pareado `0x02` e recurso Unified Battery `0x1004`. Nesta etapa não são varridos outros índices do Bolt, evitando atrasos e cards de dispositivos inexistentes.

O indicador persistente de polling pertence somente a dispositivos cuja leitura dessa capacidade foi implementada e validada. O MX Ergo S não apresenta `Detectando...` enquanto apenas sua bateria estiver no escopo.

Quando conectado pelo receptor Logi Bolt, o MX Ergo S apresenta `Polling rate: 125 Hz`, conforme a taxa wireless de 8 ms especificada pela Logitech. Esse valor representa o modo Bolt identificado pelo PID `C548`; uma futura conexão Bluetooth deve usar sua própria faixa de atualização.

O PRO X Wireless Gaming Headset original (`046D:0ABA`) usa uma rota HID++ própria na coleção `FF43:0202`, diferente das coleções dos mouses e do transporte Centurion dos modelos PRO X 2. A leitura envia o comando direto `11 FF 06 0D` e recebe a tensão da bateria em milivolts. O percentual é estimado pela equação polinomial original de descarga do PRO X, com resultado limitado entre 0% e 100%; o estado `0x03` é apresentado como carregando.

Na aba Periféricos, cada card identifica visualmente seu tipo com ícones nativos do Windows: mouse para dispositivos apontadores, headset para o PRO X e controle para gamepads. Dispositivos ainda não classificados recebem um ícone genérico de entrada.

Os cards de mouse, headset e demais periféricos de bateria são organizados em duas colunas quando a área disponível possui pelo menos 680 pixels. Em larguras menores, a grade muda automaticamente para uma coluna, mantendo nomes, estados e barras de bateria legíveis. O card detalhado do controle permanece fora dessa grade e conserva sua largura integral.

No Superlight 2, uma falha temporária de leitura não substitui o último percentual válido. O fallback de bateria `0x1000` não deve ser usado nesse modelo, pois uma resposta atrasada de descoberta pode fazer os bytes de capacidades serem confundidos com porcentagem. A taxa de polling é consultada separadamente pelos recursos `0x8060`/`0x8061` e só deve ser apresentada quando o intervalo retornado puder ser validado.

A linha de polling permanece sempre visível no card do periférico. Enquanto não houver uma resposta válida, a interface apresenta `Polling rate: Detectando...`; depois da primeira leitura válida, o valor em Hz é preservado em cache durante falhas temporárias.

No recurso estendido `0x8061`, a função `1` retorna apenas a máscara de taxas suportadas. A taxa ativa deve ser consultada pela função `2`, informando a conexão gaming wireless (`1`), e o enum retornado é convertido para 125, 250, 500, 1000, 2000, 4000 ou 8000 Hz.

`FpsMonitorService` fornece FPS e métricas como lows e frame time; `FpsOverlayWindow` apresenta o overlay. `GamepadService`, `GamepadLayoutService`, `GamepadCalibrationWindow` e `PeripheralBatteryService` tratam controles e bateria. O layout calibrado do controle é persistido em `%LocalAppData%\EMECore\config\gamepad-layout.json`, evitando gravação na pasta de instalação. A UI deve lidar com hardware ausente e valores não disponíveis sem exceção.

A montagem inicial do `MonitorWindow` é protegida contra falhas. Erros inesperados não podem manter indefinidamente a mensagem de carregamento: a janela apresenta um aviso e registra os detalhes em `%LocalAppData%\EMECore\Logs\hardware-monitor.log`. Arquivos de diagnóstico nunca devem ser gravados na pasta do executável, pois instalações em diretórios protegidos podem bloquear a escrita.

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
