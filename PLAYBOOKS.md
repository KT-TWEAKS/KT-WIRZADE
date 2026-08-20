# Sistema de Playbooks - KT WIRZADE

## Visao Geral

O KT WIRZADE e um motor de execucao de playbooks. Playbooks sao pacotes de configuracao que definem modificacoes automatizadas no sistema Windows.

## Formato do Arquivo .apbx

Um arquivo `.apbx` e um arquivo **7-Zip criptografado** com a senha `"malte"`.

### Conteudo

| Arquivo | Obrigatorio | Descricao |
|---------|-------------|-----------|
| `playbook.conf` | Sim | Metadados em XML |
| `playbook.png` | Nao | Icone do playbook |
| `Configuration/main.yml` | Sim | Arquivo YAML principal de tarefas |
| `Configuration/*.yml` | Nao | Arquivos YAML adicionais |
| `Images/*.png` | Nao | Imagens para paginas de opcoes |

## Estrutura do playbook.conf

```xml
<?xml version="1.0" encoding="utf-8"?>
<Playbook xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <!-- Identificacao -->
  <Name>Nome do Playbook</Name>
  <Username>Autor</Username>
  <Version>1.0.0</Version>
  <Details>Descricao do playbook</Details>
  <UniqueId>{GUID}</UniqueId>
  
  <!-- Verificacao e Atualizacoes -->
  <ProductCode>CODIGO_PRODUTO</ProductCode>
  <Git>https://github.com/owner/repo</Git>
  <Website>https://example.com</Website>
  <DonateLink>https://example.com/donate</DonateLink>
  
  <!-- Configuracoes -->
  <SupportedBuilds>19041,19042,19043,19044,19045</SupportedBuilds>
  <UseKernelDriver>true</UseKernelDriver>
  <Overhaul>false</Overhaul>
  
  <!-- Paginas de Opcoes -->
  <FeaturePages>
    <CheckboxPage>
      <Title>Selecione as opcoes</Title>
      <Options>
        <CheckboxOption>
          <Title>Opcao 1</Title>
          <Description>Descricao da opcao</Description>
          <Value>option1</Value>
        </CheckboxOption>
      </Options>
    </CheckboxPage>
  </FeaturePages>
  
  <!-- Requisitos -->
  <Requirements>
    <RequireDefenderDisabled>true</RequireDefenderDisabled>
    <RequireInternet>true</RequireInternet>
    <RequireBattery>false</RequireBattery>
    <RequireUCPDDisabled>true</RequireUCPDDisabled>
  </Requirements>
  
  <!-- Configuracao ISO -->
  <SupportsISO>true</SupportsISO>
  <ISO>
    <WIMImageIndex>1</WIMImageIndex>
    <WindowsEdition>Windows 11 Pro</WindowsEdition>
  </ISO>
  
  <!-- Configuracao OOBE -->
  <OOBE>
    <Username>Usuario</Username>
    <AutoLogon>true</AutoLogon>
  </OOBE>
</Playbook>
```

### Propriedades Importantes

| Propriedade | Tipo | Descricao |
|-------------|------|-----------|
| `Name` | string | Nome de exibicao do playbook |
| `Username` | string | Nome do autor |
| `Version` | string | Versao semantico (ex: "1.0.0") |
| `UniqueId` | Guid | Identificador unico para tracking |
| `ProductCode` | string | Codigo para verificacao remota |
| `Git` | string | URL do repositorio Git para atualizacoes |
| `FeaturePages` | array | Paginas de opcoes para o usuario |
| `Requirements` | object | Requisitos do sistema |
| `SupportsISO` | bool | Se pode ser injetado em ISOs |

## Sistema de Verificacao

### Como Funciona

O sistema de verificacao determina se um playbook e "oficial" ou "licenciado" consultando um servidor remoto.

### Fluxo

```
1. Playbook tem ProductCode?
   ├── Nao -> Unverified
   └── Sim -> Consultar servidor
              ├── Resposta: "true" -> Verified
              ├── Resposta: "false" -> Unverified
              ├── Resposta: "malicious" -> Malicious
              └── Sem resposta -> Unreached
```

### Servidores de Verificacao

| Regiao | Servidor |
|--------|----------|
| Europa | `wng-eu.ktwirzade.com:8000` |
| Americas/Asia-Pacific | `wng-us.ktwirzade.com:8000` |

### API

```
GET http://{servidor}/isVerified?prodID={productCode}&hash={sha256}
```

Resposta:
```json
{"isVerified": "true"}
```

### Niveis

| Nivel | Cor | Descricao |
|-------|-----|-----------|
| `Verified` | Verde | Playbook verificado e confiavel |
| `Unverified` | Amarelo | Playbook nao verificado |
| `Malicious` | Vermelho | Playbook identificado como malicioso |
| `Unreached` | Cinza | Servidor inacessivel |

## Tipos de Acoes YAML

### Acoes de Sistema

```yaml
# Executar comando
- !run
  path: "C:\\script.bat"
  args: "/silent"
  workDir: "C:\\"

# Executar PowerShell
- !powerShell
  script: "Get-Process | Stop-Process -Force"

# Executar CMD
- !cmd
  command: "net stop wuauserv"
```

### Acoes de Arquivo

```yaml
# Copiar arquivo
- !file
  operation: copy
  source: "C:\\source\\file.txt"
  destination: "C:\\dest\\file.txt"

# Deletar arquivo
- !file
  operation: delete
  path: "C:\\arquivo\\indesejado.txt"

# Renomear arquivo
- !file
  operation: rename
  source: "C:\\antigo.txt"
  destination: "C:\\novo.txt"
```

### Acoes de Registro

```yaml
# Criar chave
- !registryKey
  operation: create
  path: "HKLM\\SOFTWARE\\KTWirzade"

# Deletar chave
- !registryKey
  operation: delete
  path: "HKLM\\SOFTWARE\\KTWirzade\\Chave"

# Definir valor
- !registryValue
  operation: set
  path: "HKLM\\SOFTWARE\\KTWirzade"
  name: "Valor"
  value: "dados"
  type: REG_SZ
```

### Acoes de Servico

```yaml
# Parar servico
- !service
  operation: stop
  name: "wuauserv"

# Desabilitar servico
- !service
  operation: disable
  name: "DiagTrack"

# Deletar servico
- !service
  operation: delete
  name: "ServicoIndesejado"
```

### Acoes de Processo

```yaml
# Matar processo
- !taskKill
  process: "processo.exe"
  regex: "pattern.*"
```

### Acoes de Download

```yaml
# Baixar arquivo
- !download
  url: "https://example.com/file.exe"
  destination: "C:\\temp\\file.exe"
```

### Acoes de Appx

```yaml
# Remover aplicativo UWP
- !appx
  operation: remove
  package: "Microsoft.WindowsCalculator"

# Remover todos exceto
- !appx
  operation: removeAllExcept
  packages:
    - "Microsoft.WindowsStore"
    - "Microsoft.WindowsTerminal"
```

### Acoes de Tarefa Agendada

```yaml
# Deletar tarefa agendada
- !scheduledTask
  operation: delete
  path: "\\Microsoft\\Windows\\Defender\\Scheduled Scan"
```

## Estrutura de Tarefas YAML

### Arquivo principal (main.yml)

```yaml
- title: "Fase 1 - Remocao de componentes"
  description: "Remove componentes indesejados do Windows"
  actions:
    - !service
      operation: disable
      name: "DiagTrack"
    - !appx
      operation: remove
      package: "Microsoft.BingNews"

- title: "Fase 2 - Otimizacao"
  description: "Otimiza configuracoes do sistema"
  condition:
    option: "optimize"
  actions:
    - !registryValue
      operation: set
      path: "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection"
      name: "AllowTelemetry"
      value: 0
      type: REG_DWORD
```

### Condicoes

```yaml
# Condicao baseada em opcao do usuario
condition:
  option: "opcao_selecionada"

# Condicao baseada em build do Windows
condition:
  builds: "19041,19042,19043"

# Condicao baseada em arquitetura
condition:
  cpuArch: "x64"

# Condicao para modo ISO
condition:
  iso: true

# Condicao para modo OOBE
condition:
  oobe: true
```

### Inclusao de Tarefas

```yaml
# Incluir outro arquivo YAML
- !task
  file: "fase2.yml"
```

## Playbooks Oficiais vs Licenciados

### Oficiais

- Desenvolvidos pela equipe KT WIRZADE
- Tem `ProductCode` registrado no servidor de verificacao
- Recebem atualizacoes automaticas via Git
- Sao verificados como `Verified` pelo sistema

### Licenciados (Terceros)

- Desenvolvidos por terceiros
- Podem ter `ProductCode` registrado (se o autor solicitar verificacao)
- Recebem verificacao se registrados no servidor
- Status depende do registro no servidor

### Nao Verificados

- Sem `ProductCode`
- Nao consultam o servidor
- Sempre marcados como `Unverified`
- Podem ser usados, mas sem garantia de confiabilidade

### Deteccao de Maliciosos

O sistema detecta playbooks maliciosos se:
- O nome contem "AME", "Ameliorated", "Revision" ou "Atlas"
- O servidor retorna `"malicious"` para o ProductCode

## Criando um Playbook

### Passo 1: Criar a estrutura

```
meu-playbook/
├── playbook.conf
├── playbook.png
└── Configuration/
    └── main.yml
```

### Passo 2: Definir playbook.conf

```xml
<?xml version="1.0" encoding="utf-8"?>
<Playbook xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Name>Meu Playbook</Name>
  <Username>MeuNome</Username>
  <Version>1.0.0</Version>
  <Details>Descricao do meu playbook</Details>
  <UniqueId>{GERAR-GUID}</UniqueId>
  <Git>https://github.com/meu-usuario/meu-playbook</Git>
  <FeaturePages>
    <CheckboxPage>
      <Title>Opcional</Title>
      <Options>
        <CheckboxOption>
          <Title>Ativar otimizacao</Title>
          <Value>optimize</Value>
        </CheckboxOption>
      </Options>
    </CheckboxPage>
  </FeaturePages>
  <Requirements>
    <RequireDefenderDisabled>true</RequireDefenderDisabled>
  </Requirements>
</Playbook>
```

### Passo 3: Definir main.yml

```yaml
- title: "Desabilitar telemetria"
  description: "Remove coleta de dados do Microsoft"
  actions:
    - !service
      operation: disable
      name: "DiagTrack"
    - !registryValue
      operation: set
      path: "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection"
      name: "AllowTelemetry"
      value: 0
      type: REG_DWORD
```

### Passo 4: Empacotar como .apbx

```cmd
7z a -p"malte" -t7z -mhe=on meu-playbook.apbx meu-playbook\
```

## Distribuicao

### Via Git (Atualizacoes automaticas)

1. Crie um repositorio GitHub/GitLab/Gitea
2. Crie um release com o arquivo `.apbx` como asset
3. Configure a URL Git no `playbook.conf`
4. O KT WIRZADE detectara atualizacoes automaticamente

### Via Importacao Manual

1. Distribua o arquivo `.apbx`
2. O usuario importa arrastando para a janela do KT WIRZADE
3. O arquivo e copiado para `%PROGRAMDATA%\KTWirzade\Playbooks\`

## Registro de Playbooks Aplicados

Apos a execucao, o playbook e registrado em:

### Registro do Windows (com UniqueId)

```
HKLM\SOFTWARE\KTWirzade\Playbooks\Applied\{GUID}
```

Valores:
- `Name` - Nome do playbook
- `Username` - Autor
- `Version` - Versao
- `AppliedTimeUTC` - Data/hora da aplicacao
- `SelectedOptions` - Opcoes selecionadas pelo usuario
- `ErrorLevel` - Nivel de erro (0=sucesso)
- `AvailableOptions` - Opcoes disponiveis

### Sistema de Arquivos (sem UniqueId)

```
%PROGRAMDATA%\KTWirzade\AppliedPlaybooks\{index}\
```

Conteudo:
- `playbook.conf` - Copia do arquivo de configuracao
- `playbook.png` - Copia do icone
- `errors.txt` - Log de erros (se houver)
- `verified.txt` - Marcador de verificacao
