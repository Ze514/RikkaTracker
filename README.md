# RikkaTracker

[简体中文](./README_zh.md)

**RikkaTracker** is a lightweight application activity tracking tool designed specifically for Windows. It precisely records and analyzes every minute spent on your computer, helping you gain insights into time allocation and optimize productivity through intuitive timelines (Gantt charts) and multi-dimensional statistics.

> **Core Philosophy**: Ultra-lightweight (< 20MB background RAM), Privacy-first (Full local storage), Visual Excellence (Modern WPF interaction).

---

## ✨ Key Features

### 1. Triple-State Activity Tracking Model
Unlike simple "uptime" statistics, RikkaTracker employs a deep three-tier state machine:
- **Foreground Active**: The application you are actively interacting with (Focus window + Real-time interaction).
- **Foreground Inactive**: Window is visible but not focused, or automatically demoted due to idle timeout.
- **Background**: Process is alive but the window is minimized or hidden, preserving the complete lifecycle record.

### 2. Modern "Timeline" View
Built on high-performance `DrawingVisual` hybrid rendering technology, RikkaTracker provides a fluid timeline interface:
- **Visual Hierarchy**: Automatically adjusts line thickness and color depth based on activity status, distinguishing deep focus from idle time at a glance.
- **Seamless Zoom**: Supports free scaling from hours down to minutes, accurately reconstructing daily activity details.
- **Smart Interaction**: Millisecond-response tooltips dynamically display app names, window titles, and durations.

### 3. Superior App Identification & Icon Extraction
Leveraging mature open-source solutions, it achieves stronger icon retrieval than the native Windows Task Manager:
- **Deep UWP Support**: Precisely parses AppxManifest for UWP apps (e.g., Edge, Notepad) with support for high-DPI scaling.
- **Complex Process Parsing**: Resolves identification errors caused by `ApplicationFrameHost`, targeting the actual background process.
- **High-Fidelity Extraction**: Uses advanced icon extraction techniques compatible with all standard Win32 applications.

---

## 🛠️ Technical Architecture

RikkaTracker uses a layered architecture to ensure monitoring accuracy and UI responsiveness:

- **Core Layer**:
  - **Monitor**: Implemented via Win32 Event Hooks (`SetWinEventHook`) with zero polling overhead.
  - **Data**: High-performance asynchronous writes using SQLite (WAL mode).
  - **Strategy**: Flexible engine for whitelists, blacklists, and application exemptions.
- **Service Layer**:
  - **IconService**: Intelligent icon caching and path backtracking system.
  - **DataService**: Abstract data access layer supporting complex time-span aggregation queries.
- **Presentation Layer (UI)**:
  - **WPF**: Utilizes the `WPF-UI` framework, providing native Mica glass effects and smooth animations.

---

## 📂 Project Structure

```text
RikkaTracker/
├── Controls/        # Custom high-performance drawing controls (GanttChart, etc.)
├── Core/            # Monitoring logic, database implementation, strategy engine
├── Models/          # Data entities for activity segments, config, etc.
├── Services/        # Icon processing, local config, data brokerage services
├── ViewModels/      # Business logic for each page
├── Views/           # Modern UI pages (Dashboard, Timeline, Settings)
├── doc/             # Detailed design docs and development task lists
└── RikkaTracker.csproj
```

---

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022

### Build Steps
1. Clone the repository:
   ```bash
   git clone https://github.com/remnant-song/RikkaTracker.git
   ```
2. Open `RikkaTracker.sln` with Visual Studio.
3. Restore NuGet packages and Run (starts in tray mode by default; click "Timeline" to view activity).

---

## 🔒 Privacy Statement

RikkaTracker strictly adheres to the principle of **Local Privacy**:
- **Zero Uploads**: All data is stored only in a local encrypted SQLite database.
- **No Auditing**: The program contains no telemetry, tracking points, or third-party analysis libraries.
- **Full Control**: Support for one-click history clearing and blacklist settings to ignore sensitive apps.

---
*Developed with ❤️ by [remnant-song](https://github.com/remnant-song)*
