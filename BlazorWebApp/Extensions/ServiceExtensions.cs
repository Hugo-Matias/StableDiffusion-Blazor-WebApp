using BlazorWebApp.Services;

namespace BlazorWebApp.Extensions
{
    public static class ServiceExtensions
    {
        // For calls that return a result
        public static async Task<TResult> UseComfyAPI<TResult>(
            this IServiceProvider provider,
            Func<ComfyUIService, Task<TResult>> action)
        {
            using var scope = provider.CreateScope();
            var comfy = scope.ServiceProvider.GetRequiredService<ComfyUIService>();
            return await action(comfy);
        }

        // For calls that just return Task (no result)
        public static async Task UseComfyAPI(
            this IServiceProvider provider,
            Func<ComfyUIService, Task> action)
        {
            using var scope = provider.CreateScope();
            var comfy = scope.ServiceProvider.GetRequiredService<ComfyUIService>();
            await action(comfy);
        }
    }
}
