namespace Loupedeck.Zoneplus
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.IO;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    public class NotionWebhook : IDisposable
    {
        private static NotionWebhook _instance;
        private static readonly object _lock = new object();
        
        private readonly Plugin _plugin;
        private readonly String _prefix;
        private HttpListener _httpListener;
        private Thread _listenerThread;
        private Boolean _isRunning = false;


        private NotionWebhook(Plugin plugin, String prefix)
        {
            this._plugin = plugin;
            this._prefix = prefix;
        }

        public static NotionWebhook Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("NotionWebhook must be initialized using NotionWebhook.Initialize() before accessing Instance.");
                }
                return _instance;
            }
        }
        
        public static void Initialize(Plugin plugin, String prefix = "http://localhost:8080/")
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new NotionWebhook(plugin, prefix);
                }
            }
        }

        public void StartWebhookServer()
        {
            if (this._isRunning) return;

            try
            {
                PluginLog.Info("Starting webhook server...");
                this._httpListener = new HttpListener();
                this._httpListener.Prefixes.Add("http://+:8080/"); 
                this._httpListener.Start();

                this._isRunning = true;
                this._listenerThread = new Thread(this.ListenForRequests);
                this._listenerThread.IsBackground = true;
                this._listenerThread.Name = "NotionWebhookListener";
                this._listenerThread.Start();

                PluginLog.Info("Webhook server fully started on http://+:8080/notion-tasks");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Failed to start webhook server: {ex.Message}");
            }
        }

        public void Stop()
        {
            this._isRunning = false;
            this._httpListener?.Stop(); 
            this._httpListener?.Close();
            PluginLog.Info("Webhook server stopped");
        }

        public void Dispose() => this.Stop();

        private void ListenForRequests()
        {
            while (this._isRunning)
            {
                try
                {
                    var context = this._httpListener.GetContext();
                    this.HandleRequest(context).Wait(); 
                }
                catch (HttpListenerException) { break; }
                catch (Exception ex)
                {
                    PluginLog.Error($"Listener error: {ex.Message}");
                }
            }
        }
        
        private async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                if (request.Url.AbsolutePath != "/notion-tasks")
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                PluginLog.Info($"Received request: {request.HttpMethod} /notion-tasks");

                String body = "";
                if (request.HasEntityBody)
                {
                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        body = await reader.ReadToEndAsync();
                    }
                }
                
                List<TaskItem> notionTasks = await retrieveTasks();

                NotionTaskStore.PreviouslyRegisteredEventIds.Clear();

                foreach (var task in notionTasks)
                {
                    string eventId = $"task_{task.Title.Replace(" ", "_")}";

                    this._plugin.PluginEvents.AddEvent(
                        eventId,
                        task.Title,
                        task["Status"]
                    );

                    this._plugin.PluginEvents.RaiseEvent(eventId);
                    NotionTaskStore.PreviouslyRegisteredEventIds.Add(eventId);
                    PluginLog.Info($"Registered and raised event: {eventId} - {task.Title}");
                }
                
                PluginLog.Info($"Updated task store with {notionTasks.Count} tasks.");
                NotionTaskStore.UpdateTasks(notionTasks);
                
                String responseText = "{\"success\": true, \"message\": \"Notion task received\"}";
                Byte[] buffer = Encoding.UTF8.GetBytes(responseText);

                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();

                PluginLog.Info("Response sent successfully");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Request error: {ex.Message}");
                try
                {
                    response.StatusCode = 500;
                    response.Close();
                }
                catch { }
            }
        }
        
        public async Task<List<TaskItem>> retrieveTasks()
        {            
            String notionResult = await this.FetchNotionDataApiAsync();

            var notionTasks = new List<TaskItem>();

            if (notionResult != null)
            {
                JsonObject notionResponse = (JsonObject)JsonObject.Parse(notionResult);
                JsonArray responseResults = (JsonArray)notionResponse["results"];

                PluginLog.Info($"resp res: {responseResults}");
                foreach (var item in responseResults)
                {
                    var obj = item.AsObject();
                    var props = obj["properties"]!.AsObject();

                    string title = props["Task"]?["title"]?[0]?["text"]?["content"]?.ToString() ?? "";
                    string status = props["Status"]?["status"]?["name"]?.ToString() ?? "";
                    string url = obj["url"]?.ToString() ?? "";
                    var newTask = new TaskItem(title);

                    newTask["Status"] = status;
                    newTask["Url"] = url; 

                    notionTasks.Add(newTask);
                }
            }
            NotionTaskStore.UpdateTasks(notionTasks);
            return notionTasks;
        }

        public async Task<String> FetchNotionDataApiAsync()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    String notionToken = "ntn_v99839955111hX0mCQ6BmawbXICT6PmeJTVmdiXMKcifGI";
                    String databaseId = "2b325317ae7780d691d8d6192b1c6342";
                    String apiUrl = $"https://api.notion.com/v1/databases/{databaseId}/query";

                    PluginLog.Info($"Fetching Notion database: {databaseId}");

                    var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                    request.Headers.Add("Authorization", $"Bearer {notionToken}");
                    request.Headers.Add("Notion-Version", "2022-06-28");
                    request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                    var apiResponse = await httpClient.SendAsync(request);
                    apiResponse.EnsureSuccessStatusCode();

                    String apiContent = await apiResponse.Content.ReadAsStringAsync();
                    PluginLog.Info($"Notion API Response: {apiContent}");

                    if (apiContent == "{}")
                    {
                        return null;
                    }
                    return apiContent;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Notion API call failed: {ex.Message}");
                return null;
            }
        }
    }
}