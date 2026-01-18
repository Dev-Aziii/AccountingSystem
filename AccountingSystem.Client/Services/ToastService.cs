namespace AccountingSystem.Client.Services
{
    public class ToastService
    {
        public event Action<string, ToastLevel> OnShow;

        public void ShowToast(string message, ToastLevel level)
        {
            OnShow?.Invoke(message, level);
        }

        public void ShowSuccess(string message) => ShowToast(message, ToastLevel.Success);
        public void ShowError(string message) => ShowToast(message, ToastLevel.Error);
        public void ShowInfo(string message) => ShowToast(message, ToastLevel.Info);
        public void ShowWarning(string message) => ShowToast(message, ToastLevel.Warning);
    }

    public enum ToastLevel
    {
        Info,
        Success,
        Warning,
        Error
    }
}