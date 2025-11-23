namespace Loupedeck.Zoneplus
{
    using System;
    using System.Timers; 
    using System.Threading.Tasks;

    public class PomodoroTimer : PluginDynamicCommand
    {
        private TimeSpan _duration = TimeSpan.FromMinutes(25);
        private TimeSpan _remainingTime;
        private DateTime? _sessionStartTime = null;
        private bool _isPaused = false;
        private System.Timers.Timer _timer;
        private bool _isTimerFinished = false;

        public PomodoroTimer() : base()
        {
            this.AddParameter("Pomo", "", "Pomodoro");
            this.AddParameter("ImagePomodoroHolder", "", "Pomodoro");
            this.AddParameter("Doro", "", "Pomodoro");
            this.AddParameter("Display", "Pomodoro Clock", "Pomodoro");
            this.AddParameter("AdjustUp", "+1 Minute", "Pomodoro");
            this.AddParameter("AdjustDown", "-1 Minute", "Pomodoro");
            this.AddParameter("ResetTimer", "Reset Timer", "Pomodoro");
            
            _remainingTime = _duration;
        }

        // --- LIFE CYCLE ---
        protected override bool OnLoad()
        {
            _timer = new Timer(1000);
            _timer.Elapsed += OnTimerTick;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();

            Task.Run(async () => 
            {
                await Task.Delay(200); 
                this.ActionImageChanged("Display");
            });
            
            return base.OnLoad();
        }

        protected override bool OnUnload()
        {
            _timer?.Stop();
            _timer?.Dispose();
            return base.OnUnload();
        }

        private void OnTimerTick(object sender, ElapsedEventArgs e)
        {
            if (_sessionStartTime.HasValue && !_isPaused)
            {
                TimeSpan elapsed = DateTime.Now - _sessionStartTime.Value;
                _remainingTime = _duration - elapsed;

                if (_remainingTime <= TimeSpan.Zero)
                {
                    _remainingTime = TimeSpan.Zero;
                    _sessionStartTime = null; 
                    _isPaused = false;
                    
                    _isTimerFinished = true; 
                }
                
                this.ActionImageChanged("Display");
            }
            
            if (_isTimerFinished)
            {
                this.Plugin.PluginEvents.RaiseEvent(
                    "pomodoroTimerDone"
                );
            }
        }

        protected override void RunCommand(string actionParameter)
        {
            switch (actionParameter)
            {
                case "AdjustUp":
                    _duration = _duration.Add(TimeSpan.FromMinutes(1));
                    if (!_sessionStartTime.HasValue) _remainingTime = _duration;
                    this.ActionImageChanged();
                    break;
                case "AdjustDown":
                    if (_duration.TotalMinutes > 1) 
                    {
                        _duration = _duration.Subtract(TimeSpan.FromMinutes(1));
                        if (!_sessionStartTime.HasValue) _remainingTime = _duration;
                    }
                    this.ActionImageChanged();
                    break;
                case "ResetTimer":
                    _sessionStartTime = null;
                    _isPaused = false;
                    _remainingTime = _duration;
                    _isTimerFinished = false;
                    break;
                case "Display":
                    if (!_sessionStartTime.HasValue)
                    {
                         _sessionStartTime = DateTime.Now;
                         _remainingTime = _duration;
                         _isTimerFinished = false;
                    } else
                    {
                        _isPaused = !_isPaused;
                        if (!_isPaused) 
                        {
                            _sessionStartTime = DateTime.Now - (_duration - _remainingTime);
                        }
                    }
                    break;
            }

            this.ActionImageChanged("Display");
        }

        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            switch (actionParameter)
            {
                case "AdjustUp": return "+1 Min";
                case "AdjustDown": return "-1 Min";
                case "ResetTimer": return "Reset";
                case "Display": return !_sessionStartTime.HasValue ? "Start" : (_isPaused ? "Resume" : "Pause");
                default: return "";
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == "Display")
            {
                bool isRunning = _sessionStartTime.HasValue && !_isPaused;
                bool isFinished = _remainingTime <= TimeSpan.Zero;
                TimeSpan displayTime = _remainingTime > TimeSpan.Zero ? _remainingTime : TimeSpan.Zero;

                string timeText = $"{displayTime:mm\\:ss}";
                
                var textColor = isFinished ? BitmapColor.Red : (isRunning ? BitmapColor.White : new BitmapColor(150, 150, 150));
                var bgColor = isFinished ? new BitmapColor(80, 0, 0) : BitmapColor.Black;
                int totalSeconds = (int)displayTime.TotalSeconds;
                int pulseValue = totalSeconds % 3; 
                int dynamicFontSize = 42 + (pulseValue * 2); 

                using (var builder = new BitmapBuilder(imageSize))
                {
                    builder.Clear(bgColor);
                    
                    builder.DrawText(
                        timeText, 
                        0, 0, 
                        imageSize.GetWidth(), 
                        imageSize.GetHeight(), 
                        textColor, 
                        dynamicFontSize,
                        -1, -1
                    );
                    
                    string actionHint = this.GetCommandDisplayName(actionParameter, imageSize);
                    builder.DrawText(
                        actionHint,
                        0, imageSize.GetHeight() - 15,
                        imageSize.GetWidth(), 
                        15,
                        BitmapColor.White,
                        10,
                        -1, -1
                    );

                    return builder.ToImage();

                }
            } else if (actionParameter == "ImagePomodoroHolder") {
                using (var builder = new BitmapBuilder(imageSize))
                {
                    var resourcePath = $"Loupedeck.Zoneplus.Resources.pomodoro.png";
                    var icon = BitmapImage.FromResource(this.Plugin.Assembly, resourcePath);
                    builder.SetBackgroundImage(icon);
                    return builder.ToImage();
                }
            } else if(actionParameter == "Pomo" || actionParameter == "Doro")
            {
                using (var builder = new BitmapBuilder(imageSize))
                {                    
                    builder.DrawText(
                        actionParameter.ToUpper(), 
                        0, 0, 
                        imageSize.GetWidth(), 
                        imageSize.GetHeight(), 
                        BitmapColor.Red, 
                        30, 
                        -1, -1
                    );

                    return builder.ToImage();
                }
            }

            return base.GetCommandImage(actionParameter, imageSize);
        }
    }
}