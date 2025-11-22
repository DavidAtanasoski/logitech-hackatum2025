namespace Loupedeck.ExamplePlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Runtime.InteropServices; // For Windows API
    using System.Text;                    // For StringBuilder
    using System.Timers;                  // For the internal Timer

    public class SocialAppUsage
    {
        public TimeSpan DailyTotal { get; set; }
        public TimeSpan DailyLimit { get; set; }

        public SocialAppUsage(TimeSpan defaultLimit)
        {
            this.DailyTotal = TimeSpan.Zero;
            this.DailyLimit = defaultLimit;
        }
    }

    public class SocialMediaTimeTracker : PluginDynamicCommand
    {
        // --- WINDOWS API (P/Invoke) ---
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // --- CONFIGURATION ---
        private readonly string[] _socialSites = {
            "Facebook", "X", "Instagram",
            "TikTok", "YouTube", "Reddit"
        };

        // --- STATE ---
        private Dictionary<string, SocialAppUsage> _appStats;
        private Timer _trackerTimer;
        private string _regexPattern;
        
        // Tracker Variables
        private string _currentActiveApp = null;
        private DateTime? _lastTickTime = null;
        private DateTime _lastResetDate;

        public SocialMediaTimeTracker() : base()
        {
            // 1. Setup Data Structures & Buttons
            _appStats = new Dictionary<string, SocialAppUsage>();
            foreach (var site in _socialSites)
            {
                _appStats[site] = new SocialAppUsage(TimeSpan.FromSeconds(5)); // Default 300s limit
                this.AddParameter(site, site, "Social Time");
            }

            // 2. Setup Regex
            _regexPattern = @"\b(" + string.Join("|", _socialSites.Select(Regex.Escape)) + @")\b";
            _lastResetDate = DateTime.Today;
        }

        // --- INITIALIZATION ---
        protected override bool OnLoad()
        {
            // 3. Start the internal loop when the command loads
            _trackerTimer = new Timer(1000); // Check every 1 second
            _trackerTimer.Elapsed += OnTimerTick;
            _trackerTimer.AutoReset = true;
            _trackerTimer.Enabled = true;
            _trackerTimer.Start();
            Task.Run(async () => 
            {
                await Task.Delay(1000); 
                foreach (var key in _appStats.Keys)
                {
                    this.ActionImageChanged(key);
                }
            });
            return base.OnLoad();
        }

        protected override bool OnUnload()
        {
            _trackerTimer?.Stop();
            _trackerTimer?.Dispose();
            return base.OnUnload();
        }

        // --- MAIN LOOP ---
        private void OnTimerTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                string windowTitle = GetActiveWindowTitle();

                // B. Run the Tracking Logic
                UpdateTracking(windowTitle);

                bool isAnyOverLimit = _appStats.Values.Any(stats => stats.DailyTotal > stats.DailyLimit);

                if (isAnyOverLimit)
                {
                    // send haptic
                    this.Plugin.PluginEvents.RaiseEvent("timeRunOut");
                }
            }
            catch { /* Prevent timer crash */ }
        }

        // --- TRACKING LOGIC ---
        private void UpdateTracking(string windowTitle)
        {
            // 1. Midnight Reset check
            if (DateTime.Today > _lastResetDate) ResetDailyStats();

            // 2. Regex Detection
            string detectedApp = null;
            if (!string.IsNullOrEmpty(windowTitle))
            {
                var match = Regex.Match(windowTitle, _regexPattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string rawValue = match.Groups[1].Value;
                    detectedApp = _socialSites.FirstOrDefault(s => s.Equals(rawValue, StringComparison.OrdinalIgnoreCase));
                }
            }

            DateTime now = DateTime.Now;

            // 3. Handle App Switching
            if (_currentActiveApp != null && _currentActiveApp != detectedApp)
            {
                AccumulateTime(now);
                this.ActionImageChanged(_currentActiveApp); // Force update old app
                
                _currentActiveApp = null;
                _lastTickTime = null;
            }

            // 4. Handle Active App
            if (detectedApp != null)
            {
                if (_currentActiveApp == null)
                {
                    _currentActiveApp = detectedApp;
                    _lastTickTime = now;
                }
                else
                {
                    AccumulateTime(now);
                    this.ActionImageChanged(detectedApp); // Force update current app (Countdown effect)
                }
            }
        }

        private void AccumulateTime(DateTime now)
        {
            if (_currentActiveApp != null && _lastTickTime != null)
            {
                TimeSpan elapsed = now - _lastTickTime.Value;
                if (_appStats.ContainsKey(_currentActiveApp))
                {
                    _appStats[_currentActiveApp].DailyTotal += elapsed;
                }
                _lastTickTime = now;
            }
        }

        private string GetActiveWindowTitle()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return "";
            StringBuilder sb = new StringBuilder(256);
            return GetWindowText(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
        }

        private void ResetDailyStats()
        {
            foreach (var key in _appStats.Keys)
            {
                _appStats[key].DailyTotal = TimeSpan.Zero;
                this.ActionImageChanged(key);
            }
            _lastResetDate = DateTime.Today;
        }

        // --- USER ACTION (Button Press) ---
        protected override void RunCommand(String actionParameter)
        {
            if (_appStats.ContainsKey(actionParameter))
            {
                // Add 1 minute to the limit
                _appStats[actionParameter].DailyLimit += TimeSpan.FromMinutes(1);
                this.ActionImageChanged(actionParameter);
            }
        }

        // --- VISUALS ---
        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => null;

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter) || !_appStats.ContainsKey(actionParameter)) return null;

            // Data
            var stats = _appStats[actionParameter];
            TimeSpan remaining = stats.DailyLimit - stats.DailyTotal;
            
            // Check if time is negative (over budget)
            bool isNegative = remaining < TimeSpan.Zero; 

            // Calculate time text. Math.Abs is used for seconds to prevent display artifacts like "5:-10".
            string timeText = $"{(int)remaining.TotalMinutes}:{Math.Abs(remaining.Seconds):D2}";
            
            // FIX: Add a prefix based on the sign
            string prefix = isNegative ? "+" : ""; 
            
            // Combine the prefix and the time text
            timeText = prefix + timeText; 
            
            // Set color based on the sign
            var textColor = isNegative ? BitmapColor.Red : BitmapColor.White;

            // Resource Path (SVG)
            var resourcePath = $"Loupedeck.ExamplePlugin.Resources.{actionParameter.ToLower()}.png";

            using (var builder = new BitmapBuilder(imageSize))
            {
                try
                {
                    var icon = BitmapImage.FromResource(this.Plugin.Assembly, resourcePath);
                    builder.SetBackgroundImage(icon);
                }
                catch { /* Background stays black if missing */ }

                // Draw Bold Text
                // Note: The y-coordinate has been slightly adjusted to 55 to accommodate the larger font size.
                builder.DrawText(timeText, 0, 5, imageSize.GetWidth(), imageSize.GetHeight(), textColor, 45, -1, -1, "Arial Bold");

                return builder.ToImage();
            }
        }
    }
}