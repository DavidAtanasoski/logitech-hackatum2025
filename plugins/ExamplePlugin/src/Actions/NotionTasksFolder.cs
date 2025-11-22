namespace Loupedeck.HackieNotiePlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Diagnostics; 
    
    using Loupedeck.ExamplePlugin; 

    public class NotionTasksFolder : PluginDynamicFolder
    {
        // 1. Define the unique ID for the new action
        private const string AddNewTaskActionId = "ADD_NEW_TASK"; 
        
        // --- Constructor (unchanged) ---
        public NotionTasksFolder()
            : base()
        {
            this.DisplayName = "Notion Tasks"; 
            this.GroupName = "Notion";       
        }
        
        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _)
        {
            return PluginDynamicFolderNavigation.ButtonArea;
        }

        // 2. Add the new button to the list of actions
        public override IEnumerable<String> GetButtonPressActionNames(DeviceType _)
        {
            var tasks = NotionTaskStore.GetTasks();
            var actions = new List<string>
            {
                // Always add the "Up" button
                PluginDynamicFolder.NavigateUpActionName,
                
                // ✅ ADD THE NEW TASK BUTTON HERE (will appear next to the "Back" button)
                this.CreateCommandName(AddNewTaskActionId) 
            };

            // Add the dynamic task buttons
            actions.AddRange(tasks.Select(t => this.CreateCommandName(t.Title)));

            return actions;
        }

        // 3. Handle the click for the new button
        public override void RunCommand(String actionParameter)
        {
            // --- NEW ACTION HANDLER ---
            if (actionParameter == AddNewTaskActionId)
            {
                // NOTE: Replace the URL below with your actual Notion database URL 
                // or a special "Add New Page" link if available.
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
            // --- END NEW ACTION HANDLER ---


            // Ignore the NavigateUp action
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName) return; 

            // ... (Existing logic for opening task URL remains below) ...
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
        
        // 4. Draw the button icon (Must handle the new ID)
        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            // --- NEW IMAGE HANDLER ---
            if (actionParameter == AddNewTaskActionId)
            {
                using (var builder = new BitmapBuilder(imageSize))
                {
                    builder.Clear(new BitmapColor(0, 100, 0)); // Dark Green Background
                    builder.DrawText("+ NEW TASK", BitmapColor.White, 20, -1, -1);
                    return builder.ToImage();
                }
            }
            // --- END NEW IMAGE HANDLER ---
            
            // If the action is "Up", return the default icon (safer than drawing one)
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                return base.GetCommandImage(actionParameter, imageSize);
            }

            // ... (Existing logic for drawing task status background) ...
            var currentTask = NotionTaskStore.GetTasks().FirstOrDefault(t => t.Title == actionParameter);

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (currentTask == null)
                {
                    // Error state drawing
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("ERROR", BitmapColor.Red, 30, -1, -1);
                    return bitmapBuilder.ToImage();
                }

                // Apply existing color logic
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
        
        // 5. Set the display name (Fallback text)
        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == AddNewTaskActionId)
            {
                return "New Task"; // Display text for the new button
            }
            if (actionParameter == PluginDynamicFolder.NavigateUpActionName)
            {
                return "Back"; // Optional display text for the up button
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