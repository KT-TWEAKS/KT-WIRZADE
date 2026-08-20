# KT WIRZADE

Sistema de otimizacao e personalizacao do Windows baseado em playbooks.

## O que e o KT WIRZADE?

O KT WIRZADE e um motor de execucao de playbooks que modifica configuracoes do Windows de forma automatizada. Ele interpreta arquivos de playbook (`.apbx`) que contem definicoes YAML de tarefas de sistema e as executa com privilegios elevados (TrustedInstaller).

## Arquitetura

```
KT-Wirzade.sln
├── KTWirzade.CLI/          # Aplicativo de linha de comando
├── KTWirzade.Shared/       # Biblioteca compartilhada (motor principal)
├── KTWirzade.GUI/          # Interface grafica (WPF)
├── Core/                   # Infraestrutura de baixo nivel
├── Interprocess/           # Comunicacao entre processos
└── ManagedWimLib/          # Manipulacao de arquivos WIM
```

### Componentes

| Projeto | Tipo | Descricao |
|---------|------|-----------|
| `KTWirzade.CLI` | Exe (.NET 4.7.2) | Ponto de entrada para execucao via linha de comando |
| `KTWirzade.Shared` | Library (.NET 4.7.2) | Motor principal: parsing de playbooks, execucao de acoes |
| `KTWirzade.GUI` | WPF App (.NET 4.8) | Interface grafica interativa |
| `Core` | Shared Project | Infraestrutura: logging, Win32 interop, serializacao |
| `Interprocess` | Shared Project | Comunicacao multi-processo (User -> Admin -> TrustedInstaller) |
| `ManagedWimLib` | Library | Manipulacao de arquivos WIM via wimlib |

## Como Funciona

1. **Parsing do Playbook**: Le o arquivo `playbook.conf` (XML) que define metadados
2. **Parsing de Tarefas**: Le os arquivos YAML da pasta `Configuration/` que definem acoes do sistema
3. **Verificacao**: Checa requisitos (Defender desabilitado, internet, bateria, etc.)
4. **Execucao**: Executa as acoes sequencialmente com privilegios de TrustedInstaller
5. **Registro**: Registra playbooks aplicados no registro do Windows e sistema de arquivos

## Tipos de Acoes Suportadas

- `!run` - Executar comandos
- `!file` - Manipular arquivos
- `!registryKey` / `!registryValue` - Modificar registro
- `!service` - Gerenciar servicos
- `!appx` - Gerenciar aplicativos UWP
- `!cmd` / `!powerShell` - Executar scripts
- `!download` - Baixar arquivos
- `!taskKill` - Encerrar processos
- `!software` - Gerenciar software
- `!scheduledTask` - Gerenciar tarefas agendadas
- `!systemPackage` - Gerenciar pacotes do sistema
- `!writeStatus` - Reportar progresso

## Formato do Playbook (.apbx)

Um playbook e um arquivo compactado (7-Zip) com senha `"malte"` contendo:

```
playbook.apbx
├── playbook.conf          # Metadados XML
├── playbook.png           # Icone (opcional)
├── Images/                # Imagens adicionais
└── Configuration/
    ├── main.yml           # Arquivo YAML principal
    └── *.yml              # Arquivos YAML adicionais
```

### Estrutura do playbook.conf

```xml
<Playbook>
  <Name>Nome do Playbook</Name>
  <Username>Autor</Username>
  <Version>1.0.0</Version>
  <UniqueId>{GUID}</UniqueId>
  <ProductCode>CODIGO</ProductCode>
  <Git>https://github.com/owner/repo</Git>
  <Website>https://example.com</Website>
  <FeaturePages>...</FeaturePages>
  <Requirements>...</Requirements>
</Playbook>
```

## Sistema de Verificacao (Oficial vs Licenciado)

### Como funciona a verificacao

O KT WIRZADE usa um **sistema de verificacao remoto** para determinar a confiabilidade de um playbook:

1. **Se o playbook tem `ProductCode`**: O sistema consulta um servidor de verificacao
2. **Se nao tem `ProductCode`**: Marcado como `Unverified`

### Niveis de verificacao

| Nivel | Significado |
|-------|-------------|
| `Verified` | Playbook verificado e confiavel (oficial/licenciado) |
| `Unverified` | Playbook nao verificado |
| `Malicious` | Playbook identificado como malicioso |
| `Unreached` | Nao foi possivel contactar o servidor |

### Fluxo de verificacao

```
ProductCode + SHA256(.apbx) -> HTTP -> wng-{eu|us}.ktwirzade.com:8000/isVerified
Resposta: "true" | "false" | "malicious"
```

### Playbooks Oficiais vs Licenciados

- **Oficiais**: Playbooks desenvolvidos pela equipe KT WIRZADE, com `ProductCode` registrado no servidor
- **Licenciados**: Playbooks de terceiros que foram registrados e verificados pelo sistema
- **Nao verificados**: Playbooks sem `ProductCode` ou que nao foram registrados

### Status de verificacao

O status e armazenado localmente em arquivos `.status` criptografados em:
```
%PROGRAMDATA%\KTWirzade\Playbooks\{GUID}.status
```

## Estrutura de Diretorios

### Apos instalacao

```
%PROGRAMDATA%\KTWirzade\
├── Playbooks\                    # Playbooks importados
│   ├── {GUID}.apbx              # Copia do playbook
│   └── {GUID}.status            # Status de verificacao (criptografado)
├── AppliedPlaybooks\             # Playbooks aplicados (sem UniqueId)
├── OOBE\                         # Configuracoes OOBE
│   ├── Playbook\
│   ├── OOBE.exe
│   └── oobe.conf
└── DriverCache\                  # Cache de drivers
```

### Registro do Windows

```
HKLM\SOFTWARE\KTWirzade\Playbooks\Applied\{GUID}
├── Name
├── Username
├── Version
├── AppliedTimeUTC
├── SelectedOptions
└── ...
```

## Compilacao

### Requisitos

- **Windows 10/11** (x64)
- **Visual Studio Build Tools 2022** com workload "Desktop development with C++"
- **.NET SDK 8.0+** (para projeto GUI SDK-style)
- **.NET Framework 4.7.2 Targeting Pack** (CLI/Shared)
- **.NET Framework 4.8 Targeting Pack** (GUI)
- **NuGet CLI** (para restore manual)

### Build Automatico (Recomendado)

Execute o script `build.bat` na raiz do projeto:

```cmd
build.bat
```

O script executa em sequencia:
1. Prepara `client-helper.dll` em `Core\Helper\x64\Release\` (copia de Resources se nao houver compilador C++)
2. Restore de pacotes NuGet
3. Compilacao do Shared + CLI via MSBuild (legacy csproj)
4. Copia do `KTWirzade.Shared.dll` + todas dependencias para Resources do GUI
5. Compilacao do GUI via dotnet CLI (SDK-style)
6. Copia de dependencias Shared para GUI bin, CLI bin e CLI-Standalone
7. Verificacao de versoes criticas

> **IMPORTANTE**: Os pacotes NuGet devem estar alinhados entre os 3 projetos (Shared, CLI, GUI). Caso contrario, o GUI pode fechar imediatamente ao iniciar com `FileLoadException` em runtime. Pacotes criticos para manter alinhados:
> - `System.Text.Json`
> - `Microsoft.Win32.TaskScheduler` (TaskScheduler)
> - `Polly.Core`
> - `SharpSevenZip`
> - `Newtonsoft.Json`
> - `JetBrains.Annotations`

### Build Manual

O projeto usa dois sistemas de build diferentes:

| Projeto | Tipo | Ferramenta |
|---------|------|------------|
| `KTWirzade.Shared` | Legacy csproj (.NET 4.7.2) | MSBuild |
| `KTWirzade.CLI` | Legacy csproj (.NET 4.7.2) | MSBuild |
| `KTWirzade.GUI` | SDK-style csproj (.NET 4.8) | dotnet CLI |

#### Passo 1: Compilar Shared + CLI

```cmd
msbuild KT-Wirzade.sln /t:Build /p:Configuration=Release /p:Platform=x64 /p:SolutionDir="<caminho>\\" /m
```

#### Passo 2: Copiar Shared.dll e dependencias para GUI

```cmd
copy /Y KTWirzade.Shared\bin\x64\Release\KTWirzade.Shared.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\YamlDotNet.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\TimeZoneConverter.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\JetBrains.Annotations.dll KTWirzade.GUI\src\Resources\
```

#### Passo 3: Compilar GUI

```cmd
dotnet build KTWirzade.GUI\src\KTWirzade.GUI.csproj -c Release -p:Platform=x64 -p:SolutionDir="<caminho>\\"
```

#### Passo 4: Copiar dependencias para output do GUI

Apos compilar, copie as dependencias do Shared para o diretorio de saida do GUI:

```cmd
copy /Y KTWirzade.Shared\bin\x64\Release\YamlDotNet.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\TimeZoneConverter.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\7z.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\client-helper.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
```

> [!warning] O `build.bat` faz todas essas copias automaticamente.

### Arquivos Gerados

```
KTWirzade.CLI\bin\x64\Release\KTWirzade.CLI.exe       (~4.2 MB)
KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\KTWirzade.GUI.exe  (~24.8 MB)
```

### Notas Importantes

- O `KTWirzade.Shared.dll` precisa ser copiado para `KTWirzade.GUI\src\Resources\` antes de compilar o GUI (recursos embedded)
- Arquivos `.cab` em `KTWirzade.Shared\Properties\` sao placeholders vazios - substitua pelos originais para producao
- `Plane.dll` e um controle WPF 3D embutido em `KTWirzade.GUI\src\Resources\`
- `client-helper.dll` (native) deve estar em `Core\Helper\x64\Release\`

## Uso

### CLI

```cmd
KTWirzade.CLI.exe "caminho\do\playbook"
```

### GUI

Execute `KTWirzade.GUI.exe` e arraste um arquivo `.apbx` para a janela.

### Argumentos da GUI

| Argumento | Descricao |
|-----------|-----------|
| `--apply-package <arquivo>` | Aplica um pacote de atualizacao |
| `--service` | Inicia como servico do Windows |
| `--updated` | Modo pos-atualizacao (limpa backup) |

## Correcoes Aplicadas (KT WIRZADE Rename)

### Bugs Corrigidos

| Bug | Arquivo | Descricao |
|-----|---------|-----------|
| `7z.dll` nao encontrado | `App.xaml.cs:202` | `SetLibraryPath` usava `CurrentDirectory` (%TEMP%) em vez do diretorio do exe. Corrigido para usar `Assembly.GetExecutingAssembly().Location` |
| `FileNotFoundException` em `Log..cctor()` | Dependencias do Shared | YamlDotNet.dll, TimeZoneConverter.dll etc. nao eram copiados para o output do GUI. Adicionado na etapa 5 do `build.bat` |
| "Playbook failed" - LaunchNode | `ProgressDialog.xaml.cs:186` | Parametros `TargetLevel` e `Level` estavam invertidos. `StartProcessAsTI` precisa de `TargetLevel.Administrator`/`Level.TrustedInstaller` (conforme CLI) |
| `ame-assassin.exe` nao encontrado | `AppxAction.cs:193`, `SystemPackageAction.cs:92` | `Directory.GetCurrentDirectory()` apontava para `%TEMP%` quando roda como TrustedInstaller. Corrigido para usar `AmeliorationUtil.Playbook.Path + "\\Executables"` (caminho real do playbook extraido) |
| `7z.dll` nao encontrado (Drivers) | `Drivers.cs:218` | `SharpSevenZipBase.SetLibraryPath` usava `Directory.GetCurrentDirectory()` que pode mudar. Corrigido para `AppDomain.CurrentDomain.BaseDirectory` |
| `TaskKillAction` nao protegia CLI | `TaskKillAction.cs:126` | `RegexNoKill` ainda usava `"TrustedUninstaller\\.CLI"` (nome antigo). Corrigido para `"KTWirzade\\.CLI"` |
| Log "Skipping TU.CLI" inconsistente | `FileAction.cs:319,630` | Mensagem de log dizia "TU.CLI" mas o processo ja era "KTWirzade.CLI". Corrigido para consistencia |
| **`AppxAction` Win32Exception sem fallback** | `AppxAction.cs:218` | Falha silenciosa quando `ame-assassin.exe` nao existe. Adicionado fallback PowerShell usando `Remove-AppxPackage` com base no Type (Family/Package/App) |
| **`SystemPackageAction` Win32Exception sem fallback** | `SystemPackageAction.cs:99` | Falha silenciosa quando `ame-assassin.exe` nao existe. Adicionado fallback PowerShell usando `Remove-WindowsPackage` |
| **`ScheduledTaskAction` double dispose** | `ScheduledTaskAction.cs:241,253,257` | `subTaskKey.Dispose()`/`taskKey.Dispose()` chamados manualmente dentro de `using var` (ja faz dispose automatico). Removido |
| **`SoftwareAction` HttpClient leak** | `SoftwareAction.cs:162,415` | `HttpProgressClient` criado sem `using` + dispose manual. Corrigido para `using var` |
| **`SoftwareAction` Response double dispose** | `SoftwareAction.cs:184` | `response.Dispose()` dentro de bloco sem using. Corrigido para `using` |
| **`RegistryKeyAction` RegistryKey leak** | `Shared:65`, `Core:56` | `OpenBaseKey().OpenSubKey()` sem dispose. Adicionado `using` recursivo |
| **`ServiceAction` NullReferenceException** | `Shared:102`, `Core:99` | `(int)value` quando `value` e null. Adicionado null check + `Convert.ToInt32` |
| **`FileAction` LastIndexOf retorna -1** | `FileAction.cs:70,412` | Path sem `\\` quebra `Remove(-1)`. Adicionado guard `if (lastToken < 0)` |
| **`CLI` off-by-one em resource extraction** | `CLI.cs:332-383` | Loop `offset + MB < stream.Length` perdia ultimo chunk + `offset = -MB` inicial. Corrigido com seek/read/write por blocos completos |
| **`FinishErrorPageViewModel` self-assignment** | `FinishErrorPageViewModel.cs:22` | `finishErrorPage = finishErrorPage` (atribuicao a si mesmo). Corrigido para `this.finishErrorPage = finishErrorPage` |
| **`WinUtil` FileStream leak** | `WinUtil.cs:847` | `File.Create()` sem using + `GetManifestResourceStream` null check ausente. Corrigido |
| **`AmeliorationUtil` IconPath null** | `AmeliorationUtil.cs:807` | `Directory.GetFiles(...).FirstOrDefault()` sem checar diretorio. Adicionado null safety |
| **`Defender/AmeliorationUtil/SoftwareAction/OutputProcessor` process.Start() sem checagem** | Multiplos | Retorno bool do `Start()` ignorado. Adicionado tratamento de erro |
| **`SoftwareAction` 7za.exe caminho incompleto** | `SoftwareAction.cs:617` | `RunCommand("7za.exe", ...)` nao usa caminho completo. Adicionado fallback para `AppDomain.CurrentDomain.BaseDirectory` |
| **`RequirementsPageView`/`IsoRequirementsPageView` event leak** | `RequirementsPageView.xaml.cs:227`, `IsoRequirementsPageView.xaml.cs:136` | `base.Loaded += CheckRequirements` nunca removido. Adicionado `-= CheckRequirements` no final do metodo |

### Recursos Adicionados

| Recurso | Arquivo | Descricao |
|---------|---------|-----------|
| `WindowsUpdate` model | `KTWirzade.Shared\Models\WindowsUpdate.cs` | Classe POCO para deserializacao de JSON de updates |
| `ExcludedWindowsUpdates` | `Playbook.cs` | Lista de updates excluidos para playbooks |
| `ExcludeBadWindowsUpdates` | `Playbook.cs` | Flag para excluir updates problematicos |
| `build.bat` | Raiz do projeto | Script de build automatizado em 5 etapas |
| Licenciamento site | `licensing-site/` | Servidor Express para verificacao de playbooks |
| Obsidian docs vault | `docs/` | Documentacao completa com wiki-links e Mermaid |

## Melhorias Futuras

### Prioridade Alta

- [ ] Restaurar WUApiLib para verificacao real de Windows Updates (atualmente stubbed)
- [ ] Adicionar testes unitarios (xUnit/NUnit)
- [ ] Implementar CI/CD com build automatico via Gitea Actions
- [ ] Publicar artefatos de build como releases

### Prioridade Media

- [ ] Migrar projetos legacy (Shared/CLI) para SDK-style csproj
- [ ] Padronizar versao do .NET Framework (unificar 4.7.2 e 4.8)
- [ ] Adicionar suporte a playbooks em formato JSON (alem de XML/YAML)
- [ ] Implementar auto-atualizacao via GitHub/Gitea Releases
- [ ] Adicionar logging estruturado (Serilog)

### Prioridade Baixa

- [ ] Traduzir interface para ingles/ingles
- [ ] Adicionar tema claro na GUI
- [ ] Criar pacotes de distribuicao (MSI/MSIX)
- [ ] Implementar plugin system para playbooks
- [ ] Adicionar dashboard de playbooks aplicados

### Tecnico

- [ ] Resolver warnings CS1998 (async sem await) no GUI
- [ ] Substituir `Plane.dll` por controle WPF 3D nativo
- [ ] Remover dependencia do `7z.dll` (usar biblioteca managed)
- [ ] Atualizar pacotes NuGet para versoes mais recentes
- [ ] Implementar code signing para binarios

## Licenca

MIT License
