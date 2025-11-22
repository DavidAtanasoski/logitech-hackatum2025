namespace Loupedeck.ExamplePlugin
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Timers; 
    using System.Drawing;
    using System.Linq;

    public class CameraWatch : PluginDynamicCommand
    {
        // --- CONSTANTS ---
        private const string CameraToggleParam = "CameraToggle";
        private const string BatteryStatusParam = "BatteryStatus";
        private const string ServerUrl = "http://localhost:8085/";
        private const string ResourcePrefix = "Loupedeck.ExamplePlugin.Resources.";

        // --- STATE ---
        private bool _cameraEnabled = false;
        private int _batteryLevel = 61;
        
        // NEW: Sleepy State
        private bool _isSleepy = false;
        private System.Timers.Timer _sleepyRevertTimer; 

        // --- SERVER & TIMERS ---
        private System.Timers.Timer _batteryTimer;
        private static bool _isServerRunning = false;
        private static readonly object _serverLock = new object();
        private HttpListener _httpListener;
        private CancellationTokenSource _cancellationTokenSource;

        public CameraWatch() : base()
        {
            this.AddParameter(CameraToggleParam, "Camera Toggle", "Controls");
            this.AddParameter(BatteryStatusParam, "Battery Status", "Controls");
        }

        protected override Boolean OnLoad()
        {
            // 1. Server Init
            lock (_serverLock)
            {
                if (!_isServerRunning)
                {
                    _isServerRunning = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                    Task.Run(() => StartServer(_cancellationTokenSource.Token));
                }
            }

            // 2. Battery Timer
            _batteryTimer = new System.Timers.Timer(5000);
            _batteryTimer.Elapsed += OnBatteryTick;
            _batteryTimer.AutoReset = true;
            _batteryTimer.Start();

            // 3. NEW: Sleepy Revert Timer (One-shot, 2 seconds)
            _sleepyRevertTimer = new System.Timers.Timer(2000);
            _sleepyRevertTimer.AutoReset = false; // Run once
            _sleepyRevertTimer.Elapsed += OnSleepyRevertTick;

            // 4. Initial UI Refresh
            Task.Run(async () => 
            {
                await Task.Delay(1000); 
                this.ActionImageChanged(CameraToggleParam);
                this.ActionImageChanged(BatteryStatusParam);
            });
            return true;
        }

        protected override bool OnUnload()
        {
            _batteryTimer?.Stop();
            _batteryTimer?.Dispose();
            _sleepyRevertTimer?.Stop();
            _sleepyRevertTimer?.Dispose();
            
            _cancellationTokenSource?.Cancel();
            if (_httpListener != null && _httpListener.IsListening)
            {
                _httpListener.Stop();
                _httpListener.Close();
            }
            lock (_serverLock) { _isServerRunning = false; }
            base.OnUnload();
            return true;
        }

        // --- TIMER LOGIC ---

        private void OnBatteryTick(object sender, ElapsedEventArgs e)
        {
            if (_batteryLevel > 0)
            {
                _batteryLevel--; 
                this.ActionImageChanged(BatteryStatusParam); 
            }
            else if (_batteryLevel == 0 && _batteryTimer.Enabled)
            {
                _batteryTimer.Stop();
            }
        }

        private void OnSleepyRevertTick(object sender, ElapsedEventArgs e)
        {
            // Revert the icon state
            _isSleepy = false;
            this.ActionImageChanged(CameraToggleParam); // Refresh UI to show normal icon
            PluginLog.Info("Sleepy state ended. Icon reverted.");
        }

        // --- COMMAND HANDLER ---

        protected override void RunCommand(String actionParameter)
        {
            switch (actionParameter)
            {
                case CameraToggleParam:
                    _cameraEnabled = !_cameraEnabled;
                    this.ActionImageChanged(CameraToggleParam); 
                    break;
                case BatteryStatusParam:
                    if (_batteryLevel == 0 || _batteryLevel < 100)
                    {
                        _batteryLevel = 100;
                        if (!_batteryTimer.Enabled) _batteryTimer.Start();
                        this.ActionImageChanged(BatteryStatusParam);
                    }
                    break;
            }
        }

        // --- VISUALS ---

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            switch (actionParameter)
            {
                case CameraToggleParam:
                    string fileName;
                    
                    // NEW LOGIC: Prioritize Sleepy Icon
                    if (_isSleepy)
                    {
                        fileName = "sleepy.png"; // Ensure you have a sleepy.png (or .svg)
                    }
                    else
                    {
                        // Standard ON/OFF
                        fileName = _cameraEnabled ? "camera_on.svg" : "camera_off.svg";
                    }

                    try
                    {
                        return BitmapImage.FromResource(this.Plugin.Assembly, ResourcePrefix + fileName);
                    }
                    catch
                    {
                        return null; // Fallback
                    }

                case BatteryStatusParam:
                    return DrawBatteryWidget(imageSize);

                default:
                    return base.GetCommandImage(actionParameter, imageSize);
            }
        }

        // ... (DrawBatteryWidget & GetCommandDisplayName remain the same) ...
        // [Include your DrawBatteryWidget here]
       private BitmapImage DrawBatteryWidget(PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                double level = (double)_batteryLevel;
                int r, g, b = 0; // Blue remains 0


                // We use 200 instead of 255 for the max component intensity to desaturate the color slightly.
                const double MAX_INTENSITY = 200.0;

                if (level <= 50)
                {
                    r = 255;
                    g = (int)Math.Round(level * (MAX_INTENSITY / 50.0));

                }
                else 
                {
                    g = (int)MAX_INTENSITY; // Green stays high
                    double normalizedLevel = level - 50.0;
                    r = 255 - (int)Math.Round(normalizedLevel * (255.0 / 50.0)); // Red falls from 255 to 0
                }

                r = Math.Max(0, Math.Min(255, r));
                g = Math.Max(0, Math.Min(255, g));

                BitmapColor barColor = new BitmapColor(r, g, b);

                // New values: Reduced border, thinner vertical size
                int outerPadding = 10; // Increased padding to make the battery visually smaller/thinner
                int vOffset = 30;      // Vertical padding to center the thin bar

                // Total usable width/height for the bar (minus padding)

                int usableWidth = imageSize.GetWidth() - outerPadding * 2;

                int usableHeight = imageSize.GetHeight() - vOffset * 2;


                int capWidth = usableWidth / 15; // Make cap even smaller
                int barWidth = usableWidth - capWidth;
                int barHeight = usableHeight;

               

                // 3. Draw Outline and Cap
                // FIX 1: Draw thinner bar centered vertically (start Y position is vOffset)
                builder.DrawRectangle(outerPadding, vOffset + 10, barWidth, barHeight, BitmapColor.White);

                // Cap (adjust Y position based on vertical offset)
                builder.DrawRectangle(outerPadding + barWidth, vOffset + barHeight / 3 + 10, capWidth, barHeight / 3, BitmapColor.White);


                // 4. Draw Fill Percentage
                if (_batteryLevel > 0)
                {
                    int fillMargin = 2;
                    int fillHeight = barHeight - fillMargin * 2;
                    int maxFillWidth = barWidth - fillMargin * 2;

                    double calculatedWidth = maxFillWidth * (level / 100.0);
                    int currentFillWidth = (int)Math.Round(calculatedWidth);

                    // Draw the fill bar
                    builder.FillRectangle(
                        outerPadding + fillMargin,       // X start
                        vOffset + fillMargin + 10,            // Y start (Adjusted)
                        currentFillWidth,                // Width
                        fillHeight,                      // Height
                        barColor
                    );
                }

                // 5. Draw percentage text on top (centered using full image size)
                builder.DrawText(
                    $"{this._batteryLevel}%",
                    0, 10,
                    imageSize.GetWidth(),
                    imageSize.GetHeight(),
                    BitmapColor.White,
                    24,
                    -1, -1
                );
                return builder.ToImage();
            }
        } 
        protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
        {
            switch (actionParameter)
            {
                case CameraToggleParam:
                    // 1. Priority: Sleepy Alert
                    if (this._isSleepy)
                    {
                        return "Sleepiness detected!";
                    }
                    // 2. Standard State
                    return $"Camera\n{(this._cameraEnabled ? "ON" : "OFF")}";

                case BatteryStatusParam:
                    return "Energy Level";

                default:
                    return "";
            }
        }

        // --- SERVER LOGIC ---

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
            catch { }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "POST, GET");
            
            try
            {
                if (request.HttpMethod == "POST")
                {
                    string path = request.Url.AbsolutePath.ToLower();
                    
                    // --- STRETCHING (Works regardless of camera enabled) ---
                    if (path == "/camera_stretching")
                    {
                        // Re-charge logic
                        this._batteryLevel = Math.Min(this._batteryLevel + 1, 100);
                        this.ActionImageChanged(BatteryStatusParam);
                    }
                    
                    // --- CAMERA LOGIC (Only if enabled) ---
                    if(this._cameraEnabled) 
                    {
                        if (path == "/camera_sleepy")
                        {
                            // 1. Set Sleepy State
                            if (!_isSleepy)
                            {
                                _isSleepy = true;
                                this.ActionImageChanged(CameraToggleParam); // Force icon update to sleepy.png
                            }

                            // 2. Reset the revert timer (keep it sleepy as long as we get signals)
                            _sleepyRevertTimer.Stop(); 
                            
                            this.Plugin.PluginEvents.RaiseEvent("sleepy");
                        }
                        else if (path == "/camera_awake")
                        {
                            // 1. Start the cooldown timer
                            // The icon remains sleepy for 2 more seconds, then OnSleepyRevertTick fires
                            if (_isSleepy && !_sleepyRevertTimer.Enabled)
                            {
                                _sleepyRevertTimer.Start();
                            }
                            
                            this.Plugin.PluginEvents.RaiseEvent("awake");
                        }
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes("OK");
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    response.StatusCode = 200;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Error handling request: {ex.Message}");
            }
            finally
            {
                response.OutputStream.Close();
            }
        }
    }
}