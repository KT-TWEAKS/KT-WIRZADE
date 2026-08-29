# KT WIRZADE v1.0

Motor de execução de playbooks para otimização e personalização do Windows — versão customizada e melhorada do [AME Wizard](https://ameliorated.io).

[![Build](https://github.com/KT-TWEAKS/KT-WIRZADE/actions/workflows/build.yml/badge.svg)](https://github.com/KT-TWEAKS/KT-WIRZADE/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Download**: [Releases](https://github.com/KT-TWEAKS/KT-WIRZADE/releases/latest) · **Site**: [kt-wirzade-site.vercel.app](https://kt-wirzade-site.vercel.app) · **Playbooks**: [KT-TWEAKS-APBX](https://github.com/KT-TWEAKS/KT-TWEAKS-APBX)

---

## O que é o KT WIRZADE?

O KT WIRZADE interpreta arquivos de playbook (`.apbx`) que contêm definições YAML de tarefas de sistema e as executa com privilégios elevados (TrustedInstaller). É uma interface gráfica moderna com suporte a **PT-BR e EN**.

## Recursos

| Recurso | Descrição |
|---------|-----------|
| **Rollback real** | Registro, serviços, chaves deletadas e arquivos são restauráveis |
| **IPC seguro** | Cadeia User → Admin → TrustedInstaller com HMAC-SHA256 e ACL restrito |
| **Modo ISO** | Download de ISOs Windows 11 + ISO Local |
| **Bypass de versão** | Continuar com builds não suportadas (com aviso) |
| **Offline Support** | Cache local com SHA-256 + indicador online/offline |
| **Dashboard** | Quick Actions, rollback e updates |
| **Multi-playbook** | Sidebar com rolagem ilimitada |
| **Até 7 opções/página** | Checkbox, Radio e seletores com imagem |
| **Auto-Update** | Verifica GitHub releases com release notes |

## Arquitetura

```
KT-Wirzade.sln
├── KTWirzade.CLI/          # Linha de comando
├── KTWirzade.Shared/       # Motor principal (parsing, ações, rollback)
├── KTWirzade.GUI/          # Interface gráfica (WPF)
├── Core/                   # Infraestrutura de baixo nível
└── Interprocess/           # Comunicação multi-processo
```

| Projeto | Tipo | Descrição |
|---------|------|-----------|
| `KTWirzade.CLI` | Exe (.NET 4.7.2) | Ponto de entrada CLI |
| `KTWirzade.Shared` | Library (.NET 4.7.2) | Motor: parsing, execução, rollback |
| `KTWirzade.GUI` | WPF App (.NET 4.8) | Interface gráfica |
| `Core` | Shared Project | Logging, Win32, serialização |
| `Interprocess` | Shared Project | User → Admin → TrustedInstaller |

## Ações Suportadas

`!run` · `!file` · `!registryKey` · `!registryValue` · `!service` · `!appx` · `!cmd` · `!powerShell` · `!download` · `!taskKill` · `!software` · `!scheduledTask` · `!systemPackage` · `!writeStatus` · `!task` · `!regexFile` · `!shortcut` · `!user` · `!lineInFile` · `!update`

## Formato .apbx

```
playbook.apbx
├── playbook.conf          # Metadados XML
├── playbook.png           # Ícone (opcional)
├── Images/
└── Configuration/
    ├── main.yml
    └── *.yml
```

## Build

```bash
# Build completo (recomendado)
build.bat

# Ou via dotnet
dotnet build KT-Wirzade.sln -c Release
```

**Requisitos**: Windows 10 (build 19041+) ou Windows 11 · .NET Framework 4.8 · Privilégios de administrador

## Segurança

- **Command Injection** — comandos sanitizados antes da execução
- **Session Secret** — RandomNumberGenerator criptograficamente seguro
- **Pipe ACL** — restrita ao dono da sessão + SYSTEM + TrustedInstaller
- **HMAC-SHA256** — verificação autenticada de mensagens IPC
- **FileSystemWatcher** — monitora exclusões externas de playbooks

## Licença

MIT — veja [LICENSE](LICENSE) para detalhes.

**Autor**: [kelvenapk](https://github.com/kelvenapk) · **Organização**: [KT-TWEAKS](https://github.com/KT-TWEAKS)
