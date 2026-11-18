namespace BlazorWebApp.Services
{
    public sealed class SettingsOptions<T> : global::Microsoft.Extensions.Options.IOptions<T>
        where T : class
    {
        public SettingsOptions(T value)
        {
            Value = value;
        }

        public T Value { get; }
    }
}