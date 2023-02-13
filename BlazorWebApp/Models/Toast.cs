namespace BlazorWebApp.Models
{
    //https://www.codeproject.com/Articles/5322875/A-Blazor-Bootstrap-Toaster
    public enum MessageColor { Primary, Secondary, Dark, Light, Success, Danger, Warning, Info }
    public record Toast
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public MessageColor MessageColor { get; init; } = MessageColor.Primary;
        public DateTimeOffset TimeToBurn { get; init; } = DateTimeOffset.Now.AddSeconds(30);

        public readonly DateTimeOffset Posted = DateTimeOffset.Now;
        public bool IsBurnt => TimeToBurn < DateTimeOffset.Now;
        private TimeSpan _elapsedTime => Posted - DateTimeOffset.Now;

        public string ElapsedTimeText => _elapsedTime.Seconds > 60 ? $"{-_elapsedTime.Minutes} min ago" : $"{-_elapsedTime.Seconds} sec ago";

        public static Toast NewToast(string title, string message, MessageColor color, int secToLive) => new Toast { Title = title, Message = message, MessageColor = color, TimeToBurn = DateTimeOffset.Now.AddSeconds(secToLive) };
    }
}

