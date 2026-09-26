using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using SimpleTodo.Models;
using SimpleTodo.Services;

namespace SimpleTodo.UI
{
    /// <summary>
    /// 主窗口：任务列表 + 输入区 + 筛选/搜索 + 状态栏。
    /// 界面完全由代码构建，不依赖 WinForms 设计器文件，便于用 csc 直接编译。
    /// </summary>
    public class MainForm : Form
    {
        private const string TitleBase = "极简待办 SimpleTodo";
        private const int EmSetCueBanner = 0x1501;

        private static readonly Color WindowBackColor = Color.FromArgb(246, 248, 252);
        private static readonly Color SurfaceColor = Color.White;
        private static readonly Color BorderColor = Color.FromArgb(222, 228, 238);
        private static readonly Color PrimaryColor = Color.FromArgb(42, 104, 214);
        private static readonly Color MutedTextColor = Color.FromArgb(100, 112, 132);
        private static readonly Color AlternateRowColor = Color.FromArgb(250, 252, 255);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly TodoStore _store;
        private List<TodoItem> _items = new List<TodoItem>();

        private TodoFilter _filter = TodoFilter.All;
        private TodoSortField _sortField = TodoSortField.CreatedAt;
        private bool _sortAscending;

        /// <summary>为 true 时忽略控件事件，避免程序化改动触发业务逻辑。</summary>
        private bool _loading;

        /// <summary>存在未成功保存的改动。</summary>
        private bool _dirty;

        // 输入区
        private TextBox _addBox;
        private DateTimePicker _duePicker;
        private ComboBox _priorityBox;
        private Button _addButton;

        // 筛选区
        private TextBox _searchBox;
        private List<RadioButton> _filterButtons = new List<RadioButton>();
        private Button _clearDoneButton;

        // 列表区
        private ListView _list;
        private Label _emptyLabel;

        // 状态栏
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusCounts;
        private ToolStripStatusLabel _statusView;

        // 菜单
        private MenuStrip _menuStrip;
        private ToolStripMenuItem _topMostItem;
        private Dictionary<TodoFilter, ToolStripMenuItem> _filterMenuItems =
            new Dictionary<TodoFilter, ToolStripMenuItem>();
        private Dictionary<TodoSortField, ToolStripMenuItem> _sortMenuItems =
            new Dictionary<TodoSortField, ToolStripMenuItem>();
        private ToolStripMenuItem _sortAscendingItem;

        private readonly string[] _columnTitles = { "任务", "优先级", "截止日期", "创建时间", "状态" };

        private Font _strikeFont;
        private Font _boldFont;

        public MainForm(string dataPath)
        {
            _store = new TodoStore(string.IsNullOrEmpty(dataPath) ? TodoStore.DefaultFilePath : dataPath);

            BuildUi();
            LoadItems();
        }

        #region 界面构建

        private void BuildUi()
        {
            SuspendLayout();

            Font = CreateUiFont();
            _strikeFont = new Font(Font, FontStyle.Strikeout);
            _boldFont = new Font(Font, FontStyle.Bold);

            BackColor = WindowBackColor;
            Text = TitleBase;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(940, 620);
            MinimumSize = new Size(760, 460);
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Font;
            AllowDrop = false;

            _menuStrip = BuildMenu();

            Panel inputPanel = BuildInputPanel();
            Panel filterPanel = BuildFilterPanel();
            Control listPanel = BuildListPanel();
            _statusStrip = BuildStatusBar();

            // Dock 的叠放顺序：先加 Fill 之外的、再加 Fill
            Controls.Add(listPanel);
            Controls.Add(filterPanel);
            Controls.Add(inputPanel);
            Controls.Add(_statusStrip);
            Controls.Add(_menuStrip);

            MainMenuStrip = _menuStrip;

            FormClosing += OnFormClosing;
            KeyDown += OnFormKeyDown;

            ResumeLayout(false);
            PerformLayout();
        }

        private static Font CreateUiFont()
        {
            string[] preferred = { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
            for (int k = 0; k < preferred.Length; k++)
            {
                try
                {
                    Font font = new Font(preferred[k], 9F);
                    if (string.Equals(font.FontFamily.Name, preferred[k], StringComparison.OrdinalIgnoreCase))
                        return font;
                    font.Dispose();
                }
                catch (Exception)
                {
                    // 字体不可用时继续尝试下一个
                }
            }
            return new Font(FontFamily.GenericSansSerif, 9F);
        }

        private MenuStrip BuildMenu()
        {
            MenuStrip menu = new MenuStrip();
            menu.Dock = DockStyle.Top;

            // ---- 文件 ----
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("文件(&F)");

            ToolStripMenuItem newItem = new ToolStripMenuItem("新建任务(&N)", null, OnFocusAddBox);
            newItem.ShortcutKeys = Keys.Control | Keys.N;
            fileMenu.DropDownItems.Add(newItem);

            ToolStripMenuItem importItem = new ToolStripMenuItem("导入任务(&I)…", null, OnImport);
            importItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.I;
            fileMenu.DropDownItems.Add(importItem);

            ToolStripMenuItem exportItem = new ToolStripMenuItem("导出任务(&E)…", null, OnExport);
            exportItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.E;
            fileMenu.DropDownItems.Add(exportItem);

            fileMenu.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem reloadItem = new ToolStripMenuItem("重新载入(&R)", null, OnReload);
            reloadItem.ShortcutKeys = Keys.F5;
            fileMenu.DropDownItems.Add(reloadItem);

            ToolStripMenuItem openFolderItem = new ToolStripMenuItem("打开数据文件夹(&O)", null, OnOpenDataFolder);
            fileMenu.DropDownItems.Add(openFolderItem);

            fileMenu.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出(&X)", null, OnExit);
            exitItem.ShortcutKeys = Keys.Alt | Keys.F4;
            fileMenu.DropDownItems.Add(exitItem);

            // ---- 编辑 ----
            ToolStripMenuItem editMenu = new ToolStripMenuItem("编辑(&E)");

            ToolStripMenuItem editSelectedItem = new ToolStripMenuItem("编辑选中任务(&E)", null, OnEditSelected);
            editSelectedItem.ShortcutKeyDisplayString = "F2";
            editMenu.DropDownItems.Add(editSelectedItem);

            ToolStripMenuItem toggleItem = new ToolStripMenuItem("切换完成状态(&T)", null, OnToggleSelected);
            toggleItem.ShortcutKeyDisplayString = "空格";
            editMenu.DropDownItems.Add(toggleItem);

            ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除选中任务(&D)", null, OnDeleteSelected);
            deleteItem.ShortcutKeyDisplayString = "Delete";
            editMenu.DropDownItems.Add(deleteItem);

            editMenu.DropDownItems.Add(new ToolStripSeparator());

            editMenu.DropDownItems.Add(new ToolStripMenuItem("清除已完成任务(&C)", null, OnClearCompleted));
            editMenu.DropDownItems.Add(new ToolStripMenuItem("清除全部任务(&A)", null, OnClearAll));

            // ---- 视图 ----
            ToolStripMenuItem viewMenu = new ToolStripMenuItem("视图(&V)");

            ToolStripMenuItem filterRoot = new ToolStripMenuItem("筛选(&F)");
            AddFilterMenuItem(filterRoot, TodoFilter.All, "全部(&A)", Keys.Control | Keys.D1);
            AddFilterMenuItem(filterRoot, TodoFilter.Active, "未完成(&T)", Keys.Control | Keys.D2);
            AddFilterMenuItem(filterRoot, TodoFilter.Done, "已完成(&D)", Keys.Control | Keys.D3);
            AddFilterMenuItem(filterRoot, TodoFilter.Overdue, "已逾期(&O)", Keys.Control | Keys.D4);
            viewMenu.DropDownItems.Add(filterRoot);

            ToolStripMenuItem sortRoot = new ToolStripMenuItem("排序(&S)");
            AddSortMenuItem(sortRoot, TodoSortField.CreatedAt, "创建时间(&C)");
            AddSortMenuItem(sortRoot, TodoSortField.Priority, "优先级(&P)");
            AddSortMenuItem(sortRoot, TodoSortField.DueDate, "截止日期(&D)");
            AddSortMenuItem(sortRoot, TodoSortField.Title, "任务名(&T)");
            AddSortMenuItem(sortRoot, TodoSortField.Done, "完成状态(&S)");
            sortRoot.DropDownItems.Add(new ToolStripSeparator());

            _sortAscendingItem = new ToolStripMenuItem("升序(&A)", null, OnToggleSortDirection);
            _sortAscendingItem.CheckOnClick = false;
            sortRoot.DropDownItems.Add(_sortAscendingItem);
            viewMenu.DropDownItems.Add(sortRoot);

            viewMenu.DropDownItems.Add(new ToolStripSeparator());

            _topMostItem = new ToolStripMenuItem("窗口置顶(&P)", null, OnToggleTopMost);
            _topMostItem.CheckOnClick = true;
            viewMenu.DropDownItems.Add(_topMostItem);

            // ---- 帮助 ----
            ToolStripMenuItem helpMenu = new ToolStripMenuItem("帮助(&H)");
            ToolStripMenuItem helpItem = new ToolStripMenuItem("快捷键与关于(&A)…", null, OnShowHelp);
            helpItem.ShortcutKeys = Keys.F1;
            helpMenu.DropDownItems.Add(helpItem);

            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            menu.Items.Add(viewMenu);
            menu.Items.Add(helpMenu);

            return menu;
        }

        private void AddFilterMenuItem(ToolStripMenuItem parent, TodoFilter filter, string text, Keys shortcut)
        {
            TodoFilter captured = filter;
            ToolStripMenuItem item = new ToolStripMenuItem(text, null,
                delegate(object sender, EventArgs e) { SetFilter(captured); });
            item.ShortcutKeys = shortcut;
            parent.DropDownItems.Add(item);
            _filterMenuItems[filter] = item;
        }

        private void AddSortMenuItem(ToolStripMenuItem parent, TodoSortField field, string text)
        {
            TodoSortField captured = field;
            ToolStripMenuItem item = new ToolStripMenuItem(text, null,
                delegate(object sender, EventArgs e) { SetSortField(captured); });
            parent.DropDownItems.Add(item);
            _sortMenuItems[field] = item;
        }

        private Panel BuildInputPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Top;
            panel.Height = 64;
            panel.Padding = new Padding(12, 10, 12, 8);
            panel.BackColor = SurfaceColor;
            panel.Paint += OnSectionPanelPaint;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 4;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _addBox = new TextBox();
            _addBox.Dock = DockStyle.Fill;
            _addBox.Font = new Font(Font.FontFamily, 11F);
            _addBox.Margin = new Padding(0, 6, 10, 0);
            _addBox.KeyDown += OnAddBoxKeyDown;
            SetCueBanner(_addBox, "输入新任务，按 Enter 添加");

            _duePicker = new DateTimePicker();
            _duePicker.Format = DateTimePickerFormat.Custom;
            _duePicker.CustomFormat = "yyyy-MM-dd";
            _duePicker.ShowCheckBox = true;
            _duePicker.Checked = false;
            _duePicker.Value = DateTime.Today;
            _duePicker.Width = 138;
            _duePicker.Margin = new Padding(0, 7, 10, 0);

            _priorityBox = new ComboBox();
            _priorityBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _priorityBox.Items.AddRange(new object[] { "高", "中", "低" });
            _priorityBox.SelectedIndex = 1;
            _priorityBox.Width = 68;
            _priorityBox.Margin = new Padding(0, 7, 10, 0);

            _addButton = new Button();
            _addButton.Text = "＋ 添加";
            _addButton.Width = 88;
            _addButton.Height = 30;
            _addButton.Margin = new Padding(0, 5, 0, 0);
            _addButton.FlatStyle = FlatStyle.Flat;
            _addButton.BackColor = PrimaryColor;
            _addButton.ForeColor = Color.White;
            _addButton.FlatAppearance.BorderSize = 0;
            _addButton.Click += OnAddClick;

            layout.Controls.Add(_addBox, 0, 0);
            layout.Controls.Add(_duePicker, 1, 0);
            layout.Controls.Add(_priorityBox, 2, 0);
            layout.Controls.Add(_addButton, 3, 0);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel BuildFilterPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Top;
            panel.Height = 48;
            panel.Padding = new Padding(12, 6, 12, 8);
            panel.BackColor = SurfaceColor;
            panel.Paint += OnSectionPanelPaint;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 3;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _searchBox = new TextBox();
            _searchBox.Dock = DockStyle.Fill;
            _searchBox.Margin = new Padding(0, 5, 12, 0);
            _searchBox.TextChanged += OnSearchChanged;
            SetCueBanner(_searchBox, "搜索任务或备注（Ctrl+F）");

            FlowLayoutPanel filters = new FlowLayoutPanel();
            filters.AutoSize = true;
            filters.WrapContents = false;
            filters.Margin = new Padding(0);

            AddFilterButton(filters, TodoFilter.All, "全部");
            AddFilterButton(filters, TodoFilter.Active, "未完成");
            AddFilterButton(filters, TodoFilter.Done, "已完成");
            AddFilterButton(filters, TodoFilter.Overdue, "已逾期");

            _clearDoneButton = new Button();
            _clearDoneButton.Text = "清除已完成";
            _clearDoneButton.Width = 108;
            _clearDoneButton.Height = 28;
            _clearDoneButton.Margin = new Padding(12, 2, 0, 0);
            _clearDoneButton.FlatStyle = FlatStyle.Flat;
            _clearDoneButton.BackColor = SurfaceColor;
            _clearDoneButton.FlatAppearance.BorderColor = BorderColor;
            _clearDoneButton.Click += OnClearCompleted;

            layout.Controls.Add(_searchBox, 0, 0);
            layout.Controls.Add(filters, 1, 0);
            layout.Controls.Add(_clearDoneButton, 2, 0);

            panel.Controls.Add(layout);
            return panel;
        }

        private void AddFilterButton(FlowLayoutPanel parent, TodoFilter filter, string text)
        {
            RadioButton button = new RadioButton();
            button.Appearance = Appearance.Button;
            button.Text = text;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Width = 78;
            button.Height = 28;
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = SurfaceColor;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.CheckedBackColor = Color.FromArgb(228, 237, 255);
            button.Margin = new Padding(0, 0, 4, 0);
            button.Tag = filter;
            button.Checked = filter == _filter;
            button.CheckedChanged += OnFilterButtonChecked;

            parent.Controls.Add(button);
            _filterButtons.Add(button);
        }

        private Control BuildListPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(12, 8, 12, 8);
            panel.BackColor = WindowBackColor;

            _emptyLabel = new Label();
            _emptyLabel.Dock = DockStyle.Fill;
            _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _emptyLabel.ForeColor = MutedTextColor;
            _emptyLabel.Font = new Font(Font.FontFamily, 11F);
            _emptyLabel.Visible = false;

            _list = new ListView();
            _list.Dock = DockStyle.Fill;
            _list.Margin = new Padding(0);
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.CheckBoxes = true;
            _list.MultiSelect = true;
            _list.HideSelection = false;
            _list.ShowItemToolTips = true;
            _list.GridLines = false;
            _list.LabelEdit = false;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.BackColor = SurfaceColor;
            _list.ForeColor = Color.FromArgb(32, 38, 50);
            _list.Columns.Add(_columnTitles[0], 400, HorizontalAlignment.Left);
            _list.Columns.Add(_columnTitles[1], 70, HorizontalAlignment.Center);
            _list.Columns.Add(_columnTitles[2], 110, HorizontalAlignment.Center);
            _list.Columns.Add(_columnTitles[3], 130, HorizontalAlignment.Center);
            _list.Columns.Add(_columnTitles[4], 80, HorizontalAlignment.Center);
            _list.ColumnClick += OnColumnClick;
            _list.ItemChecked += OnItemChecked;
            _list.DoubleClick += OnEditSelected;
            _list.KeyDown += OnListKeyDown;
            _list.ContextMenuStrip = BuildContextMenu();

            panel.Controls.Add(_list);
            panel.Controls.Add(_emptyLabel);
            _emptyLabel.BringToFront();

            return panel;
        }

        private ContextMenuStrip BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("编辑…", null, OnEditSelected));
            menu.Items.Add(new ToolStripMenuItem("切换完成状态", null, OnToggleSelected));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("删除", null, OnDeleteSelected));
            menu.Opening += OnContextMenuOpening;
            return menu;
        }

        private StatusStrip BuildStatusBar()
        {
            StatusStrip strip = new StatusStrip();
            strip.Dock = DockStyle.Bottom;
            strip.BackColor = SurfaceColor;
            strip.SizingGrip = false;

            _statusCounts = new ToolStripStatusLabel();
            _statusCounts.Spring = true;
            _statusCounts.TextAlign = ContentAlignment.MiddleLeft;
            _statusCounts.ForeColor = MutedTextColor;

            _statusView = new ToolStripStatusLabel();
            _statusView.ForeColor = MutedTextColor;

            strip.Items.Add(_statusCounts);
            strip.Items.Add(_statusView);
            return strip;
        }

        #endregion

        #region 数据加载与刷新

        private void LoadItems()
        {
            _items = _store.Load();

            _sortAscending = DefaultAscending(_sortField);
            RefreshList(null);

            if (!string.IsNullOrEmpty(_store.LastWarning))
            {
                MessageBox.Show(this, _store.LastWarning, TitleBase, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool DefaultAscending(TodoSortField field)
        {
            switch (field)
            {
                case TodoSortField.CreatedAt: return false; // 最新的排前面
                case TodoSortField.Priority: return false;  // 高优先级排前面
                default: return true;
            }
        }

        private void Persist()
        {
            try
            {
                _store.Save(_items);
                _dirty = false;
            }
            catch (Exception ex)
            {
                _dirty = true;
                MessageBox.Show(this, "保存失败：" + ex.Message + Environment.NewLine + Environment.NewLine +
                    "数据仍在内存中，请检查磁盘空间或文件权限后重试。",
                    TitleBase, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshList(Guid? selectId)
        {
            List<TodoItem> visible = TodoQuery.Select(_items, _filter, _searchBox.Text, _sortField, _sortAscending);

            _loading = true;
            try
            {
                _list.BeginUpdate();
                _list.Items.Clear();

                for (int k = 0; k < visible.Count; k++)
                {
                    _list.Items.Add(CreateRow(visible[k]));
                }

                if (selectId.HasValue)
                {
                    for (int k = 0; k < _list.Items.Count; k++)
                    {
                        TodoItem item = _list.Items[k].Tag as TodoItem;
                        if (item != null && item.Id == selectId.Value)
                        {
                            _list.Items[k].Selected = true;
                            _list.Items[k].Focused = true;
                            _list.Items[k].EnsureVisible();
                            break;
                        }
                    }
                }
            }
            finally
            {
                _list.EndUpdate();
                _loading = false;
            }

            bool showList = visible.Count > 0;
            _list.Visible = showList;
            _emptyLabel.Visible = !showList;
            if (!showList)
            {
                _emptyLabel.Text = _items.Count == 0
                    ? "还没有任务" + Environment.NewLine + "在上方输入内容后按 Enter 添加"
                    : "没有符合条件的任务" + Environment.NewLine + "试试更换筛选条件，或清空搜索关键词";
            }

            UpdateFilterButtonStyles();
            UpdateColumnHeaders();
            UpdateStatus(visible.Count);
        }

        private ListViewItem CreateRow(TodoItem item)
        {
            ListViewItem row = new ListViewItem(item.Title);
            row.Tag = item;
            row.Checked = item.IsDone;
            row.SubItems.Add(TodoItem.PriorityLabel(item.Priority));
            row.SubItems.Add(item.DueDate.HasValue
                ? item.DueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "—");
            row.SubItems.Add(item.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            row.SubItems.Add(item.IsDone ? "已完成" : (item.IsOverdue ? "已逾期" : "进行中"));
            row.ToolTipText = BuildToolTip(item);
            if (_list.Items.Count % 2 == 1) row.BackColor = AlternateRowColor;
            ApplyRowStyle(row, item);
            return row;
        }

        private void ApplyRowStyle(ListViewItem row, TodoItem item)
        {
            if (item.IsDone)
            {
                row.ForeColor = Color.FromArgb(140, 140, 140);
                row.Font = _strikeFont;
                return;
            }

            if (item.IsOverdue) row.ForeColor = Color.FromArgb(200, 45, 45);
            else if (item.IsDueToday) row.ForeColor = Color.FromArgb(190, 110, 0);
            else row.ForeColor = Color.FromArgb(30, 30, 30);

            if (item.Priority == Priority.High) row.Font = _boldFont;
        }

        private static string BuildToolTip(TodoItem item)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(item.Title);
            if (!string.IsNullOrEmpty(item.Note)) sb.AppendLine("备注：" + item.Note);
            sb.AppendLine("优先级：" + TodoItem.PriorityLabel(item.Priority));
            sb.AppendLine("截止日期：" + (item.DueDate.HasValue
                ? item.DueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "无"));
            sb.AppendLine("创建时间：" + item.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            if (item.IsDone && item.CompletedAt.HasValue)
                sb.AppendLine("完成时间：" + item.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            return sb.ToString().TrimEnd();
        }

        private void UpdateStatus(int visibleCount)
        {
            int total = _items.Count;
            int done = TodoQuery.CountDone(_items);
            int overdue = TodoQuery.CountOverdue(_items);

            _statusCounts.Text = string.Format(CultureInfo.InvariantCulture,
                "共 {0} 项 · 未完成 {1} · 已完成 {2} · 已逾期 {3} · 当前显示 {4} 项",
                total, total - done, done, overdue, visibleCount);

            _statusView.Text = "筛选：" + TodoQuery.FilterLabel(_filter) +
                " · 排序：" + TodoQuery.SortLabel(_sortField) + (_sortAscending ? " ↑" : " ↓");
            _statusView.ToolTipText = "数据文件：" + _store.FilePath;
        }

        private void UpdateColumnHeaders()
        {
            for (int k = 0; k < _list.Columns.Count; k++)
            {
                string text = _columnTitles[k];
                if (FieldForColumn(k) == _sortField) text += _sortAscending ? " ▲" : " ▼";
                _list.Columns[k].Text = text;
            }
        }

        private static TodoSortField FieldForColumn(int column)
        {
            switch (column)
            {
                case 0: return TodoSortField.Title;
                case 1: return TodoSortField.Priority;
                case 2: return TodoSortField.DueDate;
                case 4: return TodoSortField.Done;
                default: return TodoSortField.CreatedAt;
            }
        }

        private void SyncMenuChecks()
        {
            foreach (KeyValuePair<TodoFilter, ToolStripMenuItem> pair in _filterMenuItems)
            {
                pair.Value.Checked = pair.Key == _filter;
            }
            foreach (KeyValuePair<TodoSortField, ToolStripMenuItem> pair in _sortMenuItems)
            {
                pair.Value.Checked = pair.Key == _sortField;
            }
            if (_sortAscendingItem != null) _sortAscendingItem.Checked = _sortAscending;

            for (int k = 0; k < _filterButtons.Count; k++)
            {
                RadioButton button = _filterButtons[k];
                bool shouldCheck = (TodoFilter)button.Tag == _filter;
                if (button.Checked != shouldCheck) button.Checked = shouldCheck;
            }

            UpdateFilterButtonStyles();
        }

        private void UpdateFilterButtonStyles()
        {
            for (int k = 0; k < _filterButtons.Count; k++)
            {
                RadioButton button = _filterButtons[k];
                if (button.Checked)
                {
                    button.BackColor = Color.FromArgb(228, 237, 255);
                    button.ForeColor = PrimaryColor;
                    button.FlatAppearance.BorderColor = PrimaryColor;
                }
                else
                {
                    button.BackColor = SurfaceColor;
                    button.ForeColor = Color.FromArgb(45, 52, 66);
                    button.FlatAppearance.BorderColor = BorderColor;
                }
            }
        }

        private static void SetCueBanner(TextBox box, string text)
        {
            if (box == null || string.IsNullOrEmpty(text)) return;
            try { SendMessage(box.Handle, EmSetCueBanner, (IntPtr)1, text); }
            catch (Exception) { /* 旧系统不支持占位提示时忽略 */ }
        }

        #endregion

        #region 任务操作

        private void AddTask()
        {
            string title = _addBox.Text.Trim();
            if (title.Length == 0)
            {
                _addBox.Focus();
                return;
            }

            TodoItem item = new TodoItem();
            item.Title = title;
            item.Priority = SelectedPriority();
            if (_duePicker.Checked) item.DueDate = _duePicker.Value.Date;

            _items.Add(item);

            _addBox.Clear();
            _duePicker.Checked = false;
            _duePicker.Value = DateTime.Today;
            _priorityBox.SelectedIndex = 1;

            Persist();

            // 新任务可能被当前筛选条件挡住，切回「全部」以便用户看到结果
            if (!TodoQuery.Matches(item, _filter, _searchBox.Text))
            {
                _filter = TodoFilter.All;
                _searchBox.Clear();
                SyncMenuChecks();
            }

            RefreshList(item.Id);
            _addBox.Focus();
        }

        private Priority SelectedPriority()
        {
            switch (_priorityBox.SelectedIndex)
            {
                case 0: return Priority.High;
                case 2: return Priority.Low;
                default: return Priority.Normal;
            }
        }

        private void EditSelected()
        {
            List<TodoItem> selected = GetSelectedItems();
            if (selected.Count == 0)
            {
                ShowHint("请先在列表中选择一个任务。");
                return;
            }

            TodoItem original = selected[0];
            TodoItem draft = original.Clone();

            using (EditItemDialog dialog = new EditItemDialog(draft))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                original.Title = draft.Title;
                original.Note = draft.Note;
                original.Priority = draft.Priority;
                original.DueDate = draft.DueDate;
            }

            Persist();
            RefreshList(original.Id);
        }

        private void ToggleSelected()
        {
            List<TodoItem> selected = GetSelectedItems();
            if (selected.Count == 0)
            {
                ShowHint("请先在列表中选择要切换状态的任务。");
                return;
            }

            // 以第一个任务的当前状态为准，其余取反，便于批量操作
            bool target = !selected[0].IsDone;
            for (int k = 0; k < selected.Count; k++) SetDone(selected[k], target);

            Persist();
            RefreshList(selected[0].Id);
        }

        private void DeleteSelected()
        {
            List<TodoItem> selected = GetSelectedItems();
            if (selected.Count == 0)
            {
                ShowHint("请先在列表中选择要删除的任务。");
                return;
            }

            string message = selected.Count == 1
                ? "确定要删除任务「" + selected[0].Title + "」吗？"
                : "确定要删除选中的 " + selected.Count.ToString(CultureInfo.InvariantCulture) + " 个任务吗？";

            if (MessageBox.Show(this, message, TitleBase, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            for (int k = 0; k < selected.Count; k++) _items.Remove(selected[k]);

            Persist();
            RefreshList(null);
        }

        private void ClearCompleted()
        {
            int completed = TodoQuery.CountDone(_items);
            if (completed == 0)
            {
                ShowHint("当前没有已完成的任务。");
                return;
            }

            if (MessageBox.Show(this,
                    "确定要清除 " + completed.ToString(CultureInfo.InvariantCulture) + " 个已完成任务吗？此操作不可撤销。",
                    TitleBase, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            // 先确认再删除：用户选择「否」时不能改动任何数据
            _items.RemoveAll(delegate(TodoItem item) { return item.IsDone; });

            Persist();
            RefreshList(null);
        }

        private void ClearAll()
        {
            if (_items.Count == 0)
            {
                ShowHint("当前没有任务。");
                return;
            }

            if (MessageBox.Show(this,
                    "确定要清除全部 " + _items.Count.ToString(CultureInfo.InvariantCulture) + " 个任务吗？此操作不可撤销。",
                    TitleBase, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            _items.Clear();
            Persist();
            RefreshList(null);
        }

        private void SetDone(TodoItem item, bool done)
        {
            item.IsDone = done;
            item.CompletedAt = done ? (DateTime?)DateTime.Now : null;
        }

        private List<TodoItem> GetSelectedItems()
        {
            List<TodoItem> result = new List<TodoItem>();
            for (int k = 0; k < _list.SelectedItems.Count; k++)
            {
                TodoItem item = _list.SelectedItems[k].Tag as TodoItem;
                if (item != null) result.Add(item);
            }
            return result;
        }

        #endregion

        #region 文件操作

        private void ReloadFromDisk()
        {
            if (_dirty && MessageBox.Show(this,
                    "有改动尚未保存成功，重新载入会丢失这些改动。要继续吗？",
                    TitleBase, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            LoadItems();
        }

        private void ExportItems()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "导出任务";
                dialog.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
                dialog.FileName = "tasks-" + DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".json";
                dialog.AddExtension = true;
                dialog.DefaultExt = "json";

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    new TodoStore(dialog.FileName).Save(_items);
                    MessageBox.Show(this,
                        "已导出 " + _items.Count.ToString(CultureInfo.InvariantCulture) + " 个任务到：" +
                        Environment.NewLine + dialog.FileName,
                        TitleBase, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：" + ex.Message, TitleBase,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportItems()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "导入任务";
                dialog.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                List<TodoItem> imported;
                string warning;
                try
                {
                    TodoStore source = new TodoStore(dialog.FileName);
                    imported = source.Load();
                    warning = source.LastWarning;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导入失败：" + ex.Message, TitleBase,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!string.IsNullOrEmpty(warning))
                {
                    MessageBox.Show(this, warning, TitleBase, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                if (imported.Count == 0)
                {
                    MessageBox.Show(this, "该文件中没有可导入的任务。", TitleBase,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                DialogResult choice = MessageBox.Show(this,
                    "读取到 " + imported.Count.ToString(CultureInfo.InvariantCulture) + " 个任务。" + Environment.NewLine + Environment.NewLine +
                    "「是」追加到现有任务；「否」替换全部任务；「取消」放弃导入。",
                    TitleBase, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (choice == DialogResult.Cancel) return;

                if (choice == DialogResult.No)
                {
                    _items = imported;
                }
                else
                {
                    // 追加时避免 Id 冲突
                    HashSet<Guid> existing = new HashSet<Guid>();
                    for (int k = 0; k < _items.Count; k++) existing.Add(_items[k].Id);

                    for (int k = 0; k < imported.Count; k++)
                    {
                        while (imported[k].Id == Guid.Empty || existing.Contains(imported[k].Id))
                        {
                            imported[k].Id = Guid.NewGuid();
                        }

                        existing.Add(imported[k].Id);
                        _items.Add(imported[k]);
                    }
                }

                Persist();
                RefreshList(null);
            }
        }

        private void OpenDataFolder()
        {
            try
            {
                string directory = Path.GetDirectoryName(_store.FilePath);
                if (string.IsNullOrEmpty(directory)) return;
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                Process.Start("explorer.exe", "\"" + directory + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开数据文件夹：" + ex.Message, TitleBase,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 事件处理

        private void OnAddClick(object sender, EventArgs e)
        {
            AddTask();
        }

        private void OnAddBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddTask();
            }
        }

        private void OnSearchChanged(object sender, EventArgs e)
        {
            RefreshList(null);
        }

        private void OnFilterButtonChecked(object sender, EventArgs e)
        {
            RadioButton button = sender as RadioButton;
            if (button == null || !button.Checked) return;

            TodoFilter filter = (TodoFilter)button.Tag;
            if (filter == _filter) return;

            _filter = filter;
            SyncMenuChecks();
            RefreshList(null);
        }

        private void OnItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_loading) return;

            TodoItem item = e.Item.Tag as TodoItem;
            if (item == null || item.IsDone == e.Item.Checked) return;

            SetDone(item, e.Item.Checked);
            Persist();

            // 延后到本次事件处理结束后再重建列表，避免在 ItemChecked 回调中销毁当前项
            Guid id = item.Id;
            BeginInvoke((MethodInvoker)delegate { RefreshList(id); });
        }

        private void OnColumnClick(object sender, ColumnClickEventArgs e)
        {
            TodoSortField field = FieldForColumn(e.Column);

            if (_sortField == field) _sortAscending = !_sortAscending;
            else
            {
                _sortField = field;
                _sortAscending = DefaultAscending(field);
            }

            SyncMenuChecks();
            RefreshList(null);
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                e.Handled = true;
                DeleteSelected();
            }
            else if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                EditSelected();
            }
            else if (e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                ToggleSelected();
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                for (int k = 0; k < _list.Items.Count; k++) _list.Items[k].Selected = true;
                e.Handled = true;
            }
        }

        private void OnEditSelected(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void OnToggleSelected(object sender, EventArgs e)
        {
            ToggleSelected();
        }

        private void OnDeleteSelected(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        private void OnClearCompleted(object sender, EventArgs e)
        {
            ClearCompleted();
        }

        private void OnClearAll(object sender, EventArgs e)
        {
            ClearAll();
        }

        private void OnReload(object sender, EventArgs e)
        {
            ReloadFromDisk();
        }

        private void OnExport(object sender, EventArgs e)
        {
            ExportItems();
        }

        private void OnImport(object sender, EventArgs e)
        {
            ImportItems();
        }

        private void OnOpenDataFolder(object sender, EventArgs e)
        {
            OpenDataFolder();
        }

        private void OnExit(object sender, EventArgs e)
        {
            Close();
        }

        private void OnFocusAddBox(object sender, EventArgs e)
        {
            _addBox.Focus();
            _addBox.SelectAll();
        }

        private void OnToggleTopMost(object sender, EventArgs e)
        {
            TopMost = _topMostItem.Checked;
        }

        private void OnToggleSortDirection(object sender, EventArgs e)
        {
            _sortAscending = !_sortAscending;
            SyncMenuChecks();
            RefreshList(null);
        }

        private void OnShowHelp(object sender, EventArgs e)
        {
            using (HelpDialog dialog = new HelpDialog(_store.FilePath))
            {
                dialog.ShowDialog(this);
            }
        }

        private void OnContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool hasSelection = _list.SelectedItems.Count > 0;
            for (int k = 0; k < _list.ContextMenuStrip.Items.Count; k++)
            {
                ToolStripItem item = _list.ContextMenuStrip.Items[k];
                if (item is ToolStripMenuItem) item.Enabled = hasSelection;
            }
        }

        private void OnSectionPanelPaint(object sender, PaintEventArgs e)
        {
            Control control = sender as Control;
            if (control == null) return;
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.DrawLine(pen, 0, control.Height - 1, control.Width, control.Height - 1);
            }
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _searchBox.Focused && _searchBox.TextLength > 0)
            {
                _searchBox.Clear();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                e.Handled = true;
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_dirty) return;

            if (MessageBox.Show(this,
                    "有改动尚未保存成功，确定要退出吗？",
                    TitleBase, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                e.Cancel = true;
            }
        }

        private void SetFilter(TodoFilter filter)
        {
            if (_filter == filter) return;
            _filter = filter;
            SyncMenuChecks();
            RefreshList(null);
        }

        private void SetSortField(TodoSortField field)
        {
            if (_sortField == field)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortField = field;
                _sortAscending = DefaultAscending(field);
            }

            SyncMenuChecks();
            RefreshList(null);
        }

        private void ShowHint(string message)
        {
            MessageBox.Show(this, message, TitleBase, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_strikeFont != null) _strikeFont.Dispose();
                if (_boldFont != null) _boldFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
