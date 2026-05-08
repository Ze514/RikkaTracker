using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RikkaTracker.Core.Models;
using RikkaTracker.Core.Monitor;

namespace RikkaTracker.Controls
{
    public enum GanttDisplayMode
    {
        Full,
        Header,
        Timeline
    }

    public class GanttChart : FrameworkElement
    {
        private readonly VisualCollection _children;
        private readonly DrawingVisual _mainVisual;
        
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable<GanttSegment>), typeof(GanttChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

        public IEnumerable<GanttSegment> ItemsSource
        {
            get => (IEnumerable<GanttSegment>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty PixelsPerHourProperty =
            DependencyProperty.Register("PixelsPerHour", typeof(double), typeof(GanttChart),
                new FrameworkPropertyMetadata(200.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public double PixelsPerHour
        {
            get => (double)GetValue(PixelsPerHourProperty);
            set => SetValue(PixelsPerHourProperty, value);
        }

        public static readonly DependencyProperty DisplayModeProperty =
            DependencyProperty.Register("DisplayMode", typeof(GanttDisplayMode), typeof(GanttChart),
                new FrameworkPropertyMetadata(GanttDisplayMode.Full, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public GanttDisplayMode DisplayMode
        {
            get => (GanttDisplayMode)GetValue(DisplayModeProperty);
            set => SetValue(DisplayModeProperty, value);
        }

        public GanttChart()
        {
            _children = new VisualCollection(this);
            _mainVisual = new DrawingVisual();
            _children.Add(_mainVisual);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (GanttChart)d;
            control.InvalidateMeasure();
            control.InvalidateVisual();
        }

        private readonly Brush[] _palette = {
            new SolidColorBrush(Color.FromRgb(255, 121, 198)), // Pink
            new SolidColorBrush(Color.FromRgb(139, 233, 253)), // Cyan
            new SolidColorBrush(Color.FromRgb(80, 250, 123)),  // Green
            new SolidColorBrush(Color.FromRgb(255, 184, 108)), // Orange
            new SolidColorBrush(Color.FromRgb(189, 147, 249)), // Purple
            new SolidColorBrush(Color.FromRgb(255, 85, 85)),   // Red
            new SolidColorBrush(Color.FromRgb(241, 250, 140))  // Yellow
        };

        private double _rowHeaderWidth = 120;

        protected override int VisualChildrenCount => _children.Count;
        protected override Visual GetVisualChild(int index) => _children[index];

        protected override Size MeasureOverride(Size availableSize)
        {
            if (ItemsSource == null || !ItemsSource.Any())
            {
                return new Size(DisplayMode == GanttDisplayMode.Header ? _rowHeaderWidth : 24 * PixelsPerHour, 100);
            }

            double rowHeight = 40;
            double rowSpacing = 10;
            int maxRow = ItemsSource.Max(s => s.RowIndex);
            double height = (maxRow + 1) * (rowHeight + rowSpacing) + 60;

            double width = DisplayMode switch
            {
                GanttDisplayMode.Header => _rowHeaderWidth,
                GanttDisplayMode.Timeline => 24 * PixelsPerHour + 50,
                _ => 24 * PixelsPerHour + _rowHeaderWidth + 50
            };
            
            return new Size(width, height);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));
            base.OnRender(drawingContext);
            RenderChart();
        }

        private void RenderChart()
        {
            using (var dc = _mainVisual.RenderOpen())
            {
                DateTime baseTime = ItemsSource != null && ItemsSource.Any() 
                    ? ItemsSource.First().Start.Date 
                    : DateTime.Today;

                double rowHeight = 40;
                double segmentHeight = 24;
                double rowSpacing = 10;
                double topOffset = 40;
                double xOffset = DisplayMode == GanttDisplayMode.Timeline ? 0 : _rowHeaderWidth;

                // 绘制背景与辅助线
                DrawGrid(dc, baseTime, rowHeight, rowSpacing, topOffset, xOffset);

                if (ItemsSource == null || !ItemsSource.Any()) return;

                // 只有在非 Header 模式下才绘制条带
                if (DisplayMode != GanttDisplayMode.Header)
                {
                    foreach (var segment in ItemsSource)
                    {
                        DateTime start = segment.Start < baseTime ? baseTime : segment.Start;
                        DateTime end = segment.End > baseTime.AddDays(1) ? baseTime.AddDays(1) : segment.End;
                        if (start >= end) continue;

                        double xStart = xOffset + (start - baseTime).TotalHours * PixelsPerHour;
                        double xEnd = xOffset + (end - baseTime).TotalHours * PixelsPerHour;
                        double width = Math.Max(2, xEnd - xStart);
                        double y = topOffset + segment.RowIndex * (rowHeight + rowSpacing) + (rowHeight - segmentHeight) / 2;

                        Brush brush = GetBrushForSegment(segment);
                        dc.DrawRoundedRectangle(brush, null, new Rect(xStart, y, width, segmentHeight), 4, 4);
                    }
                }
            }
        }

        private void DrawGrid(DrawingContext dc, DateTime baseTime, double rowHeight, double rowSpacing, double topOffset, double xOffset)
        {
            double height = RenderSize.Height;
            double width = RenderSize.Width;

            // 1. 绘制纵向时间线 (仅在 Timeline 模式)
            if (DisplayMode != GanttDisplayMode.Header)
            {
                Pen timePen = new Pen(new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)), 1);
                timePen.DashStyle = DashStyles.Dash;

                for (int i = 0; i <= 24; i++)
                {
                    double x = xOffset + i * PixelsPerHour;
                    dc.DrawLine(timePen, new Point(x, topOffset - 10), new Point(x, height));
                    
                    var text = new FormattedText(
                        $"{i}:00",
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        11,
                        Brushes.Gray,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    
                    dc.DrawText(text, new Point(x - text.Width / 2, topOffset - 30));
                }
            }

            // 2. 绘制横向行线与应用名称
            if (ItemsSource != null && ItemsSource.Any())
            {
                Pen rowPen = new Pen(new SolidColorBrush(Color.FromArgb(15, 128, 128, 128)), 1);
                var rowGroups = ItemsSource.GroupBy(s => s.RowIndex).Select(g => g.First()).OrderBy(s => s.RowIndex);

                foreach (var group in rowGroups)
                {
                    double y = topOffset + group.RowIndex * (rowHeight + rowSpacing);
                    
                    // 行底线
                    dc.DrawLine(rowPen, new Point(0, y + rowHeight + rowSpacing / 2), new Point(width, y + rowHeight + rowSpacing / 2));

                    // 应用名称 (仅在 Header 模式)
                    if (DisplayMode != GanttDisplayMode.Timeline)
                    {
                        var text = new FormattedText(
                            group.ProcessName,
                            CultureInfo.CurrentUICulture,
                            FlowDirection.LeftToRight,
                            new Typeface("Segoe UI SemiBold"),
                            12,
                            (Brush)FindResource("TextFillColorPrimaryBrush") ?? Brushes.White,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        text.MaxTextWidth = _rowHeaderWidth - 10;
                        text.MaxTextHeight = rowHeight;
                        text.Trimming = TextTrimming.CharacterEllipsis;

                        dc.DrawText(text, new Point(10, y + (rowHeight - text.Height) / 2));
                    }
                }
            }
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (ItemsSource == null || !ItemsSource.Any() || DisplayMode == GanttDisplayMode.Header) return;

            Point pos = e.GetPosition(this);
            DateTime baseTime = ItemsSource.First().Start.Date;

            double rowHeight = 40;
            double rowSpacing = 10;
            double topOffset = 40;
            double xOffset = DisplayMode == GanttDisplayMode.Timeline ? 0 : _rowHeaderWidth;

            int rowIndex = (int)((pos.Y - topOffset) / (rowHeight + rowSpacing));
            if (rowIndex < 0 || pos.X < xOffset) { this.ToolTip = null; return; }

            double timeInHours = (pos.X - xOffset) / PixelsPerHour;
            DateTime timeAtMouse = baseTime.AddHours(timeInHours);

            var hit = ItemsSource.FirstOrDefault(s => s.RowIndex == rowIndex && s.Start <= timeAtMouse && s.End >= timeAtMouse);

            if (hit != null)
            {
                this.ToolTip = $"{hit.ProcessName}\n{hit.WindowTitle}\n{hit.Start:HH:mm:ss} - {hit.End:HH:mm:ss}\n时长: {hit.Duration:hh\\:mm\\:ss}";
            }
            else
            {
                this.ToolTip = null;
            }
        }

        private Brush GetBrushForSegment(GanttSegment segment)
        {
            if (segment.Status != ActivityStatus.ForegroundActive)
            {
                return new SolidColorBrush(Color.FromArgb(60, 173, 216, 230));
            }

            return _palette[segment.RowIndex % _palette.Length];
        }
    }
}
