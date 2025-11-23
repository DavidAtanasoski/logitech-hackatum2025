namespace Loupedeck.Zoneplus
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Timers;

    public class CameraWatch : PluginDynamicCommand
    {
        private const string CameraToggleParam = "CameraToggle";
        private const string BatteryStatusParam = "BatteryStatus";
        private const string ServerUrl = "http://localhost:8085/";
        private const string ResourcePrefix = "Loupedeck.Zoneplus.Resources.";

        private bool _cameraEnabled = false;
        private int _batteryLevel = 60;
        
        private bool _isSleepy = false;
        private System.Timers.Timer _sleepyRevertTimer; 

        private System.Timers.Timer _batteryTimer;
        private static bool _isServerRunning = false;
        private static readonly object _serverLock = new object();
        private HttpListener _httpListener;
        private CancellationTokenSource _cancellationTokenSource;

        public CameraWatch() : base()
        {
            this.AddParameter(CameraToggleParam, "Camera Toggle", "Camera watch");
            this.AddParameter(BatteryStatusParam, "Battery Status", "Camera watch");
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

            _batteryTimer = new System.Timers.Timer(5000);
            _batteryTimer.Elapsed += OnBatteryTick;
            _batteryTimer.AutoReset = true;
            _batteryTimer.Start();

            _sleepyRevertTimer = new System.Timers.Timer(2000);
            _sleepyRevertTimer.AutoReset = false; // Run once
            _sleepyRevertTimer.Elapsed += OnSleepyRevertTick;

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
            _isSleepy = false;
            this.ActionImageChanged(CameraToggleParam); // Refresh UI to show normal icon
            PluginLog.Info("Sleepy state ended. Icon reverted.");
        }

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

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            switch (actionParameter)
            {
                case CameraToggleParam:
                    string fileName;
                    
                    if (_isSleepy)
                    {
                        fileName = "sleepy.png";
                    }
                    else
                    {
                        fileName = _cameraEnabled ? "camera_on.svg" : "camera_off.svg";
                    }

                    try
                    {
                        return BitmapImage.FromResource(this.Plugin.Assembly, ResourcePrefix + fileName);
                    }
                    catch
                    {
                        return null;
                    }

                case BatteryStatusParam:
                    return DrawBatteryWidget(imageSize);

                default:
                    return base.GetCommandImage(actionParameter, imageSize);
            }
        }

       private BitmapImage DrawBatteryWidget(PluginImageSize imageSize)
        {
            using (var builder = new BitmapBuilder(imageSize))
            {
                builder.Clear(BitmapColor.Black);
                double level = (double)_batteryLevel;
                int r, g, b = 0;

                const double MAX_INTENSITY = 200.0;

                if (level <= 50)
                {
                    r = 255;
                    g = (int)Math.Round(level * (MAX_INTENSITY / 50.0));

                }
                else 
                {
                    g = (int)MAX_INTENSITY;
                    double normalizedLevel = level - 50.0;
                    r = 255 - (int)Math.Round(normalizedLevel * (255.0 / 50.0));
                }

                r = Math.Max(0, Math.Min(255, r));
                g = Math.Max(0, Math.Min(255, g));

                BitmapColor barColor = new BitmapColor(r, g, b);

                int outerPadding = 10;
                int vOffset = 30;
                int usableWidth = imageSize.GetWidth() - outerPadding * 2;
                int usableHeight = imageSize.GetHeight() - vOffset * 2;
                int capWidth = usableWidth / 15;
                int barWidth = usableWidth - capWidth;
                int barHeight = usableHeight;

                builder.DrawRectangle(outerPadding, vOffset + 10, barWidth, barHeight, BitmapColor.White);
                builder.DrawRectangle(outerPadding + barWidth, vOffset + barHeight / 3 + 10, capWidth, barHeight / 3, BitmapColor.White);


                if (_batteryLevel > 0)
                {
                    int fillMargin = 2;
                    int fillHeight = barHeight - fillMargin * 2;
                    int maxFillWidth = barWidth - fillMargin * 2;

                    double calculatedWidth = maxFillWidth * (level / 100.0);
                    int currentFillWidth = (int)Math.Round(calculatedWidth);

                    builder.FillRectangle(
                        outerPadding + fillMargin, 
                        vOffset + fillMargin + 10,
                        currentFillWidth,
                        fillHeight,
                        barColor
                    );
                }

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
                    if (this._isSleepy)
                    {
                        return "Sleepiness detected!";
                    }
                    return $"Camera\n{(this._cameraEnabled ? "ON" : "OFF")}";

                case BatteryStatusParam:
                    return "Energy Level";

                default:
                    return "";
            }
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
                    
                    if (path == "/camera_stretching")
                    {
                        this._batteryLevel = Math.Min(this._batteryLevel + 1, 100);
                        this.ActionImageChanged(BatteryStatusParam);
                    }
                    if(this._cameraEnabled) 
                    {
                        if (path == "/camera_sleepy")
                        {
                            if (!_isSleepy)
                            {
                                _isSleepy = true;
                                this.ActionImageChanged(CameraToggleParam);
                            }

                            _sleepyRevertTimer.Stop(); 
                            
                            this.Plugin.PluginEvents.RaiseEvent("sleepy");
                        }
                        else if (path == "/camera_awake")
                        {
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