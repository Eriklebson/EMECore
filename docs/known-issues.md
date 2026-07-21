# Limitações e cuidados conhecidos

## Build e execução

- Feche `EMECore.WinUI.exe` antes de `dotnet clean` ou build. Limpar com o processo aberto pode remover `runtimeconfig.json` e causar falha `hostpolicy.dll not found`.
- WinUI pode fechar com `0xc000027b`/`Microsoft.UI.Xaml.dll` por exceção de UI. Validar em runtime e consultar Event Viewer; controles animados de compositor exigem cautela.
- O app pode solicitar elevação para acessar sensores de placa-mãe/SuperIO. Contextos MSIX têm tratamento específico para evitar loop de UAC.

## Hardware e rede

- WMI é lento; não usar para atualização contínua. O servidor mobile usa LHM por pedido e cache WMI de 5 s.
- Leitura de disco pode aparecer como zero em NVMe por cache do Windows.
- Descoberta mobile requer UDP 8182 e o acesso remoto requer TCP 8181/8183 liberados na rede local.
- O serviço mobile não possui autenticação nem TLS; não expor à internet.

## Dados e integrações

- Steam, RAWG, Twitch e downloads de capa são externos e podem falhar; manter fallback e não bloquear a UI.
- Capas para o celular dependem do cache e do HTTP 8183; lista de jogos deve continuar útil sem imagem.
- Parsers de save devem tolerar arquivo ausente, bloqueado ou versão desconhecida sem corromper dados.

Os itens acima são limitações reais; os documentos históricos que chamam monitor, FPS ou conquistas de “stubs” não descrevem mais o sistema atual.
