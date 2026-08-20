---
title: Code Style
aliases:
  - Naming Conventions
  - Code Patterns
tags:
  - development
  - code-style
---

# Code Style

## Naming Conventions

### C# Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `PlaybookParser` |
| Methods | PascalCase | `DeserializePlaybook()` |
| Properties | PascalCase | `ProductName` |
| Fields (private) | _camelCase | `_instance` |
| Fields (public) | PascalCase | `CurrentVersion` |
| Parameters | camelCase | `configPath` |
| Local variables | camelCase | `taskList` |
| Constants | PascalCase | `MaxMessageSize` |
| Enums | PascalCase | `UninstallTaskStatus` |
| Enum values | PascalCase | `InProgress` |

### File Naming

| Type | Convention | Example |
|------|------------|---------|
| Classes | PascalCase | `Playbook.cs` |
| Interfaces | I + PascalCase | `ITaskAction.cs` |
| Enums | PascalCase | `ErrorAction.cs` |

## Code Patterns

### Result Pattern

```csharp
var result = Wrap.ExecuteSafe(() => SomeOperation());
if (result.Failed)
{
    Log.EnqueueExceptionSafe(result.Exception);
}
```

### Async Pattern

```csharp
public async Task<bool> RunTask(Output.OutputWriter output)
{
    if (InProgress) throw new TaskInProgressException("...");
    InProgress = true;
    
    try
    {
        // Work
        return true;
    }
    finally
    {
        InProgress = false;
    }
}
```

### Null Safety

```csharp
#nullable enable
public string? OptionalProperty { get; set; }
public string RequiredProperty { get; set; }
#nullable disable
```

## XML Documentation

```csharp
/// <summary>
/// Deserializes a playbook from the specified directory.
/// </summary>
/// <param name="path">Path to the playbook directory.</param>
/// <returns>The deserialized Playbook object.</returns>
public static Playbook DeserializePlaybook(string path)
```

## File Organization

```csharp
// 1. System namespaces
using System;
using System.Collections.Generic;

// 2. Third-party namespaces
using Newtonsoft.Json;
using YamlDotNet;

// 3. Project namespaces
using Core;
using Interprocess;
using KTWirzade.Shared;
```

## Error Handling

```csharp
// Prefer specific exceptions
catch (SerializationException e)
{
    Log.WriteSafe(LogType.Error, "YAML error: " + e.Message);
}
catch (Exception e)
{
    Log.EnqueueExceptionSafe(e);
}
```

## Formatting

- 4 spaces indentation
- Opening brace on new line
- Max line length: 120 characters (soft limit)

---

> [!info] See Also
> - [[Development/Contributing]] - Contributing guide
> - [[Development/TODO]] - Planned improvements
