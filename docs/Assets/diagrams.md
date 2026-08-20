---
title: Diagrams
aliases:
  - Mermaid Diagrams
  - Architecture Diagrams
tags:
  - assets
  - diagrams
---

# Diagrams

All Mermaid diagrams used across the documentation.

## System Architecture

```mermaid
graph TB
    subgraph "User Level"
        GUI[KTWirzade.GUI<br/>WPF App<br/>.NET 4.8]
    end

    subgraph "Administrator Level"
        CLI[KTWirzade.CLI<br/>Console App<br/>.NET 4.7.2]
        Shared[KTWirzade.Shared<br/>Core Engine<br/>.NET 4.7.2]
    end

    subgraph "TrustedInstaller Level"
        TI[TrustedInstaller<br/>Process]
    end

    subgraph "Shared Projects"
        Core[Core<br/>Logging, Win32, Serialization]
        Interprocess[Interprocess<br/>InterLink IPC]
        WimLib[ManagedWimLib<br/>WIM Manipulation]
    end

    GUI -->|Embeds CLI + Shared| CLI
    CLI --> Shared
    Shared --> Core
    Shared --> Interprocess
    Shared --> WimLib
    CLI -->|Escalate| TI
    GUI -->|Escalate| TI
```

## Execution Flow Sequence

```mermaid
sequenceDiagram
    participant User
    participant GUI/CLI
    participant Shared
    participant InterLink
    participant TI as TrustedInstaller

    User->>GUI/CLI: Launch with playbook
    GUI/CLI->>Shared: DeserializePlaybook()
    Shared-->>GUI/CLI: Playbook object
    GUI/CLI->>GUI/CLI: Validate requirements
    GUI/CLI->>InterLink: InitializeConnection(Admin)
    InterLink-->>GUI/CLI: Connection ready
    GUI/CLI->>InterLink: LaunchNode(TI)
    InterLink->>TI: Start process as TrustedInstaller
    TI->>InterLink: Register node
    InterLink-->>GUI/CLI: Node ready
    GUI/CLI->>InterLink: ExecuteAsync(RunPlaybook)
    InterLink->>TI: Forward execution
    TI->>Shared: ParseActions()
    Shared-->>TI: TaskAction list
    loop For each action
        TI->>Shared: action.RunTask()
        Shared-->>TI: Status
        TI->>InterLink: Progress report
        InterLink-->>GUI/CLI: Update UI
    end
    TI-->>InterLink: Complete
    InterLink-->>GUI/CLI: Result
```

## Project Dependencies

```mermaid
graph TD
    CLI[KTWirzade.CLI] -->|References| Shared[KTWirzade.Shared]
    GUI[KTWirzade.GUI] -->|Embeds| CLI
    GUI -->|Embeds| Shared
    Shared -->|Imports| Core[Core]
    Shared -->|Imports| Interprocess[Interprocess]
    Shared -->|References| WimLib[ManagedWimLib]
    Core -->|Provides| Win32[Win32 Interop]
    Core -->|Provides| Logging[Logging System]
    Interprocess -->|Provides| Pipes[Named Pipes]
```

## Privilege Escalation

```mermaid
graph LR
    subgraph "User Level"
        U[User Process]
    end

    subgraph "Admin Level"
        A[Admin Process]
    end

    subgraph "TI Level"
        TI[TrustedInstaller Process]
    end

    subgraph "Named Pipes"
        P1[Pipe: KTWirzade_User_Admin]
        P2[Pipe: KTWirzade_Admin_TI]
    end

    U <-->|Pipe| A
    A <-->|Pipe| TI
```

## Action Class Hierarchy

```mermaid
graph TD
    ITaskAction[ITaskAction Interface]
    TaskAction[TaskAction Abstract]
    
    ITaskAction --> TaskAction
    
    TaskAction --> FileAction
    TaskAction --> RunAction
    TaskAction --> CmdAction
    TaskAction --> PowerShellAction
    TaskAction --> RegistryKeyAction
    TaskAction --> RegistryValueAction
    TaskAction --> ServiceAction
    TaskAction --> AppxAction
    TaskAction --> TaskKillAction
    TaskAction --> DownloadAction
    TaskAction --> ScheduledTaskAction
    TaskAction --> SystemPackageAction
    TaskAction --> SoftwareAction
    TaskAction --> WriteStatusAction
```

## Verification Flow

```mermaid
flowchart TD
    A[Playbook has ProductCode?] -->|No| B[Unverified]
    A -->|Yes| C[Query verification server]
    C --> D{Response}
    D -->|"true"| E[Verified]
    D -->|"false"| F[Unverified]
    D -->|"malicious"| G[Malicious]
    D -->|No response| H[Unreached]
```

## Theme Detection

```mermaid
flowchart TD
    A[App Start] --> B{Windows Version}
    B -->|Win 10| C[Load Win10 Theme]
    B -->|Win 11| D[Load Win11 Theme]
    C --> E{System Theme}
    D --> E
    E -->|Dark| F[Load Dark Resources]
    E -->|Light| G[Load Light Resources]
```

## GUI Page Flow

```mermaid
graph LR
    Intro[IntroPage] --> Select[SelectPage]
    Select --> Mode[ModePage]
    Mode --> Requirements[RequirementsPage]
    Requirements --> Progress[ProgressPage]
    Progress --> Finish[FinishPage]
```

## Task Condition Evaluation

```mermaid
flowchart TD
    A[Task Action] --> B{Option Check}
    B -->|Pass| C{Build Check}
    B -->|Fail| D[Skip]
    C -->|Pass| E{ISO/OOBE Check}
    C -->|Fail| D
    E -->|Pass| F{Upgrade Check}
    E -->|Fail| D
    F -->|Pass| G[Execute Action]
    F -->|Fail| D
```

## CLI Execution Flow

```mermaid
flowchart TD
    A[Main] --> B{Args length > 1?}
    B -->|Yes| C[ParseArguments]
    B -->|No| D[Set Working Directory]
    C --> E{Is Interprocess?}
    E -->|Yes| F[InitializeConnection]
    E -->|No| D
    D --> G{Is Administrator?}
    G -->|No| H[Error: Admin Required]
    G -->|Yes| I{Playbook path valid?}
    I -->|No| J[Error: No Playbook]
    I -->|Yes| K[DeserializePlaybook]
    K --> L{Configuration exists?}
    L -->|No| M[Error: Empty Config]
    L -->|Yes| N[Extract Resources]
    N --> O[InitializeConnection Admin]
    O --> P{Is TrustedInstaller?}
    P -->|No| Q[Check Requirements]
    P -->|Yes| R[Execute Playbook]
    Q --> S[LaunchNode TI]
    S --> R
```

---

> [!info] See Also
> - [[Architecture/Overview]] - System overview
> - [[Architecture/Execution-Flow]] - Execution pipeline
