using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace SimpleTodo.UI
{
    /// <summary>快捷键说明与关于信息。</summary>
    public class HelpDialog : Form
    {
        public HelpDialog(string dataFilePath)
        {
            SuspendLayout();

            Text = "快捷键与关于";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 400);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(246, 248, 252);

            TextBox content = new TextBox();
            content.Dock = DockStyle.Fill;
            content.Multiline = true;
            content.ReadOnly = true;
            content.WordWrap = false;
            content.ScrollBars = ScrollBars.Both;
            content.BackColor = Color.White;
            content.BorderStyle = BorderStyle.FixedSingle;
            content.Font = new Font("Consolas", 9F);
            content.Text = BuildContent(dataFilePath);

            Button ok = new Button();
            ok.Text = "关闭";
            ok.Width = 84;
            ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Color.White;
            ok.FlatAppearance.BorderColor = Color.FromArgb(222, 228, 238);
            ok.DialogResult = DialogResult.OK;

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Height = 44;
            buttons.Padding = new Padding(0, 6, 12, 8);
            buttons.Controls.Add(ok);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(14, 14, 14, 6);
            body.BackColor = Color.FromArgb(246, 248, 252);
            body.Controls.Add(content);

            Controls.Add(body);
            Controls.Add(buttons);

            AcceptButton = ok;
            CancelButton = ok;

            ResumeLayout(false);
            PerformLayout();
        }

        private static string BuildContent(string dataFilePath)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("快捷键");
            sb.AppendLine("--------------------------------------------");
            sb.AppendLine("Ctrl + N          新建任务（聚焦输入框）");
            sb.AppendLine("Enter             在输入框中按下，添加任务");
            sb.AppendLine("F2 / 双击         编辑选中任务");
            sb.AppendLine("空格              切换选中任务的完成状态");
            sb.AppendLine("Delete            删除选中任务（支持多选）");
            sb.AppendLine("Ctrl + A          全选列表项");
            sb.AppendLine("Ctrl + F          聚焦搜索框（在搜索框按 Esc 清空）");
            sb.AppendLine("Ctrl + 1 / 2 / 3 / 4   切换筛选：全部 / 未完成 / 已完成 / 已逾期");
            sb.AppendLine("F5                重新从磁盘载入");
            sb.AppendLine("Ctrl + Shift + E  导出任务到 JSON");
            sb.AppendLine("Ctrl + Shift + I  从 JSON 导入任务");
            sb.AppendLine("F1                显示本窗口");
            sb.AppendLine();
            sb.AppendLine("使用提示");
            sb.AppendLine("--------------------------------------------");
            sb.AppendLine("· 单击列表左侧复选框即可标记完成，已完成任务显示删除线。");
            sb.AppendLine("· 逾期任务显示红色，今天到期显示橙色，高优先级加粗显示。");
            sb.AppendLine("· 单击列标题可排序，再次单击同一列切换升降序。");
            sb.AppendLine("· 所有改动都会立即保存，无需手动保存。");
            sb.AppendLine();
            sb.AppendLine("关于");
            sb.AppendLine("--------------------------------------------");
            sb.AppendLine(Program.AppTitle + " " + Program.Version);
            sb.AppendLine("技术栈：C# / WinForms / .NET Framework 4.8（零第三方依赖）");
            sb.AppendLine("数据文件：");
            sb.AppendLine("  " + (dataFilePath ?? "(默认位置)"));
            sb.AppendLine();
            sb.AppendLine("命令行参数：");
            sb.AppendLine("  --data <路径>   指定数据文件");
            sb.AppendLine("  --selftest      运行内置自检（16 项）");
            sb.AppendLine("  --version       显示版本号");
            sb.AppendLine("  --help          显示帮助");
            sb.AppendLine();
            sb.AppendLine("许可证：MIT");
            sb.AppendLine("当前时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            return sb.ToString();
        }
    }
}
