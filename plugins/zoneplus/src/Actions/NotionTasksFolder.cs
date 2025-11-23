namespace Loupedeck.Zoneplus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Timers;
    public class NotionTasksFolder : PluginDynamicFolder
    {
        private const string AddNewTaskActionId = "ADD_NEW_TASK"; 
        private Timer _namesChangedTimer;

        public NotionTasksFolder()
            : base()
        {
            this.DisplayName = "Notion Tasks"; 
            this.GroupName = "Notion";   

        }
        private void OnTimerTick(object sender, ElapsedEventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    var notionWebhookInstance = NotionWebhook.Instance;
                    var fetchedTasks = notionWebhookInstance.retrieveTasks().GetAwaiter().GetResult();
                    this.ButtonActionNamesChanged();
                }
                catch (Exception ex)
                {
                }
            });
        }

        public override bool Load()
        {
            this.ButtonActionNamesChanged();
            _namesChangedTimer = new Timer(2000); 
            _namesChangedTimer.Elapsed += OnTimerTick;
            _namesChangedTimer.AutoReset = true; 
            _namesChangedTimer.Enabled = true;
            return true;
        }
        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _)
        {
            return PluginDynamicFolderNavigation.ButtonArea;
        }

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType _)
        {
            var tasks = NotionTaskStore.GetTasks();
            
            var actions = new List<string>
            {
                PluginDynamicFolder.NavigateUpActionName,
                this.CreateCommandName(AddNewTaskActionId) 
            };

            actions.AddRange(tasks.Select(t => this.CreateCommandName(t.Title)));

            return actions;
        }

        public override void RunCommand(String actionParameter)
        {
            if (actionParameter == AddNewTaskActionId)
            {
                const string NotionDatabaseUrl = "https://www.notion.so/2b325317ae7780d691d8d6192b1c6342?p=new"; 

                try
                {
                    PluginLog.Info("Launching New Task URL.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = NotionDatabaseUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Failed to launch new task: {ex.Message}");
                }
                return;
            }


            if (actionParameter == PluginDynamicFolder.NavigateUpActionName) return; 

            var currentTask = NotionTaskStore.GetTasks()
                                 .FirstOrDefault(t => t.Title == actionParameter);

            if (currentTask != null)
            {
                string urlToOpen = currentTask["Url"];
                if (!string.IsNullOrEmpty(urlToOpen))
                {
                    try
                    {
                        PluginLog.Info($"Opening Task URL: {currentTask.Title}");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = urlToOpen,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error($"Failed to launch browser: {ex.Message}");
                    }
                }
            }
        }
        
        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == AddNewTaskActionId)
            {
                using (var builder = new BitmapBuilder(imageSize))
                {
                    builder.Clear(new BitmapColor(0, 100, 0));
                    builder.DrawText("+ NEW TASK", BitmapColor.White, 20, -1, -1);
                    return builder.ToImage();
                }
            }

            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                return base.GetCommandImage(actionParameter, imageSize);
            }

            var currentTask = NotionTaskStore.GetTasks().FirstOrDefault(t => t.Title == actionParameter);

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (currentTask == null)
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("ERROR", BitmapColor.Red, 30, -1, -1);
                    return bitmapBuilder.ToImage();
                }

                BitmapColor bgColor = currentTask["Status"].ToLower() switch
                {
                    "complete" => new BitmapColor(0, 150, 0),      
                    "done" => new BitmapColor(0, 150, 0),          
                    "testing" => new BitmapColor(200, 150, 0),     
                    "in progress" => new BitmapColor(0, 100, 200), 
                    "todo" => new BitmapColor(100, 100, 100),      
                    "not started" => new BitmapColor(100, 100, 100),
                    _ => BitmapColor.Black
                };

                bitmapBuilder.Clear(bgColor);

                var title = currentTask.Title;
                if (title.Length > 10)
                    title = title.Substring(0, 8) + "..";

                bitmapBuilder.DrawText(title, BitmapColor.White, 30, -1, -1);
                
                return bitmapBuilder.ToImage();
            }
        }
        
        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == AddNewTaskActionId)
            {
                return "New Task";
            }
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                return "Back";
            }
            
            var currentTask = NotionTaskStore.GetTasks().FirstOrDefault(t => t.Title == actionParameter);

            return currentTask?["Status"] ?? actionParameter;
        }

        public override BitmapImage GetButtonImage(PluginImageSize imageSize)
        {
            var bitmapImage = PluginResources.ReadImage("Loupedeck.DemoPlugin.Resources.notion.png");
            return bitmapImage;
        }
    }
}