namespace Loupedeck.Zoneplus
{
    using System;
    using System.Reflection;
    using System.Text;


    public class Zoneplus : Plugin
    {
        // Timer & Tracker
        public override Boolean UsesApplicationApiOnly => true;
        public override Boolean HasNoApplication => true;

        public Zoneplus()
        {
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);

            // Initialize events
            this.PluginEvents.AddEvent("sleepy", "Camera sleepy", "Triggered when sleepy");
            this.PluginEvents.AddEvent("awake", "Camera awake", "Triggered when awake");
            this.PluginEvents.AddEvent("timeRunOut", "Time run out", "Triggered when social media timer runs out");
            this.PluginEvents.AddEvent("pomodoroTimerDone", "Pomodoro timer done", "Triggered when pomodoro timer is done");
        }

        private async void WebhookInit()
        {
            // Start webhook server for Notion
            NotionWebhook.Initialize(this); 
            var webhookServer = NotionWebhook.Instance; 
            webhookServer.StartWebhookServer();
            
            List<TaskItem> notionTasks = await webhookServer.retrieveTasks();

            // Now, notionTasks contains the data AND the store has been updated inside retrieveTasks().
            // We iterate over the data that was just fetched.
            
            foreach (var eventId in NotionTaskStore.PreviouslyRegisteredEventIds)
            {
                PluginLog.Info($"Previous event: {eventId}");
            }
            NotionTaskStore.PreviouslyRegisteredEventIds.Clear();

            foreach (var task in notionTasks) 
            {
                string eventId = $"task_{task.Title.Replace(" ", "_")}";

                PluginEvents.AddEvent( 
                    eventId,
                    task.Title,
                    task["Status"]
                );

                PluginEvents.RaiseEvent(eventId);

                NotionTaskStore.PreviouslyRegisteredEventIds.Add(eventId);

                PluginLog.Info($"Registered and raised event: {eventId} - {task.Title}");
            }

            PluginLog.Info("Hackie Plugin loaded with Notion webhook");
        }
        public override void Load()
        {
            PluginLog.Info("Plugin Loading...");
            LogAllEmbeddedResources();
            WebhookInit();
        }

        public override void Unload()
        {
            PluginLog.Info("Plugin Unloaded.");
        }

        private void LogAllEmbeddedResources()
        {
            // debug method for printing all available resources (images, etc.)
            Assembly assembly = this.Assembly;
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

            PluginLog.Info(log.ToString());
        }
    }
}