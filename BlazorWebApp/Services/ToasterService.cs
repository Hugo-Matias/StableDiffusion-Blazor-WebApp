using BlazorWebApp.Models;
using System.Timers;

namespace BlazorWebApp.Services
{
    //https://www.codeproject.com/Articles/5322875/A-Blazor-Bootstrap-Toaster
    public class ToasterService : IDisposable
    {
        private readonly List<Toast> _toasts = new();
        private System.Timers.Timer _timer = new();

        public event EventHandler? OnToasterChanged;
        public event EventHandler? OnToasterTimeElapsed;
        public bool HasToasts => _toasts.Count > 0;

        private bool ClearBurntToast()
        {
            var toastsToDelete = _toasts.Where(t => t.IsBurnt).ToList();
            if (toastsToDelete is not null && toastsToDelete.Count > 0)
            {
                toastsToDelete.ForEach(t => _toasts.Remove(t));
                OnToasterChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        private void TimerElapsed(object? sender, ElapsedEventArgs e)
        {
            ClearBurntToast();
            OnToasterTimeElapsed?.Invoke(this, EventArgs.Empty);
        }

        public ToasterService()
        {
            //AddToast(new ToastModel { Title = "Welcome!", Message = "Welcome to BlazorDiffusion. I'll disappear after 15 seconds.", TimeToBurn = DateTimeOffset.Now.AddHours(2) });

            _timer.Interval = 5000;
            _timer.AutoReset = true;
            _timer.Elapsed += TimerElapsed;
            _timer.Start();
        }

        public List<Toast> GetToasts()
        {
            ClearBurntToast();
            return _toasts;
        }

        public void AddToast(Toast toast)
        {
            _toasts.Add(toast);
            // Only raise the OnToasterChanged event if it hasn't already been raised by ClearBurnToast()
            if (!ClearBurntToast()) OnToasterChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearToast(Toast toast)
        {
            if (_toasts.Contains(toast))
            {
                _toasts.Remove(toast);
                // Only raise the OnToasterChanged event if it hasn't already been raised by ClearBurnToast()
                if (!ClearBurntToast()) OnToasterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Elapsed += TimerElapsed;
                _timer.Stop();
            }
        }
    }
}
