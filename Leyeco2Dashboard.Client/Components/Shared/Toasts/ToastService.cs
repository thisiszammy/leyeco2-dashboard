namespace Leyeco2Dashboard.Client.Components.Shared.Toasts
{
    public enum ToastLevel
    {
        Success,
        Error,
        Info
    }

    public sealed record ToastMessage(Guid Id, ToastLevel Level, string Title, string? Detail);

    /// <summary>
    /// App-wide transient notifications. Pages publish; the single <c>ToastHost</c> in the layout
    /// root subscribes and renders. Keep titles in the same verbs as the action that triggered
    /// them ("Add Department" → "Department added") and put the human-readable reason in
    /// <c>detail</c> for errors.
    /// </summary>
    public class ToastService
    {
        public event Action<ToastMessage>? OnPublished;

        public void Success(string title, string? detail = null) => Publish(ToastLevel.Success, title, detail);

        public void Error(string title, string? detail = null) => Publish(ToastLevel.Error, title, detail);

        public void Info(string title, string? detail = null) => Publish(ToastLevel.Info, title, detail);

        private void Publish(ToastLevel level, string title, string? detail)
            => OnPublished?.Invoke(new ToastMessage(Guid.NewGuid(), level, title, detail));
    }
}
