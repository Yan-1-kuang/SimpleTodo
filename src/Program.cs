using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using SimpleTodo.Services;
using SimpleTodo.UI;

namespace SimpleTodo
{
    internal static class Program
    {
        public const string AppName = "SimpleTodo";
        public const string AppTitle = "极简待办 SimpleTodo";
        public const string Version = "1.0.0";

        private const int AttachParentProcess = -1;

        /// <summary>同一数据文件只允许一个实例，避免两个窗口互相覆盖数据。</summary>
        private static Mutex _instanceLock;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        [STAThread]
        private static int Main(string[] args)
        {
            if (HasFlag(args, "--selftest"))
            {
                // --log <路径>：把自检报告写入文件，便于在 CI 或无人值守环境中收集结果
                string logPath = GetOption(args, "--log");
                if (!string.IsNullOrEmpty(logPath))
                {
                    StringWriter buffer = new StringWriter();
                    Console.SetOut(buffer);
                    int exitCode = SelfTest.Run();
                    try { File.WriteAllText(logPath, buffer.ToString(), new UTF8Encoding(false)); }
                    catch (Exception) { /* 写日志失败不影响退出码 */ }
                    return exitCode;
                }

                EnsureConsole();
                return SelfTest.Run();
            }
            if (HasFlag(args, "--version")) { EnsureConsole(); Console.WriteLine(AppName + " " + Version); return 0; }
            if (HasFlag(args, "--help") || HasFlag(args, "-h") || HasFlag(args, "/?"))
            {
                EnsureConsole();
                PrintHelp();
                return 0;
            }

            string dataPath = GetOption(args, "--data");
            string resolvedDataPath = string.IsNullOrEmpty(dataPath) ? TodoStore.DefaultFilePath : dataPath;

            // 同一数据文件只允许一个实例：两个窗口同时保存会互相覆盖
            _instanceLock = TryAcquireInstanceLock(resolvedDataPath);
            if (_instanceLock == null)
            {
                MessageBox.Show(
                    "已经有一个 SimpleTodo 窗口在使用该数据文件：" + Environment.NewLine + Environment.NewLine +
                    Path.GetFullPath(resolvedDataPath) + Environment.NewLine + Environment.NewLine +
                    "请切换到已打开的窗口；如果需要同时使用多份清单，请用 --data 参数指定不同的数据文件。",
                    AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 2;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            try
            {
                Application.Run(new MainForm(dataPath));
                return 0;
            }
            catch (Exception ex)
            {
                ReportFatal(ex);
                return 1;
            }
        }

        /// <summary>
        /// 本程序编译为 Windows 子系统（双击不弹黑框），因此默认没有控制台。
        /// 当从终端运行时，附加到父进程控制台，使 --help / --selftest 的输出可见。
        /// </summary>
        private static void EnsureConsole()
        {
            try
            {
                if (!AttachConsole(AttachParentProcess)) return;
                StreamWriter writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
                writer.AutoFlush = true;
                Console.SetOut(writer);

                StreamWriter errorWriter = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false));
                errorWriter.AutoFlush = true;
                Console.SetError(errorWriter);
            }
            catch (Exception)
            {
                // 无法附加控制台时静默忽略（例如被重定向或没有父控制台）
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine(AppTitle + " " + Version);
            Console.WriteLine();
            Console.WriteLine("用法：SimpleTodo.exe [选项]");
            Console.WriteLine();
            Console.WriteLine("选项：");
            Console.WriteLine("  --data <文件路径>   指定数据文件（默认 %APPDATA%\\SimpleTodo\\tasks.json）");
            Console.WriteLine("  --selftest          运行内置自检并退出（全部通过时退出码为 0）");
            Console.WriteLine("  --log <文件路径>    与 --selftest 搭配，把自检报告写入指定文件");
            Console.WriteLine("  --version           显示版本号");
            Console.WriteLine("  --help              显示本帮助");
            Console.WriteLine();
            Console.WriteLine("快捷键：");
            Console.WriteLine("  Ctrl+N 新建   F2 编辑   Delete 删除   Space 切换完成");
            Console.WriteLine("  Ctrl+F 搜索   F5 重新载入   Ctrl+Shift+E 导出   Ctrl+Shift+I 导入");
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            ReportFatal(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ReportFatal(e.ExceptionObject as Exception);
        }

        /// <summary>把未处理异常写入日志并提示用户，避免“闪退且无任何信息”。</summary>
        private static void ReportFatal(Exception ex)
        {
            string logPath = null;
            try
            {
                string directory = TodoStore.DefaultDirectory;
                Directory.CreateDirectory(directory);
                logPath = Path.Combine(directory, "error.log");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                sb.AppendLine("版本：" + AppName + " " + Version);
                sb.AppendLine("错误：" + (ex == null ? "(未知)" : ex.ToString()));
                sb.AppendLine(new string('-', 60));
                File.AppendAllText(logPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // 记录日志失败时不再抛出，避免掩盖原始错误
            }

            string message = "程序遇到未处理的错误：" + Environment.NewLine + Environment.NewLine +
                (ex == null ? "(未知错误)" : ex.Message);
            if (!string.IsNullOrEmpty(logPath)) message += Environment.NewLine + Environment.NewLine + "详细信息已写入：" + logPath;

            try
            {
                MessageBox.Show(message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception)
            {
                // 忽略：可能连消息框都无法显示
            }
        }

        /// <summary>
        /// 按数据文件路径获取单实例锁。返回 null 表示同一数据文件已有实例在运行。
        /// 互斥体由操作系统在进程结束时自动释放，因此崩溃不会导致永久占用。
        /// </summary>
        private static Mutex TryAcquireInstanceLock(string dataFilePath)
        {
            string name;
            try
            {
                name = @"Local\SimpleTodo-" + StableHash(Path.GetFullPath(dataFilePath).ToLowerInvariant());
            }
            catch (Exception)
            {
                return new Mutex(); // 路径无法规范化时不限制实例数量
            }

            try
            {
                bool createdNew;
                Mutex mutex = new Mutex(true, name, out createdNew);
                if (createdNew) return mutex;

                mutex.Dispose();
                return null;
            }
            catch (Exception)
            {
                // 无法创建命名互斥体（权限受限等）时放行，不阻塞用户使用
                return new Mutex();
            }
        }

        /// <summary>FNV-1a 64 位散列：跨进程稳定，用于生成互斥体名称。</summary>
        private static string StableHash(string text)
        {
            ulong hash = 14695981039346656037UL;
            for (int k = 0; k < text.Length; k++)
            {
                hash ^= text[k];
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null) return false;
            for (int k = 0; k < args.Length; k++)
            {
                if (string.Equals(args[k], flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>读取 "--name value" 形式的参数。</summary>
        private static string GetOption(string[] args, string name)
        {
            if (args == null) return null;
            for (int k = 0; k < args.Length - 1; k++)
            {
                if (string.Equals(args[k], name, StringComparison.OrdinalIgnoreCase)) return args[k + 1];
            }
            return null;
        }
    }
}
