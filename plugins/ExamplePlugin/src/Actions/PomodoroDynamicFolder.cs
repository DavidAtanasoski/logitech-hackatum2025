using Loupedeck;
using ProductivityCoachPlugin.Helpers;
using System;
using System.Collections.Generic;

namespace ProductivityCoachPlugin.Actions
{
    public class PomodoroDynamicFolder : PluginDynamicFolder
    {
        private PomodoroTimer _timer;

        public PomodoroDynamicFolder()
        {
            this.DisplayName = "Pomodoro";
            this.GroupName = "Productivity";
            // Do not grab the timer singleton here; the plugin framework may create
            // DynamicFolder instances before the UI thread context is ready. We'll
            // obtain the instance and set the sync context in Load().
        }

        private void OnTimerTick()
        {
            // Notify the device that the RunningTimerDisplay button's image has changed
            // This forces Loupedeck to re-query GetCommandImage for this button every tick.
            this.CommandImageChanged(this.CreateCommandName("RunningTimerDisplay"));
            // Also refresh the button names/display text so labels (center text) update reliably.
            this.ButtonActionNamesChanged();
        }

        public override bool Load()
        {
            // Obtain the singleton now (Load runs on the plugin/main thread)
            _timer = PomodoroTimer.Instance;

            // Provide the timer with the current SynchronizationContext so it can
            // marshal OnTick back to the UI thread.
            _timer.SetSynchronizationContext(System.Threading.SynchronizationContext.Current);

            // Subscribe a single handler that updates both image and labels.
            _timer.OnTick += OnTimerTick;

            return base.Load();
        }

        public override bool Unload()
        {
            if (_timer != null)
            {
                _timer.OnTick -= OnTimerTick;
            }

            return base.Unload();
        }

        // We disable the automatic navigation area because we are manually placing the Return button
        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _)
        {
            return PluginDynamicFolderNavigation.None;
        }

        // MAPPING THE 3x3 GRID (Indices 0-8)
        public override IEnumerable<string> GetButtonPressActionNames(DeviceType _)
        {
            var actions = new List<string>();

            if (!_timer.IsSet)
            {
                // ROW 1 (Top)
                actions.Add(PluginDynamicFolder.NavigateUpActionName); // Index 0: Return (Top Left)
                actions.Add(this.CreateCommandName("Title"));          // Index 1: Title
                actions.Add(this.CreateCommandName("Blank"));          // Index 2: Blank

                // ROW 2 (Middle)
                actions.Add(this.CreateCommandName("Decrease"));       // Index 3: -
                actions.Add(this.CreateCommandName("SetTimeDisplay")); // Index 4: Time
                actions.Add(this.CreateCommandName("Increase"));       // Index 5: +

                // ROW 3 (Bottom)
                actions.Add(this.CreateCommandName("Blank"));          // Index 6: Blank
                actions.Add(this.CreateCommandName("Blank"));          // Index 7: Blank
                actions.Add(this.CreateCommandName("Start"));          // Index 8: Save/Set
            }
            else
            {
                // ROW 1
                actions.Add(PluginDynamicFolder.NavigateUpActionName); // Index 0: Return
                actions.Add(this.CreateCommandName("Title"));          // Index 1: Title
                actions.Add(this.CreateCommandName("Blank"));          // Index 2: Blank

                // ROW 2
                actions.Add(this.CreateCommandName("Blank"));          // Index 3
                actions.Add(this.CreateCommandName("RunningTimerDisplay")); // Index 4: Countdown
                actions.Add(this.CreateCommandName("Blank"));          // Index 5

                // ROW 3
                actions.Add(this.CreateCommandName("Blank"));          // Index 6
                actions.Add(this.CreateCommandName("PauseResume"));    // Index 7: Pause
                actions.Add(this.CreateCommandName("Complete"));       // Index 8: Complete
            }

            return actions;
        }

        public override void RunCommand(string actionParameter)
        {
            switch (actionParameter)
            {
                case "Decrease":
                    _timer.AdjustTime(-1); // Decrement 1 minute
                    this.ButtonActionNamesChanged(); // Force immediate UI update
                    // Also force the button image to be refreshed so the center text updates
                    this.CommandImageChanged(this.CreateCommandName("SetTimeDisplay"));
                    break;
                case "Increase":
                    _timer.AdjustTime(1);  // Increment 1 minute
                    this.ButtonActionNamesChanged(); // Force immediate UI update
                    // Also force the button image to be refreshed so the center text updates
                    this.CommandImageChanged(this.CreateCommandName("SetTimeDisplay"));
                    break;
                case "Start":
                    _timer.Start();
                    this.ButtonActionNamesChanged(); 
                    break;
                case "PauseResume":
                    if (_timer.IsPaused) _timer.Resume();
                    else _timer.Pause();
                    this.ButtonActionNamesChanged();
                    break;
                case "Complete":
                    _timer.Complete();
                    this.ButtonActionNamesChanged();
                    break;
                case "Blank":
                case "Title":
                case "SetTimeDisplay":
                case "RunningTimerDisplay":
                    this.ButtonActionNamesChanged();
                    // Do nothing, these are display only
                    break;
            }
        }

        public override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            switch (actionParameter)
            {
                case "Title": return "Pomodoro";
                case "Decrease": return "-";
                case "Increase": return "+";
                case "SetTimeDisplay":
                    var setTime = _timer.IsSet ? _timer.RemainingTime : TimeSpan.FromMinutes(_timer.DurationMinutes);
                    return $"{setTime:mm\\:ss}";
                case "Start": return "Start";
                case "RunningTimerDisplay": return $"{_timer.RemainingTime:mm\\:ss}";
                case "PauseResume": return _timer.IsPaused ? "Resume" : "Pause";
                case "Complete": return "Stop";
                
                // FIX FOR WARNING SIGNS: Return empty string, not null
                case "Blank": return ""; 
                
                default: return "";
            }
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // 1. Handle the Timer Display (Drawing the text manually)
            if (actionParameter == "RunningTimerDisplay" && _timer.IsSet)
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);

                    // Format the time string here
                    var timeString = $"{_timer.RemainingTime:mm\\:ss}";

                    // Draw the text onto the bitmap
                    // Parameters: Text, x, y, width, height, color, fontSize...
                    // Using a simple helper to draw centered text is easiest:
                    bitmapBuilder.DrawText(timeString, BitmapColor.White, 20); 

                    return bitmapBuilder.ToImage();
                }
            }

            // 2. Handle the Blank buttons (Transparent)
            if (actionParameter == "Blank")
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    // Return an empty/transparent image so it looks like a gap
                    bitmapBuilder.Clear(BitmapColor.Black); 
                    return bitmapBuilder.ToImage();
                }
            }

            // 3. Default for everything else (Title, Start, etc.)
            return base.GetCommandImage(actionParameter, imageSize);
        }
    }
}