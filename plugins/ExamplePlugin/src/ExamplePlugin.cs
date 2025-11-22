namespace Loupedeck.ExamplePlugin
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Runtime.InteropServices;
    using System.Timers; // Clarity for Timer
    using System.Reflection;


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

            // Initialize the tracker
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