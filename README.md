<div align="center">
    
  <img src="assets/app-icon-4.png" width="80" height="80" alt="icon">
  
# RikkaTracker

<br>

**[中文文档](README_zh.md)**

</div>


**RikkaTracker** is a lightweight application activity tracking tool designed for Windows. It accurately records and analyzes every minute you spend on your computer, helping you understand time allocation and optimize work efficiency through an intuitive timeline (Gantt chart) and multi-dimensional statistics.

---

## Key Features

### 1. Three-Tier Activity Tracking Model
Unlike simple "running time" statistics, RikkaTracker employs a deep three-state machine model:
- **Foreground Active**: The application you are actively using (focused window + real-time interaction).
- **Foreground Inactive**: Window visible but not focused, or automatically downgraded due to idle timeout.
- **Background**: Process alive but window minimized or without a window – preserving a complete record of the application's full lifecycle.

### 2. Modern Timeline View
Built on high-performance `DrawingVisual` hybrid rendering, RikkaTracker provides an extremely smooth timeline interface:
- **Visual Hierarchy**: Line thickness and color intensity automatically adjust based on activity state, allowing you to distinguish focused work periods from idle time at a glance.
- **Seamless Zoom**: Freely zoom from hours down to minutes, revealing full daily activity details.
- **Smart Interaction**: Millisecond-response tooltips dynamically display application name, window title, and duration.

### 3. Superior Application Recognition & Icon Extraction
Drawing from mature open-source solutions, it achieves more powerful icon acquisition than the native Windows Task Manager:
- **Deep UWP Support**: Accurately parses UWP applications (e.g., Edge, Notepad) AppxManifest and supports high-DPI icon scaling.
- **Complex Process Resolution**: Resolves identification errors caused by `ApplicationFrameHost` to directly target the real background process.
- **High-Quality Extraction**: Uses high-fidelity icon extraction techniques, compatible with a wide range of standard Win32 applications.

---

## Quick Start

### Requirements
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022

### Build Instructions
1. Clone the repository:
   ```bash
   git clone https://github.com/remnant-song/RikkaTracker.git
   ```
2. Open `RikkaTracker.sln` with Visual Studio.

---
*Developed with ❤️ by [remnant-song](https://github.com/remnant-song)*