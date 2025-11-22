namespace Loupedeck.ExamplePlugin
{
    using System;
    using System.Net;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Text.Json.Nodes;
    using System.Timers; // Clarity for Timer


    public class ExamplePlugin : Plugin
    {
        // DLL Imports for reading window titles
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // Timer & Tracker
        private System.Timers.Timer _activityCheckTimer;

        public override Boolean UsesApplicationApiOnly => true;
        public override Boolean HasNoApplication => true;

        public ExamplePlugin()
        {
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);

            // Initialize events
            this.PluginEvents.AddEvent("sleepy", "Camera sleepy", "Triggered when sleepy");
            this.PluginEvents.AddEvent("awake", "Camera awake", "Triggered when awake");
            this.PluginEvents.AddEvent("timeRunOut", "Time run out", "Triggered when social media timer runs out");
            this.PluginEvents.AddEvent("pomodoroTimerDone", "Pomodoro timer done", "Triggered when pomodoro timer is done");
            // Initialize the tracker
        }

        private async void WebhookInit()
        {
            // 1. FIX: Initialize the Singleton and get the global instance
            // Note: The prefix argument is optional here, defaults to http://localhost:8080/
            NotionWebhook.Initialize(this); 
            var webhookServer = NotionWebhook.Instance; 
            
            // 2. Start the webhook server (using the instance)
            webhookServer.StartWebhookServer();
            
            // 3. FIX: Await the task retrieval from the instance
            List<TaskItem> notionTasks = await webhookServer.retrieveTasks();

            // Now, notionTasks contains the data AND the store has been updated inside retrieveTasks().
            // We iterate over the data that was just fetched.
            
            // --- Event Cleanup ---
            foreach (var eventId in NotionTaskStore.PreviouslyRegisteredEventIds)
            {
                PluginLog.Info($"Previous event: {eventId}");
            }
            NotionTaskStore.PreviouslyRegisteredEventIds.Clear();

            // --- Register New Events ---
            // Iterate over the list that was just updated/returned
            foreach (var task in notionTasks) 
            {
                // Generate a unique event ID
                string eventId = $"task_{task.Title.Replace(" ", "_")}";

                // Add the event
                // 4. FIX: Use the instance's access to the plugin events
                PluginEvents.AddEvent( 
                    eventId,
                    task.Title,
                    task["Status"]
                );

                // IMPORTANT: Raise the event to make it appear in Actions Ring
                PluginEvents.RaiseEvent(eventId);

                NotionTaskStore.PreviouslyRegisteredEventIds.Add(eventId);

                PluginLog.Info($"Registered and raised event: {eventId} - {task.Title}");
            }

            PluginLog.Info("Hackie Plugin loaded with Notion webhook");
        }
        public override void Load()
        {
            PluginLog.Info("Plugin Loading...");

            // Setup Timer
            _activityCheckTimer = new System.Timers.Timer(1000);
            _activityCheckTimer.Elapsed += OnTimerTick;
            _activityCheckTimer.AutoReset = true;
            _activityCheckTimer.Enabled = true;
            _activityCheckTimer.Start();

            PluginLog.Info("Activity check timer started.");
            LogAllEmbeddedResources();
            WebhookInit();
        }

        public override void Unload()
        {
            _activityCheckTimer?.Stop();
            _activityCheckTimer?.Dispose();
            _activityCheckTimer = null;
            PluginLog.Info("Plugin Unloaded.");
        }

        private void OnTimerTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                // 1. Get the raw window title
                IntPtr hWnd = GetForegroundWindow();
                StringBuilder titleSb = new StringBuilder(256);
                GetWindowText(hWnd, titleSb, titleSb.Capacity);
                string windowTitle = titleSb.ToString();

                // 2. Pass it to the tracker to handle logic and logging
                // PluginLog.Info("Updating tracker");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error during activity check.");
            }
        }

        private void LogAllEmbeddedResources()
        {
            // Get the assembly containing your plugin code and resources
            Assembly assembly = this.Assembly;

            // Retrieve all resource names included in the manifest
            string[] resourceNames = assembly.GetManifestResourceNames();

            if (resourceNames.Length == 0)
            {
                PluginLog.Warning("No embedded resources found in the assembly manifest.");
                return;
            }

            StringBuilder log = new StringBuilder();
            log.AppendLine("--- Embedded Resources Found ---");

            foreach (string name in resourceNames)
            {
                log.AppendLine(name);
            }
            log.AppendLine("--------------------------------");

            // Print the list to the Loupedeck log file
            PluginLog.Info(log.ToString());
        }
    }
}