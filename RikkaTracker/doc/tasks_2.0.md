## 总体架构原则

- **分层设计**：`Core`（监控/策略/数据）→ `Services`（业务逻辑）→ `ViewModels` → `Views`。**UI 层在托盘模式下完全卸载**，确保后台时内存回到基线。
- **依赖最小化**：仅使用 `CommunityToolkit.Mvvm`（约 150KB）和 `Hardcodet.NotifyIcon.Wpf`（托盘图标）。**不引入任何数据库引擎、第三方图表库或重型 UI 框架。**
- **事件驱动**：基于 Win32 事件钩子获取焦点、窗口状态变化；空闲检测使用 `GetLastInputInfo` 的低成本调用。后台完全不轮询进程。
- **双模式资源目标**：
  - **后台监控模式**：内存 < 15MB，CPU < 0.1%，磁盘每 1 秒批量写入一次，无任何窗口、无通知。
  - **交互查看模式**：内存允许临时升至 50–80MB，CPU 可短暂用于绘图，保证 60fps 滚动缩放；关闭窗口后 2 秒内强制回收所有可视化资源，回到监控模式。

---

## 分步实施方案

### 阶段 1：极简骨架与双模式生命周期

**目标**：立项即支持仅托盘运行、无主窗口常态、开机启动可选，并能随时无感切换到交互模式。

#### 任务 1.1 创建 WPF 项目并配置基本结构
- 创建 .NET 8 WPF App，项目名 `TimeTracker`。
- 文件夹组织：
  ```
  Core/
     Monitor/     (Win32 封装)
     Models/      (贫血实体，如 ActivitySegment)
     Data/        (JSON 存储与读写服务)
     Strategies/  (黑白名单引擎)
  Services/       (协调服务，如 ConfigService)
  ViewModels/
  Views/
  Helpers/
  ```
- 删除默认 `MainWindow.xaml`，仅保留 `App.xaml` 作为入口，初始资源字典留空。

#### 任务 1.2 引入最小依赖
- NuGet 包（仅两项，后续不再添加）：
  - `CommunityToolkit.Mvvm`（用于 ViewModel 生成和命令绑定）
  - `Hardcodet.NotifyIcon.Wpf`（系统托盘图标）
- **去除** `Microsoft.Data.Sqlite` 及任何其他依赖。

#### 任务 1.3 实现托盘图标与双模式切换
- 在 `App.xaml.cs` 中：
  - **启动逻辑**：应用启动时不创建任何窗口，仅初始化托盘图标（`TaskbarIcon`），绑定右键菜单：“显示”、“退出”。同时启动后台监控服务（单例）。
  - **模式切换**：用户点击“显示” → 动态创建 `MainWindow` 主窗口，实例化甘特图视图及 ViewModel，加载当前数据。窗口关闭事件中，**不退出程序**，而是销毁所有 View/ViewModel，调用 `GC.Collect()` 强制回收，回到纯后台状态。
  - 支持启动参数 `/min`（开机静默启动，直接进入后台）和 `/show`（启动后直接显示主窗口）。
- 确保从后台到前台界面打开延迟 < 500ms，关闭界面后内存 < 15MB。

#### 任务 1.4 配置管理
- 使用 `System.Text.Json` 读写 `config.json`（位于 `%APPDATA%\TimeTracker\`），保存：
  - 全局设置：空闲超时（默认 5 分钟）、开机启动、数据存储路径（默认为 `%APPDATA%\TimeTracker\Data\`）、是否开启同步文件夹支持等。
  - 黑白名单规则列表。
- 提供 `ConfigService`（单例），启动时加载并驻留内存，配置修改后自动写入磁盘。

---

### 阶段 2：核心监控服务（后台零轮询）

**目标**：实现树状状态机，CPU 占用 < 0.1%，内存增量 < 5MB。

#### 任务 2.1 Win32 API 封装
创建 `Core/Monitor/Win32Api.cs`，静态封装：
- `SetWinEventHook`：监听 `EVENT_SYSTEM_FOREGROUND`、`EVENT_OBJECT_CREATE`、`EVENT_OBJECT_DESTROY`、`EVENT_SYSTEM_MINIMIZESTART`、`EVENT_SYSTEM_MINIMIZEEND`。
- `GetForegroundWindow`、`GetWindowThreadProcessId`、`GetWindowText`。
- `IsWindowVisible`、`IsIconic`。
- `GetLastInputInfo`：获取自上次用户输入以来的毫秒数。

#### 任务 2.2 应用状态机
创建 `Core/Monitor/AppActivityTracker.cs`（单例）：
- 内部维护 `Dictionary<int, AppState>`，以进程 ID 为键。
- 状态枚举：
  ```csharp
  enum ActivityStatus { Background = 0, ForegroundInactive = 1, ForegroundActive = 2 }
  ```
- 转化逻辑（完全由 Win32 事件触发）：
  - **焦点变化**：前一个焦点窗口进程降为 `ForegroundInactive`（若其窗口仍可见）或 `Background`；新进程升为 `ForegroundActive`。
  - **窗口最小化/恢复**：直接设 `Background` 或 `ForegroundInactive`。
  - **空闲检测**：仅在存在 `ForegroundActive` 应用时，启动一个 1 秒间隔的 `DispatcherTimer`（优先级最低）。每次 Tick 调用 `GetLastInputInfo`，若超过全局空闲阈值，则将当前活动应用降级为 `ForegroundInactive`；反之若之前因空闲降级且输入恢复，重新提升为 `ForegroundActive`。**当没有任何应用处于 Active 时，立即停止 Timer。**
- **性能注意**：Timer 不常驻，只在有活动应用时运行，极低开销。

#### 任务 2.3 状态变更事件发射
- 当任何进程的 `ActivityStatus` 真正改变时，触发 `AppActivityChanged` 事件，传递元组 `(processName, windowTitle, oldStatus, newStatus, timestamp)`。
- 严格去重：同一进程连续两次状态相同不下发事件。

#### 任务 2.4 进程启动/退出检测
- 每隔 5 秒通过 `Process.GetProcesses()` 获取当前所有进程 ID 的快照（使用 `Process[]` 轻量获取 PID 和名称，用完立即释放所有句柄）。
- 对比前一快照，发现新 PID 加入字典（默认设为 `Background` 状态），消失的 PID 发射退出事件并清理字典。
- 监控范围：仅处理用户会话进程（排除 System、Idle、svchost 等），进一步减少扫描量。

---

### 阶段 3：JSON 数据存储与时长统计（纯文本，极轻量）

**目标**：每日一个 JSON 文件，内存缓冲写入，支持归档与多端同步，后台写入对性能影响不可感。

#### 任务 3.1 JSON 数据结构设计
- 数据目录：`%APPDATA%\TimeTracker\Data\`（可由配置修改）。
- 每日文件命名 `YYYY-MM-DD.json`，内容为活动片段数组：
  ```json
  [
    {
      "processName": "devenv",
      "windowTitle": "TimeTracker - Microsoft Visual Studio",
      "status": 2,
      "startTime": "2026-05-07T09:15:32.123",
      "endTime": "2026-05-07T09:17:45.456"
    }
  ]
  ```
- `status` 取值：0=Background, 1=ForegroundInactive, 2=ForegroundActive。
- **片段由状态切换生成**，连续相同状态不记录重复片段。跨天片段由存储服务自动截断。

#### 任务 3.2 文件读写与缓冲服务
创建 `Core/Data/JsonLogStore.cs`：
```csharp
public class JsonLogStore
{
    private readonly ConcurrentQueue<ActivitySegment> _writeQueue = new();
    private readonly Dictionary<string, ActivitySegment> _openSegments; // 当前未闭合片段
    private List<ActivitySegment> _todayCache; // 当日完整片段缓存

    public void RecordTransition(string processName, string windowTitle, int newStatus, DateTime timestamp);
}
```
- **写入流程**：
  1. 关闭该进程上一个同状态片段（填写 `endTime`），将完成的片段放入 `_writeQueue`。
  2. 为新状态创建一个 `startTime` 未闭合的新片段，保存在 `_openSegments` 中。
  3. **缓冲刷新**：每 1 秒由 `System.Threading.Timer` 触发，从队列取出所有完成片段，追加到对应日期 JSON 文件。对于今日文件，维护一个内存 `List<ActivitySegment>` 缓存，每 5 秒全量序列化写回（写时先创建临时文件，再原子替换），确保不会因中途崩溃丢失超过 5 秒数据。
- **跨天处理**：刷新时若片段跨越午夜，按 00:00:00 切分为两个片段，分别放入两天的文件。
- **历史读取**：使用 `System.Text.Json` 异步反序列化，提供 `async IAsyncEnumerable<ActivitySegment> GetSegments(DateTime from, DateTime to)` 流式返回，避免一次加载全部。

#### 任务 3.3 内存与归档管理
- 后台模式下，仅保持**当天文件**的片段列表在内存（`_todayCache`），约 1000–2000 条记录，内存占用 ≈ 2MB。其他日期仅在查看时加载，用完释放。
- **自动归档**：每天凌晨 3 点（或应用启动时），将 30 天前的日 JSON 文件用 `GZipStream` 打包为 `archive-YYYY-MM.json.gz`，删除原文件。数据目录保持文件数 < 30，极大减少扫描开销。
- 应用退出时，强制刷新缓冲并关闭所有文件句柄。

#### 任务 3.4 导入/导出与多端同步
- **导出**：用户选择日期范围，将相关 JSON 及归档文件打包为 ZIP，供分享或备份。
- **导入**：接受 ZIP 或单个 JSON 文件，格式校验后直接放入数据目录；启动时扫描到新文件，自动将片段合并到对应日期（按时间追加，不覆盖）。
- **多端同步**：
  - 配置中可指定数据目录路径。建议直接指向 OneDrive、Dropbox 等同步文件夹。
  - 不同设备产生同日文件时，同步工具会生成冲突副本（如 `2026-05-07-PCName.json`）。程序在启动时提供“强制合并”按钮，将所有同日期文件合并后写回标准文件，用户手动触发即可。
  - 原子写入（先写 temp 再移动）保证单机写入不会损坏正在同步的文件。

#### 任务 3.5 与监控服务集成
- `AppActivityTracker` 的状态变更事件直接调用 `JsonLogStore.RecordTransition`，因写入已完全缓冲并异步，不影响监控主循环。

---

### 阶段 4：黑白名单 & 策略引擎

**目标**：实现灵活的忽略和免空闲规则，界面极简，规则实时生效。

#### 任务 4.1 规则引擎
- 创建 `Core/Strategies/FilterEngine.cs`，从 `ConfigService` 加载规则列表。
- 规则定义：
  ```json
  {
    "processPattern": "vlc",
    "ignore": false,
    "disableIdleDetection": true
  }
  ```
- 提供方法：`ShouldIgnore(processName)` 和 `ShouldDisableIdleDetection(processName)`，支持通配符 `*`。
- **预设列表**：内置一组常见“免空闲检测”进程名（`vlc`、`wmplayer`、`spotify`、`foobar2000`、`mstsc`、`devenv`）。

#### 任务 4.2 集成到状态机
- 在 `AppActivityTracker` 处理焦点变化和空闲检测前，调用 `FilterEngine`：
  - 若 `ShouldIgnore` 返回 true，则跳过该进程的所有状态更新和记录，相当于它不存在。
  - 若 `ShouldDisableIdleDetection` 返回 true，该应用获得焦点时，空闲检测 Timer 虽仍在运行，但对其判断结果做赦免（始终视为 Active）。实现方法：空闲检测回调中检查当前活动进程是否在豁免名单，是则不降级。

#### 任务 4.3 配置界面（极简）
- `SettingsView.xaml`：仅包含 `DataGrid` 展示规则列表（进程名、是否忽略、是否免空闲），右侧添加“增加”按钮和全局空闲时间滑动条。
- `SettingsViewModel` 直接操作 `ConfigService` 中的规则集合，修改后立即保存 JSON。
- 此界面仅在用户主动打开时创建，关闭后销毁。

---

### 阶段 5：甘特图时间线可视化（高性能混合绘制）

**目标**：交互模式内存提升 < 30MB，支持数万片段虚拟化绘制，流畅缩放悬停，关闭后释放。

#### 任务 5.1 自定义绘制控件（DrawingVisual 主机）
- 创建 `GanttChart` 控件，继承 `FrameworkElement`。
- 内部维护一个 `VisualCollection`，仅包含一个 `DrawingVisual` 子对象。
- **渲染逻辑**：
  - 接收数据 `IReadOnlyList<GanttSegment>`，每个段包含 `ProcessName`, `Start`, `End`, `Status`, `RowIndex`。
  - 当数据或视口变化时，调用 `DrawingVisual.RenderOpen()` 获取 `DrawingContext`。
  - **虚拟化**：根据 `ScrollViewer.VerticalOffset` 和 `ViewportHeight` 计算可见行范围，**只绘制这些行的横条**（使用 `DrawRectangle` 或 `DrawRoundedRectangle`）。状态颜色：Active=深蓝 `#1976D2`，Inactive=浅蓝 `#BBDEFB`，Background=浅灰 `#E0E0E0`（可选，通过开关控制显示）。
  - 横轴：将时间范围线性映射到像素宽度，支持缩放因子（`pixelsPerHour`）。
- 宿主在 `ScrollViewer` 内，处理水平和垂直滚动条。

#### 任务 5.2 数据提供与聚合
- `GanttViewModel` 后台调用 `JsonLogStore.GetSegments(dateRange)` 异步加载片段。
- 将原始片段按进程名分组，合并断续的同状态片段（可选优化），生成 `GanttSegment` 列表，计算行索引。
- 数据按需加载：仅当用户切换日期或缩放范围超出已缓存数据时重新查询。关闭窗口时清空列表。

#### 任务 5.3 高性能交互（叠加透明层）
- **全局 ToolTip**：不使用每段独立 ToolTip。在 `GanttChart` 上放置一个隐藏的 `Popup` 作为全局提示。
  - 鼠标移动时，通过 `VisualTreeHelper.HitTest` 对 `DrawingVisual` 进行测试，获取命中的 `GanttSegment` 引用（存储在 `DrawingVisual` 的 `VisualHit` 属性中或自行维护映射表）。
  - 动态设置 `Popup` 的位置和内容，显示“进程名、时间段、状态、时长”。
- **点击导航**：同样通过 HitTest 获取点击的段，触发命令导航到 `ProcessDetailView`。
- **缩放**：`MouseWheel` 事件调整 `pixelsPerHour`，限制最小/最大缩放比，调用 `InvalidateVisual()` 重绘。始终以鼠标所在时间点为中心缩放，体验自然。
- **滚动**：通过 `ScrollViewer` 原生滚动，虚拟化绘制自动适配。

#### 任务 5.4 详情页（可选）
- `ProcessDetailView.xaml`：简单展示当天该应用的所有片段列表（`ListView` 或 `DataGrid`），关闭后释放。

#### 任务 5.5 资源回收验证
- 主窗口关闭时，保证 `GanttChart` 控件被移除，`DrawingVisual` 清空，所有数据缓存置空。调用 `GC.Collect()` 后内存迅速回落至后台基线。

---

## 实施检查点（不变）

1. **阶段 1**：可运行托盘程序，无窗口，生成配置文件，可加载后台监控。  
2. **阶段 2**：通过调试日志观察到焦点切换、空闲降级等状态变化完全准确。  
3. **阶段 3**：日 JSON 文件正确生成，片段连续无丢失，跨天截断正确。  
4. **阶段 4**：设置界面可增删规则，被忽略应用不再出现于日志，豁免应用在播放时保持活动。  
5. **阶段 5**：打开主窗口看到精确的甘特图，缩放流畅，悬停显示信息，关闭后内存恢复基线。

---

## 最终低资源保证策略

| 方面 | 后台监控模式 | 交互查看模式 |
|------|-------------|-------------|
| **CPU** | < 0.1%（事件+1s空闲Timer+5s进程快照） | 绘图时短暂提升，交互时保持低 |
| **内存** | < 15MB（状态机、字典、当天缓存、缓冲队列） | 临时 < 80MB（数据缓存+DrawingVisual+界面元素），关闭后 < 15MB |
| **磁盘 I/O** | 每 1 秒批量写 JSON，每次写入数 KB，几乎无感 | 首次加载短期读取，之后仅读当天 |
| **存在感** | 仅系统托盘，无窗口无通知，启动静默 | 正常窗口，允许提示和缩放动画 |
| **包体** | 启用 `PublishTrimmed=true`，框架依赖包 < 2MB，自包含约 15MB | 同左 |
