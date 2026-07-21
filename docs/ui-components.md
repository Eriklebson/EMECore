# Componentes de interface WinUI

As Views ficam em `src/EMECore.WinUI/Views`. A maior parte da interface é construída programaticamente em C#, enquanto `App.xaml` continua sendo usado para a aplicação WinUI.

| Componente | Responsabilidade |
|---|---|
| `Sidebar` | Navegação, categoria, configurações, monitor e estado colapsado. |
| `LibraryPage` / `GameCard` | Biblioteca, busca, categorias, scan e início de jogo. |
| `GameDetailPage` | Dados do jogo, requisitos, conquistas, execução e exclusão. |
| `AddGamePage` | Inclusão manual de executável. |
| `AchievementsPage` | Visão de conquistas por jogos. |
| `SettingsPage` | Preferências e troca de tema. |
| `MonitorWindow` | Hardware, gráficos, FPS, fans, discos, rede e gamepads. É uma janela independente comum e não permanece acima de outros aplicativos. |
| `FpsOverlayWindow` | Overlay de FPS. |
| `GamepadCalibrationWindow` | Calibração e layout de controle. |
| `AchievementNotificationWindow` | Notificação de conquista desbloqueada. |

Views expõem eventos para `MainWindow`; `MainWindow` delega comandos à ViewModel/serviços. Manter essa direção e sempre remover subscriptions/timers quando a View for descartada.
