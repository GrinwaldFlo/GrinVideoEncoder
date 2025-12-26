# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [GrinVideoEncoder.csproj](#grinvideoencodercsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 1 | All require upgrade |
| Total NuGet Packages | 6 | 1 need upgrade |
| Total Code Files | 19 |  |
| Total Code Files with Incidents | 3 |  |
| Total Lines of Code | 836 |  |
| Total Number of Issues | 10 |  |
| Estimated LOC to modify | 8+ | at least 1.0% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| [GrinVideoEncoder.csproj](#grinvideoencodercsproj) | net9.0 | 🟢 Low | 1 | 8 | 8+ | AspNetCore, Sdk Style = True |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 5 | 83.3% |
| ⚠️ Incompatible | 0 | 0.0% |
| 🔄 Upgrade Recommended | 1 | 16.7% |
| ***Total NuGet Packages*** | ***6*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 1 | High - Require code changes |
| 🟡 Source Incompatible | 6 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 1 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 1743 |  |
| ***Total APIs Analyzed*** | ***1751*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| Serilog | 4.2.0 |  | [GrinVideoEncoder.csproj](#grinvideoencodercsproj) | ✅Compatible |
| Serilog.AspNetCore | 9.0.0 |  | [GrinVideoEncoder.csproj](#grinvideoencodercsproj) | ✅Compatible |
| Serilog.Sinks.File | 6.0.0 |  | [GrinVideoEncoder.csproj](#grinvideoencodercsproj) | ✅Compatible |
| System.Management | 9.0.2 | 10.0.1 | [GrinVideoEncoder.csproj](#grinvideoencodercsproj) | NuGet package upgrade is recommended |
| Xabe.FFmpeg | 6.0.1 |  | [GrinVideoEncoder.csproj](#grinvideoencodercsproj) | ✅Compatible |
| Xabe.FFmpeg.Downloader | 6.0.1 |  | [GrinVideoEncoder.csproj](#grinvideoencodercsproj) | ✅Compatible |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |
| System Management (WMI) | 6 | 75.0% | Windows Management Instrumentation (WMI) APIs for system administration and monitoring that are available via NuGet package System.Management. These APIs provide access to Windows system information but are Windows-only; consider cross-platform alternatives for new code. |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |
| P:System.Management.ManagementBaseObject.Item(System.String) | 2 | 25.0% | Source Incompatible |
| T:System.Management.ManagementObjectCollection | 1 | 12.5% | Source Incompatible |
| M:System.Management.ManagementObjectSearcher.Get | 1 | 12.5% | Source Incompatible |
| T:System.Management.ManagementObjectSearcher | 1 | 12.5% | Source Incompatible |
| M:System.Management.ManagementObjectSearcher.#ctor(System.String) | 1 | 12.5% | Source Incompatible |
| M:Microsoft.AspNetCore.Builder.ExceptionHandlerExtensions.UseExceptionHandler(Microsoft.AspNetCore.Builder.IApplicationBuilder,System.String) | 1 | 12.5% | Behavioral Change |
| M:Microsoft.Extensions.Configuration.ConfigurationBinder.Get''1(Microsoft.Extensions.Configuration.IConfiguration) | 1 | 12.5% | Binary Incompatible |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>📦&nbsp;GrinVideoEncoder.csproj</b><br/><small>net9.0</small>"]
    click P1 "#grinvideoencodercsproj"

```

## Project Details

<a id="grinvideoencodercsproj"></a>
### GrinVideoEncoder.csproj

#### Project Info

- **Current Target Framework:** net9.0
- **Proposed Target Framework:** net10.0
- **SDK-style**: True
- **Project Kind:** AspNetCore
- **Dependencies**: 0
- **Dependants**: 0
- **Number of Files**: 24
- **Number of Files with Incidents**: 3
- **Lines of Code**: 836
- **Estimated LOC to modify**: 8+ (at least 1.0% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["GrinVideoEncoder.csproj"]
        MAIN["<b>📦&nbsp;GrinVideoEncoder.csproj</b><br/><small>net9.0</small>"]
        click MAIN "#grinvideoencodercsproj"
    end

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 1 | High - Require code changes |
| 🟡 Source Incompatible | 6 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 1 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 1743 |  |
| ***Total APIs Analyzed*** | ***1751*** |  |

#### Project Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |
| System Management (WMI) | 6 | 75.0% | Windows Management Instrumentation (WMI) APIs for system administration and monitoring that are available via NuGet package System.Management. These APIs provide access to Windows system information but are Windows-only; consider cross-platform alternatives for new code. |

