# GrinVideoEncoder

A local video re-encoding tool that scans your video library, identifies files that can benefit from modern compression, and re-encodes them using GPU-accelerated HEVC (H.265) — all managed through a web-based Blazor UI.

## Features

### GPU-Accelerated Encoding
- Automatically detects your **NVIDIA** or **AMD** GPU and uses hardware-accelerated HEVC encoding (`hevc_nvenc` / `hevc_amf`).
- Falls back to CPU encoding when no compatible GPU is found or when **Force CPU** is enabled.

### Video Library Indexing
- Point the application at a root folder and it indexes every video file it finds.
- Tracks original size, compressed size, resolution, FPS, duration, and a quality ratio for each file.
- Supports `.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.flv`, `.webm` by default (configurable).
- Folders can be excluded via an ignore list.

### Smart Re-Encoding
- Filters videos by **quality ratio threshold**, **minimum file size**, and **minimum file age** so only files likely to benefit from re-encoding are selected.
- Validates every encode: checks duration, resolution, FPS, and file size against the original before replacing it.
- Originals are moved to a **Trash** folder (kept for safety) and the compressed file replaces the original in-place, preserving creation and modification dates.
- Files that end up larger after encoding are flagged as **Bigger** and left untouched.

### Drop-Folder Workflow
- Drop a video file into the **Input** folder and it is automatically picked up, encoded, and placed in the **Output** folder. The original is moved to **Trash**.

### Dashboard
- At-a-glance stats: total videos, overall compression rate, original vs. compressed size, pending work, free disk space on the work and data drives, and trash size.

### Video Browser
- Sortable, filterable data grid of all indexed videos with status icons (Original, Compressed, Failed, Bigger, To Process, Processing, Kept).
- Right-click context menu to open the containing folder, copy the directory path, replay a video, reset errors, or mark a file as kept.
- Grid column settings are persisted across sessions.

### Sanity Checks
- After indexing, each video is verified for stream integrity (resolution, duration, codec presence) and tagged with a sanity status.

### Sleep Prevention & Scheduled Shutdown
- Prevents the computer from sleeping while encoding is in progress.
- Optional **scheduled shutdown**: set a date/time and the machine will shut down automatically once the current encode finishes.

### Logging
- Separate **Main** and **FFmpeg** log consoles visible in the UI.
- Logs are also written to disk via Serilog.

### Multi-Configuration Support
- Run multiple independent configurations (each with its own database, settings, and work folders).
- Select or create a configuration at startup, or pass `--config <name>` on the command line.

### Folder Maintenance
- View file counts and sizes for Processing, Trash, Log, Failed, and Temp folders.
- Browse or clear any folder directly from the Settings page.

---

## Installation

### Windows Installer
Download the latest `GrinVideoEncoder-vX.X.X-win-x64-setup.exe` from the [Releases](https://github.com/GrinwaldFlo/GrinVideoEncoder/releases) page and run the installer. It creates a Start Menu entry and an optional desktop shortcut.

### Linux
Download the latest `GrinVideoEncoder-vX.X.X-linux-x64.tar.gz` from the [Releases](https://github.com/GrinwaldFlo/GrinVideoEncoder/releases) page.

```bash
tar -xzf GrinVideoEncoder-*-linux-x64.tar.gz -C /opt/GrinVideoEncoder
cd /opt/GrinVideoEncoder
./GrinVideoEncoder
```

### Build from Source
Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/GrinwaldFlo/GrinVideoEncoder.git
cd GrinVideoEncoder
dotnet run
```

---

## Prerequisites

- **NVIDIA** or **AMD** GPU recommended for hardware-accelerated encoding.
- **FFmpeg** is downloaded automatically on first launch — no manual installation required.

---

## Getting Started

### 1. Launch the Application
Run `GrinVideoEncoder.exe` (Windows) or `./GrinVideoEncoder` (Linux). On first launch you will be prompted to create a configuration:

```
No configurations found. Enter a name for the new configuration: my-library
Created new configuration: my-library
```

If you already have configurations, pick one from the list or create a new one.
You can also skip the prompt with `--config <name>`.

### 2. Open the Web UI
After startup the console displays the listening URL:

```
 -> Listening on: http://localhost:14563
```

Open that URL in your browser.

### 3. Configure the Indexer Path
Navigate to the **Settings** page and set the **Indexer Path** to the root folder of your video library (e.g., `D:\Videos`). Adjust the other settings as needed:

| Setting | Description | Default |
|---|---|---|
| **Quality Level (CRF/CQ)** | Lower = better quality / larger files. Recommended 18–28. | `23` |
| **Force CPU** | Bypass GPU detection and use software encoding. | `false` |
| **Encoding Threshold** | Quality ratio above which a video is considered for re-encoding. | `900` |
| **Min File Size (MB)** | Ignore files smaller than this. | `100` |
| **Min File Age (hours)** | Ignore recently modified files. | `100` |
| **Video Extensions** | Comma-separated list of extensions to index. | `.mp4, .mkv, .avi, .mov, .wmv, .flv, .webm` |
| **Ignore Folders** | Comma-separated folder names to skip during indexing. | *(empty)* |

Click **Save** when done.

### 4. Wait for Indexing
The application scans the Indexer Path in the background. Progress is visible in the **Main log** tab on the Home page. The **Dashboard** tab updates with stats as files are indexed.

### 5. Re-Encode Videos
Go to the **Re-Encode** page. The grid shows videos that exceed the quality ratio threshold. Adjust the threshold, minimum size, and minimum age filters if needed, then click **Start Re-encoding**.

Progress is shown on the Home page:
- **Dashboard** — live stats.
- **Main log** — high-level messages (started, completed, errors).
- **FFMPEG log** — raw FFmpeg output.

You can **Cancel** the current task or schedule a **Shutdown** time from the Home page toolbar.

### 6. Review Results
Switch to the **Videos** page to browse all indexed files. Use the status filter buttons to show only compressed, failed, or original files. Right-click a row to open the file location or play the video.

---

## Folder Structure

The application uses two root locations per configuration:

| Location | Path |
|---|---|
| **Config & DB** (roaming) | `%APPDATA%/GrinVideoEncoder/<config>/` |
| **Work folders** (local) | `%LOCALAPPDATA%/GrinVideoEncoder/<config>/` |

Work sub-folders:

| Folder | Purpose |
|---|---|
| `Input` | Drop files here for automatic encoding. |
| `Output` | Encoded files from the drop-folder workflow. |
| `Processing` | Temporary location while a file is being encoded. |
| `Trash` | Original files replaced by their compressed version. |
| `Failed` | Files that could not be encoded. |
| `Temp` | FFmpeg binaries and temporary data. |
| `Log` | Serilog log files. |

---

## License

This project is licensed under the [MIT License](LICENSE).
