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
        private NotionWebhook _webhookServer;

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

            // Initialize the tracker
        }

        private async void WebhookInit()
        {
            this._webhookServer = new NotionWebhook(this);
            this._webhookServer.StartWebhookServer();


            String notionResult = await this._webhookServer.FetchNotionDataApiAsync("");

            var notionTasks = new List<(string Title, string Status)>();

            if (notionResult != null)
            {
                JsonObject notionResponse = (JsonObject)JsonObject.Parse(notionResult);
                JsonArray responseResults = (JsonArray)notionResponse["results"];

                PluginLog.Info($"resp res: {responseResults}");
                foreach (var item in responseResults)
                {
                    var obj = item.AsObject();
                    var props = obj["properties"]!.AsObject();

                    string title = props["Task"]?["title"]?[0]?["text"]?["content"]?.ToString() ?? "";
                    string status = props["Status"]?["status"]?["name"]?.ToString() ?? "";

                    notionTasks.Add((title, status));
                }


                NotionTaskStore.UpdateTasks(notionTasks);


                // Clear previous events
                foreach (var eventId in NotionTaskStore.PreviouslyRegisteredEventIds)
                {
                    // Note: There's no RemoveEvent in the SDK, so we just track them
                    PluginLog.Info($"Previous event: {eventId}");
                }
                NotionTaskStore.PreviouslyRegisteredEventIds.Clear();

                // Register AND raise new events
                foreach (var task in notionTasks)
                {
                    // Generate a unique event ID
                    string eventId = $"task_{task.Title.Replace(" ", "_")}";

                    // Add the event
                    this._webhookServer._plugin.PluginEvents.AddEvent(
                        eventId,
                        task.Title,
                        task.Status
                    );

                    // IMPORTANT: Raise the event to make it appear in Actions Ring
                    this._webhookServer._plugin.PluginEvents.RaiseEvent(eventId);

                    NotionTaskStore.PreviouslyRegisteredEventIds.Add(eventId);

                    PluginLog.Info($"Registered and raised event: {eventId} - {task.Title}");
                }

                PluginLog.Info("Hackie Plugin loaded with Notion webhook");

            }
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