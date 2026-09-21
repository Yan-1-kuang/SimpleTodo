using System;

namespace SimpleTodo.Models
{
    /// <summary>任务优先级。</summary>
    public enum Priority
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    /// <summary>
    /// 一条待办任务。所有属性均为简单类型，便于直接序列化到 JSON。
    /// </summary>
    public class TodoItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Note { get; set; }
        public bool IsDone { get; set; }
        public Priority Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public TodoItem()
        {
            Id = Guid.NewGuid();
            Title = string.Empty;
            Note = string.Empty;
            Priority = Priority.Normal;
            CreatedAt = DateTime.Now;
        }

        /// <summary>未完成、且截止日期早于今天。</summary>
        public bool IsOverdue
        {
            get { return !IsDone && DueDate.HasValue && DueDate.Value.Date < DateTime.Today; }
        }

        /// <summary>截止日期就是今天且尚未完成。</summary>
        public bool IsDueToday
        {
            get { return !IsDone && DueDate.HasValue && DueDate.Value.Date == DateTime.Today; }
        }

        /// <summary>深拷贝，用于“编辑后取消”时回滚。</summary>
        public TodoItem Clone()
        {
            TodoItem copy = new TodoItem();
            copy.Id = Id;
            copy.Title = Title;
            copy.Note = Note;
            copy.IsDone = IsDone;
            copy.Priority = Priority;
            copy.DueDate = DueDate;
            copy.CreatedAt = CreatedAt;
            copy.CompletedAt = CompletedAt;
            return copy;
        }

        public static string PriorityLabel(Priority priority)
        {
            switch (priority)
            {
                case Priority.High: return "高";
                case Priority.Low: return "低";
                default: return "中";
            }
        }

        public static Priority ParsePriority(string text, Priority fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string value = text.Trim();
            if (value == "高" || string.Equals(value, "high", StringComparison.OrdinalIgnoreCase)) return Priority.High;
            if (value == "低" || string.Equals(value, "low", StringComparison.OrdinalIgnoreCase)) return Priority.Low;
            if (value == "中" || string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase)) return Priority.Normal;

            int number;
            if (int.TryParse(value, out number))
            {
                if (number >= 2) return Priority.High;
                if (number <= 0) return Priority.Low;
                return Priority.Normal;
            }
            return fallback;
        }
    }
}
