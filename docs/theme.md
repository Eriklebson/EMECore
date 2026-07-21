# Tema e preferências visuais

Arquivos: `Theme/ThemeManager.cs`, `SteamColors.cs`, `Design.cs`, `AppStyles.cs` e `SettingsService.cs`.

O visual é escuro e alinhado ao Steam. Cores e brushes devem vir de `ThemeManager` e `SteamColors`; não criar cores isoladas nas Views, pois elas não acompanham a troca de tema.

`SettingsService` persiste preferências como tema, posição da janela, categoria/colapso da sidebar e opções de componentes. Ao criar preferência nova: defina uma chave estável, escolha valor padrão seguro, carregue no início e atualize a UI imediatamente.

Preservar contraste, estados de hover/foco e o comportamento responsivo da sidebar e do monitor. Mudanças visuais grandes exigem autorização explícita.
