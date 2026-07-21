# E.M.E Core — documentação técnica

Documentação oficial revisada contra o código em 20/07/2026. Antes de modificar o projeto, leia `../AGENTS.md` e o documento ligado à área afetada.

| Arquivo | Conteúdo atual |
|---|---|
| [architecture.md](architecture.md) | Camadas, inicialização, navegação e serviços do desktop. |
| [core-project.md](core-project.md) | Modelos, contratos e regras da camada de domínio. |
| [database.md](database.md) | SQLite, biblioteca, scanner, capas e gêneros. |
| [hardware-project.md](hardware-project.md) | LHM/WMI, FPS, conquistas, saves e integrações. |
| [winui-project.md](winui-project.md) | Ciclo de vida e composição da aplicação WinUI. |
| [ui-components.md](ui-components.md) | Views e telas que existem hoje. |
| [viewmodel.md](viewmodel.md) | Estado, comandos e fluxos de `MainViewModel`. |
| [theme.md](theme.md) | Tema e preferências visuais. |
| [build-setup.md](build-setup.md) | Build, execução, integração e diagnóstico. |
| [known-issues.md](known-issues.md) | Limitações e cuidados reais conhecidos. |
| [stellar-blade-*.md](stellar-blade-parser.md) | Pesquisa específica do parser Stellar Blade; validar contra o código antes de modificar. |

## Integração mobile

O desktop fornece WebSocket em `8181`, descoberta UDP em `8182` e capas HTTP em `8183`. O contrato, segurança e compatibilidade são documentados em [architecture.md](architecture.md) e [hardware-project.md](hardware-project.md).

Os arquivos foram atualizados diretamente nesta pasta; não há uma segunda árvore de documentação.
