using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RikkaTracker.Core.Models;
using RikkaTracker.Core.Monitor;
using RikkaTracker.Services;
using RikkaTracker.Controls;

namespace RikkaTracker.ViewModels
{
    public class AppExportItem : ObservableObject
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;

        public string DisplayName => string.IsNullOrEmpty(Alias) ? ProcessName : Alias;
    }

    public class CategoryExportItem : ObservableObject
    {
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Name { get; set; } = string.Empty;
    }

    public partial class ExportViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localizationService;

        public ExportViewModel(IDataService dataService, ILoggerService logger, ILocalizationService localizationService)
        {
            _dataService = dataService;
            _logger = logger;
            _localizationService = localizationService;

            _startDate = DateTime.Today.AddDays(-7);
            _endDate = DateTime.Today;
            _selectedFormat = "CSV";

            ExportFormats = new ObservableCollection<string> { "CSV", "Excel (.xls)", "PDF" };
            Apps = new ObservableCollection<AppExportItem>();
            Categories = new ObservableCollection<CategoryExportItem>
            {
                new CategoryExportItem { Name = _localizationService.GetString("StrExportBrowserTodo", "浏览器 (未来支持)") },
                new CategoryExportItem { Name = _localizationService.GetString("StrExportDevToolsTodo", "开发工具 (未来支持)") },
                new CategoryExportItem { Name = _localizationService.GetString("StrExportMediaTodo", "多媒体 (未来支持)") },
                new CategoryExportItem { Name = _localizationService.GetString("StrExportSystemTodo", "系统组件 (未来支持)") }
            };

            UpdateDefaultFilePath();
            _ = LoadAppsAsync();
        }

        public ObservableCollection<string> ExportFormats { get; }
        public ObservableCollection<AppExportItem> Apps { get; }
        public ObservableCollection<CategoryExportItem> Categories { get; }

        [ObservableProperty]
        private DateTime _startDate;

        partial void OnStartDateChanged(DateTime value)
        {
            UpdateDefaultFilePath();
            _ = LoadAppsAsync();
        }

        [ObservableProperty]
        private DateTime _endDate;

        partial void OnEndDateChanged(DateTime value)
        {
            UpdateDefaultFilePath();
            _ = LoadAppsAsync();
        }

        [ObservableProperty]
        private string _selectedFormat;

        partial void OnSelectedFormatChanged(string value)
        {
            UpdateDefaultFilePath();
        }

        [ObservableProperty]
        private string _selectedFilePath = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        private void UpdateDefaultFilePath()
        {
            string ext = SelectedFormat switch
            {
                "Excel (.xls)" => ".xls",
                "PDF" => ".pdf",
                _ => ".csv"
            };

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string fileName = $"RikkaTracker_Export_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}{ext}";
            SelectedFilePath = Path.Combine(desktop, fileName);
        }

        public async Task LoadAppsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                DateTime start = StartDate.Date;
                DateTime end = EndDate.Date.AddDays(1).AddTicks(-1);

                var appsData = await _dataService.GetAppsInPeriodAsync(start, end);

                App.Current.Dispatcher.Invoke(() =>
                {
                    Apps.Clear();
                    foreach (var item in appsData)
                    {
                        Apps.Add(new AppExportItem
                        {
                            IsSelected = true,
                            ProcessName = item.ProcessName,
                            ProcessPath = item.ProcessPath,
                            Alias = item.Alias
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load apps for export range", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void SelectAllApps(bool select)
        {
            foreach (var app in Apps)
            {
                app.IsSelected = select;
            }
        }

        [RelayCommand]
        private void BrowseFilePath()
        {
            string filter = SelectedFormat switch
            {
                "Excel (.xls)" => "Excel Spreadsheet (*.xls)|*.xls",
                "PDF" => "PDF Document (*.pdf)|*.pdf",
                _ => "CSV Files (*.csv)|*.csv"
            };

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = _localizationService.GetString("StrSelectExportLocation", "请选择导出文件的保存位置"),
                Filter = filter,
                FileName = Path.GetFileName(SelectedFilePath),
                InitialDirectory = Path.GetDirectoryName(SelectedFilePath)
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedFilePath = dialog.FileName;
            }
        }

        [RelayCommand]
        private async Task ExportData()
        {
            string promptTitle = _localizationService.GetString("StrPrompt", "提示");
            if (string.IsNullOrEmpty(SelectedFilePath))
            {
                RikkaMessageBox.Show(
                    _localizationService.GetString("StrSelectExportLocation", "请选择导出文件的保存位置"),
                    promptTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selectedAppNames = Apps.Where(a => a.IsSelected).Select(a => a.ProcessName).ToList();
            if (selectedAppNames.Count == 0)
            {
                RikkaMessageBox.Show(
                    _localizationService.GetString("StrExportNoAppSelected", "请至少勾选一个要导出的应用。"),
                    promptTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsLoading = true;

            try
            {
                DateTime start = StartDate.Date;
                DateTime end = EndDate.Date.AddDays(1).AddTicks(-1);

                // 查询所选时间段的所有日志
                var allSegments = await _dataService.GetSegmentsAsync(start, end);
                var filteredSegments = allSegments
                    .Where(s => selectedAppNames.Contains(s.ProcessName))
                    .ToList();

                if (filteredSegments.Count == 0)
                {
                    RikkaMessageBox.Show(
                        _localizationService.GetString("StrExportNoData", "所选时间区间内无活动记录，无法导出。"),
                        promptTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // 创建目录（以防不存在）
                string? dir = Path.GetDirectoryName(SelectedFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (SelectedFormat == "CSV")
                {
                    await Task.Run(() => ExportToCsv(SelectedFilePath, filteredSegments));
                }
                else if (SelectedFormat == "Excel (.xls)")
                {
                    await Task.Run(() => ExportToExcel(SelectedFilePath, filteredSegments, start, end));
                }
                else if (SelectedFormat == "PDF")
                {
                    await ExportToPdfAsync(SelectedFilePath, filteredSegments, start, end);
                }

                RikkaMessageBox.Show(
                    _localizationService.GetString("StrExportSuccess", "数据导出成功！"),
                    _localizationService.GetString("StrSuccess", "成功"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to export data", ex);
                RikkaMessageBox.Show(
                    _localizationService.GetString("StrExportFailed", "数据导出失败：") + "\n" + ex.Message,
                    _localizationService.GetString("StrError", "错误"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExportToCsv(string filePath, List<ActivitySegment> segments)
        {
            // UTF-8 With BOM (确保双击用 Excel 打开不会乱码)
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            writer.Write('\uFEFF');

            // 写入 CSV 表头
            string headerAppName = _localizationService.GetString("StrExportHeaderAppName", "应用名称");
            string headerAlias = _localizationService.GetString("StrExportHeaderAlias", "别名");
            string headerPath = _localizationService.GetString("StrExportHeaderPath", "应用路径");
            string headerWindowTitle = _localizationService.GetString("StrExportHeaderWindowTitle", "窗口标题");
            string headerStatus = _localizationService.GetString("StrExportHeaderStatus", "状态");
            string headerStartTime = _localizationService.GetString("StrExportHeaderStartTime", "开始时间");
            string headerEndTime = _localizationService.GetString("StrExportHeaderEndTime", "结束时间");
            string headerDurationSec = _localizationService.GetString("StrExportHeaderDurationSec", "时长(秒)");

            writer.WriteLine($"{headerAppName},{headerAlias},{headerPath},{headerWindowTitle},{headerStatus},{headerStartTime},{headerEndTime},{headerDurationSec}");

            foreach (var seg in segments)
            {
                string statusText = seg.Status switch
                {
                    ActivityStatus.ForegroundActive => _localizationService.GetString("StrActive", "活跃"),
                    ActivityStatus.ForegroundInactive => _localizationService.GetString("StrInactive", "非活动"),
                    ActivityStatus.Background => _localizationService.GetString("StrBackground", "后台"),
                    _ => "Unknown"
                };

                writer.WriteLine(string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7:F0}",
                    EscapeCsv(seg.ProcessName),
                    EscapeCsv(seg.Alias),
                    EscapeCsv(seg.ProcessPath),
                    EscapeCsv(seg.WindowTitle),
                    statusText,
                    seg.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    seg.EndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    (seg.EndTime - seg.StartTime).TotalSeconds
                ));
            }
        }

        private void ExportToExcel(string filePath, List<ActivitySegment> segments, DateTime start, DateTime end)
        {
            // 按应用计算时长汇总
            var summary = segments
                .GroupBy(s => new { s.ProcessName, s.ProcessPath, s.Alias })
                .Select(g =>
                {
                    double activeSec = g.Where(s => s.Status == ActivityStatus.ForegroundActive).Sum(s => (s.EndTime - s.StartTime).TotalSeconds);
                    double inactiveSec = g.Where(s => s.Status == ActivityStatus.ForegroundInactive).Sum(s => (s.EndTime - s.StartTime).TotalSeconds);
                    double bgSec = g.Where(s => s.Status == ActivityStatus.Background).Sum(s => (s.EndTime - s.StartTime).TotalSeconds);

                    return new
                    {
                        g.Key.ProcessName,
                        DisplayName = string.IsNullOrEmpty(g.Key.Alias) ? g.Key.ProcessName : g.Key.Alias,
                        g.Key.ProcessPath,
                        ActiveTime = TimeSpan.FromSeconds(activeSec),
                        InactiveTime = TimeSpan.FromSeconds(inactiveSec),
                        BackgroundTime = TimeSpan.FromSeconds(bgSec),
                        TotalTime = TimeSpan.FromSeconds(activeSec + inactiveSec + bgSec)
                    };
                })
                .OrderByDescending(x => x.ActiveTime)
                .ToList();

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            writer.WriteLine("<?mso-application progid=\"Excel.Sheet\"?>");
            writer.WriteLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            writer.WriteLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            writer.WriteLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
            writer.WriteLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            writer.WriteLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
            
            // 样式定义
            writer.WriteLine(" <Styles>");
            writer.WriteLine("  <Style ss:ID=\"Header\">");
            writer.WriteLine("   <Font ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/>");
            writer.WriteLine("   <Interior ss:Color=\"#4F81BD\" ss:Pattern=\"Solid\"/>");
            writer.WriteLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            writer.WriteLine("  </Style>");
            writer.WriteLine("  <Style ss:ID=\"Title\">");
            writer.WriteLine("   <Font ss:Bold=\"1\" ss:Size=\"14\" ss:Color=\"#1F497D\"/>");
            writer.WriteLine("  </Style>");
            writer.WriteLine(" </Styles>");

            // 本地化文本准备
            string sheetSummary = _localizationService.GetString("StrExportSheetSummary", "使用时长汇总");
            string sheetDetail = _localizationService.GetString("StrExportSheetDetail", "详细日志");
            string reportTitle = _localizationService.GetString("StrExportReportTitle", "应用活跃时长统计报告");
            string toText = _localizationService.GetString("StrTo", " 至 ");

            string headerAppName = _localizationService.GetString("StrExportHeaderAppName", "应用名称");
            string headerAlias = _localizationService.GetString("StrExportHeaderAlias", "应用别名");
            string headerPath = _localizationService.GetString("StrExportHeaderPath", "进程路径");
            
            string headerActive = _localizationService.GetString("StrExportHeaderDurationActive", "活跃时长");
            string headerInactive = _localizationService.GetString("StrExportHeaderDurationInactive", "非活动时长");
            string headerBackground = _localizationService.GetString("StrExportHeaderDurationBackground", "后台时长");
            string headerTotal = _localizationService.GetString("StrExportHeaderDurationTotal", "总时长");

            // Sheet 1: 时长汇总
            writer.WriteLine($" <Worksheet ss:Name=\"{sheetSummary}\">");
            writer.WriteLine("  <Table>");
            writer.WriteLine("   <Column ss:Width=\"150\"/>");
            writer.WriteLine("   <Column ss:Width=\"150\"/>");
            writer.WriteLine("   <Column ss:Width=\"300\"/>");
            writer.WriteLine("   <Column ss:Width=\"120\"/>");
            writer.WriteLine("   <Column ss:Width=\"120\"/>");
            writer.WriteLine("   <Column ss:Width=\"120\"/>");
            writer.WriteLine("   <Column ss:Width=\"120\"/>");

            // 标题行
            writer.WriteLine("   <Row ss:Height=\"30\">");
            writer.WriteLine($"    <Cell ss:MergeAcross=\"6\" ss:StyleID=\"Title\"><Data ss:Type=\"String\">{reportTitle} ({start:yyyy-MM-dd}{toText}{end:yyyy-MM-dd})</Data></Cell>");
            writer.WriteLine("   </Row>");
            writer.WriteLine("   <Row ss:Height=\"20\">");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerAppName}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerAlias}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerPath}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerActive}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerInactive}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerBackground}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerTotal}</Data></Cell>");
            writer.WriteLine("   </Row>");

            foreach (var item in summary)
            {
                writer.WriteLine("   <Row>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(item.ProcessName)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(item.DisplayName)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(item.ProcessPath)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{FormatDuration(item.ActiveTime)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{FormatDuration(item.InactiveTime)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{FormatDuration(item.BackgroundTime)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{FormatDuration(item.TotalTime)}</Data></Cell>");
                writer.WriteLine("   </Row>");
            }
            writer.WriteLine("  </Table>");
            writer.WriteLine(" </Worksheet>");

            // Sheet 2: 详细活动日志
            writer.WriteLine($" <Worksheet ss:Name=\"{sheetDetail}\">");
            writer.WriteLine("  <Table>");
            writer.WriteLine("   <Column ss:Width=\"100\"/>");
            writer.WriteLine("   <Column ss:Width=\"100\"/>");
            writer.WriteLine("   <Column ss:Width=\"250\"/>");
            writer.WriteLine("   <Column ss:Width=\"80\"/>");
            writer.WriteLine("   <Column ss:Width=\"130\"/>");
            writer.WriteLine("   <Column ss:Width=\"130\"/>");
            writer.WriteLine("   <Column ss:Width=\"80\"/>");

            string headerWindowTitle = _localizationService.GetString("StrExportHeaderWindowTitle", "窗口标题");
            string headerStatus = _localizationService.GetString("StrExportHeaderStatus", "状态");
            string headerStartTime = _localizationService.GetString("StrExportHeaderStartTime", "开始时间");
            string headerEndTime = _localizationService.GetString("StrExportHeaderEndTime", "结束时间");
            string headerDurationSec = _localizationService.GetString("StrExportHeaderDurationSec", "时长(秒)");

            writer.WriteLine("   <Row ss:Height=\"20\">");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerAppName}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerAlias}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerWindowTitle}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerStatus}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerStartTime}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerEndTime}</Data></Cell>");
            writer.WriteLine($"    <Cell ss:StyleID=\"Header\"><Data ss:Type=\"String\">{headerDurationSec}</Data></Cell>");
            writer.WriteLine("   </Row>");

            foreach (var seg in segments)
            {
                string statusText = seg.Status switch
                {
                    ActivityStatus.ForegroundActive => _localizationService.GetString("StrActive", "活跃"),
                    ActivityStatus.ForegroundInactive => _localizationService.GetString("StrInactive", "非活动"),
                    ActivityStatus.Background => _localizationService.GetString("StrBackground", "后台"),
                    _ => "Unknown"
                };

                writer.WriteLine("   <Row>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(seg.ProcessName)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(seg.Alias)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{EscapeXml(seg.WindowTitle)}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{statusText}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{seg.StartTime:yyyy-MM-dd HH:mm:ss}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"String\">{seg.EndTime:yyyy-MM-dd HH:mm:ss}</Data></Cell>");
                writer.WriteLine($"    <Cell><Data ss:Type=\"Number\">{(seg.EndTime - seg.StartTime).TotalSeconds:F0}</Data></Cell>");
                writer.WriteLine("   </Row>");
            }
            writer.WriteLine("  </Table>");
            writer.WriteLine(" </Worksheet>");

            writer.WriteLine("</Workbook>");
        }

        private async Task ExportToPdfAsync(string filePath, List<ActivitySegment> segments, DateTime start, DateTime end)
        {
            // 限制明细最多导出 1000 条
            var detailedList = segments.Take(1000).ToList();

            // 时长汇总 (取前 20)
            var summary = segments
                .GroupBy(s => new { s.ProcessName, s.Alias })
                .Select(g =>
                {
                    double activeSec = g.Where(s => s.Status == ActivityStatus.ForegroundActive).Sum(s => (s.EndTime - s.StartTime).TotalSeconds);
                    double inactiveSec = g.Where(s => s.Status == ActivityStatus.ForegroundInactive).Sum(s => (s.EndTime - s.StartTime).TotalSeconds);
                    double bgSec = g.Where(s => s.Status == ActivityStatus.Background).Sum(s => (s.EndTime - s.StartTime).TotalSeconds);
                    return new
                    {
                        g.Key.ProcessName,
                        DisplayName = string.IsNullOrEmpty(g.Key.Alias) ? g.Key.ProcessName : g.Key.Alias,
                        ActiveSec = activeSec,
                        ActiveTime = TimeSpan.FromSeconds(activeSec),
                        TotalTime = TimeSpan.FromSeconds(activeSec + inactiveSec + bgSec)
                    };
                })
                .OrderByDescending(x => x.ActiveSec)
                .ToList();

            double maxActiveSec = summary.FirstOrDefault()?.ActiveSec ?? 1.0;
            if (maxActiveSec < 1.0) maxActiveSec = 1.0;

            // 本地化文本准备
            string noneText = _localizationService.GetString("StrNoData", "无");
            string unitSecText = _localizationService.GetString("StrUnitSecond", "秒");
            
            // 基础指标统计
            double totalActiveSec = summary.Sum(s => s.ActiveSec);
            var totalActiveTime = TimeSpan.FromSeconds(totalActiveSec);
            int uniqueAppsCount = summary.Count;
            string topApp = summary.FirstOrDefault()?.DisplayName ?? noneText;
            string topAppTimeStr = summary.FirstOrDefault() != null ? FormatDuration(summary.First().ActiveTime) : "0" + unitSecText;

            // 1. 生成 HTML 模板
            var sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
    @page {
        size: A4;
        margin: 15mm;
    }
    body {
        font-family: 'Segoe UI', 'Microsoft YaHei', sans-serif;
        color: #333;
        margin: 0;
        padding: 0;
        background: #fff;
    }
    .header {
        border-bottom: 2px solid #ff7597;
        padding-bottom: 10px;
        margin-bottom: 25px;
    }
    .title {
        font-size: 26px;
        font-weight: bold;
        color: #1f2d3d;
        margin: 0;
    }
    .subtitle {
        font-size: 14px;
        color: #666;
        margin: 5px 0 0 0;
    }
    .card-container {
        display: flex;
        justify-content: space-between;
        margin-bottom: 25px;
        gap: 15px;
    }
    .card {
        flex: 1;
        background: #fdfdfd;
        border: 1px solid #eaeaea;
        border-radius: 8px;
        padding: 15px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.02);
    }
    .card-title {
        font-size: 12px;
        color: #999;
        text-transform: uppercase;
        margin-bottom: 5px;
    }
    .card-value {
        font-size: 20px;
        font-weight: bold;
        color: #ff7597;
    }
    .section-title {
        font-size: 18px;
        font-weight: bold;
        color: #2c3e50;
        border-left: 4px solid #ff7597;
        padding-left: 10px;
        margin: 25px 0 15px 0;
        page-break-after: avoid;
    }
    table {
        width: 100%;
        border-collapse: collapse;
        margin-bottom: 20px;
    }
    th {
        background-color: #f7f9fa;
        color: #475669;
        font-weight: 600;
        text-align: left;
        padding: 10px;
        border-bottom: 2px solid #eaeef2;
    }
    td {
        padding: 10px;
        border-bottom: 1px solid #eff2f7;
        font-size: 13px;
    }
    tr:nth-child(even) {
        background-color: #fafbfc;
    }
    .progress-bar-container {
        background: #f0f0f0;
        border-radius: 4px;
        height: 8px;
        width: 150px;
        display: inline-block;
        vertical-align: middle;
        margin-right: 10px;
    }
    .progress-bar {
        background: linear-gradient(90deg, #ff7597, #ff94b2);
        border-radius: 4px;
        height: 8px;
    }
    .footer {
        text-align: center;
        font-size: 11px;
        color: #999;
        margin-top: 30px;
        border-top: 1px solid #eee;
        padding-top: 10px;
        page-break-before: auto;
    }
</style>
</head>
<body>");

            string pdfTitle = _localizationService.GetString("StrExportPdfTitle", "RikkaTracker 活动统计报告");
            string timeRangeText = _localizationService.GetString("StrExportTimeRange", "统计区间");
            string toText = _localizationService.GetString("StrTo", " 至 ");
            string generatedTimeText = _localizationService.GetString("StrExportPdfGeneratedTime", "生成时间");
            
            string totalActiveTitle = _localizationService.GetString("StrExportPdfTotalActive", "总活跃时长");
            string uniqueAppsTitle = _localizationService.GetString("StrExportPdfUniqueApps", "记录应用数");
            string topAppTitle = _localizationService.GetString("StrExportPdfTopApp", "最长使用应用");
            string unitAppText = _localizationService.GetString("StrExportPdfUnitApp", "个");
            
            string headerAppName = _localizationService.GetString("StrExportHeaderAppName", "应用名称");
            string headerAlias = _localizationService.GetString("StrExportHeaderAlias", "别名");
            string headerActive = _localizationService.GetString("StrExportHeaderDurationActive", "活跃时长");
            string headerWindowTitle = _localizationService.GetString("StrExportHeaderWindowTitle", "窗口标题");
            string headerStatus = _localizationService.GetString("StrExportHeaderStatus", "状态");
            string headerStartTime = _localizationService.GetString("StrExportHeaderStartTime", "开始时间");
            string headerDurationSec = _localizationService.GetString("StrExportHeaderDurationSec", "时长(秒)");

            string rankTitle = _localizationService.GetString("StrExportPdfRank", "排名");
            string ratioTitle = _localizationService.GetString("StrExportPdfRatio", "占比");
            
            string detailSummaryTitle = _localizationService.GetString("StrExportPdfDetailSummary", "详细日志摘要");
            string detailTipText = _localizationService.GetString("StrExportPdfDetailTip", "* 报告中详细日志仅展示了前 1000 条中的 500 条最新摘要。完整数据请导出为 Excel 或 CSV。");
            string pdfFooterText = _localizationService.GetString("StrExportPdfFooter", "报告由 RikkaTracker 自动生成。保留所有权利。");

            // 头部
            sb.Append("<div class='header'>");
            sb.Append($" <div class='title'>{pdfTitle}</div>");
            sb.Append($" <div class='subtitle'>{timeRangeText}: {start:yyyy-MM-dd}{toText}{end:yyyy-MM-dd} | {generatedTimeText}: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");
            sb.Append("</div>");

            // 卡片
            sb.Append("<div class='card-container'>");
            sb.Append(" <div class='card'>");
            sb.Append($"  <div class='card-title'>{totalActiveTitle}</div>");
            sb.Append($"  <div class='card-value'>{FormatDuration(totalActiveTime)}</div>");
            sb.Append(" </div>");
            sb.Append(" <div class='card'>");
            sb.Append($"  <div class='card-title'>{uniqueAppsTitle}</div>");
            sb.Append($"  <div class='card-value'>{uniqueAppsCount} {unitAppText}</div>");
            sb.Append(" </div>");
            sb.Append(" <div class='card'>");
            sb.Append($"  <div class='card-title'>{topAppTitle}</div>");
            sb.Append($"  <div class='card-value' style='font-size: 16px; margin-top: 4px;'>{topApp} ({topAppTimeStr})</div>");
            sb.Append(" </div>");
            sb.Append("</div>");

            // 时长排行 (前 15)
            sb.Append($"<div class='section-title'>{headerActive}排行 (Top 15)</div>");
            sb.Append("<table>");
            sb.Append(" <thead>");
            sb.Append("  <tr>");
            sb.Append($"   <th style='width: 5%;'>{rankTitle}</th>");
            sb.Append($"   <th style='width: 30%;'>{headerAlias} / {headerAppName}</th>");
            sb.Append($"   <th style='width: 40%;'>{ratioTitle}</th>");
            sb.Append($"   <th style='width: 25%;'>{headerActive}</th>");
            sb.Append("  </tr>");
            sb.Append(" </thead>");
            sb.Append(" <tbody>");

            int rank = 1;
            foreach (var item in summary.Take(15))
            {
                double pct = (item.ActiveSec / maxActiveSec) * 100.0;
                sb.Append("  <tr>");
                sb.Append($"   <td>{rank++}</td>");
                sb.Append($"   <td><strong>{EscapeXml(item.DisplayName)}</strong><br/><span style='font-size:11px; color:#888;'>{EscapeXml(item.ProcessName)}</span></td>");
                sb.Append("   <td>");
                sb.Append("    <div class='progress-bar-container'>");
                sb.Append($"     <div class='progress-bar' style='width: {pct:F1}%;'></div>");
                sb.Append("    </div>");
                sb.Append($"    <span style='font-size:11px; color:#666;'>{(item.ActiveSec / (totalActiveSec > 0 ? totalActiveSec : 1.0) * 100.0):F1}%</span>");
                sb.Append("   </td>");
                sb.Append($"   <td>{FormatDuration(item.ActiveTime)}</td>");
                sb.Append("  </tr>");
            }
            sb.Append(" </tbody>");
            sb.Append("</table>");

            // 详细活动日志
            sb.Append($"<div class='section-title'>{detailSummaryTitle}摘要 (最新 500 条)</div>");
            sb.Append("<table>");
            sb.Append(" <thead>");
            sb.Append("  <tr>");
            sb.Append($"   <th style='width: 25%;'>{headerAlias} / {headerAppName}</th>");
            sb.Append($"   <th style='width: 35%;'>{headerWindowTitle}</th>");
            sb.Append($"   <th style='width: 10%;'>{headerStatus}</th>");
            sb.Append($"   <th style='width: 20%;'>{headerStartTime}</th>");
            sb.Append($"   <th style='width: 10%;'>{headerDurationSec}</th>");
            sb.Append("  </tr>");
            sb.Append(" </thead>");
            sb.Append(" <tbody>");

            // 按时间倒序展示最新的 500 条
            var latestSegments = detailedList.OrderByDescending(s => s.StartTime).Take(500).ToList();
            foreach (var seg in latestSegments)
            {
                string statusText = seg.Status switch
                {
                    ActivityStatus.ForegroundActive => _localizationService.GetString("StrActive", "活跃"),
                    ActivityStatus.ForegroundInactive => _localizationService.GetString("StrInactive", "非活动"),
                    ActivityStatus.Background => _localizationService.GetString("StrBackground", "后台"),
                    _ => "Unknown"
                };

                string name = string.IsNullOrEmpty(seg.Alias) ? seg.ProcessName : seg.Alias;

                sb.Append("  <tr>");
                sb.Append($"   <td><strong>{EscapeXml(name)}</strong><br/><span style='font-size:10px; color:#888;'>{EscapeXml(seg.ProcessName)}</span></td>");
                sb.Append($"   <td style='font-size: 11px; max-width: 200px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;'>{EscapeXml(seg.WindowTitle)}</td>");
                sb.Append($"   <td>{statusText}</td>");
                sb.Append($"   <td style='font-size:11px;'>{seg.StartTime:yyyy-MM-dd HH:mm:ss}</td>");
                sb.Append($"   <td>{(seg.EndTime - seg.StartTime).TotalSeconds:F0}</td>");
                sb.Append("  </tr>");
            }

            sb.Append(" </tbody>");
            sb.Append("</table>");

            if (segments.Count > 1000)
            {
                sb.Append($"<p style='font-size: 12px; color: #888; text-align: center; margin-top: 15px;'>{detailTipText}</p>");
            }

            sb.Append("<div class='footer'>");
            sb.Append($" {pdfFooterText}");
            sb.Append("</div>");
            sb.Append("</body></html>");

            // 2. 写入临时 HTML
            string tempHtmlPath = Path.Combine(Path.GetTempPath(), $"rikka_export_{Guid.NewGuid():N}.html");
            await File.WriteAllTextAsync(tempHtmlPath, sb.ToString(), Encoding.UTF8);

            try
            {
                // 3. 寻找 Edge 路径
                string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
                if (!File.Exists(edgePath))
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe");
                    if (key != null)
                    {
                        edgePath = key.GetValue("")?.ToString() ?? edgePath;
                    }
                }

                if (!File.Exists(edgePath))
                {
                    throw new FileNotFoundException("未在系统中找到 Microsoft Edge 浏览器。PDF 转换需要 Edge 的支持。");
                }

                // 4. 执行 Edge headless 转换为 PDF
                // 使用 --no-pdf-header-footer 隐藏默认页眉 and 页脚，--disable-gpu 兼容无图形环境
                string arguments = $"--headless --disable-gpu --no-pdf-header-footer --print-to-pdf=\"{filePath}\" \"{tempHtmlPath}\"";

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = edgePath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                }
                else
                {
                    throw new InvalidOperationException("启动 Edge 转换进程失败。");
                }
            }
            finally
            {
                // 5. 清理临时 HTML
                if (File.Exists(tempHtmlPath))
                {
                    try
                    {
                        File.Delete(tempHtmlPath);
                    }
                    catch
                    {
                        // 忽略清理临时文件时的异常
                    }
                }
            }
        }

        private string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            if (val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
            {
                return "\"" + val.Replace("\"", "\"\"") + "\"";
            }
            return val;
        }

        private string EscapeXml(string val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            return val.Replace("&", "&amp;")
                      .Replace("<", "&lt;")
                      .Replace(">", "&gt;")
                      .Replace("\"", "&quot;")
                      .Replace("'", "&apos;");
        }

        private string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalSeconds < 1) return "0" + _localizationService.GetString("StrUnitSecond", "秒");
            var parts = new List<string>();
            if (ts.Days > 0) parts.Add($"{ts.Days}" + _localizationService.GetString("StrUnitDay", "天"));
            if (ts.Hours > 0) parts.Add($"{ts.Hours}" + _localizationService.GetString("StrUnitHour", "小时"));
            if (ts.Minutes > 0) parts.Add($"{ts.Minutes}" + _localizationService.GetString("StrUnitMinute", "分"));
            if (ts.Seconds > 0 || parts.Count == 0) parts.Add($"{ts.Seconds}" + _localizationService.GetString("StrUnitSecond", "秒"));
            return string.Join("", parts);
        }
    }
}
