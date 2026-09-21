using System;
using System.Collections.Generic;
using SimpleTodo.Models;

namespace SimpleTodo.Services
{
    /// <summary>视图筛选条件。</summary>
    public enum TodoFilter
    {
        All,
        Active,
        Done,
        Overdue
    }

    /// <summary>排序字段。</summary>
    public enum TodoSortField
    {
        CreatedAt,
        Title,
        Priority,
        DueDate,
        Done
    }

    /// <summary>
    /// 筛选、搜索与排序逻辑。与界面完全解耦，因此可以在 --selftest 中直接测试。
    /// </summary>
    public static class TodoQuery
    {
        public static List<TodoItem> Select(IEnumerable<TodoItem> source, TodoFilter filter,
            string search, TodoSortField sortField, bool ascending)
        {
            List<TodoItem> result = new List<TodoItem>();

            if (source != null)
            {
                foreach (TodoItem item in source)
                {
                    if (Matches(item, filter, search)) result.Add(item);
                }
            }

            result.Sort(delegate(TodoItem a, TodoItem b)
            {
                return Compare(a, b, sortField, ascending);
            });

            return result;
        }

        public static bool Matches(TodoItem item, TodoFilter filter, string search)
        {
            if (item == null) return false;
            return MatchesFilter(item, filter) && MatchesSearch(item, search);
        }

        public static bool MatchesFilter(TodoItem item, TodoFilter filter)
        {
            if (item == null) return false;
            switch (filter)
            {
                case TodoFilter.Active: return !item.IsDone;
                case TodoFilter.Done: return item.IsDone;
                case TodoFilter.Overdue: return item.IsOverdue;
                default: return true;
            }
        }

        public static bool MatchesSearch(TodoItem item, string search)
        {
            if (item == null) return false;
            if (string.IsNullOrEmpty(search)) return true;

            string keyword = search.Trim();
            if (keyword.Length == 0) return true;

            if (item.Title != null && item.Title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (item.Note != null && item.Note.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static int Compare(TodoItem a, TodoItem b, TodoSortField field, bool ascending)
        {
            int result;

            switch (field)
            {
                case TodoSortField.Title:
                    result = string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
                    break;

                case TodoSortField.Priority:
                    result = a.Priority.CompareTo(b.Priority);
                    break;

                case TodoSortField.Done:
                    result = a.IsDone.CompareTo(b.IsDone);
                    break;

                case TodoSortField.DueDate:
                    bool aMissing = !a.DueDate.HasValue;
                    bool bMissing = !b.DueDate.HasValue;
                    if (aMissing || bMissing)
                    {
                        // 没有截止日期的任务永远排在最后，不受升降序影响
                        if (aMissing && bMissing) return CompareTieBreak(a, b);
                        return aMissing ? 1 : -1;
                    }
                    result = a.DueDate.Value.CompareTo(b.DueDate.Value);
                    break;

                default:
                    result = a.CreatedAt.CompareTo(b.CreatedAt);
                    break;
            }

            if (!ascending) result = -result;
            return result != 0 ? result : CompareTieBreak(a, b);
        }

        /// <summary>同值时按创建时间、再按 Id 排序，保证结果稳定可复现。</summary>
        private static int CompareTieBreak(TodoItem a, TodoItem b)
        {
            int created = a.CreatedAt.CompareTo(b.CreatedAt);
            if (created != 0) return created;
            return string.CompareOrdinal(a.Id.ToString("D"), b.Id.ToString("D"));
        }

        public static int CountOverdue(IEnumerable<TodoItem> items)
        {
            int count = 0;
            if (items == null) return count;
            foreach (TodoItem item in items)
            {
                if (item != null && item.IsOverdue) count++;
            }
            return count;
        }

        public static int CountDone(IEnumerable<TodoItem> items)
        {
            int count = 0;
            if (items == null) return count;
            foreach (TodoItem item in items)
            {
                if (item != null && item.IsDone) count++;
            }
            return count;
        }

        public static string FilterLabel(TodoFilter filter)
        {
            switch (filter)
            {
                case TodoFilter.Active: return "未完成";
                case TodoFilter.Done: return "已完成";
                case TodoFilter.Overdue: return "已逾期";
                default: return "全部";
            }
        }

        public static TodoFilter ParseFilter(string text)
        {
            if (string.IsNullOrEmpty(text)) return TodoFilter.All;
            switch (text.Trim().ToLowerInvariant())
            {
                case "active": return TodoFilter.Active;
                case "done": return TodoFilter.Done;
                case "overdue": return TodoFilter.Overdue;
                default: return TodoFilter.All;
            }
        }

        public static string SortLabel(TodoSortField field)
        {
            switch (field)
            {
                case TodoSortField.Title: return "任务";
                case TodoSortField.Priority: return "优先级";
                case TodoSortField.DueDate: return "截止日期";
                case TodoSortField.Done: return "完成状态";
                default: return "创建时间";
            }
        }
    }
}
