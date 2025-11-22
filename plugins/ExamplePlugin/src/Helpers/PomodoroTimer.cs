namespace ProductivityCoachPlugin.Helpers
{
    using System;
    using System.Threading;
    using System.Timers;

    using Timer = System.Timers.Timer; 

    public class PomodoroTimer
    {
        private static PomodoroTimer _instance;
        public static PomodoroTimer Instance => _instance ??= new PomodoroTimer();

        private Timer _timer;
        public int DurationMinutes { get; private set; } = 25; // Default 25 min
        public TimeSpan RemainingTime { get; private set; }
        public bool IsSet { get; private set; } = false;
        public bool IsPaused { get; private set; } = false;

        // Simple event to notify listeners when the timer ticks.
        // Listeners are responsible for calling SDK methods on the plugin/main thread.
        public event Action OnTick;

        // We store a SynchronizationContext that should be set from the plugin/main thread
        // (e.g. in the DynamicFolder.Load method). This allows PostTick to marshal
        // the OnTick invocation back to the UI thread safely.
        private SynchronizationContext _syncContext;

        public void SetSynchronizationContext(SynchronizationContext context)
        {
            if (context == null) return;
            _syncContext = context;
        }

        private PomodoroTimer()
        {
            _timer = new Timer(1000); // 1 second interval
            _timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (RemainingTime.TotalSeconds > 0)
            {
                RemainingTime = RemainingTime.Subtract(TimeSpan.FromSeconds(1));
                PostTick();
            }
            else
            {
                Complete();
            }
        }

        private void PostTick()
        {
            // Marshal the invocation to the captured context when available.
            if (_syncContext != null)
            {
                // If we're already on the captured context, invoke directly.
                if (SynchronizationContext.Current == _syncContext)
                {
                    OnTick?.Invoke();
                }
                else
                {
                    // Use Post to avoid blocking the timer thread.
                    _syncContext.Post(_ => OnTick?.Invoke(), null);
                }
            }
            else
            {
                // No context captured: invoke directly (best-effort).
                OnTick?.Invoke();
            }
        }

        public void AdjustTime(int deltaMinutes)
        {
            if (!IsSet)
            {
                // Prevent going below 1 minute or above 60
                int newTime = DurationMinutes + deltaMinutes;
                if (newTime >= 1 && newTime <= 60)
                {
                    DurationMinutes = newTime;
                    PostTick(); // Trigger the UI update event on main thread
                }
            }
        }

        public void Start()
        {
            if (!IsSet)
            {
                RemainingTime = TimeSpan.FromMinutes(DurationMinutes);
                IsSet = true;
            }
            
            IsPaused = false;
            _timer.Start();
            PostTick();
        }

        public void Pause()
        {
            IsPaused = true;
            _timer.Stop();
            PostTick();
        }

        public void Resume()
        {
            IsPaused = false;
            _timer.Start();
            PostTick();
        }

        public void Complete()
        {
            _timer.Stop();
            IsSet = false;
            IsPaused = false;
            PostTick();
        }
    }
}