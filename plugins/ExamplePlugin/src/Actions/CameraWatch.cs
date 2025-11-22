namespace Loupedeck.ExamplePlugin
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Dynamic;

    public class CameraWatch : PluginDynamicCommand
    {
        private bool enabled = false;
        
        // Server stuff
        private static bool _isServerRunning = false;
        private static readonly object _serverLock = new object();
        private HttpListener _httpListener;
        private CancellationTokenSource _cancellationTokenSource;
        private const string ServerUrl = "http://127.0.0.1:8085/";
        public CameraWatch()
            : base(displayName: "Camera Watch", description: "Enable camera drowsiness detector", groupName: "Commands")
        {

        }

        protected override Boolean OnLoad()
        {
            lock (_serverLock)
            {
                if (_isServerRunning)
                {
                    PluginLog.Warning("Server is already running. Skipping start.");
                    return true;
                }
                _isServerRunning = true;
            }

            PluginLog.Info("Plugin Loading...");
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => StartServer(_cancellationTokenSource.Token));

            Task.Run(async () => 
            {
                await Task.Delay(1000); 
                this.ActionImageChanged();
    
            });
            return true;
        }

        protected override void RunCommand(String actionParameter)
        {
            enabled = !enabled;
            this.ActionImageChanged(); 
            PluginLog.Info($"Camera Watch Status: {(enabled ? "Enabled" : "Disabled")}");
        }
        
        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            PluginLog.Info("Trying to load image resource...");
            const string ResourcePrefix = "Loupedeck.ExamplePlugin.Resources.";
            string fileName = this.enabled 
                ? "camera_on.svg" 
                : "camera_off.svg";

            string resourcePath = ResourcePrefix + fileName;

            try
            {
                PluginLog.Info($"Loaded: {resourcePath}");
                BitmapImage img = BitmapImage.FromResource(this.Plugin.Assembly, resourcePath);
                return img;
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Failed to load image resource: {resourcePath}. Error: {ex.Message}");
                return null;
            }
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            // This now correctly displays the current status on the Loupedeck device
            return $"Camera Watch {(enabled ? "Enabled" : "Disabled")}";
        }

        private async Task StartServer(CancellationToken token)
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(ServerUrl);
            try
            {
                _httpListener.Start();
                while (!token.IsCancellationRequested)
                {
                    var context = await _httpListener.GetContextAsync();
                    HandleRequest(context);
                }
            }
            catch (Exception) { /* Handle clean shutdown */ }
        }


        // Web server for incoming requests from python
        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            // CORS Headers (Required if you ever send from a Browser/Extension)
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "POST, GET");
            try
            {
                // Only process POST requests
                if (request.HttpMethod == "POST")
                {
                    // Check which path Python is calling
                    string path = request.Url.AbsolutePath.ToLower();
                    if(this.enabled) {
                        if (path == "/camera_sleepy")
                        {
                            PluginLog.Info("Sending sleepy haptic.");
                            this.Plugin.PluginEvents.RaiseEvent(
                                "sleepy"
                            );
                        }
                        else if (path == "/camera_awake")
                        {
                            PluginLog.Info("Sending awake haptic.");
                            this.Plugin.PluginEvents.RaiseEvent(
                                "awake"
                            );
                        }
                    }
                    // Send "200 OK" back to Python
                    byte[] buffer = Encoding.UTF8.GetBytes("OK");
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    // Handle "OPTIONS" or "GET" requests (Preflight checks)
                    response.StatusCode = 200;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Error handling request: {ex.Message}");
            }
            finally
            {
                // ALWAYS close the stream, or Python will hang waiting for a response
                response.OutputStream.Close();
            }
        }
    }
}