using System;
using System.Threading.Tasks;

namespace QuickEditer.Services
{
    public class DialogService
    {
        private TaskCompletionSource<bool>? _confirmTcs;
        private TaskCompletionSource? _alertTcs;

        public event Action? OnChange;

        public bool ShowAlert { get; private set; }
        public string AlertMessage { get; private set; } = "";
        public string AlertTitle { get; private set; } = "提示";

        public bool ShowConfirm { get; private set; }
        public string ConfirmMessage { get; private set; } = "";
        public string ConfirmTitle { get; private set; } = "确认";

        public Task AlertAsync(string message, string title = "提示")
        {
            AlertMessage = message;
            AlertTitle = title;
            ShowAlert = true;
            _alertTcs = new TaskCompletionSource();
            OnChange?.Invoke();
            return _alertTcs.Task;
        }

        public Task<bool> ConfirmAsync(string message, string title = "确认")
        {
            ConfirmMessage = message;
            ConfirmTitle = title;
            ShowConfirm = true;
            _confirmTcs = new TaskCompletionSource<bool>();
            OnChange?.Invoke();
            return _confirmTcs.Task;
        }

        public void CloseAlert()
        {
            ShowAlert = false;
            _alertTcs?.TrySetResult();
            _alertTcs = null;
            OnChange?.Invoke();
        }

        public void Confirm(bool yes)
        {
            ShowConfirm = false;
            _confirmTcs?.TrySetResult(yes);
            _confirmTcs = null;
            OnChange?.Invoke();
        }
    }
}