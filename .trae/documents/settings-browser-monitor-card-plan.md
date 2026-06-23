# 将"浏览器监控"设为独立卡片 + 扩展下载入口

## 当前状态分析

设置页 (`SettingsView.xaml`) 目前有 3 个 `ui:Card` 卡片：
1. **Card 1** (第20-144行): 全局设置 — 包含"浏览器监控"开关 (第90-100行)
2. **Card 2** (第146-171行): 软件更新
3. **Card 3** (第173-216行): 数据管理

"浏览器监控"当前仅有一个 ToggleSwitch，没有扩展下载入口。

GitHub Release 中打包的浏览器扩展文件名为 `RikkaTracker Sentry.zip`，内部结构为 `extension/manifest.json`。

## 修改方案

### 1. SettingsView.xaml — UI 结构调整

**操作 A**: 从 Card 1 中删除浏览器监控行（第90-100行）。

**操作 B**: 在 Card 1 和 Card 2（软件更新卡片）之间插入一个新的 `ui:Card`，用作浏览器监控独立卡片。新卡片布局：

```
[Card BrowserMonitor]
├── Grid (两列: * | Auto)
│   ├── StackPanel (左)
│   │   ├── TextBlock (标题: StrBrowserMonitor)
│   │   └── TextBlock (描述: StrBrowserMonitorDesc)
│   └── ToggleSwitch (右，绑定 WebMonitorEnabled)
│
├── Separator (或留白)
│
├── StackPanel (下载区域)
│   ├── Button "从商店获取" (命令: OpenExtensionStoreCommand, 图标: Shop/商店)
│   └── Expander/区域 "手动安装" (展开/折叠)
│       └── StackPanel (详细步骤，文本说明)
│           ├── 步骤1: 从 GitHub Releases 下载 "RikkaTracker Sentry.zip"
│           ├── 步骤2: 解压到本地文件夹
│           ├── 步骤3: 打开 Chrome/Edge 扩展管理页面
│           ├── 步骤4: 开启"开发者模式"
│           └── 步骤5: 点击"加载已解压的扩展"，选择解压后的 extension 文件夹
└──
```

**关键设计决策**:
- "从商店获取"按钮始终可见（在卡片主体区域）
- "手动安装"指引默认折叠，不占用设置页空间
- 使用 `Visibility` 绑定 + 一个 `Expander` 风格控件来实现折叠
- 由于 WPF-UI 不提供原生 Expander，使用 `ToggleButton` + `StackPanel` 配合 `Visibility` 绑定的方式来实现折叠效果。或者可以简化，用一个按钮"展开/折叠安装指引 + 一个可折叠的面板"

考虑到 WPF-UI 没有内置的 Expander 控件，我们可以仿照"软件更新"卡片的进度区域方式：使用一个 Button 来切换显示/隐藏指引区域，通过 `IsManualInstallVisible` 属性控制 Visibility。

**关于"商店链接"的跳转**: 这里可以设置两个按钮，一个跳转到 Chrome Web Store，一个跳转到 Edge Add-ons。但如果用户只发布了一个商店（比如只发布了 Chrome 扩展），那就只需要一个按钮。

**简化方案**:
- 在卡片底部使用一个可折叠区域（通过 Button + Visibility 控制）
- 按钮文字: "查看安装指引"
- 展开后显示：商店链接按钮 + 手动安装步骤

更优的体验：将"商店获取"和"手动安装"都放在折叠区域内，卡片主区域只显示标题、描述和开关。

这样最简洁，且完全符合用户要求"不应直接显示而占据设置页空间"。

因此调整后的设计：

```
[Card BrowserMonitor]
├── Grid (两列: * | Auto) ── 标题行
│   ├── StackPanel (左)
│   │   ├── TextBlock: "浏览器监控"
│   │   └── TextBlock: "通过 Chrome/Edge 扩展... "
│   └── ToggleSwitch (右)
│
└── Button "查看安装指引" (ToggleButton风格，控制展开/折叠)
    └── 展开区域 (默认Collapsed)
        ├── Button "从 Chrome 网上应用店获取" → 打开商店URL
        ├── Button "从 Edge 加载项获取" → 打开Edge商店URL
        └── StackPanel (手动安装步骤)
            ├── TextBlock: "方式二：手动安装"
            └── 步骤列表 (TextBlock)
```

### 2. SettingsViewModel.cs — 新增属性和命令

```csharp
// 浏览器扩展商店URL常量
private const string ChromeExtensionStoreUrl = "https://chrome.google.com/webstore/detail/...";
private const string EdgeExtensionStoreUrl = "https://microsoftedge.microsoft.com/addons/detail/..."; 

// GitHub Release 下载页URL
private const string GitHubReleaseUrl = "https://github.com/remnant-song/RikkaTrack/releases/latest";

// 安装指引展开状态
[ObservableProperty]
private bool _isInstallGuideExpanded;

// 命令：打开 Chrome 商店
[RelayCommand]
private void OpenChromeStore()
{
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = ChromeExtensionStoreUrl,
        UseShellExecute = true
    });
}

// 命令：打开 Edge 商店
[RelayCommand]
private void OpenEdgeStore()
{
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = EdgeExtensionStoreUrl,
        UseShellExecute = true
    });
}

// 命令：切换安装指引展开/折叠
[RelayCommand]
private void ToggleInstallGuide()
{
    IsInstallGuideExpanded = !IsInstallGuideExpanded;
}
```

### 3. 国际化资源文件

**zh-CN.xaml** 新增:
```xml
<sys:String x:Key="StrBrowserMonitorInstallGuide">查看安装指引</sys:String>
<sys:String x:Key="StrBrowserMonitorHideGuide">收起指引</sys:String>
<sys:String x:Key="StrBrowserMonitorGetFromStore">从浏览器扩展商店获取</sys:String>
<sys:String x:Key="StrBrowserMonitorChromeStore">Chrome 网上应用店</sys:String>
<sys:String x:Key="StrBrowserMonitorEdgeStore">Edge 加载项商店</sys:String>
<sys:String x:Key="StrBrowserMonitorManualTitle">方式二：手动安装（适用于开发者/离线环境）</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep1">1. 前往 GitHub Releases 页面下载 "RikkaTracker Sentry.zip"</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep2">2. 将压缩包解压到本地文件夹（确保内部有 extension 目录）</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep3">3. 打开 Chrome/Edge 浏览器，进入扩展管理页面 (chrome://extensions 或 edge://extensions)</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep4">4. 开启右上角的"开发者模式"</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep5">5. 点击"加载已解压的扩展"，选择解压后得到的 extension 文件夹</sys:String>
<sys:String x:Key="StrBrowserMonitorOpenRelease">打开 GitHub Releases</sys:String>
```

**en-US.xaml** 新增对应英文翻译:
```xml
<sys:String x:Key="StrBrowserMonitorInstallGuide">View Installation Guide</sys:String>
<sys:String x:Key="StrBrowserMonitorHideGuide">Hide Guide</sys:String>
<sys:String x:Key="StrBrowserMonitorGetFromStore">Get from Browser Extension Store</sys:String>
<sys:String x:Key="StrBrowserMonitorChromeStore">Chrome Web Store</sys:String>
<sys:String x:Key="StrBrowserMonitorEdgeStore">Edge Add-ons Store</sys:String>
<sys:String x:Key="StrBrowserMonitorManualTitle">Method 2: Manual Install (for developers / offline use)</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep1">1. Go to GitHub Releases and download "RikkaTracker Sentry.zip"</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep2">2. Extract the zip to a local folder (ensure the "extension" directory is inside)</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep3">3. Open Chrome/Edge and go to the extensions page (chrome://extensions or edge://extensions)</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep4">4. Enable "Developer mode" in the top-right corner</sys:String>
<sys:String x:Key="StrBrowserMonitorManualStep5">5. Click "Load unpacked" and select the extracted "extension" folder</sys:String>
<sys:String x:Key="StrBrowserMonitorOpenRelease">Open GitHub Releases</sys:String>
```

## 修改文件清单

| # | 文件 | 操作 | 说明 |
|---|---|---|---|
| 1 | `SettingsView.xaml` | 编辑 | 删除第90-100行的浏览器监控Grid；在Card1和Card2之间插入新的浏览器监控卡片 |
| 2 | `SettingsViewModel.cs` | 编辑 | 新增 `IsInstallGuideExpanded` 属性及 `OpenChromeStoreCommand`、`OpenEdgeStoreCommand`、`ToggleInstallGuideCommand` 命令 |
| 3 | `Resources/Languages/zh-CN.xaml` | 编辑 | 新增9个中文国际化字符串 |
| 4 | `Resources/Languages/en-US.xaml` | 编辑 | 新增9个英文国际化字符串 |

## 关键设计决策

1. **安装指引默认折叠**: 使用 `IsInstallGuideExpanded` + `BooleanToVisibilityConverter` 控制展开区域可见性，按钮文字随状态切换（"查看安装指引" / "收起指引"）
2. **商店链接使用 Button + Process.Start**: 直接调用系统默认浏览器打开商店 URL，与项目中打开日志文件夹的方式一致
3. **扩展文件名**: "RikkaTracker Sentry.zip" — 与 release.yml 中的打包文件名一致
4. **使用现有资源**: `BooleanToVisibilityConverter` 在 SettingsView.xaml 中已声明（第158行使用），可直接复用；`InverseBooleanConverter` 在 App.xaml 中已声明

## 验证步骤

1. 确认编译通过，无 XAML 绑定错误
2. 确认"浏览器监控"从第一个卡片中移除
3. 确认新的独立卡片显示正确：标题、描述、ToggleSwitch 正常工作
4. 确认"查看安装指引"按钮点击后可展开/折叠安装指引
5. 确认商店链接按钮能打开浏览器
6. 确认 GitHub Releases 按钮能打开浏览器
7. 确认中英文切换时所有新字符串正确显示
