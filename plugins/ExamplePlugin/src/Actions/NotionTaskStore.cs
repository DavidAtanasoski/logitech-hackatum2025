namespace Loupedeck.ExamplePlugin
{
    using System;
    using System.Collections.Generic;

    public static class NotionTaskStore
    {
        private static List<(string Title, string Status)> _tasks = new List<(string, string)>();
        public static List<string> PreviouslyRegisteredEventIds = new List<string>();

        public static void UpdateTasks(List<(string Title, string Status)> tasks)
        {
            _tasks = tasks ?? new List<(string, string)>();
            PluginLog.Info($"Task store updated with {_tasks.Count} tasks");
        }

        public static List<(string Title, string Status)> GetTasks()
        {
            return new List<(string, string)>(_tasks);
        }

        public static int GetTaskCount()
        {
            return _tasks.Count;
        }
    }
}