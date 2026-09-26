using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SimpleTodo.Models;
using SimpleTodo.Services;

namespace SimpleTodo
{
    /// <summary>
    /// 内置自检：不打开界面即可验证 JSON 读写、数据持久化、筛选/搜索/排序等核心逻辑。
    /// 用法：SimpleTodo.exe --selftest    （全部通过时退出码为 0）
    /// </summary>
    public static class SelfTest
    {
        private class Case
        {
            public string Name;
            public Action Body;

            public Case(string name, Action body)
            {
                Name = name;
                Body = body;
            }
        }

        public static int Run()
        {
            try { Console.OutputEncoding = Encoding.UTF8; }
            catch (Exception) { /* 无控制台时忽略 */ }

            List<Case> cases = new List<Case>();
            cases.Add(new Case("源码编码：中文常量未被破坏", TestSourceEncoding));
            cases.Add(new Case("JSON：字符串转义往返（引号/反斜杠/换行/制表/中文/Emoji/控制符）", TestJsonStringRoundTrip));
            cases.Add(new Case("JSON：合法输入解析（嵌套/尾随逗号/转义/数字）", TestJsonParse));
            cases.Add(new Case("JSON：非法输入被拒绝", TestJsonRejectsInvalid));
            cases.Add(new Case("数据文件：完整字段往返一致", TestTodoJsonRoundTrip));
            cases.Add(new Case("数据文件：裸数组与缺省字段容错", TestTodoJsonBareArrayAndDefaults));
            cases.Add(new Case("数据文件：忽略未知字段", TestTodoJsonIgnoresUnknownKeys));
            cases.Add(new Case("数据文件：未完成任务不保留完成时间", TestTodoJsonNormalizesCompleted));
            cases.Add(new Case("存储：保存后重新读取一致", TestStoreRoundTrip));
            cases.Add(new Case("存储：保存后不残留临时文件", TestStoreNoTempLeftover));
            cases.Add(new Case("存储：文件不存在时返回空列表", TestStoreMissingFile));
            cases.Add(new Case("存储：损坏文件自动备份且不丢数据文件", TestStoreCorruptBackup));
            cases.Add(new Case("筛选：全部/未完成/已完成/已逾期", TestFilters));
            cases.Add(new Case("搜索：中文、忽略大小写、备注命中、空关键词", TestSearch));
            cases.Add(new Case("排序：优先级、截止日期（空值排最后）、任务名", TestSorting));
            cases.Add(new Case("优先级：中英文与数字解析", TestPriorityParsing));

            Console.WriteLine("SimpleTodo 自检 —— 共 " + cases.Count.ToString(CultureInfo.InvariantCulture) + " 项");
            Console.WriteLine();

            int passed = 0;
            List<string> failures = new List<string>();

            for (int k = 0; k < cases.Count; k++)
            {
                Case item = cases[k];
                try
                {
                    item.Body();
                    passed++;
                    Console.WriteLine("  [通过] " + item.Name);
                }
                catch (Exception ex)
                {
                    failures.Add(item.Name + " -> " + ex.Message);
                    Console.WriteLine("  [失败] " + item.Name);
                    Console.WriteLine("         " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine("结果：" + passed.ToString(CultureInfo.InvariantCulture) + " 通过 / " +
                failures.Count.ToString(CultureInfo.InvariantCulture) + " 失败");

            if (failures.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("失败明细：");
                for (int k = 0; k < failures.Count; k++)
                    Console.WriteLine("  - " + failures[k]);
                return 1;
            }

            Console.WriteLine("全部通过。");
            return 0;
        }

        #region 测试用例

        private static void TestSourceEncoding()
        {
            // 用 \u 转义作为基准，可以检测编译器是否按 UTF-8 正确读取了源码中的中文
            AssertEqual("\u4efb\u52a1", "任务", "中文常量应为“任务”");
            AssertEqual("\u9ad8", TodoItem.PriorityLabel(Priority.High), "高优先级标签");
            AssertEqual("\u4e2d", TodoItem.PriorityLabel(Priority.Normal), "中优先级标签");
            AssertEqual("\u4f4e", TodoItem.PriorityLabel(Priority.Low), "低优先级标签");
            AssertEqual("\u672a\u5b8c\u6210", TodoQuery.FilterLabel(TodoFilter.Active), "筛选标签");
            AssertEqual("\u5df2\u903e\u671f", TodoQuery.FilterLabel(TodoFilter.Overdue), "筛选标签");
        }

        private static void TestJsonStringRoundTrip()
        {
            string[] samples =
            {
                "普通文本",
                "含\"双引号\"与\\反斜杠",
                "第一行\n第二行\r\n第三行\t制表",
                "中文与 Emoji \uD83D\uDE00 混合",
                "控制字符\u0001\u001f结尾",
                string.Empty
            };

            for (int k = 0; k < samples.Length; k++)
            {
                StringBuilder sb = new StringBuilder();
                SimpleJson.WriteString(sb, samples[k]);

                object parsed = SimpleJson.Parse(sb.ToString());
                AssertEqual(samples[k], SimpleJson.AsString(parsed, null), "第 " + k + " 个样本往返");
            }

            // 传入 null 时应写出合法的空字符串字面量，而不是非法的裸 null
            StringBuilder nullBuffer = new StringBuilder();
            SimpleJson.WriteString(nullBuffer, null);
            AssertEqual("\"\"", nullBuffer.ToString(), "null 写成合法的空字符串字面量");
        }

        private static void TestJsonParse()
        {
            object root = SimpleJson.Parse(
                "{\n" +
                "  \"name\": \"\\u4e2d\\u6587\",\n" +
                "  \"count\": 42,\n" +
                "  \"ratio\": -1.5e2,\n" +
                "  \"flag\": true,\n" +
                "  \"off\": false,\n" +
                "  \"nothing\": null,\n" +
                "  \"list\": [1, 2, 3,],\n" +
                "  \"nested\": { \"a\": { \"b\": \"c\" }, },\n" +
                "}");

            Dictionary<string, object> map = SimpleJson.AsObject(root);
            AssertTrue(map != null, "根节点应为对象");
            AssertEqual("中文", SimpleJson.AsString(SimpleJson.Get(map, "name"), null), "\\u 转义解码");
            AssertEqual(42, SimpleJson.AsInt(SimpleJson.Get(map, "count"), -1), "整数解析");
            AssertTrue(Math.Abs(SimpleJson.AsDouble(SimpleJson.Get(map, "ratio"), 0) - (-150.0)) < 0.0001, "科学计数法解析");
            AssertEqual(true, SimpleJson.AsBool(SimpleJson.Get(map, "flag"), false), "true 解析");
            AssertEqual(false, SimpleJson.AsBool(SimpleJson.Get(map, "off"), true), "false 解析");
            AssertTrue(SimpleJson.Get(map, "nothing") == null, "null 解析");

            List<object> list = SimpleJson.AsArray(SimpleJson.Get(map, "list"));
            AssertTrue(list != null, "数组解析");
            AssertEqual(3, list.Count, "数组长度（容忍尾随逗号）");

            Dictionary<string, object> nested = SimpleJson.AsObject(SimpleJson.Get(map, "nested"));
            AssertTrue(nested != null, "嵌套对象解析");
            AssertEqual("c",
                SimpleJson.AsString(SimpleJson.Get(SimpleJson.AsObject(SimpleJson.Get(nested, "a")), "b"), null),
                "嵌套取值");

            AssertTrue(SimpleJson.AsArray(SimpleJson.Parse("[]")) != null, "空数组");
            AssertTrue(SimpleJson.AsObject(SimpleJson.Parse("{}")) != null, "空对象");
        }

        private static void TestJsonRejectsInvalid()
        {
            string[] bad =
            {
                "{",
                "{\"a\":}",
                "[1,2",
                "tru",
                "{\"a\":1}x",
                "{\"a\" 1}",
                "\"未闭合",
                "\"包含\n未转义换行\"",
                "01",
                "+1",
                "1.",
                "1e"
            };

            for (int k = 0; k < bad.Length; k++)
            {
                bool threw = false;
                try { SimpleJson.Parse(bad[k]); }
                catch (FormatException) { threw = true; }
                AssertTrue(threw, "非法输入应被拒绝：" + bad[k]);
            }

            object value;
            string error;
            AssertTrue(!SimpleJson.TryParse("{", out value, out error), "TryParse 应返回 false");
            AssertTrue(!string.IsNullOrEmpty(error), "TryParse 应给出错误信息");
        }

        private static void TestTodoJsonRoundTrip()
        {
            List<TodoItem> items = new List<TodoItem>();

            TodoItem first = new TodoItem();
            first.Title = "写 README，包含 \"安装\" 与 \\ 说明";
            first.Note = "多行备注\n第二行";
            first.IsDone = false;
            first.Priority = Priority.High;
            first.DueDate = new DateTime(2030, 5, 20, 9, 30, 0);
            first.CreatedAt = new DateTime(2025, 1, 2, 3, 4, 5);
            items.Add(first);

            TodoItem second = new TodoItem();
            second.Title = "已完成的任务";
            second.IsDone = true;
            second.Priority = Priority.Low;
            second.DueDate = null;
            second.CreatedAt = new DateTime(2025, 2, 3, 4, 5, 6);
            second.CompletedAt = new DateTime(2025, 2, 4, 5, 6, 7);
            items.Add(second);

            string json = TodoJson.Serialize(items);
            List<TodoItem> loaded = TodoJson.Deserialize(json);

            AssertEqual(2, loaded.Count, "条目数量");

            TodoItem a = loaded[0];
            AssertEqual(first.Id, a.Id, "Id 一致");
            AssertEqual(first.Title, a.Title, "标题一致（含转义字符）");
            AssertEqual(first.Note, a.Note, "备注一致（含换行）");
            AssertEqual(false, a.IsDone, "完成状态一致");
            AssertEqual(Priority.High, a.Priority, "优先级一致");
            AssertEqual(TodoJson.FormatDate(first.DueDate.Value), TodoJson.FormatDate(a.DueDate.Value), "截止日期一致");
            AssertEqual(TodoJson.FormatDate(first.CreatedAt), TodoJson.FormatDate(a.CreatedAt), "创建时间一致");
            AssertTrue(!a.CompletedAt.HasValue, "未完成时完成时间为空");

            TodoItem b = loaded[1];
            AssertEqual(true, b.IsDone, "完成状态一致");
            AssertEqual(Priority.Low, b.Priority, "优先级一致");
            AssertTrue(!b.DueDate.HasValue, "空截止日期保持为空");
            AssertEqual(TodoJson.FormatDate(second.CompletedAt.Value), TodoJson.FormatDate(b.CompletedAt.Value), "完成时间一致");
        }

        private static void TestTodoJsonBareArrayAndDefaults()
        {
            List<TodoItem> loaded = TodoJson.Deserialize("[{\"title\":\"只有标题\"}]");
            AssertEqual(1, loaded.Count, "裸数组解析");
            AssertEqual("只有标题", loaded[0].Title, "标题");
            AssertEqual(Priority.Normal, loaded[0].Priority, "默认优先级为「中」");
            AssertEqual(false, loaded[0].IsDone, "默认未完成");
            AssertTrue(!loaded[0].DueDate.HasValue, "默认无截止日期");
            AssertTrue(loaded[0].Id != Guid.Empty, "自动生成 Id");
            AssertTrue(loaded[0].Note != null, "备注默认为空字符串而非 null");

            List<TodoItem> fromObject = TodoJson.Deserialize("{\"version\":1,\"items\":[{\"title\":\"A\"},{\"title\":\"B\"}]}");
            AssertEqual(2, fromObject.Count, "对象形式解析");

            // 数组中的非法条目应被跳过，而不是让整份数据失败
            List<TodoItem> tolerant = TodoJson.Deserialize("[{\"title\":\"有效\"}, 123, null]");
            AssertEqual(1, tolerant.Count, "跳过非法条目");

            string duplicateId = Guid.NewGuid().ToString("D");
            List<TodoItem> unique = TodoJson.Deserialize(
                "[{\"id\":\"" + duplicateId + "\",\"title\":\"A\"}," +
                "{\"id\":\"" + duplicateId + "\",\"title\":\"B\"}]");
            AssertEqual(2, unique.Count, "重复 Id 的条目不应被丢弃");
            AssertTrue(unique[0].Id != unique[1].Id, "重复 Id 应自动重建，保证任务标识唯一");
        }

        private static void TestTodoJsonIgnoresUnknownKeys()
        {
            List<TodoItem> loaded = TodoJson.Deserialize(
                "{\"version\":99,\"extra\":\"x\",\"items\":[{\"title\":\"T\",\"unknown\":{\"deep\":[1,2]},\"priority\":\"高\"}]}");

            AssertEqual(1, loaded.Count, "条目数量");
            AssertEqual("T", loaded[0].Title, "标题");
            AssertEqual(Priority.High, loaded[0].Priority, "字符串形式的优先级");
        }

        private static void TestTodoJsonNormalizesCompleted()
        {
            List<TodoItem> loaded = TodoJson.Deserialize(
                "[{\"title\":\"未完成\",\"done\":false,\"completed\":\"2025-01-01T00:00:00\"}]");

            AssertTrue(!loaded[0].CompletedAt.HasValue, "未完成的任务应清空完成时间");
        }

        private static void TestStoreRoundTrip()
        {
            string directory = CreateTempDirectory();
            try
            {
                string path = Path.Combine(directory, "tasks.json");
                TodoStore store = new TodoStore(path);

                List<TodoItem> items = new List<TodoItem>();
                TodoItem item = new TodoItem();
                item.Title = "买牛奶 🥛";
                item.Note = "低脂";
                item.Priority = Priority.High;
                item.DueDate = new DateTime(2031, 3, 4, 0, 0, 0);
                items.Add(item);

                store.Save(items);
                AssertTrue(File.Exists(path), "数据文件已生成");

                string text = File.ReadAllText(path, Encoding.UTF8);
                AssertTrue(text.IndexOf("买牛奶", StringComparison.Ordinal) >= 0, "文件为可读 UTF-8 文本");

                TodoStore reopened = new TodoStore(path);
                List<TodoItem> loaded = reopened.Load();
                AssertEqual(1, loaded.Count, "读回条目数量");
                AssertEqual("买牛奶 🥛", loaded[0].Title, "标题往返");
                AssertEqual("低脂", loaded[0].Note, "备注往返");
                AssertEqual(Priority.High, loaded[0].Priority, "优先级往返");
                AssertTrue(reopened.LastWarning == null, "正常读取不应产生警告");
            }
            finally
            {
                SafeDeleteDirectory(directory);
            }
        }

        private static void TestStoreNoTempLeftover()
        {
            string directory = CreateTempDirectory();
            try
            {
                string path = Path.Combine(directory, "tasks.json");
                TodoStore store = new TodoStore(path);

                List<TodoItem> items = new List<TodoItem>();
                TodoItem item = new TodoItem();
                item.Title = "第一次";
                items.Add(item);
                store.Save(items);

                item.Title = "第二次";
                store.Save(items); // 覆盖保存

                AssertTrue(!File.Exists(path + ".tmp"), "不应残留 .tmp 临时文件");
                AssertEqual("第二次", new TodoStore(path).Load()[0].Title, "覆盖保存生效");
            }
            finally
            {
                SafeDeleteDirectory(directory);
            }
        }

        private static void TestStoreMissingFile()
        {
            string directory = CreateTempDirectory();
            try
            {
                TodoStore store = new TodoStore(Path.Combine(directory, "not-exists.json"));
                List<TodoItem> loaded = store.Load();
                AssertEqual(0, loaded.Count, "空列表");
                AssertTrue(store.LastWarning == null, "文件不存在不算错误");
            }
            finally
            {
                SafeDeleteDirectory(directory);
            }
        }

        private static void TestStoreCorruptBackup()
        {
            string directory = CreateTempDirectory();
            try
            {
                string path = Path.Combine(directory, "tasks.json");
                File.WriteAllText(path, "{ 这不是 JSON ", new UTF8Encoding(false));

                TodoStore store = new TodoStore(path);
                List<TodoItem> loaded = store.Load();

                AssertEqual(0, loaded.Count, "损坏时返回空列表");
                AssertTrue(!string.IsNullOrEmpty(store.LastWarning), "应给出警告信息");
                AssertTrue(File.Exists(path), "原数据文件保留（不被删除）");

                string[] backups = Directory.GetFiles(directory, "tasks.corrupt-*.json");
                AssertEqual(1, backups.Length, "应生成一个备份文件");
            }
            finally
            {
                SafeDeleteDirectory(directory);
            }
        }

        private static void TestFilters()
        {
            List<TodoItem> items = BuildSample();

            AssertEqual(5, TodoQuery.Select(items, TodoFilter.All, null, TodoSortField.CreatedAt, true).Count, "全部");
            AssertEqual(3, TodoQuery.Select(items, TodoFilter.Active, null, TodoSortField.CreatedAt, true).Count, "未完成");
            AssertEqual(2, TodoQuery.Select(items, TodoFilter.Done, null, TodoSortField.CreatedAt, true).Count, "已完成");
            AssertEqual(2, TodoQuery.Select(items, TodoFilter.Overdue, null, TodoSortField.CreatedAt, true).Count, "已逾期");
            AssertEqual(2, TodoQuery.CountOverdue(items), "逾期计数");
            AssertEqual(2, TodoQuery.CountDone(items), "完成计数");
        }

        private static void TestSearch()
        {
            List<TodoItem> items = BuildSample();

            AssertEqual(1, TodoQuery.Select(items, TodoFilter.All, "报告", TodoSortField.CreatedAt, true).Count, "中文关键词");
            AssertEqual(1, TodoQuery.Select(items, TodoFilter.All, "REPORT", TodoSortField.CreatedAt, true).Count, "忽略大小写");
            AssertEqual(1, TodoQuery.Select(items, TodoFilter.All, "备注关键词", TodoSortField.CreatedAt, true).Count, "命中备注");
            AssertEqual(5, TodoQuery.Select(items, TodoFilter.All, "   ", TodoSortField.CreatedAt, true).Count, "空白关键词视为不过滤");
            AssertEqual(0, TodoQuery.Select(items, TodoFilter.All, "不存在的词", TodoSortField.CreatedAt, true).Count, "无匹配");
            AssertEqual(1, TodoQuery.Select(items, TodoFilter.Done, "报告", TodoSortField.CreatedAt, true).Count, "筛选与搜索叠加");
        }

        private static void TestSorting()
        {
            List<TodoItem> items = BuildSample();

            List<TodoItem> byPriority = TodoQuery.Select(items, TodoFilter.All, null, TodoSortField.Priority, false);
            AssertEqual(Priority.High, byPriority[0].Priority, "优先级降序首位为「高」");
            AssertEqual(Priority.Low, byPriority[byPriority.Count - 1].Priority, "优先级降序末位为「低」");

            List<TodoItem> byDue = TodoQuery.Select(items, TodoFilter.All, null, TodoSortField.DueDate, true);
            AssertTrue(!byDue[byDue.Count - 1].DueDate.HasValue, "没有截止日期的任务排在最后");
            AssertTrue(byDue[0].DueDate.HasValue, "有截止日期的任务排在前面");
            for (int k = 1; k < byDue.Count; k++)
            {
                if (byDue[k - 1].DueDate.HasValue && byDue[k].DueDate.HasValue)
                {
                    AssertTrue(byDue[k - 1].DueDate.Value <= byDue[k].DueDate.Value, "截止日期升序");
                }
            }

            List<TodoItem> byTitle = TodoQuery.Select(items, TodoFilter.All, null, TodoSortField.Title, true);
            for (int k = 1; k < byTitle.Count; k++)
            {
                AssertTrue(string.Compare(byTitle[k - 1].Title, byTitle[k].Title,
                    StringComparison.CurrentCultureIgnoreCase) <= 0, "任务名升序");
            }
        }

        private static void TestPriorityParsing()
        {
            AssertEqual(Priority.High, TodoItem.ParsePriority("高", Priority.Normal), "中文「高」");
            AssertEqual(Priority.Low, TodoItem.ParsePriority("低", Priority.Normal), "中文「低」");
            AssertEqual(Priority.Normal, TodoItem.ParsePriority("中", Priority.High), "中文「中」");
            AssertEqual(Priority.High, TodoItem.ParsePriority("HIGH", Priority.Normal), "英文 high");
            AssertEqual(Priority.Low, TodoItem.ParsePriority("low", Priority.Normal), "英文 low");
            AssertEqual(Priority.High, TodoItem.ParsePriority("2", Priority.Normal), "数字 2");
            AssertEqual(Priority.Low, TodoItem.ParsePriority("0", Priority.Normal), "数字 0");
            AssertEqual(Priority.High, TodoItem.ParsePriority("乱码", Priority.High), "无法识别时使用默认值");
        }

        #endregion

        #region 辅助

        /// <summary>构造一份固定的样本数据，供筛选/搜索/排序测试使用。</summary>
        private static List<TodoItem> BuildSample()
        {
            DateTime today = DateTime.Today;

            List<TodoItem> items = new List<TodoItem>();

            TodoItem a = new TodoItem();
            a.Title = "季度 Report 汇总";
            a.Note = "包含备注关键词";
            a.Priority = Priority.High;
            a.DueDate = today.AddDays(-2);   // 逾期
            a.CreatedAt = new DateTime(2025, 1, 1, 8, 0, 0);
            items.Add(a);

            TodoItem b = new TodoItem();
            b.Title = "预约牙医";
            b.Priority = Priority.Normal;
            b.DueDate = today.AddDays(-1);   // 逾期
            b.CreatedAt = new DateTime(2025, 1, 2, 8, 0, 0);
            items.Add(b);

            TodoItem c = new TodoItem();
            c.Title = "整理书架";
            c.Priority = Priority.Low;
            c.DueDate = today.AddDays(5);    // 未逾期
            c.CreatedAt = new DateTime(2025, 1, 3, 8, 0, 0);
            items.Add(c);

            TodoItem d = new TodoItem();
            d.Title = "归档旧文件";
            d.Priority = Priority.Normal;
            d.DueDate = null;                // 无截止日期
            d.IsDone = true;
            d.CompletedAt = new DateTime(2025, 1, 4, 9, 0, 0);
            d.CreatedAt = new DateTime(2025, 1, 4, 8, 0, 0);
            items.Add(d);

            // 样本构成：a/b 逾期未完成、c 未逾期未完成、d 已完成、e 已完成且标题含「报告」
            TodoItem e = a.Clone();
            e.Id = Guid.NewGuid();
            e.Title = "已完成的报告 Review";
            e.Note = string.Empty;
            e.IsDone = true;
            e.CompletedAt = new DateTime(2025, 1, 5, 9, 0, 0);
            e.CreatedAt = new DateTime(2025, 1, 5, 8, 0, 0);
            e.DueDate = null;
            items.Add(e);

            return items;
        }

        private static string CreateTempDirectory()
        {
            string name = "SimpleTodoSelfTest-" + Guid.NewGuid().ToString("N");

            try
            {
                string candidate = Path.Combine(Path.GetTempPath(), name);
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch (Exception)
            {
                string fallback = Path.Combine(Directory.GetCurrentDirectory(), "." + name);
                Directory.CreateDirectory(fallback);
                return fallback;
            }
        }

        private static void SafeDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch (Exception) { /* 清理失败不影响测试结论 */ }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception(message + "（期望：" + Format(expected) + "，实际：" + Format(actual) + "）");
            }
        }

        private static string Format(object value)
        {
            if (value == null) return "null";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
