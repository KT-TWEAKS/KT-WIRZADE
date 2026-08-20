---
title: KT WIRZADE Documentation
aliases:
  - Home
  - Index
tags:
  - home
  - moc
---

# KT WIRZADE Documentation

> [!info] Welcome
> KT WIRZADE is a playbook-based Windows optimization and personalization system that executes modifications with TrustedInstaller privileges.

## Quick Navigation

### [[Architecture/Overview|Architecture]]
The system architecture, project structure, execution flow, and interprocess communication.

- [[Architecture/Overview]] - System overview and privilege levels
- [[Architecture/Projects]] - Project descriptions and dependencies
- [[Architecture/Execution-Flow]] - Step-by-step execution pipeline
- [[Architecture/Interprocess]] - InterLink and privilege escalation

### [[Playbooks/Overview|Playbooks]]
Everything about the playbook system - from format to creation.

- [[Playbooks/Overview]] - What playbooks are
- [[Playbooks/APBX-Format]] - The `.apbx` archive format
- [[Playbooks/Playbook-Conf]] - XML configuration schema
- [[Playbooks/YAML-Tasks]] - YAML task file structure
- [[Playbooks/Action-Types]] - All supported action types
- [[Playbooks/Verification-System]] - Official/Licensed/Unverified levels
- [[Playbooks/Creating-Playbooks]] - Step-by-step creation guide

### [[GUI/Overview|GUI]]
The WPF graphical interface.

- [[GUI/Overview]] - WPF architecture and MVVM
- [[GUI/Pages]] - All pages documented
- [[GUI/Controls]] - Custom controls
- [[GUI/Themes]] - Windows 10/11 themes

### [[CLI/Overview|CLI]]
Command-line interface.

- [[CLI/Overview]] - Entry point and arguments
- [[CLI/Usage]] - Usage examples

### [[Shared/Overview|Shared Library]]
The core engine shared between CLI and GUI.

- [[Shared/Overview]] - Library architecture
- [[Shared/Actions]] - All 20+ action types
- [[Shared/Parser]] - PlaybookParser and TaskActionResolver
- [[Shared/Tasks]] - UninstallTask and TaskAction
- [[Shared/USB-ISO]] - USB/ISO/WIM handling

### [[API/Verification-API|API]]
Remote services.

- [[API/Verification-API]] - Verification endpoint
- [[API/Update-API]] - GitHub/GitLab/Gitea integration

### [[Deployment/Build|Deployment]]
Build and installation.

- [[Deployment/Build]] - Build requirements and process
- [[Deployment/Installation]] - System requirements

### [[Development/Contributing|Development]]
Contributing guidelines.

- [[Development/Contributing]] - How to contribute and build
- [[Development/Code-Style]] - Naming conventions
- [[Development/TODO]] - Planned improvements and known issues

---

> [!tip] Obsidian Features
> This vault uses `[[Wiki-Links]]`, callout boxes, Mermaid diagrams, YAML frontmatter, and tags for full Obsidian compatibility.
