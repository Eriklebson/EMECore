# 7. Build, validação e diagnóstico

## Regra geral

Uma modificação não está concluída apenas porque compila. Para alterações de código, validar build, inicialização e o fluxo afetado; registrar o resultado no `CHANGELOG_AI.md`. A confirmação final do usuário é necessária antes de considerar uma implementação concluída para commit.

## Desktop

Pré-requisitos: Windows 10/11 e .NET 8. Build normal:

```powershell
dotnet build EMECore.sln -c Debug -p:Platform=x64
```

### Ordem segura

1. Verificar se `EMECore.WinUI.exe` está em execução.
2. Fechar o processo antes de `dotnet clean` ou `dotnet build`.
3. Aguardar liberação de locks.
4. Compilar e confirmar zero erros.
5. Iniciar o executável gerado.
6. Aguardar alguns segundos e confirmar que continua aberto.
7. Exercitar a funcionalidade alterada.

Não limpar/buildar enquanto o app está aberto: há histórico de falha em que a limpeza remove `runtimeconfig.json` e o processo em execução passa a falhar com `hostpolicy.dll not found`.

Para crash WinUI (janela preta/fechamento, `Microsoft.UI.Xaml.dll` ou `0xc000027b`), consultar Event Viewer conforme o `AGENTS.md`. Alguns controles animados podem provocar crash de compositor em contextos específicos; investigar antes de alterar.

## Mobile

```powershell
flutter pub get
flutter analyze
flutter build apk --debug
```

Para instalação USB, usar o fluxo descrito no `EMECoreMobile/AGENTS.md`. Ao alterar dependência, revisar `pubspec.yaml` e `pubspec.lock`.

## Teste de integração

Desktop e celular devem estar na mesma rede local. Validar:

- Beacon encontra o PC na tela de conexão.
- Conexão manual por IP/porta funciona.
- `welcome`, ping e reconexão funcionam.
- Hardware atualiza sem travamento e sem consulta WMI por segundo.
- Lista de jogos, filtros e cache de abas funcionam.
- Capas são carregadas pela porta 8183 ou apresentam fallback visual.
- Conquistas carregam e progresso é exibido.
- Iniciar jogo pelo celular valida executável e gera confirmação/erro adequado.

## Checklist antes de entregar

- [ ] Mudança limitada ao escopo pedido.
- [ ] Arquitetura, contratos e desempenho preservados.
- [ ] Build executado nos projetos alterados.
- [ ] Aplicativo alterado iniciado e fluxo relevante validado.
- [ ] Integração desktop/mobile testada se o protocolo foi tocado.
- [ ] Documentação específica atualizada.
- [ ] `CHANGELOG_AI.md`, README e versão revisados quando aplicável.
- [ ] Nenhuma operação Git executada sem autorização explícita.
