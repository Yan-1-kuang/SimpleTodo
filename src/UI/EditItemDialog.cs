using System;
using System.Drawing;
using System.Windows.Forms;
using SimpleTodo.Models;

namespace SimpleTodo.UI
{
    /// <summary>
    /// 编辑任务对话框。点击「确定」时把结果写回构造函数传入的对象；
    /// 点击「取消」不会对传入对象做任何修改。
    /// </summary>
    public class EditItemDialog : Form
    {
        private readonly TodoItem _draft;

        private TextBox _titleBox;
        private TextBox _noteBox;
        private ComboBox _priorityBox;
        private DateTimePicker _duePicker;
        private Button _okButton;
        private Button _cancelButton;

        public EditItemDialog(TodoItem draft)
        {
            if (draft == null) throw new ArgumentNullException("draft");
            _draft = draft;

            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = "编辑任务";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 300);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(246, 248, 252);
            Font = CreateUiFont();

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(16, 16, 16, 10);
            layout.BackColor = Color.White;
            layout.ColumnCount = 2;
            layout.RowCount = 5;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _titleBox = new TextBox();
            _titleBox.Dock = DockStyle.Fill;
            _titleBox.Font = new Font(Font.FontFamily, 10F);
            _titleBox.Margin = new Padding(8, 4, 0, 10);

            _noteBox = new TextBox();
            _noteBox.Dock = DockStyle.Fill;
            _noteBox.Multiline = true;
            _noteBox.ScrollBars = ScrollBars.Vertical;
            _noteBox.AcceptsReturn = true;
            _noteBox.Margin = new Padding(8, 4, 0, 10);

            _priorityBox = new ComboBox();
            _priorityBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _priorityBox.Items.AddRange(new object[] { "高", "中", "低" });
            _priorityBox.Width = 90;
            _priorityBox.Margin = new Padding(8, 4, 0, 10);

            _duePicker = new DateTimePicker();
            _duePicker.Format = DateTimePickerFormat.Custom;
            _duePicker.CustomFormat = "yyyy-MM-dd";
            _duePicker.ShowCheckBox = true;
            _duePicker.Width = 150;
            _duePicker.Margin = new Padding(8, 4, 0, 10);

            layout.Controls.Add(MakeLabel("任务内容："), 0, 0);
            layout.Controls.Add(_titleBox, 1, 0);
            layout.Controls.Add(MakeLabel("备注："), 0, 1);
            layout.Controls.Add(_noteBox, 1, 1);
            layout.Controls.Add(MakeLabel("优先级："), 0, 2);
            layout.Controls.Add(_priorityBox, 1, 2);
            layout.Controls.Add(MakeLabel("截止日期："), 0, 3);
            layout.Controls.Add(_duePicker, 1, 3);

            Label hint = new Label();
            hint.Text = "不勾选复选框表示没有截止日期。";
            hint.ForeColor = Color.FromArgb(120, 120, 120);
            hint.AutoSize = true;
            hint.Margin = new Padding(6, 0, 0, 4);
            layout.Controls.Add(hint, 1, 4);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Height = 44;
            buttons.Padding = new Padding(0, 6, 12, 8);

            _cancelButton = new Button();
            _cancelButton.Text = "取消";
            _cancelButton.Width = 84;
            _cancelButton.FlatStyle = FlatStyle.Flat;
            _cancelButton.BackColor = Color.White;
            _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(222, 228, 238);
            _cancelButton.DialogResult = DialogResult.Cancel;

            _okButton = new Button();
            _okButton.Text = "确定";
            _okButton.Width = 84;
            _okButton.Margin = new Padding(8, 0, 0, 0);
            _okButton.FlatStyle = FlatStyle.Flat;
            _okButton.BackColor = Color.FromArgb(42, 104, 214);
            _okButton.ForeColor = Color.White;
            _okButton.FlatAppearance.BorderSize = 0;
            _okButton.Click += OnOkClick;

            buttons.Controls.Add(_cancelButton);
            buttons.Controls.Add(_okButton);

            Controls.Add(layout);
            Controls.Add(buttons);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            ResumeLayout(false);
            PerformLayout();
        }

        private static Label MakeLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(70, 82, 102);
            label.Margin = new Padding(0, 7, 0, 0);
            return label;
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

        private void LoadValues()
        {
            _titleBox.Text = _draft.Title;
            _noteBox.Text = _draft.Note;

            switch (_draft.Priority)
            {
                case Priority.High: _priorityBox.SelectedIndex = 0; break;
                case Priority.Low: _priorityBox.SelectedIndex = 2; break;
                default: _priorityBox.SelectedIndex = 1; break;
            }

            if (_draft.DueDate.HasValue)
            {
                _duePicker.Value = _draft.DueDate.Value;
                _duePicker.Checked = true;
            }
            else
            {
                _duePicker.Value = DateTime.Today;
                _duePicker.Checked = false;
            }

            _titleBox.SelectAll();
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            string title = _titleBox.Text.Trim();
            if (title.Length == 0)
            {
                MessageBox.Show(this, "任务内容不能为空。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _titleBox.Focus();
                return; // DialogResult 仍为 None，窗口保持打开
            }

            _draft.Title = title;
            _draft.Note = _noteBox.Text.Trim();

            switch (_priorityBox.SelectedIndex)
            {
                case 0: _draft.Priority = Priority.High; break;
                case 2: _draft.Priority = Priority.Low; break;
                default: _draft.Priority = Priority.Normal; break;
            }

            _draft.DueDate = _duePicker.Checked ? (DateTime?)_duePicker.Value.Date : null;

            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _titleBox.Focus();
        }
    }
}
