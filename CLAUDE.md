# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Local video re-encoder: a .NET 10 Blazor Server app (Radzen UI, EF Core on SQLite, Xabe.FFmpeg) that indexes a video library and re-encodes files to HEVC on the GPU. There is no CPU encoding path.

## Commands

```bash
dotnet build GrinVideoEncoder.sln                 # build app and tests
dotnet test Tests/Tests.csproj                    # all tests (xUnit)
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~VaapiRequirementsCheckTests"   # one class
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~ClassName.MethodName"          # one test
dotnet run -- --config <name>                     # run; without --config it prompts on stdin
dotnet ef migrations add <Name> --output-dir Data/Migrations   # needs the dotnet-ef tool
```

No separate lint command is configured. Migrations are applied automatically at startup.

The app opens a browser when it starts, and the port (default 14563) moves up if taken. On Linux it exits at startup if `hevc_vaapi` cannot encode a test frame, so it will not run on a machine without a working VA-API setup.

CI (`.github/workflows/ci-publish.yml`) builds and tests on Ubuntu only, so Windows code paths are not exercised. **Every push to `main` publishes Windows and Linux builds and creates a GitHub release** (`v1.0.<run_number>`).

## Architecture

### Startup order (`Program.cs`, `Startup/`)

1. `EncoderRequirements.EnsureMetAsync` runs before config or DI exist. On Linux it uses `VaapiRequirementsCheck` (dependencies injected as delegates so it is unit-testable), then sets the FFmpeg path and `GpuDetector.VaapiDevice`.
2. `ConfigurationSelector` picks the config and calls `VideoDbContext.SetPath`.
3. `InitializeDatabaseAsync` migrates, then `EnsureFfmpegReady`. Windows downloads FFmpeg into `Temp/ffmpeg`; Linux must use the system FFmpeg because the downloadable build has no VA-API.

### Two encoding flows

Both use `VideoEncoder`, but they are separate code paths with different guarantees:

- **Library re-encode** (`VideoReencodeService`): the ReEncode page sets rows to `ToProcess`; the service works on a temp copy in `Processing/`, validates duration, resolution and FPS, replaces the original in place, restores its dates, and moves the original to `Trash/`, keeping its relative folder.
- **Drop folder** (`MainBackgroundService` → `VideoProcessorService.ProcessVideo`): a `FileSystemWatcher` on `Input/`, output to `Output/`. It does not touch the database and does **not** run the post-encode validation.

`CompressionStatus` lifecycle: `Original` → `ToProcess` → `Processing` → `Compressed` | `Bigger` | `FailedToCompress`. Cancelling returns rows to `Original`; at startup, leftover `Processing` rows return to `ToProcess`.

`Trash/` is not permanent: `MainBackgroundService.CleanTrash` keeps only the 100 newest files.

### Shared state

- `CommunicationService` (singleton) connects the UI and background services. Services poll its flags (`AskTreatFiles`, `AskReIndex`, `PreventSleep`, `AskClose`) in roughly 1-second loops; progress goes through the Rx `BehaviorSubject`s in `VideoProcessingStatus`.
- `VideoDbContext` is used two ways: through DI, and through static helpers that call `new VideoDbContext()` and rely on the static `DbPath`. Background services mostly use the static helpers.
- `LogMain` and `LogFfmpeg` (`GrinLogBase`) write to a Serilog file and the console, and publish each line on a `LastLine` subject.

### Configuration

Each named config has its own settings, database and work folders. `AppSettings` is both the JSON model and the path resolver:

- Config and DB: `SpecialFolder.ApplicationData/GrinVideoEncoder/<config>/` (`~/.config/...` on Linux).
- Work folders: `SpecialFolder.LocalApplicationData/GrinVideoEncoder/<config>/` (`~/.local/share/...` on Linux).

Computed paths are `[JsonIgnore]`, and `InitializePaths` must be called after `ConfigName` is set.

## Conventions and gotchas

- **Never add `-tag:v hvc1` to the `hevc_vaapi` parameters.** Every frame then decodes as garbage while FFmpeg reports success and duration, resolution and FPS stay correct, so validation passes (see `VideoEncoder.GetVaapiOutputParameters`).
- Xabe can report success when FFmpeg failed, so do not treat `conversion.Start` returning as proof of a good encode.
- Tracked `.cs` files use LF even though `.editorconfig` says `end_of_line = crlf`; keep LF so diffs stay clean. Indent with tabs.
- Blazor components split markup and logic into `X.razor` and `X.razor.cs`.
- The main `.csproj` excludes `Tests/**`, and tests reach `internal` members through `InternalsVisibleTo`.
- `ConfigManagerTests` reads and writes the real per-user config folders, using its own config names and cleaning up after itself.
- Run external commands through `Utils/ProcessRunner`: it reads stdout and stderr concurrently, so a full pipe can't block the command, and its `ArgumentList` overload keeps paths with spaces or quotes intact.
- Check `OperatingSystem.IsLinux()` / `IsWindows()` branches when touching process, path or power code: `shutdown`, FFmpeg paths and sleep inhibition all differ per OS.
