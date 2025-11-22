namespace Loupedeck.HackieNotiePlugin
{
    using System;
    using System.Linq;

    using Loupedeck.ExamplePlugin;

    public class NotionTasksAdjustment : PluginDynamicAdjustment
    {
        private Int32 _currentTaskIndex = 0;

        public NotionTasksAdjustment()
            : base(displayName: "Notion Tasks", description: "Scroll through Notion tasks", groupName: "Notion", hasReset: true)
        {
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            var tasks = NotionTaskStore.GetTasks();

            if (!tasks.Any())
                return;

            // Navigate through tasks with dial rotation
            this._currentTaskIndex += diff;

            // Wrap around
            if (this._currentTaskIndex < 0)
                this._currentTaskIndex = tasks.Count - 1;
            if (this._currentTaskIndex >= tasks.Count)
                this._currentTaskIndex = 0;

            PluginLog.Info($"Navigated to task {this._currentTaskIndex}: {tasks[this._currentTaskIndex].Title}");

            this.AdjustmentValueChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            // When dial is pressed, log current task or trigger action
            var tasks = NotionTaskStore.GetTasks();

            if (tasks.Any() && this._currentTaskIndex < tasks.Count)
            {
                var currentTask = tasks[this._currentTaskIndex];
                PluginLog.Info($"Selected task: {currentTask.Title} - {currentTask.Status}");

                // Trigger haptic feedback
                this.Plugin.PluginEvents.RaiseEvent("buttonPress");
            }

            this.AdjustmentValueChanged();
        }

        protected override String GetAdjustmentValue(String actionParameter)
        {
            var tasks = NotionTaskStore.GetTasks();

            if (!tasks.Any())
                return "0/0";

            if (this._currentTaskIndex >= tasks.Count)
                this._currentTaskIndex = 0;

            return $"{this._currentTaskIndex + 1}/{tasks.Count}";
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var tasks = NotionTaskStore.GetTasks();

            if (!tasks.Any())
                return "No Tasks";

            if (this._currentTaskIndex >= tasks.Count)
                this._currentTaskIndex = 0;

            var currentTask = tasks[this._currentTaskIndex];

            return $"{currentTask.Title}{Environment.NewLine}{currentTask.Status}";
        }

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize)
        {
            var tasks = NotionTaskStore.GetTasks();

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (!tasks.Any())
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText(
                        "NO TASKS", 
                        BitmapColor.White, 
                        30, 
                        -1, 
                        -1
                    );
                    //var builder = new BitmapBuilder(imageSize);
                    //bitmapBuilder.DrawText("NO TASKS", 0, 5, imageSize.GetWidth(), imageSize.GetHeight(), BitmapColor.White, 30, -1, -1, "Arial Bold");
                   
                    //bitmapBuilder.DrawText("TASKS", BitmapColor.White, 14, 4, 30);
                    return bitmapBuilder.ToImage();
                }

                if (this._currentTaskIndex >= tasks.Count)
                    this._currentTaskIndex = 0;

                var currentTask = tasks[this._currentTaskIndex];

                // Color code by status
                BitmapColor bgColor = currentTask.Status.ToLower() switch
                {
                    "complete" => new BitmapColor(0, 150, 0),      // Green
                    "done" => new BitmapColor(0, 150, 0),          // Green
                    "testing" => new BitmapColor(200, 150, 0),     // Yellow
                    "in progress" => new BitmapColor(0, 100, 200), // Blue
                    "todo" => new BitmapColor(100, 100, 100),      // Gray
                    "not started" => new BitmapColor(100, 100, 100), // Gray
                    _ => BitmapColor.Black
                };

                bitmapBuilder.Clear(bgColor);

                // Truncate title if too long
                //var title = currentTask.Title;
                //if (title.Length > 18)
                //    title = title.Substring(0, 10) + "...";

                //// Draw title at top
                //bitmapBuilder.DrawText(title, BitmapColor.White, 12, 2, 4);

                //// Draw status in middle
                //bitmapBuilder.DrawText(currentTask.Status, BitmapColor.White, 8, 0, 32);


                // WORKS OK
                var title = currentTask.Title;
                if (title.Length > 10)
                    title = title.Substring(0, 6) + "...";

                // Draw title centered at top with large, readable font
                //bitmapBuilder.DrawText(
                //    text: title,
                //    color: BitmapColor.White,
                //    fontSize: 22
                //);

                bitmapBuilder.DrawText(
                    title,
                    BitmapColor.White,
                    30,
                    -1,
                    -1
                );
                return bitmapBuilder.ToImage();
            }
        }

        public void ResetToFirstTask()
        {
            this._currentTaskIndex = 0;
            this.AdjustmentValueChanged();
        }

       
    }
}