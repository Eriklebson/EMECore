# Tema e preferências visuais

Arquivos: `Theme/ThemeManager.cs`, `ThemeVisualTree.cs`, `SteamColors.cs`, `Design.cs`, `AppStyles.cs` e `SettingsService.cs`.

O visual é escuro e alinhado ao Steam. Cores e brushes devem vir de `ThemeManager` e `SteamColors`; não criar cores isoladas nas Views, pois elas não acompanham a troca de tema.

`SettingsService` persiste preferências como tema, posição da janela, categoria/colapso da sidebar e opções de componentes. Ao criar preferência nova: defina uma chave estável, escolha valor padrão seguro, carregue no início e atualize a UI imediatamente.

## Aplicação global

`ThemeManager.SetTheme` é o ponto único de troca. Ele atualiza os tokens compartilhados de `Design.C`, `AppStyles.Colors` e `SteamColors`. Em seguida, `ThemeVisualTree` converte brushes já materializados na árvore visual, inclusive gradientes, bordas, ícones, textos e formas.

A janela principal carrega `SettingsService` e resolve a chave `theme` antes de construir sidebar e páginas. Assim, uma nova execução nasce diretamente com a paleta salva, sem piscar o tema padrão. A troca feita em Configurações é gravada imediatamente no arquivo `%LocalAppData%\EMECore\settings.json`.

O mapeamento abrange biblioteca, cards de jogos, detalhes, inclusão manual, conquistas, Configurações, Monitor de Hardware, calibração do controle, overlay de FPS e notificações de conquista. Cores que comunicam estado físico ou alerta — temperatura, carga, perigo e fabricantes/plataformas — permanecem semânticas quando não representam a identidade do tema.

Os pontos coloridos de pré-visualização dos temas usam a tag `theme-preview` e não devem ser convertidos pela árvore visual.

## Diagnóstico de troca

Falhas durante a aplicação da paleta são rastreadas em `%LocalAppData%\EMECore\logs\theme-change.log`. Cada troca inicia uma sessão com tema e PID; a atualização registra elemento, propriedade e brush imediatamente antes e depois de cada operação. Os registros usam descarga física no disco para permanecer disponíveis mesmo quando o compositor WinUI encerra o processo sem gerar uma exceção gerenciada.

Preservar contraste, estados de hover/foco e o comportamento responsivo da sidebar e do monitor. Mudanças visuais grandes exigem autorização explícita.
