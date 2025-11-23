namespace Loupedeck.Zoneplus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Timers;

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
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private readonly string[] _socialSites = {
            "Facebook", "X", "Instagram",
            "TikTok", "YouTube", "Reddit"
        };

        private Dictionary<string, SocialAppUsage> _appStats;
        private Timer _trackerTimer;
        private string _regexPattern;
        
        // Tracker Variables
        private string _currentActiveApp = null;
        private DateTime? _lastTickTime = null;
        private DateTime _lastResetDate;

        public SocialMediaTimeTracker() : base()
        {
            _appStats = new Dictionary<string, SocialAppUsage>();
            foreach (var site in _socialSites)
            {
                _appStats[site] = new SocialAppUsage(TimeSpan.FromSeconds(5));
                this.AddParameter(site, site, "Social Time");
            }

            _regexPattern = @"\b(" + string.Join("|", _socialSites.Select(Regex.Escape)) + @")\b";
            _lastResetDate = DateTime.Today;
        }

        protected override bool OnLoad()
        {
            _trackerTimer = new Timer(1000);
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

        private void OnTimerTick(object sender, ElapsedEventArgs e)
        {
            try
            {
                string windowTitle = GetActiveWindowTitle();

                UpdateTracking(windowTitle);

                bool isAnyOverLimit = _appStats.Values.Any(stats => stats.DailyTotal > stats.DailyLimit);

                if (isAnyOverLimit)
                {
                    this.Plugin.PluginEvents.RaiseEvent("timeRunOut");
                }
            }
            catch { }
        }

        private void UpdateTracking(string windowTitle)
        {
            if (DateTime.Today > _lastResetDate) ResetDailyStats();

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

            if (_currentActiveApp != null && _currentActiveApp != detectedApp)
            {
                AccumulateTime(now);
                this.ActionImageChanged(_currentActiveApp);
                
                _currentActiveApp = null;
                _lastTickTime = null;
            }

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
                    this.ActionImageChanged(detectedApp);
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

        protected override void RunCommand(String actionParameter)
        {
            if (_appStats.ContainsKey(actionParameter))
            {
                _appStats[actionParameter].DailyLimit += TimeSpan.FromMinutes(1);
                this.ActionImageChanged(actionParameter);
            }
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => null;

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            if (string.IsNullOrEmpty(actionParameter) || !_appStats.ContainsKey(actionParameter)) return null;

            var stats = _appStats[actionParameter];
            TimeSpan remaining = stats.DailyLimit - stats.DailyTotal;
            bool isNegative = remaining < TimeSpan.Zero; 
            string timeText = $"{(int)remaining.TotalMinutes}:{Math.Abs(remaining.Seconds):D2}";
            string prefix = isNegative ? "+" : ""; 
            
            timeText = prefix + timeText; 
            
            var textColor = isNegative ? BitmapColor.Red : BitmapColor.White;

            var resourcePath = $"Loupedeck.Zoneplus.Resources.{actionParameter.ToLower()}.png";

            using (var builder = new BitmapBuilder(imageSize))
            {
                try
                {
                    var icon = BitmapImage.FromResource(this.Plugin.Assembly, resourcePath);
                    builder.SetBackgroundImage(icon);
                }
                catch { }

                builder.DrawText(timeText, 0, 5, imageSize.GetWidth(), imageSize.GetHeight(), textColor, 45, -1, -1);

                return builder.ToImage();
            }
        }
    }
}