# KT WIRZADE - Changelog

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.

## [1.0.2] - 2026-10-22

### Atualizado (Atualizador - verificação e completude do ciclo 1.0.2)

- **Versão 1.0.2 completa no ciclo de release**: `Globals.CurrentVersion`, `<Version>` da GUI, `scripts/build-release.ps1` (default `1.0.2` — o script travava com `Version mismatch in Globals.cs`), `build.bat`, `/build.yml` (nomes de assets + `RELEASE-1.0.2.md`), `GlobalsGUI.WizardPlaybook` (agora lê `Globals.CurrentVersion`, sem possibilidade de divergência) e textos estáticos `v1.0.1` de Welcome/About/UpdateCheckDialog.
- **Fallback de verificação via `SHA256SUMS.txt`**: quando a API do GitHub não publica o campo `digest` do asset, o atualizador baixa o `SHA256SUMS.txt` da própria release (mesma validação estrutural: HTTPS/host oficial/nome/tamanho, leitura limitada a 256 KB) e usa o hash publicado da linha do EXE. Nenhuma forma de download sem hash verificável; formato sha256sum com marcador binário suportado; divergências de nome/linha malformada falham fechado.
- **Erros de download agora chegam ao usuário**: `DownloadLatestAsync` relança falhas reais (só continua retornando `null` quando não há update/asset), então o diálogo mostra a causa exata em vez do texto genérico.
- **Diálogo de update recebe snapshot pré-verificado**: o prompt de startup não dispara uma segunda chamada à API (`UpdateCheckDialog(UpdateInfo preloaded)`), o prompt e o botão da barra de título usam o mesmo singleton (`MainWindow.ShowUpdatesDialog()`).
- **HTTP 429** tratado como limite de requisições do GitHub (antes passava pelo retry por ser 4xx não mapeado).
- **Canal prerelease ordena por data de publicação**: `/releases` lista por data de *criação*; um draft criado antes e publicado depois fazia a lista devolver release antiga. Agora `SelectReleaseFromList` ordena por `published_at`.
- **Comparação de versão reforçada** (`IsNewerVersion`): comparação elemento a elemento (suporta 4 segmentos tipo `1.0.2.1`), não-numérico falha fechado; testes de versões-sufixo (`1.0.1-beta.2` vs `1.0.1`) e ordem numérica (`1.0.10` > `1.0.9`) adicionados.
- **Erro de cancelamento/timeout com mensagem ambígua corrigida** ("Verificacao cancelada." reportada em timeout de startup).

### Atualizado (Interface - atualizador e dashboard)

- **Quick action "Atualizações" do dashboard abre o diálogo real de update** (mesmo singleton da barra de título): o fluxo anterior com `MessageBox` ignorava o canal configurado e a versão ignorada pelo usuário.
- **Diálogo de Atualizações ganhou controle de verificação automática ao iniciar** (checkbox persistido via `WizardConfig.SetAutoCheckEnabled`), com o toggle de canal reorganizado numa linha de opções própria.
- **Dashboard localizado (PT/EN)**: selo de status de conexão, cartões de ação rápida (títulos, subtítulos e textos dos botões Abrir/Restaurar via binding), cabeçalhos de seção e estados vazios reagindo à troca de idioma em tempo real.
- **Crash "FillBorder não pode ser encontrado no escopo de nome" ao abrir o diálogo de Atualizações corrigido**: inicializar o novo checkbox no construtor disparava os storyboards do estilo do `CheckBox` antes do template existir; a inicialização do valor persistido foi movida para o `Loaded`, quando o template já está aplicado, agora com guarda de falha (não propaga erro).
- **Troca de canal à prova de crash**: clicar no toggle estável/pré-release não derruba mais o aplicativo mesmo se a inicialização do atualizador falhar; o motivo real aparece dentro do diálogo (async-void blindado).
- **Inicialização defensiva do cliente HTTP do atualizador (`UpdateChecker`)**: o cliente compartilhado passou a ser construído sob demanda com guarda; uma inicialização de tipo falha no .NET Framework é cacheada pelo runtime e quebraria o atualizador em TODOS os toques seguintes — agora falhas de header/cliente viram erro normal de verificação, com fallback sem headers.
- **Rollback: botão "Abrir pasta"** no rodapé da janela abre a pasta da sessão selecionada (logs, snapshots e `session.json`) no Explorador, habilitado somente quando a sessão está selecionada e a pasta existe; tip nos botões do rodapé (Atualizar/Limpar antigas/Limpar tudo).
- **Confirmação ao ignorar "instalação fresca"**: o hipertexto "Ignorar" do requisito de instalação fresca (página de requisitos e requisitos ISO) agora pede confirmação explicando o risco, no mesmo padrão do bypass de versão do Windows.
- **Diálogo de bypass de versão com contexto de família Windows 10/11**: mostra se a sua build (ou build da ISO) é Windows 10 ou 11 e diferencia o risco entre famílias distintas e builds mais novas da mesma família.
- **Blur do Windows 10 corrigido (3 camadas)**: `MaterialManager` consulta `HKCU\...\Themes\Personalize\EnableTransparency` e, com efeitos de transparência desligados, desarma o accent e aplica `FakeBackgroundBrush` no lugar do fundo transparente (evita janela branca/achatada); `AcrylicWindow` reasserta o accent em `WM_ENTERSIZEMOVE`/`WM_EXITSIZEMOVE` (elimina as trilhas fantasma clássicas do arraste no acrylic do Win10) e no `StateChanged` (maximizar/restaurar). Windows 11 (DWM attributes) permanece inalterado.
- **Reconciliação de APBX em tempo real fecha seleção fantasma**: o reconcílio do watcher (`DispatchPlaybookReconciliation`) agora também roda `WizardConfig.RecordMissingHiddenPlaybooks` e a nova `DeselectMissingSelectedPlaybook` limpa `GlobalsGUI.Current.Playbook`/estado do card quando o `.apbx` do playbook selecionado deixa de existir — o fluxo não continua mais contra um item fantasma que falharia apenas no lançamento do CLI com arquivo inexistente.
- **Auditoria de paridade de tema Win10/11 concluída**: as 115 referências `{DynamicResource}/{StaticResource}` dos XAML resolvem nos dois conjuntos de tema (151 chaves no Win10, 152 no Win11; a única exclusiva do Win11, `FakeWindowCorner`, é recurso morto sem referência). Cores fixas remanescentes fora dos dicionários são intencionais (mocks de UAC/Windows, scrim de popups, branco sobre `BlueButton`, vermelho de perigo, cor de acento da barra de rolagem).

### Atualizado (Interface - update visual e acessibilidade)

- **Contraste WCAG AA corrigido no tema claro**: `TextSecondaryBrush` (`#787878` -> `#5F5F5F`) e os textos de arrastar-e-soltar (`#a0a0a0`/`#888888` -> `TextSecondaryBrush`) - corrige 111+ superfícies de uma vez.
- **Tokens de tema aplicados no lugar de cores fixas**: `OverAllControls`, `YesNoButton`, labels de erro de validação (`#FFFF0000` -> `StatusErrorBrush`, removido `Opacity="0.7"`), avisos de erro da página de conclusão (`#fce100`/`#433519` -> `YellowInfoCircleFill`/`WarningBoxBrush`) e os textos cinza das páginas de requisitos (`#FF808080` -> `TextSecondaryBrush`).
- **Removido o preenchimento cyan de depuração** (`#FF00FFFF`) do `ApplyPackageDialog`, que era renderizado por baixo do cartão do diálogo.
- **Sombra do popup de verificação corrigida**: o clon de sombra deixou de fixar a paleta clara, eliminando o halo errado no tema escuro; o raio passou a usar `PopupCornerRadius`.
- **Paridade Win10/Win11**: `FakePageBackgroundBrush` adicionada aos dicionários do Windows 10 (12 pontos de código já a resolviam); estilos mortos removidos (`{x:Type RadioButton}` do W11 Dark, `{x:Type GroupBox}` + `BorderGapMaskConverter` do Light).
- **`SidebarScrollBar` movida para o escopo do aplicativo** (`Themes/StaticStyles.xaml`), eliminando a dependência frágil de `StaticResource` entre BAMLs.
- **Tema aplicado de forma síncrona no startup** (`ThemeWatcher.ApplyThemeNow()` antes de criar as janelas), eliminando o flash do tema errado na primeira pintura.
- **Acessibilidade**: `AutomationProperties.Name` nos cartões de ação rápida do dashboard; retângulo de foco de teclado restaurado nos campos de usuário/senha; `IsDefault`/`IsCancel` no `MessageBox`, `UACBox`, `RollbackWindow`, `UpdateCheckDialog` e `CustomizationDialog` (Enter/Esc funcionam); `MessageBox` agora usa o estilo `Window` como os demais diálogos.
- **UX dos diálogos**: confirmação antes de fechar o `ProgressDialog` durante a execução (inclusive por Alt+F4); `UsbWriteDialog` sempre libera o Fechar (fail-safe de 45 min e caminho de erro); `ApplyPackageDialog` não reinicia mais o sistema quando a instalação falha (oferece Fechar/Reiniciar agora); `PrepareDialog` mostra erro em vez de fechar o app em falha de IPC.
- **Redução de CPU**: polling do `SecurityDialog` (200ms -> 2s) e do `AntivirusDialog` (500ms/1.5s -> 3s); `Spinner`/`ProgressBarIndeterminate` param as animações ao serem removidos da árvore visual.

## [1.0.1] — estabilidade e recuperação

Veja [notas de atualização e compatibilidade](RELEASE-1.0.1.md). Correções de parser, filtros, downloads, rollback, IPC e atualizador; interface acrílica no Windows 10; melhorias de desempenho; executável único verificável.

### Corrigido (GUI - estabilidade)

- **Congelamento da UI na página de requisitos**: removidos `.GetAwaiter().GetResult()`/`.Wait()` sobre chamadas de IPC feitos na UI thread (mesmo bug documentado no `MaterialManager`).
- **Crash na seleção de USB**: `IsoPageView.RefreshUsbPage` não retornava após falha de enumeração (NRE em `async void`) e liberava o lock sem `try/finally`.
- **"Mostrar logs" quebrado**: o `OpenFileDialog` rodava dentro de `Task.Run` (thread MTA) - agora roda na thread correta em `ProgressDialog` e `IsoProgressDialog`.
- **Falso "Amelioration failed"**: a sincronização entre `Exited` e "Playbook finished." deixou de depender de espera mágica de 8s; `HasSafelyExitted` é reiniciado a cada execução; `WaitForExit` saiu da UI thread (não mata mais processos saudáveis).
- **Navegação**: `CanExecute` real dos botões Anterior/Próximo, guarda de `null` em `CurrentViewModel`, páginas terminais retornam `null` em vez de lançar `NotImplementedException`, e `PulseNextButtonCommand` deixou de lançar no getter.
- **Assinatura perdida**: `Items.CollectionChanged` é assinada independentemente do evento `PreparationCompleted` (remoção de ISO/playbook volta a funcionar mesmo com clique tardio no Welcome).
- **Handler global de exceções**: contador com janela de 30s, fallback visível quando o nível de aplicação não é tratado, e `AssemblyResolve` loga em vez de engolir falhas.
- **Janelas modais reutilizadas**: `AboutWindow`, `RollbackWindow` e `UpdateCheckDialog` não são mais recriados a cada clique.
- **Loop infinito** na página de carregamento (timeout de 90s) e busy-wait do parser de ISO (deadline de 10s).
- **Escape de argumentos do CLI**: valores com `::` agora fazem round-trip corretamente no parser de linha de comando.
- **Status de verificação**: parse com validação (`split.Length`, `Enum.TryParse`), `SHA256` liberado corretamente e escrita abortada quando o nó admin não está disponível.
- **Diálogos de módulos**: bounds em `TweaksDialog`/`UninstallUpdatesDialog`, handles de registro com `using` no `AntivirusDialog`, `TrySetCanceled` (não `SetCanceled`) nos `TaskCompletionSource`, e helper compartilhado `DialogHelpers` no lugar de 4 cópias idênticas de conversão de bitmap.

### Corrigido (Motor e CLI)

- **`UpdateAction` reportava sucesso mesmo com falha do DISM**: agora valida o exit code via `HandleExitCodes` (`!0` -> erro), força `Wait = true`, usa `try/finally` em `InProgress` e só marca `Completed` com exit code `0`.
- **Sessões de rollback**: `EndSession` limpa a sessão em `finally` (não pode ser reprocessada) e `WriteSessionFile` cai para `File.Move` quando `File.Replace` não é suportado, registrando a perda em log em vez de descartar silenciosamente.
- **Downloads**: `AtomicDownload` não mascara mais a exceção original com a limpeza do temporário e tem fallback para `File.Move`.
- **`UpdateChecker`** usa um `HttpClient` compartilhado (evita esgotamento de soquetes) com cabeçalhos configurados uma única vez.
- **`LanguageAction`** compara também por `Culture.Name` (evita reinstalação infinita de idiomas sem tag IETF).
- **CLI**: fechamento único da sessão de rollback em `finally` (antes podia deixar a sessão aberta no caminho fatal).
- **i18n**: novas chaves `Button.Reboot`, `Progress.Running`, `Status.Never`; migração do idioma salvo de `C:\ProgramData\AME\language.json` para `C:\ProgramData\KTWirzade\language.json`.

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
