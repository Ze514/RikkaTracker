<div align="center">
    
  <img src="assets/app-icon-4.png" width="80" height="80" alt="icon">
  
# RikkaTracker

<br>

**[ENGLISH README](README.md)**

</div>


**RikkaTracker** 是一款专为 Windows 设计的轻量级应用活动追踪工具。它能够精确记录并分析您在电脑上的每一分钟，通过直观的时间轴（甘特图）和多维度的统计数据，帮助您洞察时间分配，优化工作效率。

---

## 核心特性

### 1. 三档活动追踪模型
不同于简单的“运行时间”统计，RikkaTracker 采用深度的三层状态机模型：
- **前台活跃 (Foreground Active)**：您正在操作的应用（焦点窗口 + 实时交互）。
- **前台非活动 (Foreground Inactive)**：窗口可见但非焦点，或因空闲超时自动降级。
- **后台运行 (Background)**：进程存活但窗口最小化或无窗口，为您保留应用全生命周期的完整记录。

### 2. 现代化“时间轴”视图
基于高性能 `DrawingVisual` 混合绘制技术，RikkaTracker 提供了一个极为流畅的时间线界面：
- **视觉分级**：根据活动状态自动调整线条粗细与颜色深浅，一眼区分专注时段与挂机时间。
- **无缝缩放**：支持从小时到分钟级的自由缩放，精确还原全天活动细节。
- **智能交互**：毫秒级响应的悬浮提示，动态展示应用名称、窗口标题及持续时长。

### 3. 卓越的应用识别与图标提取
借鉴了成熟的开源方案，实现了比原生 Windows 任务管理器更强大的图标获取能力：
- **UWP 深度支持**：精准解析 UWP 应用（如 Edge、记事本）的 AppxManifest，支持高分屏图标缩放。
- **复杂进程解析**：解决 `ApplicationFrameHost` 带来的识别误差，直击真实后台进程。
- **高质量提取**：采用高保真图标提取技术，适配各类标准 Win32 应用。

---

## 快速开始

### 环境要求
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022

### 构建步骤
1. 克隆仓库：
   ```bash
   git clone https://github.com/remnant-song/RikkaTracker.git
   ```
2. 使用 Visual Studio 打开 `RikkaTracker.sln`。

---
*Developed with ❤️ by [remnant-song](https://github.com/remnant-song)*
