### 阶段 1：极简骨架与双模式生命周期（调整）
### 阶段 1：极简骨架与双模式生命周期

**目标**：立项即支持仅托盘运行、无主窗口常态、开机启动可选，并能随时无感切换到交互模式。

#### 任务 1.1 创建 WPF 项目并配置基本结构
- 创建 .NET 8 WPF App，项目名 `TimeTracker`。
- 文件夹组织：
  ```
  Core/
     Monitor/
     Models/          (ActivitySegment 实体)
     Data/            (SQLite 存储实现)
     Strategies/
     Export/          (可选导出服务)
  Services/           (ConfigService 等)
  ViewModels/
  Views/
  Helpers/
  ```
- 删除默认 `MainWindow.xaml`，仅保留 `App.xaml` 作为入口，初始资源字典留空。

#### 任务 1.2 引入最小依赖
- NuGet 包（仅两项，后续不再添加）：
  - `CommunityToolkit.Mvvm`（用于 ViewModel 生成和命令绑定）
  - `Hardcodet.NotifyIcon.Wpf`（系统托盘图标）
-  `Microsoft.Data.Sqlite`

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

### 阶段 3：SQLite 数据存储与时长统计

**目标**：高性能追加写入，强事务安全，后台内存低，支持可选 Git 友好导出。

#### 任务 3.1 数据库设计与连接管理
- 数据库文件：`%APPDATA%\TimeTracker\Data\tracker.db`
- 使用 `Microsoft.Data.Sqlite`，连接串包含 `Data Source=...;Mode=ReadWriteCreate;Cache=Shared`。
- 打开后立即执行以下优化 PRAGMA：
  ```sql
  PRAGMA journal_mode=WAL;
  PRAGMA synchronous=NORMAL;
  PRAGMA cache_size=-2000;   -- 约 2MB 页面缓存
  PRAGMA temp_store=MEMORY;
  PRAGMA mmap_size=268435456; -- 256MB 内存映射（如果可用）
  ```
- 核心表设计：
  ```sql
  CREATE TABLE ActivityLog (
      Id INTEGER PRIMARY KEY AUTOINCREMENT,
      ProcessName TEXT NOT NULL,
      WindowTitle TEXT,
      Status INTEGER NOT NULL,  -- 0:Background, 1:Inactive, 2:Active
      StartTime TEXT NOT NULL,   -- ISO 8601
      EndTime TEXT NOT NULL
  );
  CREATE INDEX IX_Log_Process_Start ON ActivityLog(ProcessName, StartTime);
  CREATE INDEX IX_Log_Start ON ActivityLog(StartTime);
  ```
- 所有时间存储为 ISO 8601 文本，便于直接比对和 debug，不影响性能（SQLite 无专用日期类型）。

#### 任务 3.2 存储服务实现（写入策略）
创建 `Core/Data/SqliteLogStore.cs`，实现接口 `IActivityLogStore`：
```csharp
public class SqliteLogStore : IActivityLogStore
{
    private readonly string _connectionString;
    private readonly ConcurrentQueue<ActivitySegment> _writeQueue;
    private readonly Timer _flushTimer;
    private SqliteConnection _connection; // 后台保持单连接

    public void RecordTransition(string processName, string windowTitle, int newStatus, DateTime timestamp);
}
```
- **写入流程**（与内存未闭合片段配合）：
  1. 状态变化时，内存中闭合旧片段，生成完整 `ActivitySegment`。
  2. 将完整片段放入 `_writeQueue`。
  3. **缓冲刷新**：每 **1 秒**由 Timer 触发，从队列中取出所有片段，使用事务批量 `INSERT`。命令使用参数化，预编译可复用 `SqliteCommand`。
  4. 不执行任何 `UPDATE`，仅追加插入。
- **跨天处理**：若 `StartTime` 和 `EndTime` 跨日，在插入前拆分为两条记录，保证每一天完整性。
- **连接生命周期**：后台运行时保持一个 `SqliteConnection` 打开，并定期（每小时）执行 `PRAGMA wal_checkpoint(TRUNCATE)` 回收 WAL 文件。

#### 任务 3.3 读取与聚合服务
- 提供方法：
  ```csharp
  Task<List<ActivitySegment>> GetSegments(DateTime from, DateTime to);
  Task<Dictionary<string, TimeSpan>> GetTotalTimeByProcess(DateTime from, DateTime to, int? statusFilter = null);
  ```
- 实现使用分页查询或直接 SELECT，利用复合索引 `IX_Log_Process_Start`。
- 甘特图数据加载：直接查询 `GetSegments` 返回原始片段，由 ViewModel 分组处理。
- 复杂统计（如日总计）可通过 SQL 聚合直接在数据库完成，应用层无需循环累加。

#### 任务 3.4 后台维护与归档
- **每日 WAL checkpoint**：凌晨 3 点（或应用闲置时）执行 `PRAGMA wal_checkpoint(TRUNCATE)` 并可选执行 `VACUUM`（极低频率，每月一次）。
- **数据归档**（可选）：可将 90 天前的记录导出为 JSON Lines 归档并从 SQLite 删除，以控制 db 文件大小。但鉴于个人数据量仅数十 MB/年，可暂时不做自动删除，只提供手动清理。
- **备份**：应用退出时备份数据库到 `tracker_backup.db`（可选）。

#### 任务 3.5 Git 友好导出（新增可选任务）
- 创建 `Core/Export/GitSyncService.cs`，可通过设置启用。
- 功能：
  - 维护一个“上次导出时间”记录。
  - 定时（如每 15 分钟）或手动触发，将上次导出后新增的片段从 SQLite 查询出来，追加写入 Git 仓库目录下的 `daily-YYYY-MM-DD.jsonl` 文件。
  - 保证导出的 JSON Lines 格式与之前方案一致，方便 `git diff`。
- 这样既享用了 SQLite 的高性能本地存储，又满足了 Git 版本控制需求。
- 配置中增加开关 `GitSyncEnabled` 和目标路径。

#### 任务 3.6 与监控服务集成
- `AppActivityTracker` 的状态变更事件处理程序调用 `SqliteLogStore.RecordTransition`，写入异步进行，不影响监控。

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
- `GanttViewModel` 后台调用 `SqliteLogStore.GetSegments` 异步加载片段。
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

## 实施检查点

1. **阶段 1**：可运行托盘程序，生成配置文件。  
2. **阶段 2**：监视状态切换日志。  
3. **阶段 3**（新）：状态片段正确写入 SQLite，跨天拆分，可执行 SQL 查询验证。数据库文件可常驻，WAL 模式生效。可选 Git 导出生成 JSON Lines。  
4. **阶段 4**：设置界面正常，规则生效。  
5. **阶段 5**：甘特图基于 SQLite 数据加载，流畅缩放，关闭后内存回落。

---

## 最终低资源保证策略

| 方面 | 后台监控模式 | 交互查看模式 |
|------|-------------|-------------|
| **CPU** | < 0.1%（事件定时器+1s批量INSERT） | 绘图短暂提升，SQL 聚合毫秒级 |
| **内存** | < 20MB（状态机+连接+2MB页面缓存+队列） | 临时 < 80MB，关闭 < 20MB |
| **磁盘 I/O** | 每秒批量写 SQLite（WAL，极快） | 首次加载可能读较多页，之后缓存命中 |
| **存在感** | 仅托盘 | 正常窗口 |
| **包体** | 增加 Microsoft.Data.Sqlite (~1.2MB)，修剪后 < 2MB | 同左 |

**SQLite 的引入带来了事务安全、强大查询能力和仍可接受的资源增量，同时通过可选导出层完美保留了 Git 友好特性。方案整体依然极致轻量且易于维护。**