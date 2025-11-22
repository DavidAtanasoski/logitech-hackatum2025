namespace Loupedeck.ExamplePlugin
{
    using System;
    using System.Collections.Generic;
    using System.Timers; 
    using System.Linq;
    using System.Threading.Tasks;

    public class PomodoroTimer : PluginDynamicCommand
    {
        // --- TIMER STATE ---
        private TimeSpan _duration = TimeSpan.FromMinutes(25);
        private TimeSpan _remainingTime;
        private DateTime? _sessionStartTime = null;
        private bool _isPaused = false;
        private System.Timers.Timer _timer;
        private bool _isTimerFinished = false;

        public PomodoroTimer() : base()
        {
            // 1. Register 5 distinct actions/parameters
            this.AddParameter("Pomo", "", "Pomodoro"); // Clock face (Live update)
            this.AddParameter("ImagePomodoroHolder", "", "Pomodoro"); // Clock face (Live update)
            this.AddParameter("Doro", "", "Pomodoro"); // Clock face (Live update)
            this.AddParameter("Display", "Pomodoro Clock", "Pomodoro"); // Clock face (Live update)
            this.AddParameter("AdjustUp", "+1 Minute", "Pomodoro");
            this.AddParameter("AdjustDown", "-1 Minute", "Pomodoro");
            this.AddParameter("ResetTimer", "Reset Timer", "Pomodoro");
            
            _remainingTime = _duration;
        }

        // --- LIFE CYCLE ---
        protected override bool OnLoad()
        {
            _timer = new Timer(1000); // Check every 1 second
            _timer.Elapsed += OnTimerTick;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();

            // Initial UI refresh after a short delay
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

        // --- TIMER LOGIC ---
        private void OnTimerTick(object sender, ElapsedEventArgs e)
        {
            // A. Handle active counting and finishing
            if (_sessionStartTime.HasValue && !_isPaused)
            {
                TimeSpan elapsed = DateTime.Now - _sessionStartTime.Value;
                _remainingTime = _duration - elapsed;

                if (_remainingTime <= TimeSpan.Zero)
                {
                    // Timer has just finished. Set the finished state.
                    _remainingTime = TimeSpan.Zero;
                    _sessionStartTime = null; 
                    _isPaused = false;
                    
                    // Set flag to true when timer completes
                    _isTimerFinished = true; 
                }
                
                // Refresh the central display button every second
                this.ActionImageChanged("Display");
            }
            
            // B. HAPTIC ALARM LOGIC (Runs every tick, checking the finished state)
            if (_isTimerFinished)
            {
                // Send haptic event every second
                this.Plugin.PluginEvents.RaiseEvent(
                    "pomodoroTimerDone"
                );
            }
        }

        // --- USER ACTIONS ---
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
                    // Prevent going below 1 minute
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
                    // Start the timer if it's currently stopped/not set
                    if (!_sessionStartTime.HasValue)
                    {
                         _sessionStartTime = DateTime.Now;
                         _remainingTime = _duration;
                         _isTimerFinished = false;
                    } else
                    {
                        _isPaused = !_isPaused;
                        // When resuming, adjust _sessionStartTime to continue counting from the *current* _remainingTime
                        if (!_isPaused) 
                        {
                            _sessionStartTime = DateTime.Now - (_duration - _remainingTime);
                        }
                    }
                    break;
            }

            // Update UI for all relevant buttons after action
            this.ActionImageChanged("Display");
        }

        // --- VISUALS ---
        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            switch (actionParameter)
            {
                case "AdjustUp": return "+1 Min";
                case "AdjustDown": return "-1 Min";
                case "ResetTimer": return "Reset";
                case "Display": return !_sessionStartTime.HasValue ? "Start" : (_isPaused ? "Resume" : "Pause"); // This should be handled by GetCommandImage, but included for safety.
                default: return "";
            }
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // Only provide a custom image for the central clock display
            if (actionParameter == "Display")
            {
                // Calculate visual state
                bool isRunning = _sessionStartTime.HasValue && !_isPaused;
                bool isFinished = _remainingTime <= TimeSpan.Zero;
                TimeSpan displayTime = _remainingTime > TimeSpan.Zero ? _remainingTime : TimeSpan.Zero;

                // Format time: mm:ss
                string timeText = $"{displayTime:mm\\:ss}";
                
                // Color Logic
                var textColor = isFinished ? BitmapColor.Red : (isRunning ? BitmapColor.White : new BitmapColor(150, 150, 150));
                var bgColor = isFinished ? new BitmapColor(80, 0, 0) : BitmapColor.Black;

                // --- PULSING LOGIC ---
                // 1. Get a constantly changing integer value (Total Seconds)
                int totalSeconds = (int)displayTime.TotalSeconds;
                
                // 2. Use modulo to create a small repeating cycle (0, 1, 2, 0, 1, 2...)
                // Cycle length 3 seconds (range 0, 1, 2)
                int pulseValue = totalSeconds % 3; 
                
                // 3. Define the final dynamic size (Base 45, fluctuating up by 0, 2, or 4 pixels)
                int dynamicFontSize = 42 + (pulseValue * 2); 
                // ---------------------

                using (var builder = new BitmapBuilder(imageSize))
                {
                    builder.Clear(bgColor);
                    
                    // Draw Time in center
                    // Passing the calculated dynamicFontSize here
                    builder.DrawText(
                        timeText, 
                        0, 0, 
                        imageSize.GetWidth(), 
                        imageSize.GetHeight(), 
                        textColor, 
                        dynamicFontSize, // <-- FIX: Use the calculated integer variable
                        -1, -1
                    );
                    
                    // Draw the action text (unchanged logic)
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
                    var resourcePath = $"Loupedeck.ExamplePlugin.Resources.pomodoro.png";
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

            // Fallback for action buttons (uses default image with text overlay from GetCommandDisplayName)
            return base.GetCommandImage(actionParameter, imageSize);
        }
    }
}