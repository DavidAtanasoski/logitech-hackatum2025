namespace Loupedeck.ExamplePlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // 1. The Data Carrier
    public class TaskItem
    {
        public string Title { get; set; }
        
        // This Dictionary holds all your arbitrary data (Url, Status, Priority, etc.)
        public Dictionary<string, string> Properties { get; set; }

        public TaskItem(string title)
        {
            this.Title = title;
            this.Properties = new Dictionary<string, string>();
        }

        // Helper: Allows you to do task["Url"] instead of task.Properties["Url"]
        // Returns empty string if key doesn't exist, preventing crashes.
        public string this[string key]
        {
            get 
            {
                return this.Properties.ContainsKey(key) ? this.Properties[key] : string.Empty;
            }
            set 
            {
                this.Properties[key] = value;
            }
        }
    }

    // 2. The Store
    public static class NotionTaskStore
    {
        // Now storing List<TaskItem> instead of Tuples
        private static List<TaskItem> _tasks = new List<TaskItem>();
        
        public static List<string> PreviouslyRegisteredEventIds = new List<string>();

        public static void UpdateTasks(List<TaskItem> tasks)
        {
            _tasks = tasks ?? new List<TaskItem>();
            PluginLog.Info($"[Store] Updated. Count: {_tasks.Count}");
        }

        public static List<TaskItem> GetTasks()
        {
            // Return a shallow copy to prevent modification issues during enumeration
            return new List<TaskItem>(_tasks);
        }

        public static int GetTaskCount()
        {
            return _tasks.Count;
        }
    }
}