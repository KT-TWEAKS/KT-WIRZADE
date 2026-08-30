# KT WIRZADE - Changelog

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.

## [Não lançado]

### Corrigido (Motor - revisão completa)

- **CRITICAL: CLI options overwrite — playbooks não aplicavam 100%**: `CLI.cs` construía uma lista vazia de `options` a partir de `args.Skip(1)` quando nenhum argumento era passado e passava para `RunPlaybook`, que sobrescrevia `Playbook.Options` com essa lista vazia. Todas as actions com `option:` (ex: `install-toolbox`, `browser-brave`) eram silenciosamente ignoradas. Agora o CLI faz merge dos args com os defaults das `FeaturePages`, preservando as opções corretas.
- **CRITICAL: PowerShell quoting quebrava com `"""` do YAML Atlas**: `-C "{Command}"` concatenava aspasduplas do C# com `"""` literais do YAML, resultando em argumento malformado que PowerShell não conseguia parsear. Comandos como `Set-Theme`, `Set-LockscreenImage`, e `SOFTWARE.ps1 -Toolbox` falhavam silenciosamente. Agora usa `-EncodedCommand` com Base64 UTF-16LE — elimina TODOS os problemas de quoting.
- **HIGH: CmdAction quoting inconsistente**: `RunAsProcess` citava o comando (`/C "{cmd}"`) mas `RunAsPrivilegedProcess` não (`/C {cmd}`). Comandos com espaços quebravam no modo AugmentedProcess (TI). Agora ambos usam `/S /C "{cmd}"` com quoting consistente.
- **MEDIUM: `PowerShellAction.Timeout` tipo incorreto**: `[YamlMember(typeof(string))]` em vez de `typeof(int)`. YamlDotNet podia falhar ao deserializar timeouts numéricos.
- **MEDIUM: `PowerShellAction.Wait` tipo incorreto**: `[YamlMember(typeof(string))]` em vez de `typeof(bool)`.
- **MEDIUM: `ServiceAction.Device` tipo incorreto**: `[YamlMember(typeof(string))]` em propriedade `bool`. YamlDotNet não conseguia deserializar.
- **MEDIUM: `ServiceAction.Weight` tipo incorreto**: `[YamlMember(typeof(string))]` em propriedade `int`.
- **MEDIUM: `SystemPackageAction.Weight` tipo incorreto**: `[YamlMember(typeof(string[]))]` em propriedade `int`.
- **MEDIUM: `LanguageAction` sem `[YamlMember]`**: campos `Tag` e `Display` não eram deserializáveis do YAML. Adicionados atributos corretos.
- **FIX CONSISTÊNCIA: Todos os `RunPSCommand` migrados para `-EncodedCommand`**: `AmeliorationUtil.cs`, `Defender.cs`, `SettingsPanel.cs` (3 ocorrências), `AppxAction.cs`, `SystemPackageAction.cs`, `IntegrityCheck.cs`, `ApplyPackageDialog.xaml.cs` — eliminado padrão `-C "{cmd}"` de toda a codebase.

- **CRITICAL: `UpdateAction` crashava com NullReferenceException**: `UpdateAction.RunTask` chamava `CmdAction.RunTask` que retorna `null`, e `await null` lança `NullReferenceException`. Qualquer tag `!update:` em playbook crasharia o motor. Corrigido para chamar `RunTaskOnMainThread` (caminho síncrono correto do CmdAction).
- **HIGH: `LineInFileAction` operação Delete nunca implementada**: `RunTask` sempre adicionava linhas independentemente da propriedade `Operation`. A operação `Delete` (enum default!) nunca removeu linhas — apenas appendava. Agora implementa Delete corretamente: lê, filtra, reescreve.
- **HIGH: `DebugLogger` crashava com `DirectoryNotFoundException`**: `DebugLogger.Enable()` tentava escrever `Debug.txt` sem criar a pasta de logs antes. Corrigido com `Directory.CreateDirectory` em `Enable()` e `WriteEntry()`. GUIs (`ProgressDialog`, `IsoProgressDialog`) também criam a pasta antes de chamar `RunPlaybook`.
- **MEDIUM: `LineInFileAction` crashava em arquivo inexistente para Add**: `GetMissingLines()` chamava `File.ReadAllLines()` antes de verificar se o arquivo existia. Agora cria o arquivo (e diretório pai) antes de ler para operações Add.
- **MEDIUM: `SystemPackageAction` mensagem de erro incorreta**: dizia "Appx action" em vez de "SystemPackage action". Corrigido.
- **MEDIUM: `ShortcutAction` não criava atalhos**: código de criação `.lnk` estava comentado (dependência IWshRuntimeLibrary removida). Reimplementado via COM interop late-bound (`WScript.Shell`) sem dependência externa, com liberação adequada de COM objects.
- **MEDIUM: `SoftwareAction` leak de HttpResponseMessage**: response não era descartado no loop de retry da busca de pacotes. Wrapping em `using` block.

### Corrigido (Rollback - sessões e permissões)

- **Limpeza de sessões antigas falhava silenciosamente**: `PruneSessions` original abortava o loop inteiro se UMA sessão falhasse no `Directory.Delete` (arquivos read-only de sistema). Agora `CleanupOldSessions` usa try/catch por sessão, pastas órfãs sem `session.json` são removidas por data da pasta, sessões presas abertas (processo morreu) são limpas após 30 dias, e `ForceDeleteDirectory` limpa atributos recursivamente + fallback `attrib.exe`.
- **Rollback de chave de registro falhava em chaves protegidas**: `EnsureKeyWritableForRollback` só destravava a chave alvo — subchaves filhas (ex.: serviços do SYSTEM) continuavam negando acesso. Agora `EnsureKeyTreeWritableForRollback` percorre a árvore inteira (chave + todas as subchaves recursivas + pai) concedendo FullControl a Administrators.
- **Rollback de arquivo falhava em alvos protegidos**: `File.Copy` e `File.Delete` falhavam com `UnauthorizedAccessException` em arquivos do sistema (ReadOnly/System/Hidden). Agora `MakeFileWritable` assume ownership (Administrators) + concede FullControl antes de restaurar/deletar.
- **Saves do `session.json` podiam corromper**: escrita direta via `File.WriteAllText` sem backup atômico. Agora `WriteSessionFile` usa tmp único com PID + `File.Replace` com retry 3x.
- **Botão "Limpar Tudo" adicionado na UI de Rollback**: permite deletar todas as sessões de uma vez (com confirmação), além do botão "Limpar antigas" (>30 dias) que já existia.

### Corrigido (GUI)

- **Erro `'-420' não é um valor válido para Height`**: `MessageBox.xaml.cs` calculava `SystemParameters.WorkArea.Height - 450.0` sem proteção contra negativo. Agora usa `Math.Max(..., 100.0)` em todas as ocorrências.
- **Janela principal agora é redimensionável**: `Height=540 Width=800 MinHeight=490 MinWidth=700`, `ResizeMode=CanResize`, layout responsivo (StackPanel → Grid rows */Auto), clip decorativo ampliado.
- **RollbackWindow redimensionável**: `Height=560 Width=620 MinHeight=480 MinWidth=520`, `ResizeMode=CanResize`.
- **Rollback: status com cores**: chips "Ativo" (azul) e "Revertido" (verde) com triggers de binding.
- **Rollback: datas em hora local**: `dd/MM/yyyy HH:mm` + tooltip relativo ("há 3 dias").
- **Dashboard: subtítulo com SO**: `v1.0.0 • Windows 11 build XXXXX • Online/Offline`.
- **Sobre: mais larga + info do SO**: 360px, redimensionável, linha com versão do Windows.
- **AcrylicWindow: propriedade Resizable**: permite janelas opt-in por redimensionamento (borda de 7px).

### Corrigido (auditoria contra o código original do AME Wizard)

- **`CmdAction` escape quebrava operadores**: `EscapeArgument` escapava `&`, `|`, `>` etc., mas o comando é executado via `cmd.exe /C "comando inteiro"` — escapamento próprio do cmd quebrava pipelines e redireções. Restaurado o comportamento original (comando inteiro aspas-duplas, sem escape por argumento).
- **TaskKillAction/FileAction podiam matar/sobrepor o próprio processo**: regex de proteção agora cobre `KTWirzade|ame.?wizard|ame.?assassin` (processos próprios ficam fora do kill e de operações de arquivo).
- **`ShortcutAction` estava no-op**: agora cria o `.lnk` de verdade via COM `WScript.Shell` (late-bound, com liberação de COM objects) e registra entrada de rollback para a remoção poder desfazer.
- **`NoPendingUpdates` checava sempre falso**: verificação restaurada via processo IPC descartável com COM `Microsoft.Update.Session` (fala-o-tudo em qualquer erro), timeout de 50s, liberação de COM objects.

### KT WIRZADE Admin descontinuado

- **`CLI-Standalone/KTWirzade.Admin.exe` removido do repositório**: o app admin dedicado foi substituido pelo painel web do site de licencas (gestão de playbooks/verificação via `licensing-site/`). Sem referências em build.bat/CI; sem perda de capacidade.

### CustomizationManager integrado no fluxo de execução

- **`CustomizationManager` integrado ao ProgressDialog**: o código antes morto agora é detectado e executado durante a aplicação de playbooks. Se um playbook contém `Configuration/customizations.yml` (ou `customize.yml`), o `CustomizationDialog` é aberto automaticamente antes da execução, e as customizações são aplicadas após o `RunPlaybook()`.
- **Detecção automática**: `CustomizationManager.LoadProfile()` procura `customizations.yml` dentro do playbook extraído. Se não existir, o fluxo legado (CredentialManager) continua inalterado — zero quebra de compatibilidade.
- **Dialog de customização**: o `CustomizationDialog` é aberto no contexto do ProgressDialog (via `ShowDialog()`), permitindo ao usuário ajustar username, senha, perfil, wallpaper, lockscreen, nome do PC, cor de destaque, auto-logon e tema antes de executar.
- **Prioridade correta**: se o playbook tem customizations.yml com username/senha, o fluxo de credenciais legado (CredentialManager.SetUserCredentials/RenameUser) é pulado para evitar duplicação — o CustomizationManager cuida de tudo.
- **Métodos validados para Windows 11**: SetUsername (wmic), SetWallpaper (registry + SystemParametersInfo), SetProfilePicture (SettingsPanel), SetComputerName (WMI Win32_ComputerSystem.Rename), SetPassword (SettingsPanel.ChangePassword), ElevateUser (SettingsPanel.ElevateUser), SetAccentColor (DWM registry), SetAutoLogon (SettingsPanel.SetAutoLogon), SetThemeMode (registry).
- **Progresso reportado**: cada etapa de customização atualiza o `StatusText` do ProgressDialog ("Setting username...", "Setting custom wallpaper...", etc.).

### Playbooks: FSOS-XR9 → FSOS-XR10 + registro oficial

- **Catalogo atualizado**: `FSOS-XR9.apbx` e `playbooks/src/FSOS-XR9/` removidos; **`FSOS-XR10.apbx`** é a versão vigente (by FRAMESYNC, v10.0). A pasta `playbooks/` continua gitignored por design — o catalogo canônico agora é o release `playbooks-catalog` no GitHub.
- **Release `playbooks-catalog`** criado com os 16 .apbx oficiais (hashes reais extraídos de cada arquivo). Link de download aponta para o asset exato do release — corrija os 3 assets renomeados pelo GitHub (`PeakOS.V1.0.2.AS.IS.apbx`, `vain.v14.hotfix6.apbx`, `XOS.V0.574.apbx`) e usa tag fixa em vez de `latest/download`.
- **Registro no site de licencas** (`licensing-site`): `api/_lib/data.js` reescrito com os 16 playbooks (hashes SHA-256 reais, metadados de `playbook.conf`, `verified: true`); `api/verify.js` passa `hash` e `downloadUrl` na resposta e distingue `unknown` (não-cadastrado) de `unverified`; `api/playbooks.js` inclui hash/downloadUrl.
- **GUI: verificação por hash**: `PlaybookGUI.GetVerificationStatus` já não marca como malicioso todo playbook sem ProductCode; consulta `/api/verify?prodID=&hash=` e interpreta status `verified`/`malicious`/`unverified`/`unknown`. Playbooks oficiais sem ProductCode (ex.: FSOS-XR10, Atlas, AtmosphereOS) agora verificam como oficiais.
- **`licensing-site/playbooks.html`** regenerado: 16 cards com botões Verify (por código ou hash) e Download apontando para o asset correto. `site/index.html` atualizado FSOS-XR9 → FSOS-XR10.

### Melhorias (APBX DevKit — integração VS Code)

- **Scaffold `.vscode` criado automaticamente ao abrir projeto**: `settings.json` (associação YAML/XML, exclusões de binários), `extensions.json` (recomendações), `yaml.code-snippets` (as 22 tags de ação do motor como snippets com placeholders) e `tasks.json`. Nunca sobrescreve arquivos já existentes.
- **Janela "Preview no KT WIRZADE" (F8)**: replica a página de seleção do app usando o parse real do motor (`AmeliorationUtil.DeserializePlaybook`) — título, ícone (`playbook.png`), versão, autor, descrição, badges de builds (com nomes amigáveis 21H2/22H2/24H2...), requisitos, feature pages com opções e conteúdo do pacote (tarefas/imagens/executáveis). Se o conf for inválido, mostra exatamente o motivo da recusa do parser.
- **Empacote ignora scaffold de desenvolvimento**: 7z agora usa `-xr!.vscode -xr!.git` para o `.apbx` final não conter metadata de editor.
- **Botao "Abrir no VS Code" (F7)** na toolbar: abre a pasta do projeto no `Code.exe` (busca `%LOCALAPPDATA%\Programs`, `Program Files`, PATH) com fallback para Explorer quando o VS Code não está instalado.
- **Linha de comando**: `KTWirzade.DevKit.exe <pasta ou .apbx>` abre direto ao iniciar (via `App.StartupItem` + `Window_Loaded`).
- **Menu de contexto na arvore de arquivos**: Abrir no editor, Abrir no VS Code (aponta para o arquivo com linha/coluna), Abrir pasta no Explorer, Atualizar lista. Duplo-clique abre o arquivo (selecionar ja não abre tab automaticamente — evita acumulo de abas).
- **Validação em pré-empacote agora aguarda resultado**: `Pack_Click` virou `async`, salva todas as abas modificadas e so prossegue depois que `RunValidationAsync()` conclui — corrigia bug antigo onde o empacote checava `_issues` antes da validação assincrona terminar.
- **Desdobramento duplo**: `RunValidation` agora retorna `Task<List<ValidationIssue>>` compartilhado (`_validationTask`), impedindo disparos concorrentes de sobrescrever o painel.

### Corrigido (deadlock/congelamento da GUI)

- **MessageBox.OnClosing com recursao infinita**: `OnClosing(e)` brincava com o próprio override em vez de `base.OnClosing(e)`. Qualquer clique em OK/Yes/Bypass de QUALQUER diálogo de confirmação congelava o processo (JIT Release/x64 otimiza a recursão de cauda em loop infinito; UI thread ficava presa em 100% CPU). Bug herdado do código original compilado do fonte decompilado; corrigido com behavior neutro: fecha normalmente e aguarda result.
- **`MaterialManager.IsVMwareVM` realizava IPC na UI thread ao criar qualquer janela** (EndInit/AcrylicWindow, chamado por MessageBox, SecurityDialog, ProgressDialog etc.): quando o serviço Winmgmt estava desabilitado (estado comum desses playbooks), `EnsureWMI().GetAwaiter().GetResult()` bloqueava a UI no pipe IPC (timeout infinito) e a janela congelava antes de abrir. Agora o getter é puramente WMI-read-only.
- **Handler de saída de nó IPC** (App.xaml.cs): `Dispatcher.Invoke` síncrono num thread de pipe virava cascade deadlock com o acima; agora `Invoke` com timeout de 5s.

### Corrigido (geral)

- **DevKit não abria `.apbx`** (`0x80131040`): o `dotnet build` SDK-style regerava o `exe.config` sem os redirects do `App.config`. `AutoGenerateBindingRedirects=false` mantém os 6 redirects verbatim. Round-trip pack → unpack re-verificado.
- **ErrorWindow copiável** em DevKit e DevTool: qualquer falha abre janela com stack completo + botão "Copiar erro" (substituiu MessageBox crua).
- **Validação não bloqueia mais a UI** do DevKit (async), indicador Ln/Col, "Salvar tudo" salva todas as abas modificadas, duplo-clique num problema abre o arquivo **e pula pra linha** do erro YAML.
- **Cabecalho do DevTool** agora mostra o **icone do playbook** (playbook.png embutido).
- **APBX DevKit introduzido** na arquitetura de build (`KTWirzade.DevKit` - novo projeto).

### Melhorias (rodada consolidada)

- **APBX DevKit**: toolbar completa, abas de arquivo, indicador Ln/Col, validação assíncrona, janela de erro copiável.
- **APBX Developer**: limpeza temp robusta, classificação de `!regexFile` na aba Arquivos, guarda contra análise dupla, ícone de playbook, ErrorWindow copiável.
- **UI consistente**: brushes semânticos `StatusSuccess/Warn/Error/Info(+Soft)` e `OverlayScrimBrush` nas 4 variantes de tema (Windows10/11 × Dark/Light), migrados chips de status da janela principal, quick actions do Dashboard e spinner do DeCripple.
- **DevTool alinhado à paleta dark** da GUI (acento `#00B4D8`, bordas/barra/scrollbar ajustadas).
- **"Ignorar esta versão"** no auto-update: botão no UpdateCheckDialog persiste `SkippedUpdateVersion` no config; Dashboard/dialog respeitam.
- **Bypass de versão no CLI**: playbooks fora das SupportedBuilds avisam e pedem confirmação antes de aplicar.
- **Rollback robusto**: sessões nomeadas por playbook, merge por ID em vez de contagem, prune só de sessões concluídas, retry com backoff na gravação, backup de arquivos ao deletar diretórios.

### Corrigido (motor/IPC)

- **Requisito `DefenderToggled` invertido** (`Requirements.cs`): dava satisfeito com Defender LIGADO e bloqueava depois de desligado. Agora alinhado com CLI (toggles off = atendido).
- **Sessões de rollback eternas no CLI**: `KTWirzade.CLI.exe` nunca abria/fechava sessão. Agora cria e conclui sessão por execução.
- **Merge de rollback por contagem descartava entradas** do nó TrustedInstaller. Agora faz união por `Id` ordenada por timestamp.
- **Save de rollback sem retry**: colisão com escrita simultânea perdia entradas silenciosamente. Agora 3 tentativas com backoff.
- **Exclusão de diretórios inteiros ficava irrecuperável** - `FileAction` agora faz backup arquivo-a-arquivo e registra entradas antes de remover a pasta.
- **Bypass de RAM/CPU gravado invertido no modo ISO** (`IsoOptionsPageView`): procurava `"CPURAMCheck"` que não existe (real é `"RAMCPUCheck"`).
- **Loop infinito + NRE no UsbWriteDialog**: polling `while(true)` girava após fechar o diálogo e dereferenciava `WriteTask` nulo.
- **`Environment.Exit(1)` dentro do nó IPC do Defender**: falha do DISM matava o processo e não devolvia motivo. Agora lança exceção descritiva.
- **Bloco duplicado de "memory integrity"** no Defender.cs: mesma sequência executada 2x; removida a repetição.
- **Certificado temporário órfão** em `%TEMP%`: agora é apagado logo após o import.
- **`Task.Delay(Timeout.Infinite)` deixava TaskCanceledException** não observada na checagem de update (Dashboard e dialog).
- **Remoção do Defender restaurada**: cabs `.cab` embutidos estavam com 0 bytes no Shared/CLI/GUI; agora os reais amd64/arm64 estão embutidos. `Defender.cs` resolvia recurso por nome da GUI no assembly errado.
- **Parser de playbooks**: habilitadas as tags `!lineInFile`, `!user`, `!shortcut`, `!update` e adicionado `!regexFile`; classes `LineInFileAction`/`UpdateAction` públicas para desserialização segura.
- **Versões unificadas em 1.0.0** até o lançamento (GUI/DevTool estavam 1.7.0, build.bat v1.1.0).
- **CLI handler `--service`**: o serviço de recuperação KTWirzadePrepare apontava para um exe que não tratava o argumento; agora restaura boot normal e remove o serviço.

### Reorganização

- **PLAYBOOKS AGORA SÃO LOCAIS**: pasta `playbooks/` (com `apbx/` compilados e `src/` fontes) é **ignorada pelo git** - não fazem parte do produto, servem só como material de estudo para validar DevTool/DevKit.
- **Código morto removido**: pasta `ManagedWimLib/` (duplicada em `KTWirzade.Shared/WimLib`), `examples/` vazio, `ControlWriter.cs`, `NtStatus.cs`, `Tasks/TaskList.cs`, `uefi-ntfs-ame-old.img`.
- **Revisões antigas movidas** para `docs/` (`CODE_REVIEW.md`, `DOCS_STRUCTURE.md`, `PROJECT_ORGANIZATION.md`).
- **Resíduos limpos do csproj**: `PublishUrl C:\Users\yohas\...` e thumbprint ClickOnce removidos.
- **Multiboot próprio removido** (`ISOBootManager`): faltavam os assets GRUB2; fluxo oficial continua 1 ISO por pendrive via Modo ISO (que está funcional e testado).

### Docs

- **`docs/Playbooks/Action-Types.md`** completamente reescrito: tags ativas completas, `!task` usa `path:`, nota sobre propriedade desconhecida falhar o parse.
- **`docs/Shared/Parser.md`**: tabela de tags atualizada para todas as 22 ações suportadas.
- **`docs/Development/APBX-Developer.md`** e **`docs/Development/APBX-DevKit.md`**: novos guias completos das tools.

## [1.0.0] - 2026-08-22

### Versão Inicial Estável

Esta é a primeira versão estável do KT WIRZADE, uma versão customizada e melhorada do AME Wizard para otimização e personalização do Windows.

### Recursos Principais

- **Interface Moderna** - UI estilo Windows 11 com tema claro/escuro
- **Sistema de Playbooks** - Execução automatizada de arquivos .apbx
- **Rollback Real** - Desfaz alterações de playbooks anteriores
- **Modo ISO** - Download e preparação de ISOs Windows
- **Multi-idioma** - Português Brasileiro e Inglês
- **Auto-Update** - Verificação de atualizações via GitHub
- **APBX Developer** - Ferramenta para analisar playbooks sem aplicar
- **APBX DevKit** - Kit de desenvolvimento para criar playbooks
- **IPC Seguro** - Comunicação entre processos com HMAC-SHA256

### Segurança

- **Command Injection Protection** - Sanitização de comandos em CmdAction e PowerShellAction
- **Session Secret Seguro** - Gerado via RandomNumberGenerator (criptograficamente seguro)
- **Registry Key Disposal** - Todas as chaves de registro agora são descartadas corretamente
- **Pipe ACL** - Restrita ao dono da sessão + SYSTEM + TrustedInstaller
- **HMAC-SHA256** - Verificação autenticada de mensagens IPC

### Correções de Bugs

- **Update Check Timeout** - Adicionado timeout de 15 segundos para evitar travamento
- **Async/Await Correto** - UpdateChecker agora usa métodos async nativos
- **Windows 10 Download** - Microsoft descontinuou downloads automáticos; agora abre página no navegador
- **FileSystemWatcher para APBX** - Monitora exclusões externas de playbooks
- **ArgumentOutOfBoundsException** - Bounds checking adicionado em SelectISOPane e IsoFeaturesPane
- **Empty Catch Blocks** - Todas as exceções agora são logadas

### Notas

- Windows 10: download direto foi descontinuado pela Microsoft; usuário é redirecionado à página oficial
- Alguns warnings CS0649 restantes são normais para structs Win32/P/Invoke
- Shared project requer dependências JetBrains.Annotations para compilação completa

---

## Estrutura do Projeto

```
KT-Wirzade.sln
├── KTWirzade.CLI/          # Aplicativo de linha de comando
├── KTWirzade.Shared/       # Biblioteca compartilhada (motor principal)
├── KTWirzade.GUI/          # Interface gráfica (WPF)
├── KTWirzade.DevTool/      # APBX Developer (analisador de .apbx)
├── KTWirzade.DevKit/       # APBX DevKit (IDE de criação de .apbx)
├── Core/                   # Infraestrutura de baixo nível
├── Interprocess/           # Comunicação entre processos
├── playbooks/              # Pacotes .apbx + fontes de estudo (LOCAL, não versionado)
├── site/                   # Site público + documentação pública
├── docs-site/              # Documentação interna (Vercel)
├── licensing-site/         # Painel administrativo de licenciamento (Vercel)
├── docs/                   # Documentação interna
└── CLI-Standalone/         # Binários avulsos gerados pelo build.bat
```

## Links

- **Repositório**: https://github.com/KT-TWEAKS/KT-WIRZADE
- **Autor**: kelvenapk (https://github.com/kelvenapk)
- **Licença**: MIT (Modified by kelvenapk)
