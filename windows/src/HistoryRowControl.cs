using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;

namespace Codepass
{
    public sealed class HistoryRowControl : Border
    {
        public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register("Owner", typeof(object), typeof(HistoryRowControl), new PropertyMetadata(null, OnDataChanged));
        public static readonly DependencyProperty ItemProperty = DependencyProperty.Register("Item", typeof(object), typeof(HistoryRowControl), new PropertyMetadata(null, OnDataChanged));
        TextBlock rawCell;
        Button codeCell, actions;
        bool loaded;

        public object Owner { get { return GetValue(OwnerProperty); } set { SetValue(OwnerProperty, value); } }
        public object Item { get { return GetValue(ItemProperty); } set { SetValue(ItemProperty, value); } }

        static void OnDataChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        { ((HistoryRowControl)sender).Render(); }

        public HistoryRowControl()
        {
            Height = 40;
            Margin = new Thickness(0, 2, 0, 2);
            CornerRadius = new CornerRadius(4);
            Background = Brushes.Transparent;
            Cursor = Cursors.Hand;
            Loaded += delegate { loaded = true; Render(); };
            Unloaded += delegate { loaded = false; CloseTooltip(); };
            MouseEnter += OnMouseEnter; MouseLeave += OnMouseLeave; MouseLeftButtonDown += OnRowMouseDown;
        }

        void Render()
        {
            MainWindow owner = Owner as MainWindow;
            HistoryItem item = Item as HistoryItem;
            if (!loaded && Child != null) return;
            if (owner == null || item == null || owner.Content == null) return;
            if (Child != null) CloseTooltip();
            Grid grid = new Grid();
            AddCol(grid, 112, false); AddCol(grid, 1, true); AddCol(grid, 100, false); AddCol(grid, 2, true); AddCol(grid, 72, false);
            grid.Children.Add(Cell(item.Time, 0, MainWindow.BodyBrush, false));
            TextBlock sender = Cell((item.Channel == "email" ? "邮件 · " : "短信 · ") + item.Source, 1, MainWindow.BodyBrush, false);
            sender.ToolTip = String.IsNullOrEmpty(item.Source) ? null : item.Source;
            grid.Children.Add(sender);
            HistoryItem latest = History.LatestUnexpiredRecord(owner.Configuration.HistoryRetentionMinutes, DateTime.UtcNow);
            codeCell = new Button { Content = item.Code, Foreground = object.ReferenceEquals(item, latest) ? MainWindow.AccentBrush : MainWindow.BodyBrush,
                FontWeight = FontWeights.SemiBold, Style = ((FrameworkElement)owner.Content).Resources["SecondaryBtn"] as Style,
                Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
            Grid.SetColumn(codeCell, 2); System.Windows.Automation.AutomationProperties.SetName(codeCell, "复制此条验证码");
            codeCell.ToolTip = item.CanBeLatest ? "单击复制验证码" : "仅历史：复查/首次补查不更新最新验证码；仍可单击手动复制";
            codeCell.Click += delegate { owner.CopyHistoryCode(item); };
            grid.Children.Add(codeCell);
            rawCell = Cell(owner.HistoryPreviewText(item), 3, MainWindow.FaintBrush, false);
            rawCell.ToolTip = "查看原文"; ToolTipService.SetInitialShowDelay(rawCell, 300); ToolTipService.SetShowDuration(rawCell, 30000);
            rawCell.Background = Brushes.Transparent; rawCell.Cursor = Cursors.Hand;
            rawCell.ToolTipOpening += delegate { owner.EnsureHistoryRawTooltip(rawCell, item); };
            rawCell.MouseLeftButtonDown += delegate(object senderObject, MouseButtonEventArgs args)
            { if (args.ClickCount == 2) { owner.ShowHistoryRawFromRow(item); args.Handled = true; } };
            grid.Children.Add(rawCell);
            actions = new Button { Content = "操作 ▾", Style = ((FrameworkElement)owner.Content).Resources["SecondaryBtn"] as Style,
                Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(actions, 4); System.Windows.Automation.AutomationProperties.SetName(actions, "记录操作：查看原文或删除此条");
            actions.ToolTip = "查看原文或删除这条记录"; actions.Click += delegate { owner.ShowHistoryActionsFromRow(item, actions); };
            grid.Children.Add(actions);
            Child = grid;
            RenderSelection();
        }

        void OnMouseEnter(object sender, MouseEventArgs e)
        {
            MainWindow owner = Owner as MainWindow;
            if (owner != null && !ReferenceEquals(this, owner.SelectedHistoryRow)) Background = MainWindow.RowHoverBrush;
        }
        void OnMouseLeave(object sender, MouseEventArgs e)
        {
            MainWindow owner = Owner as MainWindow;
            if (owner != null && !ReferenceEquals(this, owner.SelectedHistoryRow)) Background = Brushes.Transparent;
        }
        void OnRowMouseDown(object sender, MouseButtonEventArgs e)
        {
            MainWindow owner = Owner as MainWindow;
            if (owner != null) owner.SelectHistoryRow(this);
        }
        void RenderSelection()
        {
            MainWindow owner = Owner as MainWindow;
            Background = owner != null && ReferenceEquals(this, owner.SelectedHistoryRow) ? MainWindow.RowSelectedBrush : Brushes.Transparent;
        }
        void CloseTooltip() { if (rawCell != null && rawCell.ToolTip is ToolTip) ((ToolTip)rawCell.ToolTip).IsOpen = false; }

        static void AddCol(Grid grid, double width, bool star)
        { grid.ColumnDefinitions.Add(new ColumnDefinition { Width = star ? new GridLength(width > 0 ? width : 1, GridUnitType.Star) : new GridLength(width) }); }
        static TextBlock Cell(string text, int col, Brush foreground, bool bold)
        {
            TextBlock block = new TextBlock { Text = text, FontSize = 13, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = col == 1 || col == 3 ? HorizontalAlignment.Stretch : HorizontalAlignment.Center,
                TextAlignment = col == 1 ? TextAlignment.Center : col == 3 ? TextAlignment.Left : TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, Margin = col == 1 || col == 3 ? new Thickness(8, 0, 8, 0) : new Thickness(0) };
            if (bold) block.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(block, col); return block;
        }
    }
}
