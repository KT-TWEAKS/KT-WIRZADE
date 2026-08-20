---
title: Interprocess Communication
aliases:
  - InterLink
  - IPC
tags:
  - architecture
  - interprocess
---

# Interprocess Communication

The InterLink system manages communication between processes at different privilege levels.

## Architecture

```mermaid
graph TB
    subgraph "User Process"
        U[User Level]
    end

    subgraph "Admin Process"
        A[Administrator Level]
    end

    subgraph "TI Process"
        TI[TrustedInstaller Level]
    end

    subgraph "Named Pipes"
        P1[Pipe: KTWirzade_User_Admin]
        P2[Pipe: KTWirzade_Admin_TI]
    end

    U <-->|Pipe| A
    A <-->|Pipe| TI
```

## Levels

```csharp
public enum Level
{
    Any,
    Disposable,
    User,
    Administrator,
    TrustedInstaller,
}
```

| Level | Description | Typical Use |
|-------|-------------|-------------|
| `User` | Standard user | Windows Update checks |
| `Administrator` | Elevated admin | CLI/GUI main process |
| `TrustedInstaller` | System-level | File/registry modifications |
| `Disposable` | Short-lived | One-off tasks |

## Connection Modes

```csharp
public enum Mode
{
    SendOnly,     // One-way communication
    ReceiveOnly,  // Listen only
    TwoWay        // Bidirectional (default)
}
```

## Named Pipe Protocol

- **Prefix**: `KTWirzade`
- **Max Message Size**: 100MB
- **Serialization**: JSON (`System.Text.Json`)
- **Security**: Windows ACL on pipe handles

### Pipe Naming Convention

```
KTWirzade_{SourceLevel}_{TargetLevel}
```

## Launching a Node

```csharp
InterLink.LaunchNode(
    parentLevel: TargetLevel.Administrator,
    launchMethod: () => NativeProcess.StartProcessAsTI(...),
    level: Level.TrustedInstaller,
    mode: Mode.TwoWay,
    hostPid: Process.GetCurrentProcess().Id,
    allowAutoRelaunch: false
);
```

## Execution Flow

```mermaid
sequenceDiagram
    participant Admin as Administrator
    participant Pipe as Named Pipe
    participant TI as TrustedInstaller

    Admin->>Pipe: InitializeConnection(Admin, TwoWay)
    Admin->>Pipe: LaunchNode(TI, launchMethod)
    Pipe->>TI: Start process with arguments
    TI->>Pipe: Connect to pipe
    Pipe->>Admin: Node registered
    Admin->>Pipe: ExecuteAsync(method)
    Pipe->>TI: Forward method call
    TI->>TI: Execute with TI privileges
    TI->>Pipe: Return result
    Pipe->>Admin: Result received
```

## Node Lifecycle

1. **Registration**: Node connects and registers via `LevelController.Register()`
2. **Monitoring**: Host PID monitored for unexpected exits
3. **Communication**: Methods invoked via pipe messages
4. **Shutdown**: `InterLink.ShutdownNode(level)` terminates a specific node

## Error Recovery

- **Auto-relaunch**: If `allowAutoRelaunch` is true and node exits unexpectedly, it will be restarted
- **Timeout**: 30-second timeout on node launch
- **Cancellation**: Pending operations cancelled on node exit

## Security Considerations

> [!warning] Pipe Security
> Named pipes use Windows security descriptors. Only processes with appropriate access can connect to the pipe.

> [!info] TrustedInstaller Launch
> The `NativeProcess.StartProcessAsTI()` method uses `NSudoLC.exe` with `-U:T` flag to launch processes as TrustedInstaller.

---

> [!info] See Also
> - [[Architecture/Overview]] - System overview
> - [[Architecture/Execution-Flow]] - Complete execution pipeline
