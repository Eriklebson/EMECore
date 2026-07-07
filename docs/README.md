# E.M.E Core - Documentacao do Sistema

## Visao Geral

**E.M.E Core** e um launcher de jogos desktop inspirado no Steam, construido com C# WinUI 3 e .NET 8.0. O projeto foi migrado de um launcher Electron/React para uma aplicacao nativa Windows com tema escuro estilo Steam.

**Stack tecnico:**
- C# / .NET 8.0.422 (SDK fixada via `global.json`)
- WinUI 3 (WindowsAppSDK 1.8.250907003)
- SQLite (Microsoft.Data.Sqlite 8.0.0)
- MVVM (CommunityToolkit.Mvvm 8.4.2)
- Portuguese (pt-BR) como idioma principal

**Localizacao do banco:** `%LocalAppData%\EMECore\eme_core.db`

---

## Indice de Documentacao

| Arquivo | Descricao |
|---------|-----------|
| [architecture.md](architecture.md) | Arquitetura, estrutura de pastas, diagrama de dependencias |
| [build-setup.md](build-setup.md) | Configuracao de build, SDK, problemas conhecidos, workarounds |
| [core-project.md](core-project.md) | EMECore.Core - Modelos, interfaces, helpers |
| [hardware-project.md](hardware-project.md) | EMECore.Hardware - Implementacoes de servicos |
| [winui-project.md](winui-project.md) | EMECore.WinUI - Camada de aplicacao, entry point, MainWindow |
| [ui-components.md](ui-components.md) | Componentes de UI - Views, sidebar, cards, paginas |
| [theme.md](theme.md) | Sistema de tema Steam (cores, brushes, resources) |
| [database.md](database.md) | Schema SQLite, operacoes CRUD, tabelas |
| [viewmodel.md](viewmodel.md) | MainViewModel - Logica MVVM, comandos, navegacao |
| [known-issues.md](known-issues.md) | Limitacoes atuais, workarounds aplicados, stubs pendentes |

---

## Arquitetura Resumida

```
EMECore.WinUI (WinExe - .NET 8.0-windows10.0.26100.0)
  ├── EMECore.Core (Class Library - net8.0)
  └── EMECore.Hardware (Class Library - net8.0)
        └── EMECore.Core
```

- **Core**: Define modelos de dados e interfaces de servicos (sem implementacao)
- **Hardware**: Implementa servicos concretos (SQLite, Steam API, scanning)
- **WinUI**: Aplicacao desktop com UI pura em C# (sem XAML)

---

## Status Atual

| Funcionalidade | Status |
|----------------|--------|
| Build (0 erros) | Funcionando |
| Execucao (app abre, UI aparece) | Funcionando |
| Sidebar com navegacao | Funcionando |
| Biblioteca com grid de jogos | Funcionando |
| Cards de jogos com play time | Funcionando |
| Pagina de detalhes do jogo | Funcionando |
| Formulario adicionar jogo | Funcionando |
| File picker (.exe) | Funcionando |
| Scanning de jogos (Steam + diretorios) | Funcionando |
| Banco SQLite (CRUD completo) | Funcionando |
| Steam Store API (capa, descricao) | Funcionando |
| Monitoramento de hardware | Stub (retorna 0) |
| FPS monitor | Stub (retorna 0) |
| Achievement checker | Stub (retorna vazio) |
| Parser Stellar Blade | Stub (retorna vazio) |
