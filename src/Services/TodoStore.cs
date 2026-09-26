using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SimpleTodo.Models;

namespace SimpleTodo.Services
{
    /// <summary>
    /// 任务数据的加载与保存。默认保存到 %APPDATA%\SimpleTodo\tasks.json。
    /// 保存采用「临时文件 + 替换」，避免写入过程中断电/崩溃导致数据文件损坏。
    /// </summary>
    public class TodoStore
    {
        public string FilePath { get; private set; }

        /// <summary>最近一次加载遇到的问题（例如数据文件损坏），无问题时为 null。</summary>
        public string LastWarning { get; private set; }

        public DateTime? LastSavedAt { get; private set; }

        public TodoStore(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException("filePath");
            FilePath = Path.GetFullPath(filePath);
        }

        public static string DefaultDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(appData)) appData = Directory.GetCurrentDirectory();
                return Path.Combine(appData, "SimpleTodo");
            }
        }

        public static string DefaultFilePath
        {
            get { return Path.Combine(DefaultDirectory, "tasks.json"); }
        }

        public bool Exists
        {
            get { return File.Exists(FilePath); }
        }

        /// <summary>读取全部任务；文件不存在或为空时返回空列表。</summary>
        public List<TodoItem> Load()
        {
            LastWarning = null;

            if (!File.Exists(FilePath)) return new List<TodoItem>();

            string text;
            try
            {
                text = File.ReadAllText(FilePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LastWarning = "读取数据文件失败：" + ex.Message;
                return new List<TodoItem>();
            }

            if (text.Trim().Length == 0) return new List<TodoItem>();

            try
            {
                return TodoJson.Deserialize(text);
            }
            catch (Exception ex)
            {
                string backup = BackupCorruptFile();
                LastWarning = "数据文件解析失败：" + ex.Message +
                    (backup != null ? "（原文件已备份为 " + Path.GetFileName(backup) + "）" : string.Empty);
                return new List<TodoItem>();
            }
        }

        /// <summary>保存全部任务，写入前会确保目录存在。</summary>
        public void Save(IList<TodoItem> items)
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = TodoJson.Serialize(items);
            string temp = Path.Combine(
                string.IsNullOrEmpty(directory) ? "." : directory,
                Path.GetFileName(FilePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(false));

                if (File.Exists(FilePath))
                {
                    try
                    {
                        File.Replace(temp, FilePath, null);
                    }
                    catch (IOException)
                    {
                        ReplaceByCopy(temp);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        ReplaceByCopy(temp);
                    }
                }
                else
                {
                    File.Move(temp, FilePath);
                }

                LastSavedAt = DateTime.Now;
            }
            finally
            {
                // 成功时临时文件通常已经被 Move/Replace 消费；失败时尽力清理本次保存产生的临时文件。
                TryDelete(temp);
            }
        }

        private void ReplaceByCopy(string temp)
        {
            File.Copy(temp, FilePath, true);
            TryDelete(temp);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { /* 临时文件残留不影响使用 */ }
            catch (UnauthorizedAccessException) { /* 临时文件残留不影响使用 */ }
        }

        /// <summary>把损坏的数据文件另存为 tasks.corrupt-时间戳.json，返回备份路径。</summary>
        public string BackupCorruptFile()
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                string name = Path.GetFileNameWithoutExtension(FilePath);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string target = Path.Combine(directory ?? string.Empty, name + ".corrupt-" + stamp + ".json");
                File.Copy(FilePath, target, true);
                return target;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
