using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SimpleTodo.Models;

namespace SimpleTodo.Services
{
    /// <summary>
    /// 数据文件的 JSON 读写：格式稳定、可读、可手工编辑、便于用 git 做版本管理。
    /// <code>
    /// {
    ///   "version": 1,
    ///   "items": [
    ///     { "id": "...", "title": "...", "note": "...", "done": false,
    ///       "priority": 1, "due": "2025-01-01T09:00:00", "created": "...", "completed": null }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public static class TodoJson
    {
        /// <summary>本地时间格式，不含时区，便于人工阅读与修改。</summary>
        public const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss";

        public const int CurrentVersion = 1;

        private static readonly string[] AcceptedDateFormats =
        {
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "yyyy/MM/dd"
        };

        #region 序列化

        public static string Serialize(IList<TodoItem> items)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"items\": [");

            if (items != null && items.Count > 0)
            {
                sb.Append('\n');
                for (int k = 0; k < items.Count; k++)
                {
                    WriteItem(sb, items[k], "    ");
                    if (k < items.Count - 1) sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append("  ");
            }

            sb.Append("]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static void WriteItem(StringBuilder sb, TodoItem item, string indent)
        {
            string inner = indent + "  ";
            sb.Append(indent).Append("{\n");

            sb.Append(inner).Append("\"id\": ");
            SimpleJson.WriteString(sb, item.Id.ToString("D"));
            sb.Append(",\n");

            sb.Append(inner).Append("\"title\": ");
            SimpleJson.WriteString(sb, item.Title);
            sb.Append(",\n");

            sb.Append(inner).Append("\"note\": ");
            SimpleJson.WriteString(sb, item.Note);
            sb.Append(",\n");

            sb.Append(inner).Append("\"done\": ").Append(item.IsDone ? "true" : "false").Append(",\n");
            sb.Append(inner).Append("\"priority\": ")
              .Append(((int)item.Priority).ToString(CultureInfo.InvariantCulture)).Append(",\n");

            sb.Append(inner).Append("\"due\": ");
            WriteDateOrNull(sb, item.DueDate);
            sb.Append(",\n");

            sb.Append(inner).Append("\"created\": ");
            WriteDateOrNull(sb, item.CreatedAt);
            sb.Append(",\n");

            sb.Append(inner).Append("\"completed\": ");
            WriteDateOrNull(sb, item.CompletedAt);
            sb.Append('\n');

            sb.Append(indent).Append('}');
        }

        private static void WriteDateOrNull(StringBuilder sb, DateTime? value)
        {
            if (value.HasValue) SimpleJson.WriteString(sb, FormatDate(value.Value));
            else sb.Append("null");
        }

        #endregion

        #region 反序列化

        /// <summary>
        /// 解析数据文件。根节点可以是 {"items": [...]} 对象，也可以直接是数组。
        /// 单个条目损坏时跳过该条目，而不是让整份数据加载失败。
        /// </summary>
        public static List<TodoItem> Deserialize(string json)
        {
            object root;
            string error;
            if (!SimpleJson.TryParse(json, out root, out error))
                throw new FormatException("数据文件不是合法 JSON：" + error);

            List<object> rawItems = null;
            Dictionary<string, object> rootMap = SimpleJson.AsObject(root);
            if (rootMap != null) rawItems = SimpleJson.AsArray(SimpleJson.Get(rootMap, "items"));
            else rawItems = SimpleJson.AsArray(root);

            if (rawItems == null)
                throw new FormatException("数据文件结构不正确：缺少 items 数组");

            List<TodoItem> items = new List<TodoItem>(rawItems.Count);
            HashSet<Guid> seenIds = new HashSet<Guid>();
            for (int k = 0; k < rawItems.Count; k++)
            {
                Dictionary<string, object> map = SimpleJson.AsObject(rawItems[k]);
                if (map == null) continue;

                TodoItem item;
                try
                {
                    item = ReadItem(map);
                }
                catch (Exception)
                {
                    // 单个条目损坏时跳过该条目，不影响其它任务加载。
                    continue;
                }

                if (item.Id == Guid.Empty || seenIds.Contains(item.Id))
                {
                    do { item.Id = Guid.NewGuid(); }
                    while (seenIds.Contains(item.Id));
                }

                seenIds.Add(item.Id);
                items.Add(item);
            }
            return items;
        }

        private static TodoItem ReadItem(Dictionary<string, object> map)
        {
            TodoItem item = new TodoItem();

            string idText = SimpleJson.AsString(SimpleJson.Get(map, "id"), null);
            Guid id;
            if (!string.IsNullOrEmpty(idText) && Guid.TryParse(idText, out id)) item.Id = id;

            item.Title = SafeText(SimpleJson.AsString(SimpleJson.Get(map, "title"), string.Empty));
            item.Note = SafeText(SimpleJson.AsString(SimpleJson.Get(map, "note"), string.Empty));
            item.IsDone = SimpleJson.AsBool(SimpleJson.Get(map, "done"), false);
            item.Priority = ReadPriority(SimpleJson.Get(map, "priority"));

            DateTime value;
            if (TryParseDate(SimpleJson.AsString(SimpleJson.Get(map, "due"), null), out value)) item.DueDate = value;
            if (TryParseDate(SimpleJson.AsString(SimpleJson.Get(map, "created"), null), out value)) item.CreatedAt = value;
            if (TryParseDate(SimpleJson.AsString(SimpleJson.Get(map, "completed"), null), out value)) item.CompletedAt = value;

            // 归一化：未完成的任务不应保留完成时间
            if (!item.IsDone) item.CompletedAt = null;

            return item;
        }

        private static string SafeText(string text)
        {
            return text == null ? string.Empty : text;
        }

        private static Priority ReadPriority(object raw)
        {
            if (raw is double)
            {
                double number = (double)raw;
                return TodoItem.ParsePriority(((int)Math.Round(number)).ToString(CultureInfo.InvariantCulture), Priority.Normal);
            }
            return TodoItem.ParsePriority(SimpleJson.AsString(raw, null), Priority.Normal);
        }

        #endregion

        #region 日期助手

        public static string FormatDate(DateTime value)
        {
            return value.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        public static bool TryParseDate(string text, out DateTime value)
        {
            value = DateTime.MinValue;
            if (string.IsNullOrEmpty(text)) return false;

            if (DateTime.TryParseExact(text, AcceptedDateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out value))
            {
                return true;
            }

            // 宽松兜底：允许带时区偏移等其它写法
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        #endregion
    }
}
